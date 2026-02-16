package config

import (
	"net/url"

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
	dbURL := GetEnv("POSTGRES_URL", "")
	if dbURL == "" {
		dbUser := GetEnv("POSTGRES_USER", "user")
		dbPassword := GetEnv("POSTGRES_PASSWORD", "password")
		dbHost := GetEnv("POSTGRES_HOST", "localhost")
		dbPort := GetEnv("POSTGRES_PORT", "5432")
		dbName := GetEnv("POSTGRES_DB", "musicclubbot")

		u := url.URL{
			Scheme: "postgres",
			User:   url.UserPassword(dbUser, dbPassword),
			Host:   dbHost + ":" + dbPort,
			Path:   "/" + dbName,
		}
		q := u.Query()
		q.Set("sslmode", "disable")
		u.RawQuery = q.Encode()
		dbURL = u.String()
	}
	jwtSecret := []byte(GetEnv("JWT_SECRET", "change-this-in-prod"))
	botUsername := GetEnv("BOT_USERNAME", "YourBotUsername")
	botToken := GetEnv("BOT_TOKEN", "")
	chatID := GetEnv("CHAT_ID", "")
	skipCheck := GetEnv("SKIP_CHAT_MEMBERSHIP_CHECK", "false") == "true"
	allowedOrigins := SplitCommaList(GetEnv("CORS_ALLOWED_ORIGINS", "*"))

	return Config{
		GRPCPort:                port,
		MetricsPort:             metricsPort,
		DbUrl:                   dbURL,
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
