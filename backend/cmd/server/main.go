package main

import (
	"context"
	"log"
	"os/signal"
	"syscall"

	"musicclubbot/backend/internal/app"
	"musicclubbot/backend/internal/config"
	"musicclubbot/backend/internal/db"
)

func main() {
	ctx, stop := signal.NotifyContext(context.Background(), syscall.SIGINT, syscall.SIGTERM)
	defer stop()

	cfg := config.Load()
	ctx = context.WithValue(ctx, "cfg", cfg)
	ctx = context.WithValue(ctx, "db", db.MustInitDb(ctx, cfg.DbUrl))

	if err := app.Run(ctx); err != nil {
		log.Fatalf("backend exited with error: %v", err)
	}
}
