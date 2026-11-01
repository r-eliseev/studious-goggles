ALTER TABLE chat_messages ADD COLUMN cost_usd TEXT NULL;
ALTER TABLE chat_messages ADD COLUMN provider_id TEXT NULL;
ALTER TABLE chat_messages ADD COLUMN model_id TEXT NULL;

CREATE TABLE expenses (
    operation_id TEXT NOT NULL PRIMARY KEY,
    kind INTEGER NOT NULL,
    provider_id TEXT NOT NULL,
    model_id TEXT NOT NULL,
    cost_usd TEXT NOT NULL,
    occurred_at TEXT NOT NULL,
    session_id TEXT NULL
);

CREATE INDEX ix_expenses_occurred_at ON expenses(occurred_at);

INSERT OR IGNORE INTO expenses (operation_id, kind, provider_id, model_id, cost_usd, occurred_at, session_id)
SELECT 'gen:' || id, 0, provider_id, model_id, cost_usd, COALESCE(completed_at, created_at), session_id
FROM generations
WHERE cost_usd IS NOT NULL;

INSERT OR IGNORE INTO expenses (operation_id, kind, provider_id, model_id, cost_usd, occurred_at, session_id)
SELECT 'desc:' || id, 3, provider_id, model_id, cost_usd, created_at, session_id
FROM image_descriptions
WHERE cost_usd IS NOT NULL;

INSERT OR IGNORE INTO expenses (operation_id, kind, provider_id, model_id, cost_usd, occurred_at, session_id)
SELECT 'sum:' || session_id || ':' || covers_up_to_message_id, 2, provider_id, model_id, cost_usd, created_at, session_id
FROM conversation_summaries
WHERE cost_usd IS NOT NULL;
