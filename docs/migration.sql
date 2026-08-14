-- ============================================================
-- MIGRATE legacy_db -> db
--
-- SOURCE DATABASE:
--     legacy_db
--
-- TARGET DATABASE:
--     db
--
-- PostgreSQL connection to legacy_db:
--     user     = admin
--     password = password
--     host     = 127.0.0.1
--     port     = 5432
--
-- IMPORTANT:
-- Run this entire script while connected to the NEW database:
--
--     db
--
-- The migration is transactional.
-- If anything fails, execute:
--
--     ROLLBACK;
--
-- before trying again.
-- ============================================================


BEGIN;


-- ============================================================
-- 1. EXTENSIONS
-- ============================================================

CREATE EXTENSION IF NOT EXISTS postgres_fdw;

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ============================================================
-- 2. CREATE FOREIGN SCHEMA FOR LEGACY DATABASE
-- ============================================================

DROP SCHEMA IF EXISTS legacy CASCADE;

CREATE SCHEMA legacy;


-- ============================================================
-- 3. CREATE CONNECTION TO legacy_db
-- ============================================================

DROP SERVER IF EXISTS legacy_db_server CASCADE;


CREATE SERVER legacy_db_server
    FOREIGN DATA WRAPPER postgres_fdw
    OPTIONS
        (
        host   '127.0.0.1',
        port   '5432',
        dbname 'legacy_db'
        );


CREATE USER MAPPING FOR CURRENT_USER
    SERVER legacy_db_server
    OPTIONS
        (
        user     'admin',
        password 'password'
        );


-- ============================================================
-- 4. IMPORT LEGACY TABLES
-- ============================================================

IMPORT FOREIGN SCHEMA public
    LIMIT TO
    (
    app_user,
    calendar,
    calendar_attach_state,
    event,
    event_participant,
    event_track_item,
    song,
    song_role,
    song_role_assignment,
    song_topic,
    tg_auth_user,
    refresh_tokens,
    user_permissions
    )
    FROM SERVER legacy_db_server
    INTO legacy;


-- ============================================================
-- 5. VERIFY LEGACY DATABASE CONNECTION
-- ============================================================

DO $$
    BEGIN

        RAISE NOTICE '------------------------------------------';
        RAISE NOTICE 'Legacy database connection OK';
        RAISE NOTICE '------------------------------------------';

        RAISE NOTICE 'app_user:             %',
            (SELECT COUNT(*) FROM legacy.app_user);

        RAISE NOTICE 'calendar:             %',
            (SELECT COUNT(*) FROM legacy.calendar);

        RAISE NOTICE 'event:                %',
            (SELECT COUNT(*) FROM legacy.event);

        RAISE NOTICE 'song:                 %',
            (SELECT COUNT(*) FROM legacy.song);

        RAISE NOTICE 'song_role:            %',
            (SELECT COUNT(*) FROM legacy.song_role);

        RAISE NOTICE 'song_role_assignment: %',
            (SELECT COUNT(*) FROM legacy.song_role_assignment);

        RAISE NOTICE 'event_track_item:     %',
            (SELECT COUNT(*) FROM legacy.event_track_item);

        RAISE NOTICE 'event_participant:    %',
            (SELECT COUNT(*) FROM legacy.event_participant);

        RAISE NOTICE 'song_topic:           %',
            (SELECT COUNT(*) FROM legacy.song_topic);

    END
$$;


-- ============================================================
-- Expected values based on your current legacy database:
--
-- app_user              = 111
-- calendar              = 0
-- event                 = 1
-- song                  = 130
-- song_role             = 576
-- song_role_assignment  = 325
-- event_track_item      = 0
-- event_participant     = 0
-- song_topic            = 57
-- ============================================================


-- ============================================================
-- 6. USERS
--
-- legacy.app_user
--        ->
-- public."AspNetUsers"
-- ============================================================

