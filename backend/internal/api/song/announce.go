package song

import (
	"bytes"
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"strings"
	"time"

	"musicclubbot/backend/internal/config"
	"musicclubbot/backend/internal/helpers"
	"musicclubbot/backend/proto"
)

const telegramTimeout = 5 * time.Second

type telegramSendMessageRequest struct {
	ChatID                string `json:"chat_id"`
	Text                  string `json:"text"`
	DisableWebPagePreview bool   `json:"disable_web_page_preview,omitempty"`
}

type telegramSendMessageResponse struct {
	Ok          bool   `json:"ok"`
	Description string `json:"description,omitempty"`
	ErrorCode   int    `json:"error_code,omitempty"`
}

func announceNewSong(ctx context.Context, db *sql.DB, userID string, req *proto.CreateSongRequest) {
	cfgValue := ctx.Value("cfg")
	cfg, ok := cfgValue.(config.Config)
	if !ok {
		log.Printf("[WARN] Failed to read config from context; skipping song announcement")
		return
	}
	if strings.TrimSpace(cfg.BotToken) == "" || strings.TrimSpace(cfg.ChatID) == "" {
		return
	}

	title := strings.TrimSpace(req.GetTitle())
	artist := strings.TrimSpace(req.GetArtist())
	link := strings.TrimSpace(req.GetLink().GetUrl())

	if title == "" && artist == "" && link == "" {
		return
	}

	addedBy := "Someone"
	user, err := helpers.LoadUserById(ctx, db, userID)
	if err == nil {
		if strings.TrimSpace(user.DisplayName) != "" {
			addedBy = user.DisplayName
		} else if strings.TrimSpace(user.Username) != "" {
			addedBy = "@" + user.Username
		}
	}

	message := buildAnnouncementMessage(title, artist, addedBy, link)
	if message == "" {
		return
	}

	go func(token, chatID, text string) {
		bg, cancel := context.WithTimeout(context.Background(), telegramTimeout)
		defer cancel()
		if err := sendTelegramMessage(bg, token, chatID, text); err != nil {
			log.Printf("[WARN] Failed to send song announcement: %v", err)
		}
	}(cfg.BotToken, cfg.ChatID, message)
}

func buildAnnouncementMessage(title, artist, addedBy, link string) string {
	title = strings.TrimSpace(title)
	artist = strings.TrimSpace(artist)
	addedBy = strings.TrimSpace(addedBy)
	link = strings.TrimSpace(link)

	main := ""
	switch {
	case title != "" && artist != "":
		main = fmt.Sprintf("%s — %s", title, artist)
	case title != "":
		main = title
	case artist != "":
		main = artist
	}

	if main == "" && link == "" {
		return ""
	}

	var b strings.Builder
	b.WriteString("New song added")
	if main != "" {
		b.WriteString(": ")
		b.WriteString(main)
	}
	if addedBy != "" {
		b.WriteString("\nAdded by: ")
		b.WriteString(addedBy)
	}
	if link != "" {
		b.WriteString("\n")
		b.WriteString(link)
	}
	return b.String()
}

func sendTelegramMessage(ctx context.Context, token, chatID, text string) error {
	body, err := json.Marshal(telegramSendMessageRequest{
		ChatID:                chatID,
		Text:                  text,
		DisableWebPagePreview: false,
	})
	if err != nil {
		return fmt.Errorf("marshal telegram request: %w", err)
	}

	req, err := http.NewRequestWithContext(
		ctx,
		http.MethodPost,
		fmt.Sprintf("https://api.telegram.org/bot%s/sendMessage", token),
		bytes.NewReader(body),
	)
	if err != nil {
		return fmt.Errorf("create telegram request: %w", err)
	}
	req.Header.Set("Content-Type", "application/json")

	client := &http.Client{Timeout: telegramTimeout}
	resp, err := client.Do(req)
	if err != nil {
		return fmt.Errorf("send telegram request: %w", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return fmt.Errorf("telegram API HTTP %d", resp.StatusCode)
	}

	var telegramResp telegramSendMessageResponse
	if err := json.NewDecoder(resp.Body).Decode(&telegramResp); err != nil {
		return fmt.Errorf("decode telegram response: %w", err)
	}
	if !telegramResp.Ok {
		if telegramResp.ErrorCode != 0 || telegramResp.Description != "" {
			return fmt.Errorf("telegram API error %d: %s", telegramResp.ErrorCode, telegramResp.Description)
		}
		return fmt.Errorf("telegram API returned ok=false")
	}

	return nil
}
