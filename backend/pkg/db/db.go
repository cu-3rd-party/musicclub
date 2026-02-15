package db

import (
	"context"
	"database/sql"
	"time"

	_ "github.com/lib/pq"
)

func MustInitDb(ctx context.Context, dbUrl string) *sql.DB {
	db, err := sql.Open("postgres", dbUrl)
	if err != nil {
		panic("Failed to open database: " + err.Error())
	}
	go MonitorDbConnection(db, time.Second*time.Duration(100))
	return db
}
