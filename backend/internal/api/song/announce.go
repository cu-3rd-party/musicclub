package song

import (
	"context"
	"database/sql"
	"fmt"
	"log"
	"strings"
	"time"

	"musicclubbot/backend/internal/config"
	"musicclubbot/backend/internal/helpers"
	"musicclubbot/backend/internal/notify"
	"musicclubbot/backend/proto"
)

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

	addedBy := displayNameForUser(ctx, db, userID, "Кто-то")

	message := buildAnnouncementMessage(title, artist, addedBy, link)
	if message == "" {
		return
	}

	go func(token, chatID, text string) {
		bg, cancel := context.WithTimeout(context.Background(), notifyTimeout())
		defer cancel()
		if err := notify.SendTelegramMessage(bg, token, chatID, text); err != nil {
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
	b.WriteString("Добавлена новая песня")
	if main != "" {
		b.WriteString(": ")
		b.WriteString(main)
	}
	if addedBy != "" {
		b.WriteString("\nДобавил(а): ")
		b.WriteString(addedBy)
	}
	if link != "" {
		b.WriteString("\n")
		b.WriteString(link)
	}
	return b.String()
}

func announceRoleChange(ctx context.Context, db *sql.DB, userID string, song *proto.Song, role, verb string) {
	cfgValue := ctx.Value("cfg")
	cfg, ok := cfgValue.(config.Config)
	if !ok {
		log.Printf("[WARN] Failed to read config from context; skipping role announcement")
		return
	}
	if strings.TrimSpace(cfg.BotToken) == "" || strings.TrimSpace(cfg.ChatID) == "" {
		return
	}

	role = strings.TrimSpace(role)
	if role == "" || song == nil {
		return
	}

	title := strings.TrimSpace(song.GetTitle())
	artist := strings.TrimSpace(song.GetArtist())
	link := strings.TrimSpace(song.GetLink().GetUrl())

	userName := displayNameForUser(ctx, db, userID, "Кто-то")
	message := buildRoleChangeMessage(title, artist, userName, role, verb, link)
	if message == "" {
		return
	}

	go func(token, chatID, text string) {
		bg, cancel := context.WithTimeout(context.Background(), notifyTimeout())
		defer cancel()
		if err := notify.SendTelegramMessage(bg, token, chatID, text); err != nil {
			log.Printf("[WARN] Failed to send role announcement: %v", err)
		}
	}(cfg.BotToken, cfg.ChatID, message)
}

func buildRoleChangeMessage(title, artist, userName, role, verb, link string) string {
	title = strings.TrimSpace(title)
	artist = strings.TrimSpace(artist)
	userName = strings.TrimSpace(userName)
	role = strings.TrimSpace(role)
	verb = strings.TrimSpace(verb)
	link = strings.TrimSpace(link)

	if userName == "" || role == "" {
		return ""
	}

	main := ""
	switch {
	case title != "" && artist != "":
		main = fmt.Sprintf("%s — %s", title, artist)
	case title != "":
		main = title
	case artist != "":
		main = artist
	}

	var b strings.Builder
	if verb == "left" {
		b.WriteString("Участник покинул роль")
	} else {
		b.WriteString("Участник занял роль")
	}
	if main != "" {
		b.WriteString(": ")
		b.WriteString(main)
	}
	b.WriteString("\n")
	b.WriteString(userName)
	b.WriteString(" — ")
	b.WriteString(role)
	if link != "" {
		b.WriteString("\n")
		b.WriteString(link)
	}
	return b.String()
}

func displayNameForUser(ctx context.Context, db *sql.DB, userID, fallback string) string {
	user, err := helpers.LoadUserById(ctx, db, userID)
	if err != nil {
		return fallback
	}
	if strings.TrimSpace(user.DisplayName) != "" {
		return user.DisplayName
	}
	if strings.TrimSpace(user.Username) != "" {
		return "@" + user.Username
	}
	return fallback
}

func notifyTimeout() time.Duration {
	return 5 * time.Second
}
