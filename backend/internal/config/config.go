package config

import (
	"os"
)

// Config groups runtime configuration for the backend service.
type Config struct {
	GRPCPort string
	DbUrl    string
}

// Load reads configuration from environment with sane defaults.
func Load() Config {
	port := getenv("GRPC_PORT", "6969")
	url := getenv("POSTGRES_URL", "postgres://user:password@localhost:5432/musicclubbot")

	return Config{
		GRPCPort: port,
		DbUrl:    url,
	}
}

func (c Config) GRPCAddr() string {
	return ":" + c.GRPCPort
}

func getenv(key, fallback string) string {
	if v, ok := os.LookupEnv(key); ok && v != "" {
		return v
	}
	return fallback
}
