-- Task 043: the code of an error message, so the interface shows its text in the current language. NULL for messages
-- without a code, including every message saved before this migration (their text is shown as it was saved).
ALTER TABLE chat_messages ADD COLUMN error_kind INTEGER NULL;
