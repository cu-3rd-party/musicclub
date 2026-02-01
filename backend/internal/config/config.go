package config

import (
	"os"
	"strings"
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
	port := getenv("GRPC_PORT", "6969")
	metricsPort := getenv("METRICS_PORT", "9091")
	url := getenv("POSTGRES_URL", "postgres://user:password@localhost:5432/musicclubbot")
	jwtSecret := []byte(getenv("JWT_SECRET", "change-this-in-prod"))
	botUsername := getenv("BOT_USERNAME", "YourBotUsername")
	botToken := getenv("BOT_TOKEN", "")
	chatID := getenv("CHAT_ID", "")
	skipCheck := getenv("SKIP_CHAT_MEMBERSHIP_CHECK", "false") == "true"
	allowedOrigins := splitCommaList(getenv("CORS_ALLOWED_ORIGINS", "*"))

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

func getenv(key, fallback string) string {
	if v, ok := os.LookupEnv(key); ok && v != "" {
		return v
	}
	return fallback
}

func splitCommaList(value string) []string {
	parts := strings.Split(value, ",")
	out := make([]string, 0, len(parts))
	for _, part := range parts {
		item := strings.TrimSpace(part)
		if item != "" {
			out = append(out, item)
		}
	}
	return out
}
