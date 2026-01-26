package song

import (
	"context"
	"database/sql"
	"musicclubbot/backend/internal/helpers"
	"musicclubbot/backend/proto"
	"sort"
	"strings"

	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

func (s *SongService) UpdateSong(ctx context.Context, req *proto.UpdateSongRequest) (*proto.SongDetails, error) {
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

	var creatorID sql.NullString
	row := db.QueryRowContext(ctx, `SELECT COALESCE(created_by, NULL) FROM song WHERE id = $1`, req.GetId())
	if err := row.Scan(&creatorID); err != nil {
		if err == sql.ErrNoRows {
			return nil, status.Error(codes.NotFound, "song not found")
		}
		return nil, status.Errorf(codes.Internal, "load song: %v", err)
	}
	if !helpers.PermissionAllowsSongEdit(perms, creatorID, userID) {
		return nil, status.Error(codes.PermissionDenied, "no rights to edit song")
	}
	featuredAllowed := perms.Songs != nil && perms.Songs.EditFeaturedSongs
	if req.GetFeatured() && !featuredAllowed {
		return nil, status.Error(codes.PermissionDenied, "no rights to feature songs")
	}

	linkKind, err := helpers.MapSongLinkKindToDB(req.GetLink().GetKind())
	if err != nil {
		return nil, status.Error(codes.InvalidArgument, err.Error())
	}

	// Auto-extract or use custom thumbnail URL
	thumbnailURL := helpers.NormalizeThumbnailURL(req.GetThumbnailUrl(), linkKind, req.GetLink().GetUrl())

	normalizeRoles := func(roles []string) []string {
		uniq := make(map[string]struct{}, len(roles))
		out := make([]string, 0, len(roles))
		for _, role := range roles {
			role = strings.TrimSpace(role)
			if role == "" {
				continue
			}
			if _, ok := uniq[role]; ok {
				continue
			}
			uniq[role] = struct{}{}
			out = append(out, role)
		}
		sort.Strings(out)
		return out
	}

	currentRoles, err := helpers.LoadSongRoles(ctx, db, req.GetId())
	if err != nil {
		return nil, status.Errorf(codes.Internal, "load roles: %v", err)
	}
	normalizedCurrent := normalizeRoles(currentRoles)
	normalizedRequested := normalizeRoles(req.GetAvailableRoles())

	tx, err := db.BeginTx(ctx, nil)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "begin tx: %v", err)
	}
	defer tx.Rollback()

	if featuredAllowed {
		if _, err := tx.ExecContext(ctx, `
			UPDATE song
			SET title = $1, artist = $2, description = $3, link_kind = $4, link_url = $5, thumbnail_url = $6, is_featured = $7, updated_at = NOW()
			WHERE id = $8
		`, req.GetTitle(), req.GetArtist(), req.GetDescription(), linkKind, req.GetLink().GetUrl(), thumbnailURL, req.GetFeatured(), req.GetId()); err != nil {
			return nil, status.Errorf(codes.Internal, "update song: %v", err)
		}
	} else {
		if _, err := tx.ExecContext(ctx, `
			UPDATE song
			SET title = $1, artist = $2, description = $3, link_kind = $4, link_url = $5, thumbnail_url = $6, updated_at = NOW()
			WHERE id = $7
		`, req.GetTitle(), req.GetArtist(), req.GetDescription(), linkKind, req.GetLink().GetUrl(), thumbnailURL, req.GetId()); err != nil {
			return nil, status.Errorf(codes.Internal, "update song: %v", err)
		}
		}

	if len(normalizedRequested) != len(normalizedCurrent) || !slicesEqual(normalizedRequested, normalizedCurrent) {
		if err := replaceSongRoles(ctx, tx, req.GetId(), normalizedRequested); err != nil {
			return nil, status.Errorf(codes.Internal, "set roles: %v", err)
		}
	}

	if err := tx.Commit(); err != nil {
		return nil, status.Errorf(codes.Internal, "commit: %v", err)
	}

	return helpers.LoadSongDetails(ctx, db, req.GetId(), userID)
}

func slicesEqual(a, b []string) bool {
	if len(a) != len(b) {
		return false
	}
	for i := range a {
		if a[i] != b[i] {
			return false
		}
	}
	return true
}
