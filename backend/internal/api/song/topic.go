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
	"musicclubbot/backend/internal/notify"
)

const (
	forumTopicClaimValue int64 = -1
	forumTopicNameLimit        = 128
)

type songParticipant struct {
	displayName string
	username    string
	telegramID  sql.NullInt64
}

func ensureSongTopicAndNotify(ctx context.Context, db *sql.DB, cfg config.Config, songID, title, artist, link string) {
	topicTitle := buildSongTopicTitle(title, artist)
	if topicTitle == "" {
		return
	}

	topicID, created, err := ensureSongTopic(ctx, db, cfg.BotToken, cfg.ChatID, songID, topicTitle)
	if err != nil {
		log.Printf("[WARN] Failed to ensure topic for song %s: %v", songID, err)
		return
	}
	if !created {
		return
	}

	participants, err := loadSongParticipants(ctx, db, songID)
	if err != nil {
		log.Printf("[WARN] Failed to load song participants for %s: %v", songID, err)
		return
	}

	message := buildSongTopicMessage(title, artist, link, participants)
	if message == "" {
		return
	}

	bg, cancel := context.WithTimeout(context.Background(), notifyTimeout()*2)
	defer cancel()
	if err := notify.SendTelegramMessageToThread(bg, cfg.BotToken, cfg.ChatID, topicID, message); err != nil {
		log.Printf("[WARN] Failed to send topic message for song %s: %v", songID, err)
	}
}

func ensureSongTopic(ctx context.Context, db *sql.DB, token, chatID, songID, topicTitle string) (int64, bool, error) {
	if strings.TrimSpace(token) == "" || strings.TrimSpace(chatID) == "" {
		return 0, false, nil
	}

	var topicID sql.NullInt64
	row := db.QueryRowContext(ctx, `SELECT topic_id FROM song_topic WHERE song_id = $1`, songID)
	switch err := row.Scan(&topicID); err {
	case nil:
		if topicID.Valid && topicID.Int64 > 0 {
			return topicID.Int64, false, nil
		}
		claimed, err := claimSongTopic(ctx, db, songID)
		if err != nil || !claimed {
			return 0, false, err
		}
	case sql.ErrNoRows:
		claimed, err := insertSongTopicClaim(ctx, db, songID)
		if err != nil || !claimed {
			return 0, false, err
		}
	default:
		return 0, false, fmt.Errorf("load song topic: %w", err)
	}

	createdID, err := createForumTopic(ctx, token, chatID, topicTitle)
	if err != nil {
		_ = releaseSongTopicClaim(ctx, db, songID)
		return 0, false, err
	}

	if _, err := db.ExecContext(
		ctx,
		`UPDATE song_topic SET topic_id = $2, updated_at = NOW() WHERE song_id = $1`,
		songID,
		createdID,
	); err != nil {
		return 0, false, fmt.Errorf("update song topic: %w", err)
	}

	return createdID, true, nil
}

func insertSongTopicClaim(ctx context.Context, db *sql.DB, songID string) (bool, error) {
	var claimed string
	err := db.QueryRowContext(
		ctx,
		`INSERT INTO song_topic (song_id, topic_id)
		 VALUES ($1, $2)
		 ON CONFLICT (song_id) DO NOTHING
		 RETURNING song_id`,
		songID,
		forumTopicClaimValue,
	).Scan(&claimed)
	if err == sql.ErrNoRows {
		return false, nil
	}
	if err != nil {
		return false, fmt.Errorf("insert song topic claim: %w", err)
	}
	return true, nil
}

func claimSongTopic(ctx context.Context, db *sql.DB, songID string) (bool, error) {
	var claimed string
	err := db.QueryRowContext(
		ctx,
		`UPDATE song_topic
		 SET topic_id = $2, updated_at = NOW()
		 WHERE song_id = $1 AND (topic_id IS NULL OR topic_id <= 0)
		 RETURNING song_id`,
		songID,
		forumTopicClaimValue,
	).Scan(&claimed)
	if err == sql.ErrNoRows {
		return false, nil
	}
	if err != nil {
		return false, fmt.Errorf("claim song topic: %w", err)
	}
	return true, nil
}

func releaseSongTopicClaim(ctx context.Context, db *sql.DB, songID string) error {
	if _, err := db.ExecContext(
		ctx,
		`UPDATE song_topic SET topic_id = NULL, updated_at = NOW() WHERE song_id = $1 AND topic_id = $2`,
		songID,
		forumTopicClaimValue,
	); err != nil {
		return fmt.Errorf("release song topic claim: %w", err)
	}
	return nil
}

func createForumTopic(ctx context.Context, token, chatID, title string) (int64, error) {
	bg, cancel := context.WithTimeout(ctx, notifyTimeout()*2)
	defer cancel()
	return notify.CreateTelegramForumTopic(bg, token, chatID, title)
}

