CREATE TABLE text_embeddings (
    source_key TEXT NOT NULL PRIMARY KEY,
    session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    kind INTEGER NOT NULL,
    content_hash TEXT NOT NULL,
    model TEXT NOT NULL,
    dimensions INTEGER NOT NULL,
    vector BLOB NOT NULL,
    created_at TEXT NOT NULL
);

CREATE INDEX ix_text_embeddings_session_id ON text_embeddings(session_id);
