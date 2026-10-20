# Skynet Arena contributor instructions

Skynet Arena is a self-hosted Node.js duel experiment console derived from WindBot Arena. It manages experiments through a browser, launches native SKYNET clients or calls two compatible WindBot Servers to create best-of-three matches on a user-selected, dedicated [arena-srvpro](https://github.com/mercury233/arena-srvpro) instance, actively polls that server's rankings, and stores configuration, experiment history and statistics in SQLite. A dedicated arena-srvpro instance is required; a generic duel server is not a substitute for its management and cumulative-ranking APIs.

## Architecture

- `client/`: Vue 3 + Naive UI frontend, communicating with the server through same-origin JSON APIs and Server-Sent Events. SSE heartbeats carry only version counters for separate data domains; the frontend fetches `system`, run lists or active runs when their counters change.
- `server/app.js`: website, API and live-event entry points.
- `server/arena-service.js`: SRVPro restarts, AI lifecycle coordination, room scheduling and result settlement.
- `server/skynet-runtime.js`: native SKYNET process ownership, whole-room capacity reservations, launch arguments, bounded diagnostics, deadlines and cancellation.
- `server/skynet-config.js`: explicit SKYNET executable, database and deck-profile configuration and validation.
- `server/arena-settings.js`: application settings stored in SQLite, validation and sensitive-field redaction.
- `server/database.js` and `server/migrations/`: SQLite access and versioned schema migrations.
- `server/bot-config.js`: local or pasted remote `bot.conf` parsing, explicit SKYNET profile catalogs and matchup construction.

Only the listening address, HTTP port and SQLite path are environment-level startup settings. SRVPro, AI runtime and scheduling settings must be configured through the browser and saved in SQLite. Do not reintroduce project configuration files such as `settings.js`.

There are three runtime modes:

- `skynet`: Arena starts one owned `SKYNET.exe` process per match participant, using explicitly configured executable, database, deck and optional model paths. `Agent=skynet` is the general policy; `Agent=expert-oldschool` is an explicit, frozen expert comparison. Expert mode does not accept a learned model. Arena forces training off, reserves all local participant slots for a room together, and enforces process limits and a deadline covering capacity wait and execution. Do not discover files outside the configured paths or silently enable archived executors. SKYNET tag duels remain disabled until validated.
- `local`: Arena starts and stops a compatible `WindBot.exe` HTTP server and reads its local `bot.conf` directly.
- `remote`: the user runs WindBot Server manually. Arena only calls its remote HTTP endpoint. Because Arena cannot read that host's filesystem, the user pastes `bot.conf` into the browser and its contents are stored in SQLite.

The existing `windbots.current` and `windbots.old` configuration keys remain for compatibility and can select any supported mode. Do not rename persisted keys or remove upstream WindBot compatibility as part of presentation changes.

Arena generates a unique match room name such as `M#123456789` for each group of bots and passes it to both sides. SRVPro places participants using that name in the same room and records ordinary match results in its cumulative rankings. Room names are generated internally, not configured by users. While a run is active, Arena uses `GET /api/getroomscount` for lightweight room counts and polls `GET /api/getscores` with administrative credentials every 15 seconds for cumulative rankings. Room cleanup and server restarts also use administrative APIs. Compatible tag experiments use generated `T#...` room names.

## Pairing and statistics constraints

- Each group uses one unique room name, ensuring its participants join the same room. Different groups must never reuse room names.
- A WindBot Server HTTP response only confirms that a participant's launch request was accepted; it cannot directly undo a bot that already joined. If either participant fails to start or times out, Arena must use SRVPro's room-close API to clean up that group's unique room. Only if room cleanup also fails may the first participant temporarily remain in an isolated room, which must not be paired with a later duel.
- Native SKYNET launch rollback must also stop its owned participant processes. Do not terminate unrelated processes. Keep process capacity occupied until actual process termination is confirmed; failed cleanup must be reported rather than treated as success.
- SRVPro's room list excludes rooms that are not yet established, so fast scheduling can temporarily exceed `maxRooms`. A small transient overshoot is acceptable. Do not introduce reservations for in-flight SRVPro rooms solely to remove this discrepancy. This is distinct from the required native SKYNET process-capacity reservations. The extreme case where all 100 rooms become permanently stuck and scheduling cannot advance does not need separate handling.
- There is currently no independent per-duel scoring source: wins and losses must come from SRVPro's cumulative match rankings. SKYNET console markers diagnose process completion; they do not replace the ranking source or turn disconnects into losses.

## Reading win rates from SQLite manually

The default database is `data/arena.sqlite`. Open it read-only from the Arena directory using the project's `better-sqlite3` dependency. Do not read deprecated precomputed win-rate fields. In regression runs, `competitors.slot = 1` identifies the current version and `slot = 2` the previous version. The displayed current-version win rate is always `current wins / (current wins + previous wins)`. Inspect `flee` separately as an anomaly count; do not add it to the denominator and penalize it twice.

Overall win rate for the most recent run:

```sql
WITH latest_run AS (
    SELECT id FROM runs ORDER BY created_at DESC LIMIT 1
)
SELECT
    latest_run.id AS run_id,
    SUM(CASE WHEN competitors.slot = 1 THEN competitors.win ELSE 0 END) AS current_wins,
    SUM(CASE WHEN competitors.slot = 2 THEN competitors.win ELSE 0 END) AS old_wins,
    ROUND(
        100.0 * SUM(CASE WHEN competitors.slot = 1 THEN competitors.win ELSE 0 END)
        / NULLIF(SUM(competitors.win), 0),
        1
    ) AS current_win_rate
FROM latest_run
JOIN matchups ON matchups.run_id = latest_run.id
JOIN competitors ON competitors.matchup_id = matchups.id
GROUP BY latest_run.id;
```

Per-deck win rates and observed completion counts for the most recent run:

```sql
WITH latest_run AS (
    SELECT id FROM runs ORDER BY created_at DESC LIMIT 1
)
SELECT
    matchups.label AS deck,
    matchups.ai_level,
    MIN(competitors.observed_games) AS observed_games,
    MAX(CASE WHEN competitors.slot = 1 THEN competitors.win END) AS current_wins,
    MAX(CASE WHEN competitors.slot = 2 THEN competitors.win END) AS old_wins,
    ROUND(
        100.0 * MAX(CASE WHEN competitors.slot = 1 THEN competitors.win END)
        / NULLIF(SUM(competitors.win), 0),
        1
    ) AS current_win_rate
FROM latest_run
JOIN matchups ON matchups.run_id = latest_run.id
JOIN competitors ON competitors.matchup_id = matchups.id
GROUP BY matchups.id, matchups.label, matchups.ai_level
ORDER BY matchups.ai_level IS NULL, matchups.ai_level DESC, matchups.label;
```

## Development requirements

- The SRVPro instances supplied by the user in this project's configuration are dedicated to Arena. Connecting, querying, restarting and cleaning their rooms is allowed; no unrelated tasks on those instances need preservation.
- Run only one experiment at a time on each SRVPro instance. Experiments on different instances may run concurrently. Concurrent runs share the two WindBot endpoints; clean up local WindBot server processes only after the last active run finishes. Native SKYNET processes instead belong to their individual rooms and runs, while their shared capacity limits apply across concurrent runs.
- Never store SRVPro administrative passwords in run records. Settings-read APIs must not return them to the browser.
- Arena must clean up locally owned processes when runs complete, fail or stop, and when the service shuts down. Remote-mode processes are not owned by Arena.
- Keep the API same-origin when deploying on a NAS or behind a reverse proxy. Protect the console with the reverse proxy's access controls. Arena actively queries the SRVPro management port for rankings; this does not require exposing any additional inbound endpoint. The local console defaults to loopback.
- Every database field addition or change must include a migration that runs automatically during startup, upgrades existing databases and preserves their data. Do not only edit the initial migration or require users to migrate manually. Record migration execution so it cannot run twice.

## Related source

Use explicitly selected source locations; do not search parent or sibling directories automatically at runtime.

- The SKYNET repository root is `..` relative to `Arena/`. `Program.cs` defines the native client CLI used by each match participant. `Game/AI/AgentFactory.cs` selects the general or explicit expert lifecycle.
- The upstream development layout places `arena-srvpro` beside the Arena checkout (`../arena-srvpro`). If that checkout is present at an explicitly configured location, inspect its `index.js` for room and ranking API behavior. Room counts and match rankings are queried through `/api/getroomscount` and `/api/getscores` respectively. This repository does not assume that upstream layout or provide the server automatically.
- The upstream layout similarly uses `../windbot` for a separate WindBot source checkout. When a compatible checkout has been explicitly supplied, its `Program.cs` defines the ServerMode HTTP protocol and its own implementation defines deck/runtime resource behavior. The ignored `LegacyWindBot` archive in SKYNET is reference material, not an automatically enabled runtime collection.

## Validation priorities

- When changing the settings model, cover validation, sensitive-field redaction and SQLite persistence.
- When changing matchup construction, cover native SKYNET plus local and remote WindBot. Verify deck intersections, dialogs, profiles and endpoints.
- Scheduling tests must not actually restart SRVPro or launch WindBot. Use unit tests and HTTP smoke checks that do not access external services. Exercise SKYNET process ownership and cancellation with controlled fixtures; distinguish these from explicitly requested real-duel validation.
- Frontend changes require at least `npm.cmd run build`. Backend and data-layer changes require at least `npm.cmd test`.
