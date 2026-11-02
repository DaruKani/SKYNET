# MDPro4 duel AI workspace

Three separate projects live here. Normal WindBot development and the experimental
SKYNET training project have their own source trees, solutions and executables.

| Folder | Purpose | Start here |
| --- | --- | --- |
| `WindBot/` | Normal WindBot, with its original deck executors, names, launcher and `WindBot.exe` | [WindBot build and run instructions](WindBot/README.md) |
| `SKYNET/` | Experimental general duel AI, reviewed expert modes, episode collection and offline learning; produces `SKYNET.exe` | [SKYNET capabilities and training](SKYNET/README.md) |
| `Arena/` | Browser console for comparing bots and recording results; includes dedicated-server setup tools | [Arena setup and running instructions](Arena/README.md) |

WindBot was restored from the explicitly selected `windbot-elder` checkout at
revision `0aa00265d7b378f7e0aee59dedb298d4e356b9b3`. Its original project, namespaces,
deck names and executable names are retained. The existing SKYNET work was moved
intact; this split does not change its duel policy or train a new model. All three
folders remain part of this Git repository, so changes can be reviewed and committed
together. The external `windbot-elder` checkout is unchanged.

## Build the bots

Run from this directory in a Visual Studio Developer PowerShell with the .NET
Framework 4.8 targeting pack installed:

```powershell
msbuild WindBot/WindBot.sln /p:Configuration=Release /p:Platform="Any CPU"
msbuild SKYNET/Skynet.sln /p:Configuration=Release /p:Platform="Any CPU"
```

The outputs are `WindBot/bin/Release/WindBot.exe`,
`WindBot/BotWrapper/bin/Release/Bot.exe`, and `SKYNET/bin/Release/SKYNET.exe`.
Each bot has its own `Decks` and `Dialogs` resources. Supply an explicit compatible
`cards.cdb` with `DbPath=...`; card databases and the duel server are not bundled.

## Run Arena

With Node.js installed (Node.js 24 is the validated runtime):

```powershell
cd Arena
npm ci --no-audit --no-fund
.\Start-SkynetArena.ps1
```

Open <http://127.0.0.1:3000>. Later, `Arena/Skynet Arena.cmd` starts the same console.
The launcher builds the frontend if needed. Actual matches also require a dedicated
compatible `arena-srvpro` server and configured bot/database paths in **Settings**;
starting the dashboard alone does not start a duel server. See the
[complete Arena instructions](Arena/README.md) for normal WindBot, native SKYNET
and local server setup. Arena comparisons keep SKYNET training disabled; opt-in
episode collection and offline training are documented in `SKYNET/README.md`.

## Dialogs and bot roster

Both bot projects include the dialog JSON files supplied from
`D:\Programs\GitHub\MDPro4_Data\Data\Windbot\Dialogs`.
`default.json` is a byte-for-byte alias of `Universal.EN.json`, because the supplied
folder has no default file and both original clients use that name by default.
The normal launcher uses the supplied English bot roster from
`D:\Programs\GitHub\MDPro4_Data\Data\locales\en-US\bot.conf`, stored here as
`WindBot/BotWrapper/bot.conf`. These are versioned copies, so a fresh checkout does
not need those external paths. They are not live links to the data repository.
Source paths and hashes are recorded in
[`WindBot/resource-provenance.json`](WindBot/resource-provenance.json).

`bot.conf` is the normal WindBot roster, not SKYNET's configuration file. Arena can
use it for normal WindBot and use explicit deck profiles for SKYNET. Original bot
and dialog names are preserved.

## Validation and preserved local data

```powershell
python SKYNET/Tools/Skynet/test_learning.py
python SKYNET/Tools/Skynet/test_ygo_agent_inspection.py
python SKYNET/Tools/Skynet/test_ygo_agent_shadow.py
python SKYNET/Tools/Skynet/test_demonstrations.py
python Arena/Tools/test_arena_process_owner.py
cd Arena
npm test
npm run build
```

The previous ignored root `Artifacts/`, `bin/`, `obj/` and `LegacyWindBot/` folders
are preserved as local historical material. New builds use the separate project
folders. Arena stores local settings and results under `Arena/data/` by default;
check saved absolute paths when selecting a newly built version. Historical
benchmark receipts are evidence for their recorded binaries, not new results for
this layout.

WindBot/YGOSharp attribution and licenses are retained. SKYNET's current coverage,
learning limits and research provenance remain documented in its own README.
