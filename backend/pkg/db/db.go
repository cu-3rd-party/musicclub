package db

import (
	"context"
	"database/sql"
	"fmt"
	"log"
	"time"

	_ "github.com/lib/pq"
)

func MustInitDb(ctx context.Context, dbUrl string) *sql.DB {
	db, err := sql.Open("postgres", dbUrl)
	if err != nil {
		panic("Failed to open database: " + err.Error())
	}
	waitForDb(ctx, db, 30*time.Second, 1*time.Second)
	go MonitorDbConnection(db, time.Second*time.Duration(100))
	return db
}

func waitForDb(ctx context.Context, db *sql.DB, timeout, interval time.Duration) {
	deadline := time.Now().Add(timeout)
	var lastErr error
	for {
		if err := db.Ping(); err == nil {
			return
		} else {
			lastErr = err
		}

		if time.Now().After(deadline) {
			panic(fmt.Sprintf("Failed to connect to database after %s: %v", timeout, lastErr))
		}

		log.Printf("Database not ready yet, retrying in %s: %v", interval, lastErr)
		select {
		case <-ctx.Done():
			panic(fmt.Sprintf("Database connect canceled: %v", ctx.Err()))
		case <-time.After(interval):
		}
	}
}
