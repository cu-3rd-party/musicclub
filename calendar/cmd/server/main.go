package main

import (
	"context"
	"log"

	"musicclubbot/calendar/internal/api"
	"musicclubbot/calendar/internal/config"
	"musicclubbot/calendar/internal/yandex"
	"musicclubbot/calendar/pkg/db"
)

func main() {
	cfg, err := config.Load()
	if err != nil {
		log.Fatalf("failed to load config: %v", err)
	}

	dbConn, err := db.Open(context.Background(), cfg.DbURL)
	if err != nil {
		log.Fatalf("failed to connect to database: %v", err)
	}
	defer func() {
		if err := dbConn.Close(); err != nil {
			log.Printf("failed to close database: %v", err)
		}
	}()

	store := db.NewCalendarStore(dbConn)
	var yandexClient *yandex.Client
	if cfg.YandexCookie != "" {
		client, err := yandex.NewClient(cfg.YandexCookie, cfg.YandexTimezone)
		if err != nil {
			log.Printf("failed to initialize yandex client: %v", err)
		} else {
			yandexClient = client
		}
	}

	router := api.NewRouter(api.Config{
		BasePath:      cfg.APIBasePath,
		EnableMetrics: cfg.EnableMetrics,
		Store:         store,
		Yandex:        yandexClient,
	})

	addr := ":" + cfg.Port
	log.Printf("calendar api listening on %s", addr)
	if err := router.Run(addr); err != nil {
		log.Fatalf("server error: %v", err)
	}
}
