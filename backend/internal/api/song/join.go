package song

import (
	"context"
	"database/sql"
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

	wasFull, err := isSongFull(ctx, db, req.GetSongId())
	if err != nil {
		return nil, status.Errorf(codes.Internal, "check song fullness: %v", err)
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
		isFull, err := isSongFull(ctx, db, req.GetSongId())
		if err != nil {
			return nil, status.Errorf(codes.Internal, "check song fullness: %v", err)
		}
		if isFull && !wasFull {
			announceSongFull(ctx, db, details.GetSong())
		}
	}

	return details, nil
}

func isSongFull(ctx context.Context, db *sql.DB, songID string) (bool, error) {
	var totalRoles int32
	if err := db.QueryRowContext(ctx, `
		SELECT COUNT(*)
		FROM song_role
		WHERE song_id = $1
	`, songID).Scan(&totalRoles); err != nil {
		return false, err
	}
	if totalRoles == 0 {
		return false, nil
	}

	var filledRoles int32
	if err := db.QueryRowContext(ctx, `
		SELECT COUNT(DISTINCT sra.role)
		FROM song_role_assignment sra
		JOIN song_role sr
			ON sr.song_id = sra.song_id
			AND sr.role = sra.role
		WHERE sra.song_id = $1
	`, songID).Scan(&filledRoles); err != nil {
		return false, err
	}

	return filledRoles >= totalRoles, nil
}
