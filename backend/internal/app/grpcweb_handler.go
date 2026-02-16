package app

import (
	"net/http"

	"github.com/traefik/grpc-web/go/grpcweb"
	"golang.org/x/net/http2"
	"golang.org/x/net/http2/h2c"
	"google.golang.org/grpc"

	"musicclubbot/backend/internal/config"
)

func newHTTPHandler(grpcServer *grpc.Server, cfg config.Config) http.Handler {
	grpcWeb := grpcweb.WrapServer(
		grpcServer,
		grpcweb.WithOriginFunc(func(origin string) bool {
			_, ok := resolveAllowedOrigin(origin, cfg.AllowedOrigins)
			return ok
		}),
	)

	return h2c.NewHandler(
		http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			if handlePreflight(w, r, cfg.AllowedOrigins) {
				return
			}

			if isGrpcWebRequest(grpcWeb, r) {
				grpcWeb.ServeHTTP(w, r)
				return
			}

			http.NotFound(w, r)
		}),
		&http2.Server{},
	)
}

func isGrpcWebRequest(gw *grpcweb.WrappedGrpcServer, r *http.Request) bool {
	return gw.IsGrpcWebRequest(r) ||
		gw.IsGrpcWebSocketRequest(r) ||
		gw.IsAcceptableGrpcCorsRequest(r)
}

func resolveAllowedOrigin(origin string, allowedOrigins []string) (string, bool) {
	for _, allowed := range allowedOrigins {
		if allowed == "*" {
			return "*", true
		}
		if origin != "" && origin == allowed {
			return origin, true
		}
	}
	return "", false
}
