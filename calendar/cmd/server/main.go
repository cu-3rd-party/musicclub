package main

import (
	"log"

	"musicclubbot/calendar/internal/api"
	"musicclubbot/calendar/internal/config"
)

func main() {
	cfg, err := config.Load()
	if err != nil {
		log.Fatalf("failed to load config: %v", err)
	}

	router := api.NewRouter(api.Config{
		BasePath:      cfg.APIBasePath,
		EnableMetrics: cfg.EnableMetrics,
	})

	addr := ":" + cfg.Port
	log.Printf("calendar api listening on %s", addr)
	if err := router.Run(addr); err != nil {
		log.Fatalf("server error: %v", err)
	}
}
