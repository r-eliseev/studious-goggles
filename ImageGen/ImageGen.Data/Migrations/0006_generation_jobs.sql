-- Task 025: the queued provider job of a generation (JSON of its handle), so the job is followed again after a restart.
-- NULL for generations without a queued job, including all rows written before this migration.
ALTER TABLE generations ADD COLUMN job_json TEXT NULL;
