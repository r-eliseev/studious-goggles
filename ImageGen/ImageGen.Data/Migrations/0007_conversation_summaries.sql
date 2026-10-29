-- Task 083, ADR 0015: one rolling summary of the older part of a session's history. A new summary replaces the old one.
CREATE TABLE conversation_summaries (
    session_id TEXT NOT NULL PRIMARY KEY REFERENCES sessions(id) ON DELETE CASCADE,
    text TEXT NOT NULL,
    covers_up_to_message_id TEXT NOT NULL,
    provider_id TEXT NOT NULL,
    model_id TEXT NOT NULL,
    cost_usd TEXT NULL,
    created_at TEXT NOT NULL
);
