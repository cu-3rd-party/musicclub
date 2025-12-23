package app

import (
	"context"
	"time"

	"github.com/apsdehal/go-logger"
	"google.golang.org/grpc"
)

func loggingInterceptor(ctx context.Context, req any, info *grpc.UnaryServerInfo, handler grpc.UnaryHandler) (any, error) {
	log := ctx.Value("log").(*logger.Logger)
	start := time.Now()
	resp, err := handler(ctx, req)
	duration := time.Since(start)
	if err != nil {
		log.Errorf("Error handling %s: %v", info.FullMethod, err)
	} else {
		log.Infof("Successfully handled %s in %s", info.FullMethod, duration)
	}
	return resp, err
}
