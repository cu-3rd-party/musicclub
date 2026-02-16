package db

import (
	"context"
	"database/sql"
	"strings"
)

// UserEmail stores user lookup data for email queries.
type UserEmail struct {
	Email sql.NullString
}

// GetEmailByTelegramID returns the email for a Telegram user ID.
func (s *CalendarStore) GetEmailByTelegramID(ctx context.Context, tgID int64) (string, error) {
	row := s.db.QueryRowContext(
		ctx,
		`SELECT email FROM app_user WHERE tg_user_id = $1`,
		tgID,
	)

	var email sql.NullString
	if err := row.Scan(&email); err != nil {
		return "", err
	}
	if !email.Valid || strings.TrimSpace(email.String) == "" {
		return "", sql.ErrNoRows
	}
	return strings.TrimSpace(email.String), nil
}

// GetEmailByName returns the email for a user by display name or username.
func (s *CalendarStore) GetEmailByName(ctx context.Context, name string) (string, error) {
	row := s.db.QueryRowContext(
		ctx,
		`SELECT email FROM app_user
		 WHERE display_name ILIKE $1 OR username ILIKE $1
		 ORDER BY updated_at DESC
		 LIMIT 1`,
		"%"+name+"%",
	)

	var email sql.NullString
	if err := row.Scan(&email); err != nil {
		return "", err
	}
	if !email.Valid || strings.TrimSpace(email.String) == "" {
		return "", sql.ErrNoRows
	}
	return strings.TrimSpace(email.String), nil
}
