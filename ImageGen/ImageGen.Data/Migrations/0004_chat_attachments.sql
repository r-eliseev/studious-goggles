-- Task 018: images the user attaches to a message, and the reference images of a generation.
-- A message keeps its attachments; deleting the message only unlinks them (the file goes with the session).
CREATE TABLE chat_attachments (
    id TEXT NOT NULL PRIMARY KEY,
    session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    message_id TEXT NULL REFERENCES chat_messages(id) ON DELETE SET NULL,
    relative_path TEXT NOT NULL,
    media_type TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE INDEX chat_attachments_message ON chat_attachments(message_id);

-- Attachment ids of the reference images, as a JSON array; NULL for generations made before this migration.
ALTER TABLE generations ADD COLUMN reference_attachment_ids_json TEXT NULL;
