CREATE TABLE embedding_models (
    provider_id TEXT NOT NULL,
    id TEXT NOT NULL,
    json TEXT NOT NULL,
    fetched_at TEXT NOT NULL,
    PRIMARY KEY (provider_id, id)
);