INSERT INTO public."AspNetUsers"
(
    "Id",
    "TgUserId",
    "IsChatMember",
    "DisplayName",
    "AvatarUrl",
    "CreatedAt",
    "UpdatedAt",
    "UserName",
    "NormalizedUserName",
    "Email",
    "NormalizedEmail",
    "EmailConfirmed",
    "PasswordHash",
    "SecurityStamp",
    "ConcurrencyStamp",
    "PhoneNumber",
    "PhoneNumberConfirmed",
    "TwoFactorEnabled",
    "LockoutEnd",
    "LockoutEnabled",
    "AccessFailedCount"
)
SELECT
    u.id,

    u.tg_user_id,

    COALESCE(
        u.is_chat_member,
        false
    ),

    u.display_name,

    u.avatar_url,

    u.created_at,

    u.updated_at,

    u.username,

    CASE
        WHEN u.username IS NULL
            THEN NULL
        ELSE UPPER(u.username)
        END,

    u.email,

    CASE
        WHEN u.email IS NULL
            THEN NULL
        ELSE UPPER(u.email)
        END,

    false,

    u.password_hash,

    gen_random_uuid()::text,

    gen_random_uuid()::text,

    NULL,

    false,

    false,

    NULL,

    false,

    0

FROM legacy.app_user u;


-- ============================================================
-- 7. CALENDAR
-- ============================================================

INSERT INTO public.calendar
(
    user_id,
    calendar_url,
    created_at,
    updated_at
)
SELECT
    user_id,
    calendar_url,
    created_at,
    updated_at
FROM legacy.calendar;


-- ============================================================
-- 8. CALENDAR ATTACH STATE
-- ============================================================

INSERT INTO public.calendar_attach_state
(
    tg_user_id,
    state,
    pending_user_id,
    pending_email,
    updated_at
)
SELECT
    tg_user_id,
    state::text,
    pending_user_id,
    pending_email,
    updated_at
FROM legacy.calendar_attach_state;


-- ============================================================
-- 9. EVENTS
-- ============================================================

INSERT INTO public.event
(
    id,
    title,
    start_at,
    location,
    notify_day_before,
    notify_hour_before,
    created_by,
    created_at,
    updated_at
)
SELECT
    id,
    title,
    start_at,
    location,
    notify_day_before,
    notify_hour_before,
    created_by,
    created_at,
    updated_at
FROM legacy.event;


-- ============================================================
-- 10. SONGS
-- ============================================================

INSERT INTO public.song
(
    id,
    title,
    artist,
    description,
    link_kind,
    link_url,
    created_by,
    thumbnail_url,
    is_featured,
    created_at,
    updated_at
)
SELECT
    id,
    title,
    artist,
    description,
    link_kind,
    link_url,
    created_by,
    thumbnail_url,
    COALESCE(is_featured, false),
    created_at,
    updated_at
FROM legacy.song;


-- ============================================================
-- 11. SONG ROLES / ASSIGNMENTS
--
-- IMPORTANT LEGACY SCHEMA DETAIL:
--
-- legacy.song_role:
--
--     song_id
--     role
--
-- legacy.song_role_assignment:
--
--     song_id
--     role              <-- TEXT ROLE NAME
--     user_id
--     joined_at
--
-- Therefore DO NOT use:
--
--     r.id = a.role
--
-- The correct relationship is:
--
--     r.song_id = a.song_id
--     r.role    = a.role
--
--
-- NEW MODEL:
--
-- Legacy:
--
--     гитара -> Alice
--     гитара -> Bob
--
-- becomes:
--
--     гитара 1 -> Alice
--     гитара 2 -> Bob
--
-- Every assignment gets its own new SongRole UUID.
-- ============================================================


-- ============================================================
-- 11.1 TEMPORARY MIGRATION MAP
-- ============================================================

CREATE TEMP TABLE role_migration_map
(
    old_assignment_id UUID NOT NULL,

    old_song_id       UUID NOT NULL,

    old_role_name     TEXT NOT NULL,

    old_user_id       UUID NOT NULL,

    old_joined_at     TIMESTAMPTZ NOT NULL,

    role_number       INTEGER NOT NULL,

    assignment_count  INTEGER NOT NULL,

    new_role_id       UUID NOT NULL,

    new_role_name     TEXT NOT NULL
)
    ON COMMIT DROP;


-- ============================================================
-- 11.2 GENERATE ONE NEW ROLE FOR EVERY ASSIGNMENT
--
-- If a legacy role has:
--
--     one assignment:
--         гитара
--
-- then new role:
--
--     гитара
--
-- If a legacy role has:
--
--     Alice
--     Bob
--     Charlie
--
-- then new roles:
--
--     гитара 1
--     гитара 2
--     гитара 3
-- ============================================================

