# Skynet Arena

An English experiment dashboard for **MDPro4 AI Systems**. Compare SKYNET policies
and expert executors, watch experiment progress, inspect process output, and
export results. Configuration and history persist in SQLite.

Derived from [WindBot Arena](https://github.com/mercury233/windbot-arena), pinned
at `371e62de0b0b29a8f097f84293e9f8f0f15a7509`. Original file hashes and provenance
are in [UPSTREAM.json](UPSTREAM.json); Git tracks our changes after import.
Upstream package metadata declares MIT; its snapshot has no separate LICENSE
file. Upstream attribution remains applicable.

## Start

Use Node.js 24 (validated), then:

```powershell
cd Arena
npm ci
.\Start-SkynetArena.ps1
```

Open **http://127.0.0.1:3000**. The launcher builds the interface if necessary.
After installing dependencies, `Skynet Arena.cmd` provides a double-click entry
point. Keep its console open; Ctrl+C shuts down Arena and its owned processes.
Use `-Build` after frontend changes, or `-Port 3137` for another port.

```powershell
.\Start-SkynetArena.ps1 -Build -Port 3137
```

You can also run `npm run build` and `npm start` directly. No private runtime
paths, model files, card databases or server credentials are shipped.

## Configure a comparison

1. Build `../Skynet.sln` in Release with Visual Studio MSBuild.
2. In **Settings → Agents**, select **SKYNET** for the candidate.
3. Enter the absolute `SKYNET.exe` path and a compatible `cards.cdb`.
4. Add a profile: a unique matchup ID, a display name (up to 17 characters),
   an absolute `.ydk` path, a decision policy, and optional frozen model weights.
5. Configure the baseline with the same matchup ID. For a first integration
   comparison, use `Decks/AI_ExpertOldSchool.ydk` for both participants,
   `skynet` for the candidate and `expert-oldschool` for the baseline.
6. In **Settings → SRVPro**, configure an already running, dedicated
   [arena-srvpro](https://github.com/mercury233/arena-srvpro) instance.
7. Select **Regression**, choose the matching profile and start a small run.

The included OldSchool executor is a simple historical baseline, not a claim
of modern competitive strength. Review and benchmark newer executors before
using their decisions as training demonstrations.

Native launch profiles configure participants. They do not yet author card
effects, combo routes or strategy goals. Those remain separate SKYNET work.

## Participant modes

| Mode | Behavior |
| --- | --- |
| SKYNET | Direct, owned `SKYNET.exe` process for each duel seat; no HTTP bridge |
| Local WindBot | Arena owns a WindBot Server process and reads its `bot.conf` |
| Remote WindBot | Connects to a manually started WindBot Server; catalog is pasted or fetched from a configured URL |

Candidate and baseline can use different modes. WindBot HTTP endpoints must be
distinct. Native SKYNET profiles accept `skynet` or `expert-oldschool`; an expert
cannot use a learned model. Evaluation always forces training and chat off.

SKYNET process limits are 2–32 (default 4), shared by executable path; when slots
share an executable, the stricter active limit applies. A global cap of 32
applies across executable paths. Room reservations
take all required seats together, avoiding partial-slot deadlocks. A 10–3600
second deadline (default 300) bounds queueing and process lifetime. Failed,
cancelled or expired launches release their owned children; unrelated bots
are never killed. Output buffers are bounded.

## Experiments and results

- **Regression:** candidate versus baseline on matching profile IDs.
- **Challenge:** one chosen profile versus a list of opponents.
- **Ranking:** continuously sample candidate deck pairs until stopped.
- **Tag smoke test:** WindBot-only stability checks; native SKYNET tag play is
  disabled until its lifecycle is validated.

Arena requires the dedicated SRVPro management and cumulative-score APIs.
MDPro4's `ygoserver.dll` smoke-test host is a separate integration and does not
replace that server.

Wins come from SRVPro scores. A successful process exit is not a win. Regression
win rate is candidate wins divided by candidate plus baseline wins; disconnects
are shown separately. Each SRVPro instance runs one experiment at a time.
Different dedicated instances can run concurrently.

Process-fixture tests and native fixed-order duels verify plumbing and cleanup.
They do not establish strength. For strength evaluation, use varied decks,
both seats, controlled versions, sufficient completed matches, and separate
held-out comparisons before promoting a model.

The read-only results contract is documented in [API.md](API.md).

## Local data and hosting

| Environment variable | Default | Purpose |
| --- | --- | --- |
| `SKYNET_ARENA_HOST` | `127.0.0.1` | HTTP bind address |
| `SKYNET_ARENA_PORT` | `3000` | UI and API port |
| `SKYNET_ARENA_DATABASE` | `data/arena.sqlite` | SQLite path, relative to Arena or absolute |

The former `WINDBOT_ARENA_*` variables remain fallback aliases. Business settings
belong in the UI and SQLite. Database migrations preserve earlier configuration
and history; native SKYNET settings are additive.

The application has no built-in login. Keep the default loopback binding for
local use; a shared deployment needs authenticated reverse-proxy access.
Enable SSE streaming without proxy buffering and persist/backup `data/`.
The included PM2 example also binds to localhost.

## Development

```powershell
npm run dev
npm test
npm run build
```

Tests use owned process fixtures and mocked SRVPro responses. They do not restart
a real duel server. Native SKYNET launch tests use Node as a harmless executable
fixture; actual binary/protocol completion is checked separately by the parent
project's native-duel tool.
