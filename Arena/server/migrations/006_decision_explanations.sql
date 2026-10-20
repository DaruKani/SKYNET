-- Existing settings keep their runtime, profiles, credentials and history.
UPDATE arena_settings
SET settings_json = json_set(settings_json, '$.windbots.current.skynet.explainDecisions', json('false'))
WHERE json_type(settings_json, '$.windbots.current.skynet.explainDecisions') IS NULL;

UPDATE arena_settings
SET settings_json = json_set(settings_json, '$.windbots.old.skynet.explainDecisions', json('false'))
WHERE json_type(settings_json, '$.windbots.old.skynet.explainDecisions') IS NULL;
