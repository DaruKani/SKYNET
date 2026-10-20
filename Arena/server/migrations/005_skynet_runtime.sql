-- Add optional local SKYNET settings without replacing existing WindBot endpoints.
-- Existing databases keep their selected execution mode and all saved runs.
UPDATE arena_settings
SET settings_json = json_set(settings_json,
    '$.windbots.current.skynet', json('{"executablePath":"","databasePath":"","version":4962,"maxProcesses":4,"duelTimeoutSeconds":300,"profiles":[]}')
)
WHERE json_type(settings_json, '$.windbots.current.skynet') IS NULL;

UPDATE arena_settings
SET settings_json = json_set(settings_json,
    '$.windbots.old.skynet', json('{"executablePath":"","databasePath":"","version":4962,"maxProcesses":4,"duelTimeoutSeconds":300,"profiles":[]}')
)
WHERE json_type(settings_json, '$.windbots.old.skynet') IS NULL;
