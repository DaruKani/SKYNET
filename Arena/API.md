# Skynet Arena read-only run API

This document defines the read-only run-data API exposed by Skynet Arena. Its supported public scope is limited to:

- Listing runs.
- Listing active runs.
- Reading the complete details of one run.

Other `/api` paths are internal management interfaces for the Arena console. Even if accessible, they are outside this public API contract. External programs must not call the internal endpoints that create or stop runs, change settings or refresh configuration.

## Conventions

The API uses the same origin as the Arena website. For example, if the website is:

```text
https://arena.example.com/
```

then the API base URL is:

```text
https://arena.example.com/api
```

All ordinary responses are JSON. Timestamp fields use UTC in ISO 8601 format, such as `2026-08-13T08:30:00.000Z`. Responses include `Cache-Control: no-store`; clients must not rely on HTTP caching.

The application currently does not require an API key. If its reverse proxy enforces login, Basic Auth or another access-control mechanism, API requests must provide the corresponding credentials. The local console defaults to a loopback listener; protect any externally reachable deployment with access controls.

The server does not enable Cross-Origin Resource Sharing (CORS). Direct browser navigation, curl and server-side programs are unaffected. Browser scripts on another origin should use a same-origin reverse proxy.

Clients should ignore unknown response fields to remain compatible with future additions. Human-readable messages are descriptive text, not stable machine identifiers. Persisted records may retain messages or labels from earlier versions.

## Quick start

Read recent runs directly in a browser:

```text
https://arena.example.com/api/runs?limit=10&offset=0
```

Using curl:

```bash
curl "https://arena.example.com/api/runs?limit=10&offset=0"
```

Read one run:

```bash
curl "https://arena.example.com/api/runs/42cc5a41-3b25-46e5-b010-9cf47c9a61ea"
```

From a script hosted on the same origin as Arena:

```js
const response = await fetch('/api/runs?limit=10&offset=0');
if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
}
const { runs, total } = await response.json();
```

## List runs

```http
GET /api/runs?limit=30&offset=0
```

Runs are ordered by creation time, newest first.

### Query parameters

| Parameter | Type | Default | Meaning |
| --- | --- | --- | --- |
| `limit` | integer | `30` | Number of records to return, from 1 to 100 |
| `offset` | integer | `0` | Number of records to skip; must be at least 0 |

### Example response

```json
{
  "runs": [
    {
      "config": {
        "duelServer": "srvpro.lan:7911",
        "selection": "selected",
        "srvproId": "srvpro-1",
        "srvproName": "SRVPro 1",
        "targetDeck": null,
        "windbots": {
          "current": { "endpoint": "127.0.0.1:2399", "mode": "local" },
          "old": { "endpoint": "windbot-old.lan:2398", "mode": "remote" }
        }
      },
      "createdAt": "2026-08-13T08:30:00.000Z",
      "deckName": "Dragon",
      "error": null,
      "finishedAt": "2026-08-13T09:10:00.000Z",
      "gamesPerMatchup": 100,
      "id": "42cc5a41-3b25-46e5-b010-9cf47c9a61ea",
      "kind": "regression",
      "latestRankAt": "2026-08-13T09:10:00.000Z",
      "launchedGames": 100,
      "matchupCount": 1,
      "roomCount": 0,
      "srvproId": "srvpro-1",
      "startedAt": "2026-08-13T08:30:08.000Z",
      "status": "completed",
      "stopReason": "Experiment completed",
      "totalGames": 100
    }
  ],
  "total": 1
}
```

Entries in `runs` are summaries. They do not include `matchups`, `observedGames` or `events`. Request an individual run for win/loss statistics. `total` is the total number of runs and is unaffected by `limit` or `offset`.

`deckName` is only a list-summary field. The authoritative deck and pairing details are in the individual run's `matchups`.

