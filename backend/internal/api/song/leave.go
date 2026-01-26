package song

import (
	"context"
	"musicclubbot/backend/internal/helpers"
	"musicclubbot/backend/proto"

	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

func (s *SongService) LeaveRole(ctx context.Context, req *proto.LeaveRoleRequest) (*proto.SongDetails, error) {
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
		return nil, status.Error(codes.PermissionDenied, "no rights to leave roles")
	}

	res, err := db.ExecContext(ctx, `
		DELETE FROM song_role_assignment WHERE song_id = $1 AND role = $2 AND user_id = $3
	`, req.GetSongId(), req.GetRole(), userID)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "leave role: %v", err)
	}

	details, err := helpers.LoadSongDetails(ctx, db, req.GetSongId(), userID)
	if err != nil {
		return nil, err
	}

	if rows, _ := res.RowsAffected(); rows > 0 {
		announceRoleChange(ctx, db, userID, details.GetSong(), req.GetRole(), "left")
	}

	return details, nil
}
