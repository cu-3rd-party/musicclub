package event

import (
	"context"
	"database/sql"
	"fmt"
	"html"
	"log"
	"strings"
	"time"

	"musicclubbot/backend/internal/config"
	"musicclubbot/backend/internal/helpers"
	"musicclubbot/backend/internal/notify"
	"musicclubbot/backend/proto"

	"google.golang.org/protobuf/types/known/timestamppb"
)

func announceNewEvent(ctx context.Context, db *sql.DB, userID string, event *proto.Event) {
	cfgValue := ctx.Value("cfg")
	cfg, ok := cfgValue.(config.Config)
	if !ok {
		log.Printf("[WARN] Failed to read config from context; skipping event announcement")
		return
	}
	if strings.TrimSpace(cfg.BotToken) == "" || strings.TrimSpace(cfg.ChatID) == "" {
		return
	}

	if event == nil {
		return
	}

	title := strings.TrimSpace(event.GetTitle())
	location := strings.TrimSpace(event.GetLocation())
	when := formatEventStart(event.GetStartAt())
	addedBy := userLink(ctx, db, userID, "Кто-то")

	message := buildEventAnnouncementMessage(title, location, when, addedBy)
	if message == "" {
		return
	}

	go func(token, chatID, text string) {
		bg, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		if err := notify.SendTelegramMessage(bg, token, chatID, text); err != nil {
			log.Printf("[WARN] Failed to send event announcement: %v", err)
		}
	}(cfg.BotToken, cfg.ChatID, message)
}

func buildEventAnnouncementMessage(title, location, when, addedBy string) string {
	title = html.EscapeString(strings.TrimSpace(title))
	location = html.EscapeString(strings.TrimSpace(location))
	when = strings.TrimSpace(when)
	addedBy = strings.TrimSpace(addedBy)

	if title == "" && when == "" && location == "" {
		return ""
	}

	var b strings.Builder
	b.WriteString("Добавлен новый концерт")
	if title != "" {
		b.WriteString(": ")
		b.WriteString(title)
	}
	if when != "" {
		b.WriteString("\nКогда: ")
		b.WriteString(when)
	}
	if location != "" {
		b.WriteString("\nГде: ")
		b.WriteString(location)
	}
	if addedBy != "" {
		b.WriteString("\nДобавил(а): ")
		b.WriteString(addedBy)
	}
	return b.String()
}

func formatEventStart(ts *timestamppb.Timestamp) string {
	if ts == nil {
		return ""
	}
	t := ts.AsTime()
	if t.IsZero() {
		return ""
	}
	return t.Local().Format("02.01.2006 15:04")
}

func userLink(ctx context.Context, db *sql.DB, userID, fallback string) string {
	displayName, username, telegramID, err := helpers.LoadUserTelegramInfo(ctx, db, userID)
	if err != nil {
		return html.EscapeString(fallback)
	}

	label := strings.TrimSpace(displayName)
	if label == "" {
		label = strings.TrimSpace(username)
		if label != "" {
			label = "@" + label
		}
	}
	if label == "" {
		label = fallback
	}

	escapedLabel := html.EscapeString(label)
	if telegramID.Valid {
		return fmt.Sprintf(`<a href="tg://user?id=%d">%s</a>`, telegramID.Int64, escapedLabel)
	}
	return escapedLabel
}