The `windbots` key remains for compatibility. Each configured instance can use `local` or `remote` WindBot mode, or native `skynet` mode. For `skynet`, the configuration snapshot has `endpoint: null` and a `skynet` object containing the explicit executable/database paths, version, process limit, timeout and profile configuration. Profiles identify their deck path, agent and optional model. These snapshots can contain local filesystem paths; apply the same access controls as the console.

Native SKYNET uses one owned process per participant through `server/skynet-runtime.js`, with training disabled. Its console lifecycle markers diagnose completion; results still come from the dedicated arena-srvpro cumulative rankings. Neither these read-only APIs nor the native runtime eliminate the arena-srvpro requirement.

## List active runs

```http
GET /api/runs/active
```

Returns unfinished runs across all configured SRVPro instances, ordered by creation time, newest first. Different instances can run experiments concurrently, so `runs` may contain multiple entries.

The response is `{ "runs": RunDetail[] }`. Each entry has the same complete structure as `run` in the individual-run endpoint.

When no runs are active:

```json
{
  "runs": []
}
```

Active statuses:

| Status | Meaning |
| --- | --- |
| `preparing` | Preparing the environment |
| `running` | Running duels |
| `settling` | Scheduling has stopped; waiting for results to stabilize |
| `stopping` | Stopping the run |

## Read one run

```http
GET /api/runs/{id}
```

Successful response:

```json
{
  "run": {
    "config": {
      "duelServer": "srvpro.lan:7911",
      "selection": "selected",
      "srvproId": "srvpro-1",
      "srvproName": "SRVPro 1",
      "targetDeck": null,
      "windbots": {
        "current": { "endpoint": "127.0.0.1:2399", "mode": "local" },
        "old": { "endpoint": "windbot-old.lan:2398", "mode": "remote" }
      }
    },
    "createdAt": "2026-08-13T08:30:00.000Z",
    "error": null,
    "events": [
      {
        "at": "2026-08-13T08:30:00.000Z",
        "id": 101,
        "level": "info",
        "message": "Experiment created with 1 deck",
        "type": "created"
      }
    ],
    "finishedAt": null,
    "gamesPerMatchup": 100,
    "id": "42cc5a41-3b25-46e5-b010-9cf47c9a61ea",
    "kind": "regression",
    "latestRankAt": "2026-08-13T08:45:00.000Z",
    "launchedGames": 40,
    "matchups": [
      {
        "aiLevel": 4,
        "competitors": [
          {
            "botLabel": "Dragon",
            "combo": 3,
            "deck": "Dragon",
            "dialog": null,
            "endpointHost": "127.0.0.1",
            "endpointPort": 2399,
            "executionMode": "local",
            "flee": 1,
            "games": 37,
            "id": 201,
            "lose": 15,
            "rankName": "Current-Dragon",
            "slot": 1,
            "source": "current",
            "win": 22
          },
          {
            "botLabel": "Legacy Dragon",
            "combo": 2,
            "deck": "Dragon",
            "dialog": null,
            "endpointHost": "windbot-old.lan",
            "endpointPort": 2398,
            "executionMode": "remote",
            "flee": 0,
            "games": 37,
            "id": 202,
            "lose": 22,
            "rankName": "Previous-Dragon",
            "slot": 2,
            "source": "old",
            "win": 15
          }
        ],
        "currentWinRate": 0.5945945945945946,
        "id": 301,
        "label": "Dragon",
        "launchedGames": 40,
        "observedGames": 37,
        "targetGames": 100
      }
    ],
    "observedGames": 37,
    "roomCount": 3,
    "srvproId": "srvpro-1",
    "startedAt": "2026-08-13T08:30:08.000Z",
    "status": "running",
    "stopReason": null,
    "totalGames": 100
  }
}
```

If the run does not exist, the endpoint returns HTTP `404`:

```json
{
  "error": "Run not found"
}
```

### Run fields

