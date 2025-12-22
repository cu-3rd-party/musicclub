package main

import (
	"context"
	"log"
	"os/signal"
	"syscall"

	"musicclubbot/backend/internal/app"
	"musicclubbot/backend/internal/config"
)

func main() {
	ctx, stop := signal.NotifyContext(context.Background(), syscall.SIGINT, syscall.SIGTERM)
	defer stop()

	cfg := config.Load()

	if err := app.Run(ctx, cfg); err != nil {
		log.Fatalf("backend exited with error: %v", err)
	}
}
