CREATE TABLE IF NOT EXISTS restaurants (
    id uuid PRIMARY KEY,
    name varchar(200) NOT NULL,
    slug varchar(200) NOT NULL UNIQUE,
    phone varchar(32) NOT NULL DEFAULT '',
    whatsapp_phone varchar(32) NOT NULL DEFAULT '',
    email varchar(256) NOT NULL DEFAULT '',
    address varchar(512) NOT NULL DEFAULT '',
    logo_url varchar(512) NOT NULL DEFAULT '',
    default_language varchar(8) NOT NULL DEFAULT 'tr',
    currency varchar(8) NOT NULL DEFAULT 'TRY',
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamptz NOT NULL DEFAULT NOW(),
    updated_at timestamptz NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS users (
    id uuid PRIMARY KEY,
    restaurant_id uuid NOT NULL REFERENCES restaurants(id) ON DELETE CASCADE,
    full_name varchar(200) NOT NULL,
    email varchar(256) NOT NULL UNIQUE,
    phone varchar(32) NOT NULL DEFAULT '',
    password_hash text NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamptz NOT NULL DEFAULT NOW(),
    updated_at timestamptz NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS roles (
    code varchar(64) PRIMARY KEY,
    name varchar(128) NOT NULL
);

CREATE TABLE IF NOT EXISTS user_roles (
    user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_code varchar(64) NOT NULL REFERENCES roles(code) ON DELETE CASCADE,
    PRIMARY KEY (user_id, role_code)
);

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash varchar(128) NOT NULL UNIQUE,
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT NOW(),
    created_by_ip varchar(64) NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS audit_logs (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    restaurant_id uuid NULL REFERENCES restaurants(id) ON DELETE SET NULL,
    user_id uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    action_type varchar(128) NOT NULL,
    entity_name varchar(128) NOT NULL,
    entity_id varchar(128) NOT NULL,
    payload jsonb NULL,
    created_at timestamptz NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_users_restaurant_id ON users (restaurant_id);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user_id ON refresh_tokens (user_id);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_token_hash ON refresh_tokens (token_hash);
CREATE INDEX IF NOT EXISTS ix_audit_logs_restaurant_id_created_at ON audit_logs (restaurant_id, created_at DESC);
