package api

import (
	"context"
	"database/sql"
	"errors"
	"strings"
	"time"

	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
	"google.golang.org/protobuf/types/known/timestamppb"

	authpb "musicclubbot/backend/proto"
	eventpb "musicclubbot/backend/proto"
	permissionpb "musicclubbot/backend/proto"
	songpb "musicclubbot/backend/proto"
)

func dbFromCtx(ctx context.Context) (*sql.DB, error) {
	db, ok := ctx.Value("db").(*sql.DB)
	if !ok || db == nil {
		return nil, status.Error(codes.Internal, "database connection not available in context")
	}
	return db, nil
}

func userIDFromCtx(ctx context.Context) (string, error) {
	userID, ok := ctx.Value("user_id").(string)
	if !ok || userID == "" {
		return "", status.Error(codes.Unauthenticated, "user not authenticated")
	}
	return userID, nil
}

func loadUserById(ctx context.Context, db *sql.DB, userID string) (*authpb.User, error) {
	row := db.QueryRowContext(ctx, `
		SELECT id, display_name, username, COALESCE(avatar_url, '')
		FROM app_user WHERE id = $1
	`, userID)
	var u authpb.User
	if err := row.Scan(&u.Id, &u.DisplayName, &u.Username, &u.AvatarUrl); err != nil {
		return nil, err
	}
	return &u, nil
}

func loadUserByUsername(ctx context.Context, db *sql.DB, username string) (*authpb.User, error) {
	row := db.QueryRowContext(ctx, `
		SELECT id, display_name, username, COALESCE(avatar_url, '')
		FROM app_user WHERE username = $1
	`, username)
	var u authpb.User
	if err := row.Scan(&u.Id, &u.DisplayName, &u.Username, &u.AvatarUrl); err != nil {
		return nil, err
	}
	return &u, nil
}

func loadPermissions(ctx context.Context, db *sql.DB, userID string) (*permissionpb.PermissionSet, error) {
	row := db.QueryRowContext(ctx, `
		SELECT edit_own_participation, edit_any_participation,
		       edit_own_songs, edit_any_songs,
		       edit_events, edit_tracklists
		FROM user_permissions WHERE user_id = $1
	`, userID)
	var p permissionpb.PermissionSet
	var joinOwn, joinAny, songsOwn, songsAny, events, tracks bool
	switch err := row.Scan(&joinOwn, &joinAny, &songsOwn, &songsAny, &events, &tracks); err {
	case nil:
		// ok
	case sql.ErrNoRows:
		// default permissions are all false
	default:
		return nil, err
	}

	p.Join = &permissionpb.JoinPermissions{
		EditOwnParticipation: joinOwn,
		EditAnyParticipation: joinAny,
	}
	p.Songs = &permissionpb.SongPermissions{
		EditOwnSongs: songsOwn,
		EditAnySongs: songsAny,
	}
	p.Events = &permissionpb.EventPermissions{
		EditEvents:     events,
		EditTracklists: tracks,
	}
	return &p, nil
}

func mapSongLinkType(dbValue string) songpb.SongLinkType {
	switch strings.ToLower(dbValue) {
	case "youtube":
		return songpb.SongLinkType_SONG_LINK_TYPE_YOUTUBE
	case "yandex_music":
		return songpb.SongLinkType_SONG_LINK_TYPE_YANDEX_MUSIC
	case "soundcloud":
		return songpb.SongLinkType_SONG_LINK_TYPE_SOUNDCLOUD
	default:
		return songpb.SongLinkType_SONG_LINK_TYPE_UNKNOWN
	}
}

func mapSongLinkKindToDB(kind songpb.SongLinkType) (string, error) {
	switch kind {
	case songpb.SongLinkType_SONG_LINK_TYPE_YOUTUBE:
		return "youtube", nil
	case songpb.SongLinkType_SONG_LINK_TYPE_YANDEX_MUSIC:
		return "yandex_music", nil
	case songpb.SongLinkType_SONG_LINK_TYPE_SOUNDCLOUD:
		return "soundcloud", nil
	default:
		return "", errors.New("unsupported song link type")
	}
}

