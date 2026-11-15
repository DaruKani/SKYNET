# WindBot

WindBot is a C# duel bot for [YGOPro](https://github.com/Fluorohydride/ygopro). It is compatible with [YGOSharp](https://github.com/IceYGO/ygosharp) and [SRVPro](https://github.com/mycard/srvpro) servers.

## This workspace

`WindBot/` contains the normal deck-executor bot, including `WindBot.sln`,
`WindBot.exe`, the `BotWrapper` launcher and the existing deck registrations.
The separate training AI and comparison console are described in the
[workspace README](../README.md). All three projects share the enclosing Git
repository. Source origins are retained in the provenance receipts below.

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

## Build and check on Windows or Linux

[Tools/bot_tool.py](Tools/bot_tool.py) provides the same commands on both systems.
It discovers Visual Studio MSBuild on Windows, uses `msbuild` on Linux, and falls
back to the deprecated `xbuild` only when MSBuild is absent. It keeps the existing
.NET Framework 4.8 solution, executors and `BotWrapper` intact. Python 3.8+ is
required; no Python packages are needed.

- **Windows:** install Visual Studio or Build Tools with .NET desktop build tools
  and the .NET Framework 4.8 targeting pack. A normal PowerShell is sufficient;
  MSBuild does not need to be on PATH. The checked-in x86/x64 SQLite libraries are
  copied by the project.
