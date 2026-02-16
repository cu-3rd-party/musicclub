package api

import (
	"musicclubbot/calendar/internal/yandex"
	"musicclubbot/calendar/pkg/db"
)

// Config controls HTTP API routing behavior.
type Config struct {
	BasePath      string
	EnableMetrics bool
	Store         *db.CalendarStore
	Yandex        *yandex.Client
}