func buildSongTopicTitle(title, artist string) string {
	title = strings.TrimSpace(title)
	artist = strings.TrimSpace(artist)

	name := ""
	switch {
	case title != "" && artist != "":
		name = fmt.Sprintf("%s — %s", title, artist)
	case title != "":
		name = title
	case artist != "":
		name = artist
	default:
		name = "Песня"
	}
	return truncateRunes(name, forumTopicNameLimit)
}

func loadSongParticipants(ctx context.Context, db *sql.DB, songID string) ([]songParticipant, error) {
	rows, err := db.QueryContext(ctx, `
		SELECT DISTINCT au.display_name, au.username, au.tg_user_id
		FROM song_role_assignment sra
		JOIN app_user au ON au.id = sra.user_id
		WHERE sra.song_id = $1
		ORDER BY au.display_name, au.username
	`, songID)
	if err != nil {
		return nil, fmt.Errorf("query song participants: %w", err)
	}
	defer rows.Close()

	var participants []songParticipant
	for rows.Next() {
		var p songParticipant
		if err := rows.Scan(&p.displayName, &p.username, &p.telegramID); err != nil {
			return nil, fmt.Errorf("scan song participant: %w", err)
		}
		participants = append(participants, p)
	}
	if err := rows.Err(); err != nil {
		return nil, fmt.Errorf("iterate song participants: %w", err)
	}
	return participants, nil
}

func buildSongTopicMessage(title, artist, link string, participants []songParticipant) string {
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
	default:
		main = "Песня"
	}

	mentions := buildParticipantMentions(participants)
	if mentions == "" && main == "" && link == "" {
		return ""
	}

	var b strings.Builder
	b.WriteString("Тема для песни готова")
	if main != "" {
		b.WriteString(": ")
		b.WriteString(main)
	}
	if mentions != "" {
		b.WriteString("\n\nУчастники: ")
		b.WriteString(mentions)
	}
	if link != "" {
		b.WriteString("\n\n")
		b.WriteString(songLink(link))
	}
	return b.String()
}

func buildParticipantMentions(participants []songParticipant) string {
	if len(participants) == 0 {
		return ""
	}

	items := make([]string, 0, len(participants))
	for _, p := range participants {
		label := strings.TrimSpace(p.displayName)
		if label == "" {
			label = strings.TrimSpace(p.username)
		}
		if label == "" {
			label = "Участник"
		}
		escaped := html.EscapeString(label)
		if p.telegramID.Valid {
			items = append(items, fmt.Sprintf(`<a href="tg://user?id=%d">%s</a>`, p.telegramID.Int64, escaped))
			continue
		}
		items = append(items, escaped)
	}
	return strings.Join(items, ", ")
}

func truncateRunes(value string, limit int) string {
	if limit <= 0 {
		return ""
	}
	runes := []rune(value)
	if len(runes) <= limit {
		return value
	}
	return string(runes[:limit])
}

func BackfillSongTopics(ctx context.Context) {
	cfgValue := ctx.Value("cfg")
	cfg, ok := cfgValue.(config.Config)
	if !ok {
		log.Printf("[WARN] Failed to read config from context; skipping song topic backfill")
		return
	}
	if strings.TrimSpace(cfg.BotToken) == "" || strings.TrimSpace(cfg.ChatID) == "" {
		return
	}

	dbValue := ctx.Value("db")
	db, ok := dbValue.(*sql.DB)
	if !ok || db == nil {
		log.Printf("[WARN] Failed to read db from context; skipping song topic backfill")
		return
	}

	ctx, cancel := context.WithTimeout(ctx, 2*time.Minute)
	defer cancel()

	rows, err := db.QueryContext(ctx, `
		SELECT s.id, s.title, s.artist, s.link_url
		FROM song s
		JOIN song_role sr ON sr.song_id = s.id
		LEFT JOIN song_role_assignment sra
			ON sra.song_id = sr.song_id AND sra.role = sr.role
		LEFT JOIN song_topic st ON st.song_id = s.id
		GROUP BY s.id, s.title, s.artist, s.link_url, st.topic_id
		HAVING COUNT(DISTINCT sr.role) > 0
			AND COUNT(sra.id) >= COUNT(DISTINCT sr.role)
			AND (st.topic_id IS NULL OR st.topic_id <= 0)
	`)
	if err != nil {
		log.Printf("[WARN] Failed to query songs for topic backfill: %v", err)
		return
	}
	defer rows.Close()

	for rows.Next() {
		var songID, title, artist, link string
		if err := rows.Scan(&songID, &title, &artist, &link); err != nil {
			log.Printf("[WARN] Failed to scan song for topic backfill: %v", err)
			continue
		}
		ensureSongTopicAndNotify(ctx, db, cfg, songID, title, artist, link)
	}
	if err := rows.Err(); err != nil {
		log.Printf("[WARN] Failed to iterate songs for topic backfill: %v", err)
	}
}
