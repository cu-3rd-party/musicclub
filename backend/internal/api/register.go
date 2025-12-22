package api

import (
	"google.golang.org/grpc"

	authpb "musicclubbot/backend/proto/auth"
	eventpb "musicclubbot/backend/proto/event"
	songpb "musicclubbot/backend/proto/song"
)

// Register wires all service handlers to the gRPC server.
func Register(server *grpc.Server) {
	authpb.RegisterAuthServiceServer(server, &AuthService{})
	songpb.RegisterSongServiceServer(server, &SongService{})
	eventpb.RegisterEventServiceServer(server, &EventService{})
}