func permissionAllowsSongEdit(perms *permissionpb.PermissionSet, ownerID sql.NullString, currentID string) bool {
	if perms == nil || perms.Songs == nil {
		return false
	}
	if perms.Songs.EditAnySongs {
		return true
	}
	return perms.Songs.EditOwnSongs && ownerID.String != "" && ownerID.String == currentID
}

func permissionAllowsJoinEdit(perms *permissionpb.PermissionSet, ownerID, currentID string) bool {
	if perms == nil || perms.Join == nil {
		return false
	}
	if perms.Join.EditAnyParticipation {
		return true
	}
	return perms.Join.EditOwnParticipation && ownerID != "" && ownerID == currentID
}

func permissionAllowsEventEdit(perms *permissionpb.PermissionSet) bool {
	return perms != nil && perms.Events != nil && perms.Events.EditEvents
}

func permissionAllowsTracklistEdit(perms *permissionpb.PermissionSet) bool {
	return perms != nil && perms.Events != nil && (perms.Events.EditTracklists || perms.Events.EditEvents)
}

func loadSongDetails(ctx context.Context, db *sql.DB, songID, currentUserID string) (*songpb.SongDetails, error) {
	row := db.QueryRowContext(ctx, `
		SELECT id, title, artist, description, link_kind, link_url, COALESCE(created_by, NULL)
		FROM song WHERE id = $1
	`, songID)
	var s songpb.Song
	var linkKind, linkURL string
	var creatorID sql.NullString
	if err := row.Scan(&s.Id, &s.Title, &s.Artist, &s.Description, &linkKind, &linkURL, &creatorID); err != nil {
		return nil, err
	}
	s.Link = &songpb.SongLink{Kind: mapSongLinkType(linkKind), Url: linkURL}

	roles, err := loadSongRoles(ctx, db, songID)
	if err != nil {
		return nil, err
	}
	s.AvailableRoles = roles

	perms, err := loadPermissions(ctx, db, currentUserID)
	if err != nil {
		return nil, err
	}
	s.EditableByMe = permissionAllowsSongEdit(perms, creatorID, currentUserID)

	assignments, err := loadSongAssignments(ctx, db, songID)
	if err != nil {
		return nil, err
	}

	return &songpb.SongDetails{
		Song:        &s,
		Assignments: assignments,
		Permissions: perms,
	}, nil
}

