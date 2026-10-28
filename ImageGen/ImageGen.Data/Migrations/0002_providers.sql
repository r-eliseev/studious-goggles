-- Provider ids (задача 036). Existing rows came from OpenRouter, the only provider before this migration.

ALTER TABLE generations ADD COLUMN provider_id TEXT NOT NULL DEFAULT 'openrouter';

-- SQLite cannot change a primary key with ALTER: copy the cache into a table with the new key, then replace it.
CREATE TABLE image_models_new (
    provider_id TEXT NOT NULL,
    id TEXT NOT NULL,
    name TEXT NOT NULL,
    supports_seed INTEGER NOT NULL,
    supports_image_input INTEGER NOT NULL,
    max_images_per_request INTEGER NOT NULL,
    fetched_at TEXT NOT NULL,
    PRIMARY KEY (provider_id, id)
);

INSERT INTO image_models_new (provider_id, id, name, supports_seed, supports_image_input, max_images_per_request, fetched_at)
SELECT 'openrouter', id, name, supports_seed, supports_image_input, max_images_per_request, fetched_at
FROM image_models;

DROP TABLE image_models;

ALTER TABLE image_models_new RENAME TO image_models;
