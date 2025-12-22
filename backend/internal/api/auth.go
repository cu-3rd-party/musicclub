package api

import (
	"context"

	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"

	authpb "musicclubbot/backend/proto"

	emptypb "google.golang.org/protobuf/types/known/emptypb"
)

// AuthService implements auth-related gRPC endpoints.
type AuthService struct {
	authpb.UnimplementedAuthServiceServer
}

func (s *AuthService) LoginWithTelegram(ctx context.Context, req *authpb.TgLoginRequest) (*authpb.AuthSession, error) {
	// example of accessing database or config from context
	// db := ctx.Value("db").(*sql.DB)
	// cfg := ctx.Value("cfg").(config.Config)
	return nil, status.Errorf(codes.Unimplemented, "LoginWithTelegram not yet implemented")
}

func (s *AuthService) GetProfile(ctx context.Context, _ *emptypb.Empty) (*authpb.ProfileResponse, error) {
	return nil, status.Errorf(codes.Unimplemented, "GetProfile not yet implemented")
}
