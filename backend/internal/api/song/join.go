package song

import (
	"context"
	"musicclubbot/backend/internal/helpers"
	"musicclubbot/backend/proto"

	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

func (s *SongService) JoinRole(ctx context.Context, req *proto.JoinRoleRequest) (*proto.SongDetails, error) {
	userID, err := helpers.UserIDFromCtx(ctx)
	if err != nil {
		return nil, err
	}
	db, err := helpers.DbFromCtx(ctx)
	if err != nil {
		return nil, err
	}
	perms, err := helpers.LoadPermissions(ctx, db, userID)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "load permissions: %v", err)
	}
	if !helpers.PermissionAllowsJoinEdit(perms, userID, userID) {
		return nil, status.Error(codes.PermissionDenied, "no rights to join roles")
	}

	res, err := db.ExecContext(ctx, `
		INSERT INTO song_role_assignment (song_id, role, user_id)
		VALUES ($1, $2, $3)
		ON CONFLICT (song_id, role, user_id) DO NOTHING
	`, req.GetSongId(), req.GetRole(), userID)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "join role: %v", err)
	}

	details, err := helpers.LoadSongDetails(ctx, db, req.GetSongId(), userID)
	if err != nil {
		return nil, err
	}

	if rows, _ := res.RowsAffected(); rows > 0 {
		announceRoleChange(ctx, db, userID, details.GetSong(), req.GetRole(), "joined")
	}

	return details, nil
}