func loadSongRoles(ctx context.Context, db *sql.DB, songID string) ([]string, error) {
	rows, err := db.QueryContext(ctx, `SELECT role FROM song_role WHERE song_id = $1 ORDER BY role`, songID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var roles []string
	for rows.Next() {
		var r string
		if err := rows.Scan(&r); err != nil {
			return nil, err
		}
		roles = append(roles, r)
	}
	return roles, rows.Err()
}

func loadSongAssignments(ctx context.Context, db *sql.DB, songID string) ([]*songpb.RoleAssignment, error) {
	rows, err := db.QueryContext(ctx, `
		SELECT sra.role,
		       au.id, au.display_name, COALESCE(au.username, ''), COALESCE(au.avatar_url, ''),
		       sra.joined_at
		FROM song_role_assignment sra
		JOIN app_user au ON sra.user_id = au.id
		WHERE sra.song_id = $1
		ORDER BY sra.joined_at ASC
	`, songID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var items []*songpb.RoleAssignment
	for rows.Next() {
		var role, uid, display, username, avatar string
		var joined time.Time
		if err := rows.Scan(&role, &uid, &display, &username, &avatar, &joined); err != nil {
			return nil, err
		}
		items = append(items, &songpb.RoleAssignment{
			Role: role,
			User: &authpb.User{
				Id:          uid,
				DisplayName: display,
				Username:    username,
				AvatarUrl:   avatar,
			},
			JoinedAt: timestamppb.New(joined),
		})
	}
	return items, rows.Err()
}

func loadEventDetails(ctx context.Context, db *sql.DB, eventID, currentUserID string) (*eventpb.EventDetails, error) {
	row := db.QueryRowContext(ctx, `
		SELECT id, title, start_at, location, notify_day_before, notify_hour_before
		FROM event WHERE id = $1
	`, eventID)
	var e eventpb.Event
	var start sql.NullTime
	if err := row.Scan(&e.Id, &e.Title, &start, &e.Location, &e.NotifyDayBefore, &e.NotifyHourBefore); err != nil {
		return nil, err
	}
	if start.Valid {
		e.StartAt = timestamppb.New(start.Time)
	}

	tracklist, err := loadTracklist(ctx, db, eventID)
	if err != nil {
		return nil, err
	}

	participants, err := loadEventParticipants(ctx, db, eventID)
	if err != nil {
		return nil, err
	}

	perms, err := loadPermissions(ctx, db, currentUserID)
	if err != nil {
		return nil, err
	}

	return &eventpb.EventDetails{
		Event:        &e,
		Tracklist:    tracklist,
		Participants: participants,
		Permissions:  perms,
	}, nil
}

func loadTracklist(ctx context.Context, db *sql.DB, eventID string) (*eventpb.Tracklist, error) {
	rows, err := db.QueryContext(ctx, `
		SELECT position, COALESCE(song_id, ''), COALESCE(custom_title, ''), COALESCE(custom_artist, '')
		FROM event_track_item
		WHERE event_id = $1
		ORDER BY position
	`, eventID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var items []*eventpb.TrackItem
	for rows.Next() {
		var pos int32
		var songID, customTitle, customArtist string
		if err := rows.Scan(&pos, &songID, &customTitle, &customArtist); err != nil {
			return nil, err
		}
		items = append(items, &eventpb.TrackItem{
			Order:        uint32(pos),
			SongId:       songID,
			CustomTitle:  customTitle,
			CustomArtist: customArtist,
		})
	}
	return &eventpb.Tracklist{Items: items}, rows.Err()
}

func loadEventParticipants(ctx context.Context, db *sql.DB, eventID string) ([]*songpb.RoleAssignment, error) {
	rows, err := db.QueryContext(ctx, `
		SELECT ep.role,
		       au.id, au.display_name, COALESCE(au.username, ''), COALESCE(au.avatar_url, ''),
		       ep.joined_at
		FROM event_participant ep
		JOIN app_user au ON ep.user_id = au.id
		WHERE ep.event_id = $1
		ORDER BY ep.joined_at
	`, eventID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var items []*songpb.RoleAssignment
	for rows.Next() {
		var role, uid, display, username, avatar string
		var joined time.Time
		if err := rows.Scan(&role, &uid, &display, &username, &avatar, &joined); err != nil {
			return nil, err
		}
		items = append(items, &songpb.RoleAssignment{
			Role: role,
			User: &authpb.User{
				Id:          uid,
				DisplayName: display,
				Username:    username,
				AvatarUrl:   avatar,
			},
			JoinedAt: timestamppb.New(joined),
		})
	}
	return items, rows.Err()
}

func replaceTracklist(ctx context.Context, tx *sql.Tx, eventID string, tracklist *eventpb.Tracklist) error {
	if _, err := tx.ExecContext(ctx, `DELETE FROM event_track_item WHERE event_id = $1`, eventID); err != nil {
		return err
	}
	if tracklist == nil {
		return nil
	}
	for _, item := range tracklist.Items {
		if _, err := tx.ExecContext(ctx, `
			INSERT INTO event_track_item (event_id, position, song_id, custom_title, custom_artist)
			VALUES ($1, $2, NULLIF($3, ''), NULLIF($4, ''), NULLIF($5, ''))
		`, eventID, item.GetOrder(), item.GetSongId(), item.GetCustomTitle(), item.GetCustomArtist()); err != nil {
			return err
		}
	}
	return nil
}
