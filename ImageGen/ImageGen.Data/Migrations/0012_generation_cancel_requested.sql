-- Persist a provider's acknowledgement that a user cancellation was requested but is not yet terminal.
ALTER TABLE generations ADD COLUMN cancel_requested_at TEXT NULL;
