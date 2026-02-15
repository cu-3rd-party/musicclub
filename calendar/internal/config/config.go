package config

import "github.com/caarlos0/env/v11"

// Config defines env-driven settings for the calendar service.
type Config struct {
	Port          string `env:"PORT" envDefault:"8080"`
	APIBasePath   string `env:"API_BASE_PATH" envDefault:"/"`
	EnableMetrics bool   `env:"API_ENABLE_METRICS" envDefault:"true"`
}

// Load parses environment variables into a Config.
func Load() (Config, error) {
	cfg := Config{}
	return cfg, env.Parse(&cfg)
}
