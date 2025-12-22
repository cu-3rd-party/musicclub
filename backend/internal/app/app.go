package app

import (
	"context"
	"fmt"
	"net"

	"google.golang.org/grpc"
	"google.golang.org/grpc/reflection"

	"musicclubbot/backend/internal/api"
	"musicclubbot/backend/internal/config"
)

// Run initializes and starts the gRPC server with stub handlers.
func Run(ctx context.Context) error {
	cfg := ctx.Value("cfg").(config.Config)
	lis, err := net.Listen("tcp", cfg.GRPCAddr())
	if err != nil {
		return fmt.Errorf("listen on %s: %w", cfg.GRPCAddr(), err)
	}

	grpcServer := grpc.NewServer()

	api.Register(grpcServer)
	reflection.Register(grpcServer)

	// Graceful stop on context cancellation.
	go func() {
		<-ctx.Done()
		grpcServer.GracefulStop()
	}()

	if err := grpcServer.Serve(lis); err != nil {
		return fmt.Errorf("serve gRPC: %w", err)
	}

	return nil
}