WITH numbered AS
         (
             SELECT

                 a.id AS old_assignment_id,

                 a.song_id AS old_song_id,

                 a.role AS old_role_name,

                 a.user_id AS old_user_id,

                 a.joined_at AS old_joined_at,


                 ROW_NUMBER() OVER
                     (
                     PARTITION BY
                         a.song_id,
                         a.role

                     ORDER BY
                         a.joined_at,
                         a.id

                     )::INTEGER AS role_number,


                 COUNT(*) OVER
                     (
                     PARTITION BY
                         a.song_id,
                         a.role

                     )::INTEGER AS assignment_count


             FROM legacy.song_role_assignment a

                      JOIN legacy.song_role r
                           ON r.song_id = a.song_id
                               AND r.role    = a.role
         ),


     base_names AS
         (
             SELECT

                 n.*,

                 CASE

                     WHEN n.assignment_count = 1
                         THEN n.old_role_name

                     ELSE
                         n.old_role_name
                             || ' '
                             || n.role_number::TEXT

                     END AS base_name

             FROM numbered n
         )


INSERT INTO role_migration_map
(
    old_assignment_id,
    old_song_id,
    old_role_name,
    old_user_id,
    old_joined_at,
    role_number,
    assignment_count,
    new_role_id,
    new_role_name
)
SELECT

    old_assignment_id,

    old_song_id,

    old_role_name,

    old_user_id,

    old_joined_at,

    role_number,

    assignment_count,

    gen_random_uuid(),

    base_name

FROM base_names;


-- ============================================================
-- 11.3 RESOLVE ROLE NAME COLLISIONS
--
-- Handles cases such as:
--
--     гитара
--     гитара 1
--     гитара 2
--
-- and ensures every generated role name is unique
-- within a song.
-- ============================================================

DO $$
    DECLARE

        r RECORD;

        candidate TEXT;

        suffix INTEGER;

    BEGIN

        FOR r IN

            SELECT

                m.new_role_id,

                m.old_song_id,

                m.old_role_name,

                m.role_number,

                m.new_role_name,

                m.old_assignment_id

            FROM role_migration_map m

            ORDER BY

                m.old_song_id,

                m.old_role_name,

                m.role_number,

                m.old_assignment_id

            LOOP

                candidate := r.new_role_name;


                -- ----------------------------------------------------
                -- Check legacy role names and already-generated names.
                -- ----------------------------------------------------

                IF EXISTS
                       (
                           SELECT 1

                           FROM legacy.song_role lr

                           WHERE lr.song_id = r.old_song_id
                             AND lr.role = candidate
                       )

                    OR EXISTS
                       (
                           SELECT 1

                           FROM role_migration_map m

                           WHERE m.old_song_id = r.old_song_id

                             AND m.new_role_name = candidate

                             AND m.new_role_id <> r.new_role_id
                       )

                THEN

                    suffix := GREATEST(
                        r.role_number,
                        1
                              );


                    LOOP

                        suffix := suffix + 1;


                        candidate :=
                            r.old_role_name
                                || ' '
                                || suffix::TEXT;


                        EXIT WHEN

                            NOT EXISTS
                                (
                                    SELECT 1

                                    FROM legacy.song_role lr

                                    WHERE lr.song_id = r.old_song_id
                                      AND lr.role = candidate
                                )

                                AND

                            NOT EXISTS
                                (
                                    SELECT 1

                                    FROM role_migration_map m

                                    WHERE m.old_song_id = r.old_song_id

                                      AND m.new_role_name = candidate

                                      AND m.new_role_id <> r.new_role_id
                                );

                    END LOOP;


                    UPDATE role_migration_map

                    SET new_role_name = candidate

                    WHERE new_role_id = r.new_role_id;

                END IF;

            END LOOP;

    END
$$;


-- ============================================================
-- 11.4 VALIDATE GENERATED ROLE NAMES
-- ============================================================

