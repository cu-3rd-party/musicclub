-- Initial schema derived from Alembic migration d4c8d9a520d1 and subsequent updates.

-- Concerts table
CREATE TABLE IF NOT EXISTS concerts (
    id SERIAL PRIMARY KEY,
    name VARCHAR(150) NOT NULL,
    date DATE DEFAULT now()
);

-- People table
CREATE TABLE IF NOT EXISTS people (
    id BIGINT PRIMARY KEY,
    name VARCHAR(100) NOT NULL
);

-- Songs table
CREATE TABLE IF NOT EXISTS songs (
    id SERIAL PRIMARY KEY,
    title VARCHAR(200) NOT NULL,
    link VARCHAR(200)
);

-- Song participations table
CREATE TABLE IF NOT EXISTS song_participations (
    id SERIAL PRIMARY KEY,
    song_id INTEGER NOT NULL REFERENCES songs(id),
    person_id BIGINT NOT NULL REFERENCES people(id),
    role VARCHAR(200) NOT NULL,
    CONSTRAINT unique_song_role_per_person UNIQUE (song_id, person_id, role)
);

-- Tracklist entries table
CREATE TABLE IF NOT EXISTS tracklist_entries (
    id SERIAL PRIMARY KEY,
    concert_id INTEGER NOT NULL REFERENCES concerts(id) ON DELETE CASCADE,
    song_id INTEGER NOT NULL REFERENCES songs(id),
    position INTEGER NOT NULL,
    CONSTRAINT unique_song_position_per_concert UNIQUE (concert_id, position)
);

-- Add description to songs (from migration d19f157143d1)
ALTER TABLE songs
    ADD COLUMN IF NOT EXISTS description VARCHAR(500);

-- Pending roles table (from migration 9845bf0b8a8e)
CREATE TABLE IF NOT EXISTS pending_roles (
    id SERIAL PRIMARY KEY,
    song_id INTEGER NOT NULL REFERENCES songs(id),
    role VARCHAR(200) NOT NULL,
    created_at TIMESTAMP DEFAULT now() NOT NULL
);
