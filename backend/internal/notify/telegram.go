package notify

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"time"
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

func SendTelegramMessage(ctx context.Context, token, chatID, text string) error {
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
