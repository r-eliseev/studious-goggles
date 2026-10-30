-- Task 093: image model prices are catalog cache and are refetched when the model catalog is next opened.
ALTER TABLE image_models ADD COLUMN prices_json TEXT NULL;
DELETE FROM image_models;