DO $$
    DECLARE

        duplicate_count BIGINT;

    BEGIN

        SELECT COUNT(*)

        INTO duplicate_count

        FROM
            (
                SELECT

                    old_song_id,

                    new_role_name,

                    COUNT(*) AS cnt

                FROM role_migration_map

                GROUP BY

                    old_song_id,

                    new_role_name

                HAVING COUNT(*) > 1

            ) duplicates;


        IF duplicate_count > 0 THEN

            RAISE EXCEPTION
                'DUPLICATE GENERATED ROLE NAMES: %',
                duplicate_count;

        END IF;

    END
$$;


-- ============================================================
-- 11.5 INSERT NEW ROLES FOR ASSIGNED USERS
-- ============================================================

INSERT INTO public.song_role
(
    id,
    song_id,
    role
)
SELECT

    new_role_id,

    old_song_id,

    new_role_name

FROM role_migration_map;


-- ============================================================
-- 11.6 INSERT NEW ASSIGNMENTS
--
-- IMPORTANT:
--
-- The new assignment.role points to the NEW SongRole UUID.
-- ============================================================

INSERT INTO public.song_role_assignment
(
    id,
    song_id,
    role,
    user_id,
    joined_at
)
SELECT

    old_assignment_id,

    old_song_id,

    new_role_id,

    old_user_id,

    old_joined_at

FROM role_migration_map;


-- ============================================================
-- 11.7 PRESERVE LEGACY ROLES WITH NO ASSIGNMENTS
--
-- These roles do not need to be split.
--
-- Example:
--
--     vocals
--
-- with nobody assigned remains:
--
--     vocals
--
-- ============================================================

INSERT INTO public.song_role
(
    id,
    song_id,
    role
)
SELECT

    gen_random_uuid(),

    r.song_id,

    r.role

FROM legacy.song_role r

WHERE NOT EXISTS
    (
        SELECT 1

        FROM legacy.song_role_assignment a

        WHERE a.song_id = r.song_id
          AND a.role    = r.role
    )

  AND NOT EXISTS
    (
        SELECT 1

        FROM role_migration_map m

        WHERE m.old_song_id = r.song_id
          AND m.new_role_name = r.role
    );


-- ============================================================
-- 11.8 VALIDATE FINAL ROLE NAME UNIQUENESS
-- ============================================================

DO $$
    DECLARE

        duplicate_count BIGINT;

    BEGIN

        SELECT COUNT(*)

        INTO duplicate_count

        FROM
            (
                SELECT

                    song_id,

                    role,

                    COUNT(*) AS cnt

                FROM public.song_role

                GROUP BY

                    song_id,

                    role

                HAVING COUNT(*) > 1

            ) duplicates;


        IF duplicate_count > 0 THEN

            RAISE EXCEPTION
                'SONG ROLE DUPLICATES DETECTED: %',
                duplicate_count;

        END IF;

    END
$$;


-- ============================================================
-- 11.9 VALIDATE ASSIGNMENTS POINT TO EXISTING ROLES
-- ============================================================

DO $$
    DECLARE

        invalid_count BIGINT;

    BEGIN

        SELECT COUNT(*)

        INTO invalid_count

        FROM public.song_role_assignment a

        WHERE NOT EXISTS
                  (
                      SELECT 1

                      FROM public.song_role r

                      WHERE r.id = a.role

                        AND r.song_id = a.song_id
                  );


        IF invalid_count > 0 THEN

            RAISE EXCEPTION
                'INVALID SONG ROLE ASSIGNMENTS: %',
                invalid_count;

        END IF;

    END
$$;


-- ============================================================
-- 11.10 VALIDATE EVERY NEW ROLE HAS AT MOST ONE ASSIGNMENT
-- ============================================================

DO $$
    DECLARE

        duplicate_assignment_count BIGINT;

    BEGIN

        SELECT COUNT(*)

        INTO duplicate_assignment_count

        FROM
            (
                SELECT

                    song_id,

                    role,

                    COUNT(*) AS cnt

                FROM public.song_role_assignment

                GROUP BY

                    song_id,

                    role

                HAVING COUNT(*) > 1

            ) duplicates;


        IF duplicate_assignment_count > 0 THEN

            RAISE EXCEPTION
                'ROLE HAS MULTIPLE ASSIGNMENTS: %',
                duplicate_assignment_count;

        END IF;

    END
$$;


