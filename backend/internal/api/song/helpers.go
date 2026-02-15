package song

import (
	"context"
	"database/sql"

	"github.com/lib/pq"
)

func replaceSongRoles(ctx context.Context, tx *sql.Tx, songID string, desiredRoles []string) error {
	rows, err := tx.QueryContext(ctx, `SELECT role FROM song_role WHERE song_id = $1`, songID)
	if err != nil {
		return err
	}
	defer rows.Close()

	currentSet := make(map[string]struct{})
	for rows.Next() {
		var r string
		if err := rows.Scan(&r); err != nil {
			return err
		}
		currentSet[r] = struct{}{}
	}
	if err := rows.Err(); err != nil {
		return err
	}

	desiredSet := make(map[string]struct{}, len(desiredRoles))
	toAdd := make([]string, 0)
	for _, r := range desiredRoles {
		desiredSet[r] = struct{}{}
		if _, ok := currentSet[r]; !ok {
			toAdd = append(toAdd, r)
		}
	}

	toRemove := make([]string, 0)
	for r := range currentSet {
		if _, ok := desiredSet[r]; !ok {
			toRemove = append(toRemove, r)
		}
	}

	if len(toRemove) > 0 {
		if _, err := tx.ExecContext(ctx,
			`DELETE FROM song_role WHERE song_id = $1 AND role = ANY($2)`,
			songID, pq.Array(toRemove),
		); err != nil {
			return err
		}
	}

	if len(toAdd) > 0 {
		stmt, err := tx.PrepareContext(ctx, `
			INSERT INTO song_role (song_id, role)
			VALUES ($1, $2)
			ON CONFLICT DO NOTHING
		`)
		if err != nil {
			return err
		}
		defer stmt.Close()

		for _, r := range toAdd {
			if _, err := stmt.ExecContext(ctx, songID, r); err != nil {
				return err
			}
		}
	}

	return nil
}