| Field | Type | Meaning |
| --- | --- | --- |
| `id` | string | Run UUID |
| `kind` | string | `regression`, `challenge`, `ranking` or compatible WindBot `tag` experiment |
| `status` | string | Current run status |
| `srvproId` | string | ID of the selected SRVPro instance |
| `config` | object | Environment and selection snapshot saved when the run was created |
| `gamesPerMatchup` | integer | Planned games per deck or opponent; 0 for ranking/tag experiments |
| `totalGames` | integer | Planned total games; 0 for open-ended ranking/tag experiments |
| `launchedGames` | integer | Number of duel groups already scheduled |
| `observedGames` | integer | Number of games confirmed from cumulative rankings |
| `roomCount` | integer | Most recently queried SRVPro room count |
| `createdAt` | string | Creation time |
| `startedAt` | string \| null | Start time |
| `finishedAt` | string \| null | Finish time |
| `latestRankAt` | string \| null | Time of the latest successful ranking query |
| `stopReason` | string \| null | Stop or completion reason |
| `error` | string \| null | Failure details |
| `matchups` | array | Deck or pairing statistics |
| `events` | array | Up to the 30 most recent run events, in chronological order |
| `challengeTargetFlee` | integer | Challenge runs only: cumulative anomaly count for the challenger |

Terminal statuses:

| Status | Meaning |
| --- | --- |
| `completed` | Completed normally |
| `stopped` | Stopped by the user |
| `failed` | Execution failed; see `error` |
| `interrupted` | Interrupted, for example by an Arena process restart |

### Matchup fields

| Field | Type | Meaning |
| --- | --- | --- |
| `id` | integer | Matchup record ID |
| `label` | string | Display name of the deck or opponent |
| `aiLevel` | integer \| null | AI level, or `null` when unavailable |
| `targetGames` | integer | Planned games; 0 for open-ended ranking/tag experiments |
| `launchedGames` | integer | Number of scheduled duels |
| `observedGames` | integer | Number of confirmed win/loss games |
| `currentWinRate` | number | First participant's win rate, from 0 to 1, not a percentage |
| `competitors` | array | Participant statistics |

The denominator of `currentWinRate` includes only wins by the two sides, excluding `flee`. For example, `0.5946` means approximately `59.46%`. The field is `0` when there are no decided games.

### Participant fields

| Field | Type | Meaning |
| --- | --- | --- |
| `id` | integer | Participant record ID |
| `slot` | integer | Matchup slot, 1 or 2 |
| `source` | string | Participant source, such as `current`, `old`, `target` or `opponent` |
| `deck` | string | WindBot deck name or SKYNET profile ID |
| `botLabel` | string | Bot display name |
| `rankName` | string | Name used in the SRVPro rankings |
| `dialog` | string \| null | WindBot dialogue resource; `null` for native SKYNET |
| `executionMode` | string | `local`, `remote` or `skynet` |
| `endpointHost` | string | WindBot host, or compatibility metadata for native SKYNET |
| `endpointPort` | integer | WindBot port, or compatibility metadata for native SKYNET |
| `win` | integer | Wins |
| `lose` | integer | Losses |
| `flee` | integer | Abnormal disconnect count, tracked separately and excluded from the win-rate denominator |
| `combo` | integer | Combo statistic returned by SRVPro |
| `games` | integer | `win + lose` |

For `executionMode: "skynet"`, `endpointHost` and `endpointPort` do not identify a callable WindBot HTTP endpoint. Native clients are launched by Arena with explicit local paths and connect directly to the selected duel server. The run configuration snapshot describes that runtime.

In regression runs, `slot = 1` is the current version and `slot = 2` the previous version. In challenge runs, the first side is the challenger and the second is its opponent. Each `matchup` in a ranking run usually contains one participant. Native SKYNET tag experiments are currently disabled; retained WindBot tag behavior does not establish SKYNET tag support.

## Error responses

Errors use a consistent JSON envelope:

```json
{
  "error": "Error description"
}
```

Common status codes for public read endpoints:

| Status code | Meaning |
| --- | --- |
| `200` | Query succeeded |
| `404` | Requested run does not exist |
| `500` | Internal Arena error |

Check the HTTP status before interpreting the response. Successful JSON parsing alone does not mean the request succeeded.
