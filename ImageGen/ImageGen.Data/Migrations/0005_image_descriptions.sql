-- Task 019: texts the assistant model wrote about a picture. The price is NULL when the provider did not say.
CREATE TABLE image_descriptions (
    id TEXT NOT NULL PRIMARY KEY,
    session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    source_generation_id TEXT NOT NULL,
    mode INTEGER NOT NULL,
    provider_id TEXT NOT NULL,
    model_id TEXT NOT NULL,
    text TEXT NOT NULL,
    cost_usd TEXT NULL,
    created_at TEXT NOT NULL
);

CREATE INDEX image_descriptions_session ON image_descriptions(session_id);
