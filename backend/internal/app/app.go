package app

import (
	"context"
	"fmt"
	"net"
	"net/http"

	"github.com/apsdehal/go-logger"
	"google.golang.org/grpc"
	"google.golang.org/grpc/reflection"

	"musicclubbot/backend/internal/api"
	"musicclubbot/backend/internal/api/auth"
	"musicclubbot/backend/internal/api/song"
	"musicclubbot/backend/internal/config"
)

var propagatedCtxKeys = []string{"cfg", "log", "db"}

func Run(ctx context.Context) error {
	cfg := mustCfg(ctx)
	log := mustLog(ctx)

	lis, err := net.Listen("tcp", cfg.GRPCAddr())
	if err != nil {
		return fmt.Errorf("listen on %s: %w", cfg.GRPCAddr(), err)
	}

	grpcServer := newGrpcServer(ctx)
	api.Register(grpcServer)
	reflection.Register(grpcServer)

	httpServer := &http.Server{
		Handler: newHTTPHandler(grpcServer, cfg),
	}
	metricsServer := &http.Server{
		Addr:    cfg.MetricsAddr(),
		Handler: newMetricsHandler(),
	}

	go gracefulShutdown(ctx, grpcServer, httpServer)
	go gracefulMetricsShutdown(ctx, metricsServer)
	go song.BackfillSongTopics(ctx)

	log.Infof("Starting gRPC server on %s", cfg.GRPCAddr())
	log.Infof("Starting metrics server on %s", cfg.MetricsAddr())
	go func() {
		if err := metricsServer.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			log.Errorf("metrics server exited with error: %v", err)
		}
	}()
	if err := httpServer.Serve(lis); err != nil && err != http.ErrServerClosed {
		return fmt.Errorf("serve gRPC/gRPC-Web: %w", err)
	}

	return nil
}

/* -------------------- helpers -------------------- */

func newGrpcServer(baseCtx context.Context) *grpc.Server {
	return grpc.NewServer(
		grpc.ChainUnaryInterceptor(
			withBaseContext(baseCtx),
			metricsInterceptor,
			loggingInterceptor,
			auth.AuthInterceptor,
		),
	)
}

func gracefulShutdown(ctx context.Context, grpcServer *grpc.Server, httpServer *http.Server) {
	<-ctx.Done()
	grpcServer.GracefulStop()
	_ = httpServer.Shutdown(context.Background())
}

func gracefulMetricsShutdown(ctx context.Context, metricsServer *http.Server) {
	<-ctx.Done()
	_ = metricsServer.Shutdown(context.Background())
}

func withBaseContext(base context.Context) grpc.UnaryServerInterceptor {
	return func(
		ctx context.Context,
		req interface{},
		_ *grpc.UnaryServerInfo,
		handler grpc.UnaryHandler,
	) (interface{}, error) {
		for _, key := range propagatedCtxKeys {
			if v := base.Value(key); v != nil {
				ctx = context.WithValue(ctx, key, v)
			}
		}
		return handler(ctx, req)
	}

}

func mustCfg(ctx context.Context) config.Config {
	return ctx.Value("cfg").(config.Config)
}

func mustLog(ctx context.Context) *logger.Logger {
	return ctx.Value("log").(*logger.Logger)
}

func handlePreflight(w http.ResponseWriter, r *http.Request, allowedOrigins []string) bool {
	if r.Method != http.MethodOptions {
		return false
	}

	origin := r.Header.Get("Origin")
	allowOrigin, ok := resolveAllowedOrigin(origin, allowedOrigins)
	if !ok {
		w.WriteHeader(http.StatusForbidden)
		return true
	}

	w.Header().Set("Access-Control-Allow-Origin", allowOrigin)
	w.Header().Set("Vary", "Origin")
	w.Header().Set("Access-Control-Allow-Methods", "POST, OPTIONS")
	w.Header().Set(
		"Access-Control-Allow-Headers",
		"Content-Type, X-Grpc-Web, X-User-Agent, Authorization",
	)
	w.WriteHeader(http.StatusNoContent)
	return true
}
