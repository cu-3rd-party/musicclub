package db

import (
	"database/sql"
	"log"
	"time"
)

// MonitorDbConnection pings database every delay and fails the program when a ping fails
func MonitorDbConnection(db *sql.DB, delay time.Duration) {
	ticker := time.NewTicker(delay)
	defer ticker.Stop()

	TestDbConnection(db)
	for range ticker.C {
		TestDbConnection(db)
	}
}

func TestDbConnection(db *sql.DB) {
	if err := db.Ping(); err != nil {
		log.Fatalf("Failed to ping database: %v", err)
	}
}
