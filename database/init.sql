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

CREATE TABLE IF NOT EXISTS menu_categories (
    id uuid PRIMARY KEY,
    restaurant_id uuid NOT NULL REFERENCES restaurants(id) ON DELETE CASCADE,
    name varchar(200) NOT NULL,
    slug varchar(200) NOT NULL,
    description text NOT NULL DEFAULT '',
    image_url varchar(512) NOT NULL DEFAULT '',
    sort_order integer NOT NULL DEFAULT 0,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamptz NOT NULL DEFAULT NOW(),
    updated_at timestamptz NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_menu_categories_restaurant_slug UNIQUE (restaurant_id, slug)
);

CREATE TABLE IF NOT EXISTS menu_category_translations (
    category_id uuid NOT NULL REFERENCES menu_categories(id) ON DELETE CASCADE,
    language_code varchar(8) NOT NULL,
    name varchar(200) NOT NULL,
    description text NOT NULL DEFAULT '',
    PRIMARY KEY (category_id, language_code)
);

CREATE TABLE IF NOT EXISTS menu_items (
    id uuid PRIMARY KEY,
    restaurant_id uuid NOT NULL REFERENCES restaurants(id) ON DELETE CASCADE,
    category_id uuid NOT NULL REFERENCES menu_categories(id) ON DELETE CASCADE,
    name varchar(200) NOT NULL,
    slug varchar(200) NOT NULL,
    description text NOT NULL DEFAULT '',
    price numeric(12,2) NOT NULL,
    discounted_price numeric(12,2) NULL,
    currency varchar(8) NOT NULL DEFAULT 'TRY',
    image_url varchar(512) NOT NULL DEFAULT '',
    preparation_time_minutes integer NULL,
    calories integer NULL,
    spice_level integer NOT NULL DEFAULT 0,
    is_vegetarian boolean NOT NULL DEFAULT FALSE,
    is_vegan boolean NOT NULL DEFAULT FALSE,
    is_gluten_free boolean NOT NULL DEFAULT FALSE,
    is_featured boolean NOT NULL DEFAULT FALSE,
    is_available boolean NOT NULL DEFAULT TRUE,
    is_active boolean NOT NULL DEFAULT TRUE,
    sort_order integer NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT NOW(),
    updated_at timestamptz NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_menu_items_restaurant_slug UNIQUE (restaurant_id, slug),
    CONSTRAINT chk_menu_items_price CHECK (price >= 0),
    CONSTRAINT chk_menu_items_discounted_price CHECK (discounted_price IS NULL OR (discounted_price >= 0 AND discounted_price <= price)),
    CONSTRAINT chk_menu_items_spice_level CHECK (spice_level >= 0 AND spice_level <= 5)
);

CREATE TABLE IF NOT EXISTS menu_item_translations (
    menu_item_id uuid NOT NULL REFERENCES menu_items(id) ON DELETE CASCADE,
    language_code varchar(8) NOT NULL,
    name varchar(200) NOT NULL,
    description text NOT NULL DEFAULT '',
    PRIMARY KEY (menu_item_id, language_code)
);

CREATE TABLE IF NOT EXISTS languages (
    code varchar(8) PRIMARY KEY,
    name varchar(80) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS restaurant_languages (
    restaurant_id uuid NOT NULL REFERENCES restaurants(id) ON DELETE CASCADE,
    language_code varchar(8) NOT NULL REFERENCES languages(code) ON DELETE CASCADE,
    is_enabled boolean NOT NULL DEFAULT TRUE,
    PRIMARY KEY (restaurant_id, language_code)
);

INSERT INTO languages (code, name, is_active)
VALUES
    ('tr', 'Turkce', TRUE),
    ('en', 'English', TRUE),
    ('de', 'Deutsch', TRUE),
    ('ru', 'Russkiy', TRUE)
ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    is_active = EXCLUDED.is_active;

CREATE INDEX IF NOT EXISTS ix_users_restaurant_id ON users (restaurant_id);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user_id ON refresh_tokens (user_id);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_token_hash ON refresh_tokens (token_hash);
CREATE INDEX IF NOT EXISTS ix_audit_logs_restaurant_id_created_at ON audit_logs (restaurant_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_menu_categories_restaurant_sort_order ON menu_categories (restaurant_id, sort_order);
CREATE INDEX IF NOT EXISTS ix_menu_categories_restaurant_is_active ON menu_categories (restaurant_id, is_active);
CREATE INDEX IF NOT EXISTS ix_menu_items_restaurant_category_sort_order ON menu_items (restaurant_id, category_id, sort_order);
CREATE INDEX IF NOT EXISTS ix_menu_items_restaurant_is_active ON menu_items (restaurant_id, is_active);
CREATE INDEX IF NOT EXISTS ix_restaurant_languages_restaurant_enabled ON restaurant_languages (restaurant_id, is_enabled);