-- ============================================================
-- 12. EVENT TRACK ITEMS
-- ============================================================

INSERT INTO public.event_track_item
(
    id,
    event_id,
    position,
    song_id,
    custom_title,
    custom_artist
)
SELECT

    id,

    event_id,

    position,

    song_id,

    custom_title,

    custom_artist

FROM legacy.event_track_item;


-- ============================================================
-- 13. EVENT PARTICIPANTS
-- ============================================================

INSERT INTO public.event_participant
(
    id,
    event_id,
    track_item_id,
    user_id,
    role,
    joined_at
)
SELECT

    id,

    event_id,

    track_item_id,

    user_id,

    role,

    joined_at

FROM legacy.event_participant;


-- ============================================================
-- 14. SONG TOPICS
-- ============================================================

INSERT INTO public.song_topic
(
    song_id,
    topic_id,
    created_at,
    updated_at
)
SELECT

    song_id,

    topic_id,

    created_at,

    updated_at

FROM legacy.song_topic

WHERE topic_id IS NOT NULL;


-- ============================================================
-- 15. FINAL VALIDATION
--
-- IMPORTANT:
--
-- Song roles are NOT expected to have the same count.
--
-- Legacy:
--
--     576 roles
--     325 assignments
--
-- Some legacy roles have multiple assignments.
--
-- New:
--
--     one role per assignment
--     PLUS unassigned legacy roles
--
-- Therefore:
--
--     expected_new_roles =
--
--         legacy_roles
--         - distinct assigned legacy roles
--         + assignments
--
-- ============================================================

