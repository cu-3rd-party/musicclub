package api

import (
	"github.com/gin-gonic/gin"
	"github.com/prometheus/client_golang/prometheus/promhttp"

	"musicclubbot/calendar/internal/api/handlers"
	"musicclubbot/calendar/pkg/middleware"
)

// NewRouter builds and configures the HTTP router.
func NewRouter(cfg Config) *gin.Engine {
	if cfg.BasePath == "" {
		cfg.BasePath = "/"
	}

	router := gin.New()
	router.Use(middleware.Logging())
	router.Use(middleware.Metrics())
	router.Use(gin.Recovery())

	registerRoutes(router, cfg)
	return router
}

func registerRoutes(router *gin.Engine, cfg Config) {
	group := router.Group(cfg.BasePath)

	calendarHandler := handlers.NewCalendarHandler(cfg.Store, cfg.Yandex)

	group.GET("/ping", handlers.Ping)
	group.GET("/echo", handlers.Echo)
	group.GET("/busy", calendarHandler.BusyIntervals)
	group.GET("/profile/busy", calendarHandler.BusyIntervals)

	if cfg.EnableMetrics {
		group.GET("/metrics", gin.WrapH(promhttp.Handler()))
	}
}