- **Linux:** install your distribution's Mono development tools (including a C#
  compiler, MSBuild or xbuild, and 4.8 API reference assemblies), Python 3, and
  the SQLite runtime providing `libsqlite3.so.0`. Use recent Mono/compiler
  packages. The imported executor collection already contains C# 7 constructs;
  new workshop source and harnesses remain C# 6. `App.config` maps `sqlite3` to
  the Linux shared library through [Mono's dllmap mechanism](https://www.mono-project.com/docs/advanced/pinvoke/dllmap/).
  Its `configSections` declaration makes Windows .NET ignore the Mono-only mapping;
  keep both when copying the configuration to a runtime or check harness.
  The modern `dotnet` runtime alone is insufficient for this classic project.

Run these from the workspace root. On Linux use `python3` instead of `python`:

```powershell
python WindBot/Tools/bot_tool.py doctor --database DeckWorkshop/Data/standard/cards.cdb
python WindBot/Tools/bot_tool.py build
python WindBot/Tools/bot_tool.py check --database DeckWorkshop/Data/standard/cards.cdb --deck WindBot/Decks/AI_TheCrimsonKing.ydk --registration TheCrimsonKing --output Artifacts/my-runtime-check
python WindBot/Tools/bot_tool.py check --suite rda --database DeckWorkshop/Data/standard/cards.cdb --deck WindBot/Decks/AI_TheCrimsonKing.ydk --output Artifacts/my-rda-check
```

`build` compiles Debug and Release; select one with `--configuration Debug` or
`--configuration Release`. An unusual installation can use `--msbuild /path/to/msbuild`
and checks can use `--compiler /path/to/csc`. Build errors about missing 4.8
references require the targeting/API package, not retargeting the bot to another
framework. [Mono documents its 4.8 reference profile support](https://www.mono-project.com/docs/about-mono/compatibility/).

The default `check` suite loads the actual bot assembly and native SQLite,
initializes the executor registry, checks every supplied deck ID against the
selected CDB, and optionally verifies an exact compiled registration. It does not
validate a banlist, card rules, behavior or server protocol. `--suite rda`,
`--suite voiceless`, and `--suite metalmorph` run the existing deck-specific
offline fixtures; use each suite's matching `.ydk`. They are not generic tests of
a newly generated bot. Every check requires a fresh `--output` directory and
retains hashes, results and logs. `--bot-exe` selects a frozen binary explicitly;
otherwise checks use the Debug output. No checks start a duel server.

After starting a compatible, explicitly configured duel host, connect one client:

```powershell
python WindBot/Tools/bot_tool.py run --database DeckWorkshop/Data/standard/cards.cdb --deck TheCrimsonKing --host 127.0.0.1 --port 7911 --debug
```

This sets the resource working directory and runs `mono WindBot.exe` on Linux.
`--deck` is the exact `[Deck]` registration, not the filename; check it first,
since the underlying client falls back to a random normal executor for unknown
names. Add `--version`, `--host-info`, `--configuration Release`, or `--bot-exe`
as needed by your host/runtime. The default timeout is 300 seconds; choose
`--timeout` explicitly for longer sessions. Cancellation/timeout stops only this
invocation's child. Arena can compare your frozen bot using its local or remote
WindBot modes; see [Arena setup](../Arena/README.md).

Validated locally on Windows: Debug/Release builds, the runtime check and the
14 existing RDA model/scheduling checks. Linux execution was not available on
this machine: the registered Ubuntu WSL instance could not start because its
required virtualization service was unavailable. The repository also contains a
Mono 6.8 Linux CI build job; its presence is not a claimed successful run of this
change. Native duels and Arena strength results are separate evidence.

Tooling-only regressions: `python WindBot/Tools/test_bot_tool.py`.

## Generate your own executor in Deck Workshop

Open Arena's **Deck Workshop**, import or paste a `.ydk`, select Standard or Rush,
and inspect card text, prompt strings and Lua references. The workshop exports
the analysis text and a registered RDA starter plus its separate planner base.
See [Deck Workshop](../DeckWorkshop/README.md) for the imported data and limits.

For a Standard starter, copy **both** generated `.cs` files to `Game/AI/Decks/`
and its generated `AI_<Name>.ydk` to `Decks/` under this WindBot folder. Those
directories already use project wildcards. Preserve existing files and use a
unique name; the generated registration is `Test`, outside the random normal
bot pool. Rebuild with `bot_tool.py build`, then run the default `check` with the
new deck file and exact registration name. Add it to your launcher roster only
when you deliberately want it offered there; direct `Deck=<Name>` does not need
a roster entry.

The generated model intentionally starts disabled. Implement card transitions,
offered-action matching, effect-specific selections and reset behavior using
the [authoring guide](Guides/ExecutorAuthoring.md) before expecting meaningful
play. Card text and static Lua references do not establish a complete effect
interpreter or automatic strategic understanding. Rush analysis does not make
the current normal WindBot protocol/runtime a supported Rush Duel bot.

The supplied resources are tracked snapshots, so this checkout is usable without
the original source folders. All 259 dialog files come from
`D:\Programs\GitHub\MDPro4_Data\Data\Windbot\Dialogs`; `default.json` is an
additional byte-identical copy of `Universal.EN.json` because the original
WindBot default expects that filename. `BotWrapper/bot.conf` comes from
`D:\Programs\GitHub\MDPro4_Data\Data\locales\en-US\bot.conf`. Exact SHA-256 hashes
and source provenance are in [resource-provenance.json](resource-provenance.json).
The English roster commands resolve to the included executor and dialog names. Both projects copy their runtime resources when built.
These snapshots do not automatically follow later changes in MDPro4_Data.

`BotWrapper` is a game integration launcher: its working directory must contain
`bot.conf` and a `WindBot/` subdirectory containing `WindBot.exe` plus its resources
and SQLite libraries. Building the solution leaves the two outputs separate;
copy them into that game layout when deploying. Running `WindBot.exe` directly
does not read `bot.conf`; it uses `Deck=`, `Dialog=`, and the other options below.
No external game files are changed by this workspace setup.

## Executors and RDA-first authoring

These additional executors are included in the normal bot. They are
listed in `BotWrapper/bot.conf` with `Dialog=Universal.EN`; their original executor
registrations are preserved. Difficulty flags are left unassigned until measured.

| Roster name | `Deck=` | Source |
| --- | --- | --- |
| Sirokami Dark Magician | `SirokamiDarkMagician` | [PR #296](https://github.com/IceYGO/windbot/pull/296), pinned `12d5270` |
| Raise Moon | `RaiseMoon` | [PR #297](https://github.com/IceYGO/windbot/pull/297), pinned `12a1d1b` |
| Metalmorph | `Metalmorph` | Supplied personal source and deck |
| Odion | `Odion` | Supplied personal source and deck |
| Bakura - Destiny Board | `DestinyBoard` | Workspace implementation; unrestricted, selected prerelease Necrofear |

The pinned upstream revisions were unreviewed at import. The
[upstream receipt](executor-provenance.json) records exact revisions, script
evidence, local compatibility changes and focused checks. The
[personal-executor receipt](personal-executor-provenance.json) records the supplied
files and fixes. Metalmorph's obsolete `100256007` and Odion's `100257013` were
mapped to released cards `76636978` and `27118421`, respectively, using the selected
database and matching Lua scripts. Both `.ydk` files and their handlers use those IDs.

The personal executors have a follow-up script and callback audit described below.
Coverage is bounded; large untested branches remain. Sirokami's upstream combo gate is disabled in its submitted code.
RaiseMoon's optional lexical script helper accepts `SHENGYUE_SCRIPT_DIR` or a
runtime-local `script/` directory, with bounded reads; it does not inspect an
engine's private state or extract `script.zip`. Its guesses are not rule simulation.

For **new executor development**, use the
[RDA-based template](Templates/RdaPlannerExecutor.cs.template), the newer live
[Crimson King executor](Game/AI/Decks/TheCrimsonKingExecutor.cs), and the concise
[authoring guide](Guides/ExecutorAuthoring.md). The template
separates observed state, deck-specific model transitions, bounded search and
committing actual offered actions. It is deliberately unregistered and requires
the new deck's effects and prompt handling before it can play.

To request a new executor or an improvement, copy the
[starter prompt](Guides/ExecutorAuthoring.md#copyable-starter-prompt) and fill in
the deck list, intended format and any combo notes. It covers the three-agent
workflow, script review, integration, native combo checks and frozen Arena tests.

The live RDA planner now shares one monotonic deadline across background queueing,
search-slot wait, route search and post-processing. Cancellation retires waiting
work, and concurrency/node/candidate caps bound resource use. Three fixed model
hands (Soul, Bone, and Power Vice + Darkness) retain the baseline's exact final
boards, scores and step counts. This establishes those model regressions, not
full rules correctness or a higher win rate.

The import receipts retain their original compatibility checks and native pilots.
Those pilots establish completion for the recorded binaries, not current
competitive strength. Run the checks and compare frozen builds after each
behavior change.

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
RDA has its own local opponent memory. For a fixed-opponent benchmark, create an
empty `WindBot_knowledge/TheCrimsonKing_knowledge.json.off` file beside **each
participating executable** before launching any clients. This existing marker
disables both collection and memory-based decisions. Verify the trace reports
`memory: disabled, deciding without it`; a frozen executable alone does not freeze
RDA's policy when its memory file keeps changing. Keep normal play's memory
settings separate from these isolated benchmark runtimes.

Use a **Debug** normal WindBot build with the existing `native_smoke.py` verifier:
the original client's start/result markers are compiled out in Release, so a
Release duel cannot satisfy that tool's completion assertions. Arena uses the
server's ranking API and can compare Release binaries.

The [executor skill](Skills/windbot-executor-builder/SKILL.md) and
[authoring guide](Guides/ExecutorAuthoring.md) contain the maintained workflow.
Use these with the current card scripts and observed duel traces.

## Personal executor checks

Metalmorph retains its supplied deck list, registration and strategic priorities.
Its maintained checks exercise real WindBot dispatch and selection callbacks
against the selected card database. **Odion retains its original executor.** Its
attempted callback/effect fixes passed 16 focused checks but scored worse in Arena,
so the broad candidate was rejected. Its source, patch, harness and receipts remain
in local `Artifacts/executor-review-20261102/rejected-odion/` for investigation.

Metal Illusionist distinguishes its summon tutor from its race
change, Flame uses the actual replacement prompt, and Time Engine answers its
resolution benefits without re-running activation logic. Fullmetal chooses burn
damage using original ATK. Max/Flame, Phoenix and Accesscode selections follow
the scripts' actual cost/target/resolution prompts without duplicate queues.
Callback identity comes from accepted actions or immutable chain snapshots.

```powershell
.\WindBot\Tools\Invoke-MetalmorphChecks.ps1 `
  -BotExecutable WindBot/bin/Debug/WindBot.exe `
  -Database 'D:/Programs/GitHub/MDPro4_Data/Data/locales/en-US/cards.cdb' `
  -Deck WindBot/Decks/AI_Metalmorph.ydk `
  -OutputDirectory Artifacts/metalmorph-checks-my-change
```

Use fresh output directories. These synthetic checks establish dispatch, prompts
and state behavior; real Lua-backed duels and Arena results are separate evidence.
The [personal-executor receipt](personal-executor-provenance.json) retains source
origins, exact script hashes and before/after validation.

## Voiceless Voice routes and recovery

`Deck=Voiceless` loads `Decks/AI_Voiceless.ydk`. Its 40-card main deck includes
Blessing; the nine-card Extra Deck includes Linkuriboh and Secure Gardna. The executor uses the RDA structure of
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
reset at their actual chain/phase/turn boundaries. Existing names remain. Voiceless now uses the default `Normal` registration and
can enter the normal random-deck pool. The measured scenarios do not imply
exhaustive card, matchup or tag-duel coverage.

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

## Current validation and Arena results

The loss-analysis update preserves all three deck lists. Voiceless now targets
live effects beneath graveyard chain links, coordinates Ash with an available
Droll against opening Gaia, and preserves the Skull/Lo core and direct attacks
when considering S:P. The previous three-match RDA/Swordsoul results lacked
usable decision traces; new diagnostic duels supplied the concrete findings.

The accepted combined build passes Debug/Release and C# 6 compilation, **70 focused
checks** (39 Voiceless, 31 Metalmorph), the 22 preserved import checks,
and all **nine native Voiceless combo/recovery scenarios**. Two paired interaction
fixtures also demonstrated the new Chixiao and Gaia responses; the RDA fixture
still lost after the corrected response. These establish behavior, not guaranteed
wins through every interruption.

The Arena comparison uses preserved revision `363f7eb` against candidate Debug
`8ff56a0c`, identical deck lists, the same database/scripts and frozen opponents.
The accepted build `7516a6ab` restores original Odion and promotes Voiceless to
`Normal`. Voiceless decision code differs from the measured candidate only in its
registration attribute; Metalmorph source is identical. Builds and all checks above
were rerun. The Arena runs retain their original binary identity.
RDA memory collection and decisions are disabled and verified in the traces.
Scores below are authoritative server **best-of-three match** wins-losses, not
individual duel counts.

| Executor | Opponent | Original W-L | Candidate W-L | Decision |
| --- | --- | --- | --- | --- |
| Voiceless | RDA | 12-8 | 15-5 | Accept fixes |
| Voiceless | Swordsoul | 9-11 | 12-8 | Accept fixes |
| Metalmorph | RDA | 0-6 | 1-5 | Accept fixes |
| Metalmorph | Swordsoul | 0-6 | 4-2 | Accept fixes |
| Odion | RDA | 0-6 | 0-6 | Retain original |
| Odion | Swordsoul | 9-11 | 2-18 | Retain original |

All **156 scored matches / 312 clients** completed with zero recorded flees, client
errors or forced client cleanup. Thirty-four diagnostic matches with evolving RDA
memory or a misnamed memory marker are excluded from this comparison.

Voiceless and Metalmorph improved in these samples. Odion's candidate fell from
9-11 to 2-18 against Swordsoul despite passing its prompt checks. Three controlled
opening pairs did not isolate a causal regression, and two pairs diverged in
unseeded Swordsoul choices before changed Odion actions. That uncertainty does not
justify promotion: the accepted source retains original Odion. Its existing
TY-PHON material-reservation policy and Metalmorph's willingness to tribute a
fresh Synchro interaction remain known tactical limits for separate testing.

Shuffled draws were not paired, the core RNG seed is unknown, and first-turn
choices were not forced. This no-deck-check pool and the sample sizes do not
establish a universal or legal-format win rate. RDA's broad removal boards and
repeated interruptions still beat supported Voiceless routes. The local comparison
uses task-owned loopback HTTP-to-CLI adapters because Windows rejected the normal
server's wildcard URL reservation; real frozen clients and SRVPro rankings are
used without changing Windows ACLs.

Exact inputs, per-run results, confidence intervals, trace findings and separate
validation receipts are retained in [Voiceless provenance](voiceless-provenance.json),
[personal-executor provenance](personal-executor-provenance.json) and local
`Artifacts/executor-review-20261102/`.

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


## Bakura / Destiny Board

`Deck=DestinyBoard` loads `Decks/AI_DestinyBoard.ydk`: the selected Bakura-focused
40-card Main / 15-card Extra Deck, using unrestricted/custom rules. It includes
three Pot of Greed and Graceful Charity, two Evil Lord - Zorc (`73320394`), and
two prerelease Dark Necrofear, the Ruler of Demise (`100268007`). This list is not
legal under ordinary tournament banlists. The original Necrofear, Curse, both
Dark Spirits, Spirit Shield and full Board package retain the Bakura theme.

Prepare the selected prerelease data without changing your installed game:

```sh
python DeckWorkshop/prepare_runtime.py --expansion DeckWorkshop/Expansions/bakura-prerelease --output Artifacts/bakura-data
python WindBot/Tools/bot_tool.py build
python WindBot/Tools/bot_tool.py check --suite runtime --database Artifacts/bakura-data/locales/en-US/cards.cdb --deck WindBot/Decks/AI_DestinyBoard.ydk --registration DestinyBoard --output Artifacts/bakura-runtime-check
python WindBot/Tools/bot_tool.py check --suite destiny-board --database Artifacts/bakura-data/locales/en-US/cards.cdb --deck WindBot/Decks/AI_DestinyBoard.ydk --output Artifacts/bakura-decision-check
```

Use `python3` on Linux and follow the Mono prerequisites above. Output directories
must be new. The bot and duel host must load the same prepared CDB; the host must
also load the prepared `script.zip`. The base Workshop catalog remains released
Standard; the optional prerelease snapshot and its provenance are under
`DeckWorkshop/Expansions/bakura-prerelease/`. The exporter refuses future ID
collisions rather than silently replacing a released card.

For an explicitly configured compatible duel server, replace host/port/version
with its actual settings:

```sh
python WindBot/Tools/bot_tool.py run --database Artifacts/bakura-data/locales/en-US/cards.cdb --deck DestinyBoard --host 127.0.0.1 --port 7911 --debug
```

The deck uses a small synchronous RDA-style boundary between observations,
material/board evaluation, and the server's legal offers. It does not execute Lua
inside the client or predict a favorable die roll. Extra Deck evaluations share
a bounded deadline; each decision is reconsidered after actual moves and chains.

Opening and recovery routes include:

- Tour Guide recruits Doomdog Octhros, then Cherubini sends Rhino. Octhros finds
  Ruler and Rhino sends another Fiend, establishing the three-Fiend GY threshold.
- Zorc discards itself and Rhino, searches Ruler, then Rhino supplies the third
  Fiend. Ruler's own summon counts these cards without banishing them.
- Dark Spirits and Muckraker revive legal Fiends. Original Necrofear cannot be
  revived before its proper summon; the new Ruler has a different restriction.
- Sentence advances missing later letters without consuming the naturally due
  I/N. Board's progress belongs to each Board instance, survives turn changes,
  and resets when that card leaves or is reset.
- Zorc's adverse die roll can destroy Ruler for its return/control/burn sequence,
  or select an immune Sanctuary message. Ruler needs an eligible opposing monster;
  Rhino and Cherubini protection can prevent intentional destruction.

The Extra Deck has purposes beyond simply summoning the largest available card:

| Card | Intended timing / role |
| --- | --- |
| Cherubini (2) | Early graveyard setup; use its mill before spending it as material. |
| Muckraker (2) | Revival with expendable discard; account for its Fiend-only lock and prohibition on Link material during its Link Summon turn. |
| Darius → Deus Machinex | Alternate two-Level-3 Fiend route; preserve Darius's battle opportunity when useful, then overlay. Machinex absorbs a monster; it does not negate that activation. |
| Dingirsu | Rank-8 non-targeting removal and destruction protection for Board. |
| Hope Harbinger | Rank-8 Spell interaction when holding bodies is less useful. |
| Dharc | Exploit an eligible opposing DARK monster in the GY. |
| Phoenix / Unicorn | Backrow destruction / broader shuffling with a worthwhile discard. |
| S:P Little Knight | Removal and temporary banishing; preserve useful direct attacks when its summon effect would forbid them. |
| Unchained Soul Rage Abominator | Opposing-turn disruption, potentially destroying Ruler for its follow-up trigger. |
| Unchained Abomination | Repeat destruction when the field presents useful targets. |
| Underworld Goddess | Remove an opposing monster through its legal material procedure when sufficient expendable effect monsters exist. |

Spirit Messages are protected from ordinary material, tribute, discard and
replacement preferences. Backrow/monster capacity, imminent Board wins, graveyard
fuel and retained interaction affect whether another summon is worthwhile.
A late A/L may be discarded to retain protection when Board remains accessible
and a retained Sentence or Mastery supplies a recovery route. The naturally due
letter and the fourth missing letter of an imminent win remain protected; the
choice also accounts for known negation and banishment effects. Phoenix keeps a
live Ruler recovery plan when its only payoff is probing unknown backrow.
Because the client does not observe the engine's pending-destruction flags,
Muckraker deliberately declines optional destruction replacement while message
monsters exist; Cherubini declines it while any Board/message exists. This avoids
accepting a replacement whose later mandatory payment might sacrifice the win
condition. Dingirsu's overlay-based destruction protection remains available.
Unrelated archetype branches with no supporting cards (such as Darius's Dark
Contract effect or Machinex's Pendulum setup) are intentionally declined.

Run reproducible native scenarios against the explicitly selected native host
using Debug builds (movement assertions need debug traces):

```sh
python WindBot/Tools/destiny_board_native_checks.py --bot-exe WindBot/bin/Debug/WindBot.exe --opponent-exe /path/to/frozen/WindBot.exe --data-root Artifacts/bakura-data --server /path/to/ygoserver.dll --deck WindBot/Decks/AI_DestinyBoard.ydk --output-dir Artifacts/bakura-native
```

On Linux, supply a compatible Linux native duel library in `--server` and Mono
on PATH (or pass `--mono /path/to/mono`). A Windows `ygoserver.dll` cannot run on
Linux. The recorded native scenarios were executed on Windows; the Mono launch
path has not been exercised in this workspace.

The native tool uses no-shuffle/no-deck-check fixture decks and records actual
opening hands and intermediate moves. Its core RNG seed is unknown. Focused
checks establish specified decisions, native scenarios establish those observed
routes, and Arena results measure only the recorded matchups. Exact validation
and source identities are recorded in `destiny-board-provenance.json`.

Validated the final executor with Debug/Release and C# 6 builds, 63 focused
checks, and 10 real-script native scenarios containing 66 assertions. These
include actual natural/accelerated Board wins, Muckraker revival and Fiend lock,
Darius into Machinex, four interruption cases, and Ruler control plus 1000 burn.
Windows runtime checks loaded the database, all 55 deck copies and registration.

The frozen Arena comparison completed 20 best-of-three matches per opponent
for each version. Scores below are match wins-losses:

| Frozen opponent | Initial v7 | Final v8 |
| --- | --- | --- |
| TheCrimsonKing | 0-20 | 0-20 |
| Swordsoul | 0-20 | 0-20 |
| Odion | 3-17 | 2-18 |
| Metalmorph | 0-20 | 2-18 |

Both 80-match runs completed with zero client errors, nonzero exits, forced
cleanup or flees; all frozen runtime hashes remained unchanged. The final v8
policy retains protection when a late letter is recoverable and preserves a
live Ruler against speculative Phoenix removal; the 40/15 list is unchanged.
These results still show a weak matchup profile for this thematic deck. Unknown
core RNG, unpaired draws and only 20 matches per opponent do not establish a
reliable improvement or tournament strength. Pilot runs are excluded. Exact
inputs, observed first-player counts and separate evidence are in
[Destiny Board provenance](destiny-board-provenance.json); detailed local logs
remain under `Artifacts/destiny-board-implementation-20261102/`.

## Shaddoll 2025 executor

`Shaddoll2025` is a normal WindBot executor for the exact 40 Main / 15 Extra
card list supplied as `Shaddoll2025.ydk`. It uses the released Standard catalog
in `DeckWorkshop/Data/standard` as its implementation reference and the
[Master Duel Meta Shaddoll guide](https://www.masterduelmeta.com/articles/guides/shaddoll-pancakes)
as strategic background. The installed scripts and card database take priority
over the guide.

The executor re-evaluates the current legal offers rather than assuming a fixed
combo. Its implemented decisions include:

- contextual Lev dumps: Apkallone for search/discard setup, Meshachrer for a
  live revival, Fusion recovery when a Spell/Trap is already in the GY, and
  Lunalight Perfume Dancer only when its GY ATK reduction has battle value;
- Void Unleashing searches that preserve the required different card types and
  discard Shaddolls by effect when their unused GY effect is useful;
- state-based Squamata sends, Hedgehog/Wendi searches, Ariel GY disruption,
  Construct setup, Apkallone search/discard and Meshachrer search/revival;
- legal-offer Fusion target and material scoring for Shaddoll Fusion, El
  Shaddoll Fusion, Schism, Tohushaddoll Grysta, Cartesia, Faimena and Scheiren;
- delayed opponent-turn Winda/Schism use, Wendikuruhu flip control, Shekhinaga
  response deduplication, and Maxx "C"/Mulcharmy extension shortening once a
  functional board is established;
- going-second Construct/Apkallone priorities, visible backrow and GY answers,
  grind-game Spell/Trap recovery, and conservative use of hand traps.

The supplied list has no Infernoid Evil, Resh Shaddoll Incarnation, Fallen of
Albaz, Lunalight Main Deck monster, or Dracotail Spell/Trap. The executor does
not invent those guide lines: Void Unleashing's GY effect and Faimena's Set
trigger therefore have no target, Cartesia cannot self-Special Summon by her
Albaz condition, and Perfume Dancer is a Lev GY utility card rather than a normal
Fusion target.

Build and run the offline registration and focused callback checks from the
workspace root:

```powershell
msbuild WindBot/WindBot.sln /p:Configuration=Debug /p:Platform="Any CPU"
msbuild WindBot/WindBot.sln /p:Configuration=Release /p:Platform="Any CPU"
python WindBot/Tools/bot_tool.py check --suite runtime --database DeckWorkshop/Data/standard/cards.cdb --deck WindBot/Decks/AI_Shaddoll2025.ydk --registration Shaddoll2025 --output Artifacts/shaddoll-runtime
python WindBot/Tools/bot_tool.py check --suite shaddoll-2025 --database DeckWorkshop/Data/standard/cards.cdb --deck WindBot/Decks/AI_Shaddoll2025.ydk --output Artifacts/shaddoll-focused
```

The focused suite uses synthetic legal offers against the compiled dispatcher;
it verifies selection bounds and decisions but does not execute Lua. A real
script-backed duel still requires an explicitly selected compatible host using
the same database and scripts. No native Shaddoll combo result or Arena win rate
is claimed until those runs are recorded. The client cannot see hidden opponent
cards, simulate future Lua resolution, or know an opponent's unobserved hand.
