create table if not exists calendar (
    user_id UUID PRIMARY KEY REFERENCES app_user(id) ON DELETE CASCADE,
    calendar_url URL NOT NULL,

    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