DO $$
    DECLARE

        old_count BIGINT;

        new_count BIGINT;

        expected_count BIGINT;

        assigned_role_count BIGINT;

        unassigned_role_count BIGINT;

    BEGIN


        -- --------------------------------------------------------
        -- USERS
        -- --------------------------------------------------------

        SELECT COUNT(*)
        INTO old_count
        FROM legacy.app_user;


        SELECT COUNT(*)
        INTO new_count
        FROM public."AspNetUsers";


        IF old_count <> new_count THEN

            RAISE EXCEPTION
                'USER COUNT MISMATCH: legacy=% new=%',
                old_count,
                new_count;

        END IF;


        -- --------------------------------------------------------
        -- CALENDAR
        -- --------------------------------------------------------

        SELECT COUNT(*)
        INTO old_count
        FROM legacy.calendar;


        SELECT COUNT(*)
        INTO new_count
        FROM public.calendar;


        IF old_count <> new_count THEN

            RAISE EXCEPTION
                'CALENDAR COUNT MISMATCH: legacy=% new=%',
                old_count,
                new_count;

        END IF;


        -- --------------------------------------------------------
        -- EVENTS
        -- --------------------------------------------------------

        SELECT COUNT(*)
        INTO old_count
        FROM legacy.event;


        SELECT COUNT(*)
        INTO new_count
        FROM public.event;


        IF old_count <> new_count THEN

            RAISE EXCEPTION
                'EVENT COUNT MISMATCH: legacy=% new=%',
                old_count,
                new_count;

        END IF;


        -- --------------------------------------------------------
        -- SONGS
        -- --------------------------------------------------------

        SELECT COUNT(*)
        INTO old_count
        FROM legacy.song;


        SELECT COUNT(*)
        INTO new_count
        FROM public.song;


        IF old_count <> new_count THEN

            RAISE EXCEPTION
                'SONG COUNT MISMATCH: legacy=% new=%',
                old_count,
                new_count;

        END IF;


        -- --------------------------------------------------------
        -- ASSIGNED LEGACY ROLES
        --
        -- Because legacy assignment.role is TEXT, distinct role
        -- records are identified by:
        --
        --     song_id + role
        -- --------------------------------------------------------

        SELECT COUNT(*)

        INTO assigned_role_count

        FROM
            (
                SELECT DISTINCT

                    a.song_id,

                    a.role

                FROM legacy.song_role_assignment a

                         JOIN legacy.song_role r

                              ON r.song_id = a.song_id
                                  AND r.role    = a.role

            ) assigned;


        -- --------------------------------------------------------
        -- UNASSIGNED LEGACY ROLES
        -- --------------------------------------------------------

        SELECT COUNT(*)

        INTO unassigned_role_count

        FROM legacy.song_role r

        WHERE NOT EXISTS
                  (
                      SELECT 1

                      FROM legacy.song_role_assignment a

                      WHERE a.song_id = r.song_id
                        AND a.role    = r.role
                  );


        -- --------------------------------------------------------
        -- EXPECTED NEW ROLE COUNT
        --
        -- Assigned roles are replaced by one role per assignment.
        --
        -- Unassigned roles are preserved one-for-one.
        -- --------------------------------------------------------

        SELECT COUNT(*)
        INTO old_count
        FROM legacy.song_role;


        expected_count :=
            old_count
                - assigned_role_count
                + (
                SELECT COUNT(*)
                FROM legacy.song_role_assignment
            );


        SELECT COUNT(*)
        INTO new_count
        FROM public.song_role;


        RAISE NOTICE '';
        RAISE NOTICE '------------------------------------------';
        RAISE NOTICE 'SONG ROLE MIGRATION';
        RAISE NOTICE '------------------------------------------';

        RAISE NOTICE 'Legacy roles:              %',
            old_count;

        RAISE NOTICE 'Assigned legacy roles:     %',
            assigned_role_count;

        RAISE NOTICE 'Unassigned legacy roles:   %',
            unassigned_role_count;

        RAISE NOTICE 'Legacy assignments:        %',
            (
                SELECT COUNT(*)
                FROM legacy.song_role_assignment
            );

        RAISE NOTICE 'Expected new roles:        %',
            expected_count;

        RAISE NOTICE 'Actual new roles:          %',
            new_count;


        IF expected_count <> new_count THEN

            RAISE EXCEPTION
                'SONG ROLE COUNT MISMATCH: expected=% new=%',
                expected_count,
                new_count;

        END IF;


        -- --------------------------------------------------------
        -- ASSIGNMENT COUNT
        -- --------------------------------------------------------

        SELECT COUNT(*)
        INTO old_count
        FROM legacy.song_role_assignment;


        SELECT COUNT(*)
        INTO new_count
        FROM public.song_role_assignment;


        IF old_count <> new_count THEN

            RAISE EXCEPTION
                'SONG ROLE ASSIGNMENT COUNT MISMATCH: legacy=% new=%',
                old_count,
                new_count;

        END IF;


        -- --------------------------------------------------------
        -- EVENT TRACK ITEMS
        -- --------------------------------------------------------

        SELECT COUNT(*)
        INTO old_count
        FROM legacy.event_track_item;


        SELECT COUNT(*)
        INTO new_count
        FROM public.event_track_item;


        IF old_count <> new_count THEN

            RAISE EXCEPTION
                'EVENT TRACK ITEM COUNT MISMATCH: legacy=% new=%',
                old_count,
                new_count;

        END IF;


        -- --------------------------------------------------------
        -- EVENT PARTICIPANTS
        -- --------------------------------------------------------

        SELECT COUNT(*)
        INTO old_count
        FROM legacy.event_participant;


        SELECT COUNT(*)
        INTO new_count
        FROM public.event_participant;


        IF old_count <> new_count THEN

            RAISE EXCEPTION
                'EVENT PARTICIPANT COUNT MISMATCH: legacy=% new=%',
                old_count,
                new_count;

        END IF;


        -- --------------------------------------------------------
        -- SONG TOPICS
        -- --------------------------------------------------------

        SELECT COUNT(*)
        INTO old_count
        FROM legacy.song_topic
        WHERE topic_id IS NOT NULL;


        SELECT COUNT(*)
        INTO new_count
        FROM public.song_topic;


        IF old_count <> new_count THEN

            RAISE EXCEPTION
                'SONG TOPIC COUNT MISMATCH: legacy=% new=%',
                old_count,
                new_count;

        END IF;


        -- --------------------------------------------------------
        -- FINAL SUCCESS
        -- --------------------------------------------------------

        RAISE NOTICE '';
        RAISE NOTICE '==============================================';
        RAISE NOTICE '       MIGRATION COMPLETED SUCCESSFULLY';
        RAISE NOTICE '==============================================';
        RAISE NOTICE '';

    END
$$;


-- ============================================================
-- 16. COMMIT
-- ============================================================

COMMIT;
