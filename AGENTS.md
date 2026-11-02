# MDPro4 AI workspace contributor instructions

## Project boundaries

This repository has three independent source folders:

- `WindBot/`: the normal WindBot project restored from the explicitly selected
  `windbot-elder` checkout. Preserve `WindBot.sln`, `WindBot.csproj`, `WindBot.exe`,
  `BotWrapper`, namespaces, executor registrations and deck names. Read its own
  `AGENTS.md` before changing the normal bot.
- `SKYNET/`: the existing experimental training AI. Read `SKYNET/AGENTS.md` for
  policy, legality, hidden information, training and evidence requirements. Its
  solution is `SKYNET/Skynet.sln`, output `SKYNET.exe`. The normal WindBot collection
  is not automatically part of SKYNET's policy or training.
- `Arena/`: the comparison dashboard and dedicated-server tools. Read
  `Arena/AGENTS.md` before changing it. Bootstrap/process tools live in `Arena/Tools`.

These folders share this repository's Git history, not compiled C# source or
mutable duel state. Do not merge the two agents' decision/callback lifecycles.
Expedition gameplay, progression and campaign content remain outside scope.
The ignored root `LegacyWindBot/` and existing root build/artifact folders preserve
historical local data; they are not current build inputs or publication payloads.

## Runtime data and safety

The user selected dialogs from
`D:\Programs\GitHub\MDPro4_Data\Data\Windbot\Dialogs` and the English bot roster
from `D:\Programs\GitHub\MDPro4_Data\Data\locales\en-US\bot.conf`.
Both projects contain versioned dialog copies; `default.json` aliases the supplied
`Universal.EN.json`. The roster belongs to `WindBot/BotWrapper/bot.conf` and normal
WindBot/Arena integration. It is not a SKYNET `Key=Value` configuration file.
Preserve original names and record provenance when refreshing these copies.

Use explicitly configured card databases and duel servers. Never inspect hidden
opponent identities, fabricate legal responses, kill unrelated processes, or
overwrite external game settings. Preserve the WindBot/YGOSharp licenses and
attribution. Do not automatically compile downloaded or source-indexed research.
External repositories are read-only references unless separately authorized.

## Validation and collaboration

Build classic C# projects with Visual Studio/MSBuild for .NET Framework 4.8.
Keep C# 6 compatibility for legacy Mono. Use four spaces, Allman braces and
UTF-8/CRLF for C#; avoid unrelated formatting. New C# source needs project inclusion.
The removed standalone Tests project must not be restored.

From this workspace root, use the applicable checks:

```powershell
msbuild WindBot/WindBot.sln /p:Configuration=Debug /p:Platform="Any CPU"
msbuild WindBot/WindBot.sln /p:Configuration=Release /p:Platform="Any CPU"
msbuild SKYNET/Skynet.sln /p:Configuration=Debug /p:Platform="Any CPU"
msbuild SKYNET/Skynet.sln /p:Configuration=Release /p:Platform="Any CPU"
python SKYNET/Tools/Skynet/test_learning.py
python SKYNET/Tools/Skynet/test_ygo_agent_inspection.py
python SKYNET/Tools/Skynet/test_ygo_agent_shadow.py
python SKYNET/Tools/Skynet/test_demonstrations.py
python Arena/Tools/test_arena_process_owner.py
```

For Arena, run `npm test` and `npm run build` from `Arena/`. Report builds,
regressions, protocol smoke tests and real duel outcomes as separate evidence;
none proves competitive playing strength. Keep training opt-in and evaluation
frozen; no automatic checkpoint promotion.

Preserve user changes and use explicit staged paths. The owner requests a commit
with a clear title and descriptive body for each completed update and a push to
the private SKYNET repository. Companion game commits remain local unless asked.
Update the relevant README with capabilities, exact run commands and actual
limits. Do not add standalone design documents unless requested. Read
`AGENTS.local.md` and `AGENTS.user.md` when present.
