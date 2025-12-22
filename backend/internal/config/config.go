package config

import (
	"os"
)

// Config groups runtime configuration for the backend service.
type Config struct {
	GRPCPort string
}

// Load reads configuration from environment with sane defaults.
func Load() Config {
	port := getenv("GRPC_PORT", "6969")

	return Config{
		GRPCPort: port,
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
