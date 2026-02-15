package config

import (
	. "musicclubbot/backend/pkg/config"
)

// Config groups runtime configuration for the backend service.
type Config struct {
	GRPCPort                string
	MetricsPort             string
	DbUrl                   string
	JwtSecretKey            []byte
	BotUsername             string
	BotToken                string
	ChatID                  string
	SkipChatMembershipCheck bool
	AllowedOrigins          []string
}

// Load reads configuration from environment with sane defaults.
func Load() Config {
	port := GetEnv("GRPC_PORT", "6969")
	metricsPort := GetEnv("METRICS_PORT", "9091")
	url := GetEnv("POSTGRES_URL", "postgres://user:password@localhost:5432/musicclubbot")
	jwtSecret := []byte(GetEnv("JWT_SECRET", "change-this-in-prod"))
	botUsername := GetEnv("BOT_USERNAME", "YourBotUsername")
	botToken := GetEnv("BOT_TOKEN", "")
	chatID := GetEnv("CHAT_ID", "")
	skipCheck := GetEnv("SKIP_CHAT_MEMBERSHIP_CHECK", "false") == "true"
	allowedOrigins := SplitCommaList(GetEnv("CORS_ALLOWED_ORIGINS", "*"))

	return Config{
		GRPCPort:                port,
		MetricsPort:             metricsPort,
		DbUrl:                   url,
		JwtSecretKey:            jwtSecret,
		BotUsername:             botUsername,
		BotToken:                botToken,
		ChatID:                  chatID,
		SkipChatMembershipCheck: skipCheck,
		AllowedOrigins:          allowedOrigins,
	}
}

func (c Config) GRPCAddr() string {
	return ":" + c.GRPCPort
}

func (c Config) MetricsAddr() string {
	return ":" + c.MetricsPort
}
