-- Task 020: parameters of the catalog, of the runs and of the generations.
-- NULL means "not stored": an old cache row, an old run or an old generation.
ALTER TABLE image_models ADD COLUMN supported_parameters_json TEXT NULL;
ALTER TABLE runs ADD COLUMN parameters_json TEXT NULL;
ALTER TABLE generations ADD COLUMN outcomes_json TEXT NULL;
