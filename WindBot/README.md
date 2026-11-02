# WindBot

WindBot is a C# duel bot for [YGOPro](https://github.com/Fluorohydride/ygopro). It is compatible with [YGOSharp](https://github.com/IceYGO/ygosharp) and [SRVPro](https://github.com/mycard/srvpro) servers.

## This workspace

`WindBot/` is the normal deck-executor repository restored from
[`ElderLich/windbot-elder`](https://github.com/ElderLich/windbot-elder) at
`0aa00265d7b378f7e0aee59dedb298d4e356b9b3` (clean source checkout). The normal
architecture, solution/project names, executor/deck names, `WindBot.exe` and
`Bot.exe` are retained; subsequent executor improvements are documented below.
The separate training AI and Arena are described in
the [workspace README](../README.md). This folder has no nested Git repository;
changes are tracked by the enclosing workspace.

From the workspace root, build with Visual Studio/MSBuild for .NET Framework 4.8:

```powershell
msbuild .\WindBot\WindBot.sln /p:Configuration=Debug /p:Platform="Any CPU"
msbuild .\WindBot\WindBot.sln /p:Configuration=Release /p:Platform="Any CPU"
```

The outputs are `WindBot/bin/<Configuration>/WindBot.exe` and
`WindBot/BotWrapper/bin/<Configuration>/Bot.exe`. Run with the working directory
set to the WindBot output directory so its copied `Decks/` and `Dialogs/` can load.
For example, after starting a compatible duel host on port 7911:

```powershell
Push-Location .\WindBot\bin\Debug
try {
    .\WindBot.exe Name=WindBot Deck=Blue-Eyes Dialog=Universal.EN `
        "DbPath=D:\Programs\GitHub\MDPro4_Data\Data\locales\en-US\cards.cdb" `
        Host=127.0.0.1 Port=7911
}
finally { Pop-Location }
```

Set `Version=` and `HostInfo=` when required by your host. A successful build does
not establish protocol compatibility or duel strength.

The supplied resources are tracked snapshots, so this checkout is usable without
the original source folders. All 259 dialog files come from
`D:\Programs\GitHub\MDPro4_Data\Data\Windbot\Dialogs`; `default.json` is an
additional byte-identical copy of `Universal.EN.json` because the original
WindBot default expects that filename. `BotWrapper/bot.conf` comes from
`D:\Programs\GitHub\MDPro4_Data\Data\locales\en-US\bot.conf`. Exact SHA-256 hashes
and source provenance are in [resource-provenance.json](resource-provenance.json).
The supplied English roster's 76 explicit bot commands resolve to existing
executor and dialog names. Both projects copy their runtime resources when built.
These snapshots do not automatically follow later changes in MDPro4_Data.

`BotWrapper` is a game integration launcher: its working directory must contain
`bot.conf` and a `WindBot/` subdirectory containing `WindBot.exe` plus its resources
and SQLite libraries. Building the solution leaves the two outputs separate;
copy them into that game layout when deploying. Running `WindBot.exe` directly
does not read `bot.conf`; it uses `Deck=`, `Dialog=`, and the other options below.
No external game files are changed by this workspace setup.

## Executor imports and RDA-first authoring

The normal bot now includes these explicitly selected imports. All four are
listed in `BotWrapper/bot.conf` with `Dialog=Universal.EN`; their original executor
registrations are preserved. Difficulty flags are left unassigned until measured.

| Roster name | `Deck=` | Source |
| --- | --- | --- |
| Sirokami Dark Magician | `SirokamiDarkMagician` | [PR #296](https://github.com/IceYGO/windbot/pull/296), pinned `12d5270` |
| Raise Moon | `RaiseMoon` | [PR #297](https://github.com/IceYGO/windbot/pull/297), pinned `12a1d1b` |
| Metalmorph | `Metalmorph` | User's desktop `metalmorph` source and deck |
| Odion | `Odion` | User's desktop `metalmorph` source and deck |

The upstream PRs were open and had no submitted reviews at import. The
[upstream receipt](executor-provenance.json) records exact revisions, script
evidence, local compatibility changes and focused checks. The
[personal-executor receipt](personal-executor-provenance.json) records the supplied
files and fixes. Metalmorph's obsolete `100256007` and Odion's `100257013` were
mapped to released cards `76636978` and `27118421`, respectively, using the selected
database and matching Lua scripts. Both `.ydk` files and their handlers use those IDs.

These are bounded integration reviews. Other branches of the large imported
executors remain unreviewed; the personal sources still contain legacy callback
assumptions. Sirokami's upstream combo gate is disabled in its submitted code.
RaiseMoon's optional lexical script helper accepts `SHENGYUE_SCRIPT_DIR` or a
runtime-local `script/` directory, with bounded reads; it does not inspect an
engine's private state or extract `script.zip`. Its guesses are not rule simulation.

For **new executor development**, use the
[RDA-based template](Templates/RdaPlannerExecutor.cs.template), the newer live
[Crimson King executor](Game/AI/Decks/TheCrimsonKingExecutor.cs), and the concise
[authoring guide](Guides/ExecutorAuthoring.md). The desktop RDA source is older
than this repository's implementation and was not copied over it. The template
separates observed state, deck-specific model transitions, bounded search and
committing actual offered actions. It is deliberately unregistered and requires
the new deck's effects and prompt handling before it can play.

The live RDA planner now shares one monotonic deadline across background queueing,
search-slot wait, route search and post-processing. Cancellation retires waiting
work, and concurrency/node/candidate caps bound resource use. Three fixed model
hands (Soul, Bone, and Power Vice + Darkness) retain the baseline's exact final
boards, scores and step counts. This establishes those model regressions, not
full rules correctness or a higher win rate.

This update passed Debug and Release builds, 25 upstream-import assertions,
22 personal-executor assertions, and 14 RDA scheduling/model checks. Each of the
four imports and the final RDA Debug build completed a native pilot against
OldSchool with owned processes retiring normally. The catalog constructs all
79 explicitly selected Arena matchups (default bulk selection excludes LV1).
These pilots use no-shuffle/no-deck-check fixtures and do not establish strength.

Run the maintained checks from the workspace root with explicit inputs:

```powershell
.\WindBot\Tools\Invoke-RdaPlannerChecks.ps1 `
  -BotExecutable WindBot/bin/Release/WindBot.exe `
  -Database 'D:/Programs/GitHub/MDPro4_Data/Data/locales/en-US/cards.cdb' `
  -Deck WindBot/Decks/AI_TheCrimsonKing.ydk `
  -OutputDirectory Artifacts/rda-checks-my-change
```

Use a new output directory for each receipt. Combo fixtures exercise specific
hands, expected actions, interruptions and resets. Native fixtures then verify
actual script prompts. [Arena](../Arena/README.md) compares preserved baseline and
candidate binaries across opponents; its current UI does not inject hands or
assert combo milestones. Keep match results separate from fixed-hand model tests.
Use a **Debug** normal WindBot build with the existing `native_smoke.py` verifier:
the original client's start/result markers are compiled out in Release, so a
Release duel cannot satisfy that tool's completion assertions. Arena uses the
server's ranking API and can compare Release binaries.

The updated [executor skill](Skills/windbot-executor-builder/SKILL.md) is maintained
here and installed locally. It replaces the old desktop prompt workflow; unrelated
skills remain intact. The old guides, card notes, source variants and three RDA
replays are preserved in a verified archive under `Artifacts/guide-consolidation/`.
The [inventory](Guides/legacy-guide-inventory.json) records their hashes.

## Sky Striker 2026 executor

The original `Deck=SkyStriker` executor and `Decks/AI_SkyStriker.ydk` remain
unchanged. The separate `Deck=SkyStriker2026` entry loads the new ordinary WindBot
executor and `Decks/AI_SkyStriker2026.ydk`. Its 40-card Main Deck, 15-card Extra
Deck and 15-card Side Deck were imported from the user-supplied
`D:\Games\MDPro4\Deck\Sky striker.ydk` on 2026-11-03; MDPro4 appearance and
accessory metadata are intentionally not part of the runtime deck resource.

The executor prioritizes an OTK when going second and a Zero-centered setup when
going first. It covers Raye/Roze access, Engage/Kagari recovery, Linkage battle
extension, Lemnisgate recycling, Zero/Amatsu/Widow Anchor interaction, the modern
Camellia/Azalea/Zeke links, the Fallen/Dogma/Ecclesia package and TY-PHON. The
listed Side Deck board breakers and counter traps have ordinary executor support.
All target, material, recycle and option choices remain restricted to candidates
offered by the duel server.

After building, start it from the output directory with an explicit database and
compatible duel host, for example:

```powershell
Push-Location .\WindBot\bin\Debug
try {
    .\WindBot.exe Name=SkyStriker2026 Deck=SkyStriker2026 Dialog=Universal_3.EN `
        "DbPath=D:\Games\MDPro4\Data\locales\en-US\cards.cdb" `
        Host=127.0.0.1 Port=7911
}
finally { Pop-Location }
```

This is a heuristic executor, not an exhaustive combo search or proof of lethal.
It does not predict unknown opposing cards, and its Linkage/resource ordering can
miss a stronger line in unusual boards. Debug/Release compilation and deck-list
checks do not substitute for a script-backed duel; record real duel outcomes as
separate evidence before treating the executor as competitively tuned.

## Voiceless Voice routes and recovery

`Deck=Voiceless` still loads `Decks/AI_Voiceless.ydk`. Its 40-card main deck now
includes Blessing in place of one Solemn Strike; Linkuriboh and Secure Gardna
bring the Extra Deck to nine cards. The executor uses the RDA structure of
observed state, isolated card transitions, bounded search and actual-offer
execution. It does not run Lua inside its planner or share SKYNET training state.

The five supplied routes are implemented with these minimum starters (assuming
the required remaining deck/Extra Deck cards and no initial restrictions):

| Starter | Own-turn route | Opponent-turn follow-up |
| --- | --- | --- |
| Lo | Barrier, Saffira, Skull Guardian, then Dyna Mondo | Revive Skull, revive Lo and place Radiance. |
| Diviner | Send Trias, Tribute Diviner, recruit Lo, then the Dyna route | Revive Skull/Lo and place Radiance. |
| Lo plus an extra Spell | Establish Skull, Link Lo into Linkuriboh, summon Dragon Sage | Respond with Sage to summon Sauravis, revive Lo and place Radiance. |
| Pre-Preparation plus a suitable Level 7+ LIGHT tribute | Prayers summons Skull; find Lo, Barrier and Dragon Sage | Sage supplies the second Ritual, Lo and Radiance. |
| Lo plus Skull Guardian | Barrier finds Blessing; Linkuriboh sends Lo to GY; Blessing recovers Lo; Secure Gardna triggers the Ritual Summon | Blessing responds to a non-Ritual summon with Sauravis; Lo returns and places Radiance. |

Blessing does not perform Link Summons itself. Opponent-turn effects are
conditional opportunities, not disruption already available at the end of our
turn. The planner compares current protection with those opportunities and
replans from actual card locations, negation, effect usage and observed search
locks. It supports alternatives after interruptions instead of advancing an
unchecked fixed combo counter. Search is synchronous, capped at 100 ms, 2,048
expanded states, beam width 128 and depth 24, with cancellation and a bounded
per-duel cache. This is a model of the supported routes, not a complete duel
simulator or an arbitrary ritual-material solver; the server still owns legality.

Location/effect-specific handlers and script-stage selection callbacks cover Lo,
Barrier, Saffira, Skull Guardian, Prayers, Diviner/Trias, Blessing, Dragon Sage,
Sauravis, Radiance and the three Link routes. Single optional triggers with an
unknown description use observed summon context. Selection and effect-use state
reset at their actual chain/phase/turn boundaries. Existing names and the explicit
`NotFinished` registration remain; the measured scenarios do not imply exhaustive
card, matchup or tag-duel coverage.

Run focused dispatcher/prompt checks from the workspace root:

```powershell
.\WindBot\Tools\Invoke-VoicelessChecks.ps1 `
  -BotExecutable WindBot/bin/Debug/WindBot.exe `
  -Database 'D:/Programs/GitHub/MDPro4_Data/Data/locales/en-US/cards.cdb' `
  -Deck WindBot/Decks/AI_Voiceless.ydk `
  -OutputDirectory Artifacts/voiceless-focused-my-change
```

Run the five real-engine routes, Ash/Droll recovery, Veiler protection and a
resolved-Veiler recovery on the next own turn (nine scenarios):

```powershell
python WindBot/Tools/voiceless_native_checks.py `
  --bot-exe WindBot/bin/Debug/WindBot.exe `
  --opponent-exe Artifacts/voiceless-20261102/baseline/Debug/WindBot.exe `
  --data-root D:/Programs/GitHub/MDPro4_Data/Data `
  --server Artifacts/arena-native-loopback-ready/source/bin/release/x64/ygoserver.dll `
  --deck WindBot/Decks/AI_Voiceless.ydk `
  --output-dir Artifacts/voiceless-native-my-change
```

Both commands require fresh output directories. The native runner takes explicit
host/opponent inputs, forces the fixture play order through rock-paper-scissors,
verifies the observed opening hand, and asserts actual moves and board milestones
through the opponent's next turn. The resolved-Veiler case instead checks the
next own turn: its opening has no surviving extender, so recovery uses the
fixture's known Saffira draw rather than inventing a first-turn continuation.
Interruption assertions require an ordered, incomplete-board-to-recovery sequence
and check the stopped search/placement; the separate protection case checks
Skull Guardian answering Veiler. Its no-shuffle/no-deck-check fixtures and
scripted opponent deck lists are correctness tests, not legal-format strength
comparisons. Core RNG is not seeded or claimed reproducible. The supplied native
DLL and frozen baseline are local ignored artifacts; select your own equivalent
paths on another machine. Use a Debug client for its authoritative result logs.

Exact source/script/deck hashes and separate focused, native and Arena evidence
are recorded in [voiceless-provenance.json](voiceless-provenance.json).

The 2026-11-02 candidate passed Debug/Release builds, C# 6 compilation of both
changed executor sources, 27 dispatcher/lifecycle checks, 50 pure-model assertions
and all nine native scenarios. The final frozen Arena comparison completed 44
best-of-three **matches**, with zero flees, client errors or forced client cleanup.
Both versions used the same updated Voiceless deck. The balanced mirror was
24-0 for the new executor (12 matches in each endpoint slot), plus a separate
2-0 completion pilot. The other 18 matches compared each version against the
same frozen opponents:

| Opponent | New Voiceless wins-losses | Old Voiceless wins-losses |
| --- | --- | --- |
| OldSchool | 3-0 | 1-2 |
| Swordsoul | 2-1 | 0-3 |
| The Crimson King / RDA | 1-2 | 0-3 |

Three matches per opponent/version is an initial sample, not a universal win rate.
Endpoint swapping did not force equal first-turn choices; shuffled draws were not
paired or seeded. No-deck-check mode supported the supplied pool. The local
benchmark used task-owned loopback HTTP-to-CLI launchers because Windows rejected
the original HTTP server's wildcard URL reservation; frozen duel clients and
authoritative SRVPro rankings were still used, without changing Windows ACLs.
Fourteen earlier matches used a prefinal binary and are excluded from these
results. Detailed local receipts remain under `Artifacts/voiceless-20261102/`.

## Usage

1. Build `WindBot.sln` with Visual Studio/MSBuild or Mono.
2. Place `cards.cdb` next to the compiled `WindBot.exe`.
3. Start a YGOPro host.
4. Run WindBot with options in `Key=Value` format, for example:
   ```text
   WindBot.exe Name=WindBot Deck=Blue-Eyes Host=127.0.0.1 Port=7911
   ```
5. The bot will connect to the host and start dueling.
6. Refer to `BotWrapper/bot.conf` or the `[Deck]` declarations of each file in `Game/AI/Decks` for available decks.

Contributor guidance for creating and modifying deck AIs is available in `AGENTS.md`.

## Command-line options

- `Name`: The bot's nickname.
- `Deck`: The built-in deck AI to use. Leave this empty to select a random deck with the `Normal` level. Deck names are defined by the executors in `Game/AI/Decks`.
- `DeckFile`: The `.ydk` deck file to use. It is selected automatically by `Deck`, but can be overridden. Cards not supported by the selected deck AI may not be used correctly.
- `Dialog`: The dialog set to use. Available dialog files are in `Dialogs`.
- `Host`: The host name or IP address of the YGOPro server.
- `Port`: The server port.
- `HostInfo`: The room password.
- `Version`: The YGOPro protocol version.
- `Hand`: For testing your deck, forces the bot's rock-paper-scissors choice: `1` for Scissors, `2` for Rock, or `3` for Paper. The default behavior is random for most deck AIs.
- `Chat`: Set to `False` to disable bot chat. The default is `True`.
- `Debug`: Set to `True` to print detailed card-movement logs. The default is `False`.
- `Config`: Loads additional options from a configuration file. Command-line options take precedence.
- `DbPath`: Specifies the path to `cards.cdb`. The default is `cards.cdb`. The program also looks for `cards.cdb` in the parent directory of the working directory, which is the typical location for YGOPro.
- `ServerMode` and `ServerPort`: Enable the HTTP server mode and set its port. The default port is `2399`.

## Server mode

Server mode exposes an HTTP endpoint that starts a bot for each valid request. Example:

```text
http://127.0.0.1:2399/?name=%E2%91%A8&deck=Blue-Eyes&host=127.0.0.1&port=7911&dialog=cirno.zh-CN
```

Query parameter names are lowercase. Supported parameters are `name`, `deck`, `host`, `port`, `dialog`, `version`, `password`, `hand`, `debug`, and `chat`. The `name`, `host`, and `port` parameters are required. `deckfile` is not supported.

The HTTP listener binds to all interfaces. On Windows, run with sufficient privileges or reserve the URL for the selected port (using `2399` here):

```text
netsh http add urlacl url=http://+:2399/ user=Everyone
```

## Known issue

- When a chain contains multiple activations that rely on preselected `AI.SelectCard` choices, a later activation may consume the wrong selection.

## TODO list

- Add and update deck AIs.
- Add linked-zone and non-linked-zone preferences to `AI.SelectPlace`.
- Add an `AI.SelectTribute` preselection interface.
- Keep the known-card enums up to date.
- Add more executors for commonly used cards.

## Related projects

### BotWrapper

`BotWrapper` is a lightweight launcher for YGOPro's built-in bot mode. It converts the arguments supplied by YGOPro into WindBot options, supports random bot selection, and starts `WindBot.exe`.

`BotWrapper/bot.conf` is in the format that YGOPro expects for its bot mode.

### Template generator

The [WindBot template generator](https://mercury233.me/windbot/gen.html) creates a deck AI template from a YGOPro `.ydk` file.

### WindBot Arena

[WindBot Arena](https://github.com/mercury233/windbot-arena) is a self-hosted automated duel experimentation console for running regression tests, deck challenges, smoke tests, and win-rate rankings with WindBot.
