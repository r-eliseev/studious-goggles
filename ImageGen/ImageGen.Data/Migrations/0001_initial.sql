CREATE TABLE sessions (
    id TEXT NOT NULL PRIMARY KEY,
    title TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    settings_json TEXT NOT NULL
);

CREATE TABLE chat_messages (
    id TEXT NOT NULL PRIMARY KEY,
    session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    role INTEGER NOT NULL,
    text TEXT NOT NULL,
    created_at TEXT NOT NULL,
    run_id TEXT NULL,
    is_error INTEGER NOT NULL
);

CREATE INDEX ix_chat_messages_session ON chat_messages(session_id, created_at);

CREATE TABLE runs (
    id TEXT NOT NULL PRIMARY KEY,
    session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    prompt TEXT NOT NULL,
    origin INTEGER NOT NULL,
    created_at TEXT NOT NULL
);

CREATE INDEX ix_runs_session ON runs(session_id, created_at);

CREATE TABLE generations (
    id TEXT NOT NULL PRIMARY KEY,
    run_id TEXT NOT NULL REFERENCES runs(id) ON DELETE CASCADE,
    session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    model_id TEXT NOT NULL,
    prompt TEXT NOT NULL,
    seed INTEGER NOT NULL,
    seed_applied INTEGER NOT NULL,
    status INTEGER NOT NULL,
    image_path TEXT NULL,
    media_type TEXT NULL,
    cost_usd TEXT NULL,
    error_kind INTEGER NULL,
    error_message TEXT NULL,
    created_at TEXT NOT NULL,
    completed_at TEXT NULL,
    sort_order INTEGER NOT NULL
);

CREATE INDEX ix_generations_run ON generations(run_id, sort_order);
CREATE INDEX ix_generations_session ON generations(session_id);
CREATE INDEX ix_generations_status ON generations(status);

CREATE TABLE image_models (
    id TEXT NOT NULL PRIMARY KEY,
    name TEXT NOT NULL,
    supports_seed INTEGER NOT NULL,
    supports_image_input INTEGER NOT NULL,
    max_images_per_request INTEGER NOT NULL,
    fetched_at TEXT NOT NULL
);
