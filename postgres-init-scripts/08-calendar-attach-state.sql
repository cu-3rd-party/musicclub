CREATE TABLE IF NOT EXISTS calendar_attach_state (
    tg_user_id BIGINT PRIMARY KEY,
    state SMALLINT NOT NULL,
    pending_user_id UUID,
    pending_email TEXT,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
