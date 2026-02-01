CREATE TABLE IF NOT EXISTS song_topic (
    song_id UUID PRIMARY KEY REFERENCES song(id) ON DELETE CASCADE,
    topic_id BIGINT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_song_topic_topic_id ON song_topic (topic_id);
