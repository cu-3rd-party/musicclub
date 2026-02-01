package song

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

	addedBy := userLink(ctx, db, userID, "Кто-то")

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
	title = html.EscapeString(strings.TrimSpace(title))
	artist = html.EscapeString(strings.TrimSpace(artist))
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
		b.WriteString(songLink(link))
	}
	return b.String()
}

func announceSongFull(ctx context.Context, db *sql.DB, song *proto.Song) {
	cfgValue := ctx.Value("cfg")
	cfg, ok := cfgValue.(config.Config)
	if !ok {
		log.Printf("[WARN] Failed to read config from context; skipping full song announcement")
		return
	}
	if strings.TrimSpace(cfg.BotToken) == "" || strings.TrimSpace(cfg.ChatID) == "" {
		return
	}

	if song == nil {
		return
	}

	title := strings.TrimSpace(song.GetTitle())
	artist := strings.TrimSpace(song.GetArtist())
	link := strings.TrimSpace(song.GetLink().GetUrl())

	message := buildSongFullMessage(title, artist, link)
	if message == "" {
		return
	}

	go func(token, chatID, text string) {
		bg, cancel := context.WithTimeout(context.Background(), notifyTimeout())
		defer cancel()
		if err := notify.SendTelegramMessage(bg, token, chatID, text); err != nil {
			log.Printf("[WARN] Failed to send full song announcement: %v", err)
		}
	}(cfg.BotToken, cfg.ChatID, message)

	go func(songID, title, artist, link string) {
		bg, cancel := context.WithTimeout(context.Background(), notifyTimeout()*3)
		defer cancel()
		ensureSongTopicAndNotify(bg, db, cfg, songID, title, artist, link)
	}(song.GetId(), title, artist, link)
}

func buildSongFullMessage(title, artist, link string) string {
	title = html.EscapeString(strings.TrimSpace(title))
	artist = html.EscapeString(strings.TrimSpace(artist))
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

	var b strings.Builder
	b.WriteString("Песня укомплектована")
	if main != "" {
		b.WriteString(": ")
		b.WriteString(main)
	}
	if link != "" {
		b.WriteString("\n")
		b.WriteString("\n")
		b.WriteString(songLink(link))
	}
	return b.String()
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

func songLink(link string) string {
	link = strings.TrimSpace(link)
	if link == "" {
		return ""
	}
	return fmt.Sprintf(`<a href="%s">Послушать</a>`, html.EscapeString(link))
}

func notifyTimeout() time.Duration {
	return 5 * time.Second
}
