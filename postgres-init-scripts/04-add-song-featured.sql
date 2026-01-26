-- Add featured flag to songs and permissions for featuring
ALTER TABLE song ADD COLUMN IF NOT EXISTS is_featured BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE user_permissions ADD COLUMN IF NOT EXISTS edit_featured_songs BOOLEAN NOT NULL DEFAULT FALSE;
