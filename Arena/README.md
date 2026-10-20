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

## Dedicated local server (Windows)

The parent project's explicit bootstrap uses pinned
[arena-srvpro](https://github.com/mercury233/arena-srvpro) commit
`af87d3a8c4269c105144a67221728aa25a747910` and its single locked `deepmerge`
dependency. The official source archive SHA256 is
`c6779f6d7d226bae22fa27f4eb0b860d6d5a27a575b5d430baca158918e24d37`.
Upstream is AGPL-3.0; its source and license remain in the staged runtime.
The bootstrap verifies the complete allowlisted runtime file set before patching
only listener addresses, the native host name and its parent-process guard.

Run the following from the SKYNET repository root, using Python 3.12 or newer,
Node.js 24 and the reviewed Visual Studio 2022 C++ toolchain. These examples
require explicitly supplied local inputs; they do not download dependencies or
search neighboring directories.

1. Export exact MDPro4 server/core commits and reuse dependency sources from an
   explicit previous Windows build candidate. The core must match the selected
   server's `mdpro4/build/windows-server.lock.json`. `build_arena_native.py`
   patches only the isolated copy's native listener from `INADDR_ANY` to
   `INADDR_LOOPBACK`, builds it and writes an input/build receipt.

   ```powershell
   python Tools/Skynet/build_arena_native.py `
     --server-repository D:/YourSources/mdpro4-ygopro `
     --server-commit 9b65c3fc90a782742a92c3e9afa685aa0eb3feca `
     --core-repository D:/YourSources/mdpro4-ygopro-core `
     --core-commit 25eb27f6292ea407f615958fd5ff1d1db77066dc `
     --dependency-candidate D:/YourBuilds/windows-server-candidate `
     --work-dir Artifacts/arena-native `
     --msbuild 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe'
   ```

2. Build the owned `SKYNET_DUEL_HOST.exe` with explicit engine and data inputs.
   It accepts exactly the native API's twelve room arguments, loads only its
   staged library, rejects external expansion/script environment overrides and
   remains alive until that native room retires.

   ```powershell
   ./Tools/Skynet/Build-DuelHost.ps1 `
     -Destination Artifacts/arena-host `
     -ServerLibrary Artifacts/arena-native/source/bin/release/x64/ygoserver.dll `
     -Database D:/YourGame/Data/locales/en-US/cards.cdb `
     -ScriptsArchive D:/YourGame/Data/script.zip
   ```

3. Supply the exact upstream source checkout/archive, then stage a fresh ignored
   runtime. Existing directories are never overwritten.

   ```powershell
   python Tools/Skynet/arena_server.py prepare `
     --source-root Artifacts/arena-srvpro-source --runtime-dir Artifacts/arena-server `
     --native-host Artifacts/arena-host/SKYNET_DUEL_HOST.exe `
     --native-library Artifacts/arena-native/source/bin/release/x64/ygoserver.dll `
     --native-receipt Artifacts/arena-native/arena-native-receipt.json `
     --database D:/YourGame/Data/locales/en-US/cards.cdb `
     --scripts D:/YourGame/Data/script.zip
   npm --prefix Artifacts/arena-server ci --ignore-scripts --no-audit --no-fund
   python Tools/Skynet/arena_server.py serve `
     --runtime-dir Artifacts/arena-server --node D:/YourNode/node.exe --minutes 60
   ```

4. In Arena's SRVPro settings, enter host `127.0.0.1`, duel port `17911`,
   management port `17922`, username `skynet-local`, and the generated password
   from that runtime's `config/admin_user.json`. Set at most two concurrent rooms
   for the default four-bot process limit. Keep credentials and runtime files
   outside commits. The dashboard and duel supervisor are separate processes.

The supervisor accepts authenticated Arena reboots, bounds its duration/restarts
and log size, and uses a private Windows job to retire its process tree on exit
or abrupt supervisor termination. An exclusive lease prevents duplicate owners.
Create `stop.request` inside the selected runtime to stop it; remove that exact
file explicitly before a later restart. A stale lease after forced termination
requires checking that its recorded supervisor has exited before removing it.
There is no global service, firewall change or installed-game replacement.

This bootstrap enables shuffling and disables deck legality checks for unrestricted
research comparisons. It does not expose the core RNG seed. Record these settings
with results; this is not a tournament-format benchmark. Native hosts reject
`YGOPRO_EXPANSIONS` and `YGOPRO_EXTRA_SCRIPT` overrides so staged input hashes
remain meaningful.

## Participant modes

| Mode | Behavior |
| --- | --- |
| SKYNET | Direct, owned `SKYNET.exe` process for each match participant; no HTTP bridge |
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
The local bootstrap above provides those APIs around a separately built MDPro4
native room host. Running the standalone `native_smoke.py` fixture alone does
not provide these management APIs.

Wins come from SRVPro scores. A successful process exit is not a win. Regression
win rate is candidate wins divided by candidate plus baseline wins; disconnects
are shown separately. Each SRVPro instance runs one experiment at a time.
Different dedicated instances can run concurrently.

Regression, Challenge and Ranking use `M#` best-of-three match rooms. Their
counts and win rates refer to matches, while Tag reports launched tag duels
and per-deck appearances. API keys such as `gamesPerMatchup` retain their
historical names. Native process deadlines cover the whole match.

The initial local run on 2026-10-20 completed 20 shuffled OldSchool mirror-deck
matches: general SKYNET won 2 and the explicit OldSchool expert won 18, with no
recorded disconnects. All clients/native rooms retired. The separately recorded
four-match integration pilot finished 2–2. These unrestricted, unseeded runs
establish a baseline, not model improvement; see the parent
[evaluation receipt](../Catalog/expert-baselines.json) for exact inputs and
the initial card-coverage gaps.

After the four shared card behaviors were added, two fresh 20-match runs used
the same deck and preserved baseline executable: updated SKYNET won **18–2
against previous SKYNET** and **9–11 against the OldSchool expert**, with zero
recorded disconnects in either run. Weights stayed frozen; no learning or
checkpoint promotion occurred. These small unseeded comparisons remain separate
from the initial baseline and correctness fixtures. They show progress on this
deck, not general competitive strength.

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
