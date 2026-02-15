package db

import (
	"context"
	"database/sql"
	"time"
)

// Calendar represents a calendar row in the database.
type Calendar struct {
	UserID      string
	CalendarURL string
	CreatedAt   time.Time
	UpdatedAt   time.Time
}

// CalendarStore provides CRUD access to the calendar table.
type CalendarStore struct {
	db *sql.DB
}

// User represents minimal user info for calendar-related queries.
type User struct {
	ID          string
	Username    string
	DisplayName string
	TelegramID  sql.NullInt64
	AvatarURL   sql.NullString
	CreatedAt   time.Time
	UpdatedAt   time.Time
}

// NewCalendarStore creates a new store bound to a database handle.
func NewCalendarStore(db *sql.DB) *CalendarStore {
	return &CalendarStore{db: db}
}

// Create inserts a new calendar row.
func (s *CalendarStore) Create(ctx context.Context, entry *Calendar) error {
	return s.db.QueryRowContext(
		ctx,
		`INSERT INTO calendar (user_id, calendar_url)
		 VALUES ($1, $2)
		 RETURNING created_at, updated_at`,
		entry.UserID,
		entry.CalendarURL,
	).Scan(&entry.CreatedAt, &entry.UpdatedAt)
}

// Get fetches a calendar row by user ID.
func (s *CalendarStore) Get(ctx context.Context, userID string) (Calendar, error) {
	row := s.db.QueryRowContext(
		ctx,
		`SELECT user_id, calendar_url, created_at, updated_at
		 FROM calendar
		 WHERE user_id = $1`,
		userID,
	)

	var entry Calendar
	err := row.Scan(&entry.UserID, &entry.CalendarURL, &entry.CreatedAt, &entry.UpdatedAt)
	return entry, err
}

// UpdateURL updates the calendar URL for the given user.
func (s *CalendarStore) UpdateURL(ctx context.Context, userID, calendarURL string) (time.Time, error) {
	var updatedAt time.Time
	err := s.db.QueryRowContext(
		ctx,
		`UPDATE calendar
		 SET calendar_url = $2,
		     updated_at = NOW()
		 WHERE user_id = $1
		 RETURNING updated_at`,
		userID,
		calendarURL,
	).Scan(&updatedAt)
	return updatedAt, err
}

// Delete removes the calendar row for the given user.
func (s *CalendarStore) Delete(ctx context.Context, userID string) error {
	_, err := s.db.ExecContext(
		ctx,
		`DELETE FROM calendar WHERE user_id = $1`,
		userID,
	)
	return err
}

// Upsert inserts or updates a calendar row in one call.
func (s *CalendarStore) Upsert(ctx context.Context, entry *Calendar) error {
	return s.db.QueryRowContext(
		ctx,
		`INSERT INTO calendar (user_id, calendar_url)
		 VALUES ($1, $2)
		 ON CONFLICT (user_id) DO UPDATE
		 SET calendar_url = EXCLUDED.calendar_url,
		     updated_at = NOW()
		 RETURNING created_at, updated_at`,
		entry.UserID,
		entry.CalendarURL,
	).Scan(&entry.CreatedAt, &entry.UpdatedAt)
}

// ListUsersWithoutCalendar returns users who do not have a calendar row.
func (s *CalendarStore) ListUsersWithoutCalendar(ctx context.Context) ([]User, error) {
	rows, err := s.db.QueryContext(
		ctx,
		`SELECT u.id, u.username, u.display_name, u.tg_user_id, u.avatar_url, u.created_at, u.updated_at
		 FROM app_user u
		 LEFT JOIN calendar c ON c.user_id = u.id
		 WHERE c.user_id IS NULL
		 ORDER BY u.created_at DESC`,
	)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	users := make([]User, 0)
	for rows.Next() {
		var user User
		if err := rows.Scan(
			&user.ID,
			&user.Username,
			&user.DisplayName,
			&user.TelegramID,
			&user.AvatarURL,
			&user.CreatedAt,
			&user.UpdatedAt,
		); err != nil {
			return nil, err
		}
		users = append(users, user)
	}
	if err := rows.Err(); err != nil {
		return nil, err
	}

	return users, nil
}
