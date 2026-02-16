ALTER TABLE app_user
    ADD COLUMN IF NOT EXISTS email TEXT;

CREATE UNIQUE INDEX IF NOT EXISTS idx_app_user_email ON app_user (email);
