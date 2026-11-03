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

These additional executors are included in the normal bot. All four are
listed in `BotWrapper/bot.conf` with `Dialog=Universal.EN`; their original executor
registrations are preserved. Difficulty flags are left unassigned until measured.

| Roster name | `Deck=` | Source |
| --- | --- | --- |
| Sirokami Dark Magician | `SirokamiDarkMagician` | [PR #296](https://github.com/IceYGO/windbot/pull/296), pinned `12d5270` |
| Raise Moon | `RaiseMoon` | [PR #297](https://github.com/IceYGO/windbot/pull/297), pinned `12a1d1b` |
| Metalmorph | `Metalmorph` | Supplied personal source and deck |
| Odion | `Odion` | Supplied personal source and deck |

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

The bot runtime copy replaces the supplied Extra Deck's `The Dragon that Devours
the Dogma` slot with `Albion the Branded Dragon` for the intended Fallen & the
Virtuous resource line. It always sends Albion first while another Fallen & the
Virtuous remains in the Deck, then scores the remaining Ecclesia/Dogma sends.
Albion sets the replacement during the bot's End Phase so it is live on the
opponent's turn; during the opponent's End Phase it normally adds the card to
hand, setting only to avoid the hand-size discard. The activation compares the
best observed destruction with every legal graveyard revival and holds the card
unless one branch reaches its minimum payoff. A harmless face-up card is no
longer enough reason to spend the effect merely to send an Extra Deck monster.

Forbidden Crown, Widow Anchor and Forbidden Droplet now share a tactical decision
layer instead of accepting independently in executor order. For every observed
window it scores the resolving monster, floodgate/danger classifications, battle
lethal, targetability, Widow's immediate control/Link conversion, Droplet's
number and value of costs, and multi-negate payoff. Only the highest scoring
known response commits selections. Droplet prefers a disposable cost whose card
type also prevents a matching response, preserves the last copy of core engine
resources unless the exchange justifies it, and is held when Widow or Crown
solves the same threat more efficiently. The same dynamic reserve values are
used for hand/field/GY costs and Link materials; an unspent setup Zero is heavily
protected from generic Link conversion. Battle Phase Droplet decisions count only
enemy attackers that have not attacked yet, so an already-spent Kagari cannot
create a false lethal alarm. In the mirror, a live Sky Striker Ace effect is an
explicit chain target, allowing Droplet to stop the effect before damage instead
of being wasted after that monster has already attacked.

Widow Anchor preserves the empty Main Monster Zone as a deck invariant. On the
opponent's turn it negates without taking control, because the stolen monster
cannot be converted into Link material and would disable the remaining Sky
Striker spells. The control option is used there only when changing control of
the current attacker prevents lethal damage. The same turn-aware conversion
check prevents Shark Cannon from reviving an unusable monster into the Main
Monster Zone during the opponent's turn.

Zero's Quick Effect is not fired merely because the opponent has any card on the
field. Outside the Linkage OTK attack sequence it now requires a removal dodge,
lethal defense, or a high-value persistent target whose destruction repays losing
Zero and filling both Main Monster Zones. Normal Spells that have already spent
their effects are worth zero, and a mirror Ace Link is not destroyed when doing
so would immediately enable the opponent's Raye in the Graveyard. The optional
destruction prompt is tied to that scored target instead of selecting an arbitrary
face-up card after Raye and Roze are summoned.

Spell/Trap destruction follows resolution semantics instead of treating every
card on the Chain as a negate. A face-up Normal, Quick-Play or Ritual Spell, or a
Normal/Counter Trap, has zero interruption value after successful activation
unless an explicit card-text interaction applies. Continuous, Field, Equip,
Pendulum and Continuous Trap cards retain destruction value because their effects
depend on remaining face-up. The explicit Azamina exception recognizes The
Hallowed Azamina and Azamina Debtors: a publicly visible face-up "Sinful Spoils"
card is a high-priority target because removing that resolution resource can deny
the Fusion Summon. This shared classification is used by Zero, TFTV, Mystical
Space Typhoon and Twin Twisters.

Mirror-match policy is enabled only after the opponent reveals a Sky Striker
card on the public field, in a public zone, or on the Chain; it never inspects a
hidden identity. In the mirror, Shark Cannon prioritizes denying Raye/Roze and
revives them when they can become immediate Link material, Widow Anchor can take
an otherwise non-threatening opposing Ace for the same purpose, Multirole is the
first face-up back-row removal target, and searches raise Shark Cannon, Widow
Anchor and Multirole ahead of generic follow-up when their mirror conditions are
present.
The user-owned MDPro4 deck file is not modified.

The opening setup now treats Engage and Reinforcement of the Army as access to
Raye or Hornet Drones. A lone Raye can tag directly into Zero. The legal Drones
route is Token into Kagari (Kagari, not Shizuku, recovers Drones), a second Token,
Kagari into Amatsu with Hayate/Shizuku as fallbacks, then the bridge Link plus the
Token into Zero. If Lemnisgate is available, that route first recycles the spent
Drones/Kagari pair so Lemnisgate remains in the GY beside Zero. Outside the OTK
route, Zero always accepts its search and prioritizes Engage. When both Deck and
GY copies are offered, it takes Engage from the GY only if at least three other
Spells will remain there; otherwise it takes a Deck copy and preserves the GY
Engage toward the three-Spell threshold. The executor tracks Zero entering the
Monster Zone so this search is still accepted when the duel server reports its
activation description as unknown (`-1`); the deliberate Linkage OTK search skip
remains the only strategic exception.

Linkage is no longer a generic empty-board conversion into the first available
Ace. In Main Phase it defers to live Raye, Engage/ROTA or Drones access; only when
those better starters are unavailable does it enter a tracked fallback route
whose Extra Deck choice is locked to Zero. The existing battle-phase Kagari ->
Zero lethal route remains separate and owns its own send targets.

The Linkage OTK starts only after the opposing field is empty, an unused
1500-or-higher Link Monster in the Extra Monster Zone has attacked, Kagari and
Zero remain unused, Raye/Roze are available from Deck/GY, and the conservative
damage total reaches the opponent's LP. It sends that attacker for Kagari,
recovers Linkage and attacks with Kagari, sends Kagari for Zero, deliberately declines
Zero's search, attacks with Zero, then tags into Raye and Roze without taking the
optional destruction. Its minimum modeled damage is 8,000 before Linkage's
LIGHT/DARK bonus. Lemnisgate's GY Link trigger is held during this sequence so it
does not consume either final attacker.

After building, start it from the output directory with an explicit database and
compatible duel host, for example:

For the local MDPro4 setup, open a compatible room on port `7911`, then
double-click `Tools\Run-SkyStriker2026.bat`. The launcher locates Visual Studio
MSBuild, builds `Debug|Any CPU`, and joins `127.0.0.1:7911` with decision logging
enabled. It only reads the external card database and does not modify MDPro4's
deck or settings files. The defaults and optional overrides are:

```cmd
WindBot\Tools\Run-SkyStriker2026.bat
WindBot\Tools\Run-SkyStriker2026.bat --build-only
WindBot\Tools\Run-SkyStriker2026.bat 127.0.0.1 7911 "D:\Games\MDPro4\Data\locales\en-US\cards.cdb"
WindBot\Tools\Run-SkyStriker2026.bat 127.0.0.1 7911 "D:\Games\MDPro4\Data\locales\en-US\cards.cdb" "room-password"
```

The fourth argument is optional `HostInfo` for a room that requires it. If the
room is not listening yet, WindBot reports a connection failure; start or reopen
the room and run the launcher again. The equivalent manual command is:

```powershell
Push-Location .\WindBot\bin\Debug
try {
    .\WindBot.exe Name=SkyStriker2026 Deck=SkyStriker2026 Dialog=Universal_3.EN `
        "DbPath=D:\Games\MDPro4\Data\locales\en-US\cards.cdb" `
        Host=127.0.0.1 Port=7911
}
finally { Pop-Location }
```

This is a hybrid executor: supported setup/OTK routes use tracked stages, while
competing interaction uses a pure observed-state scoring layer before the normal
executor commits an offered action. It is not an exhaustive Lua simulator or a
proof of lethal, does not predict unknown opposing cards, and cards outside the
modeled packages still use ordinary WindBot handlers. Debug/Release compilation
and deck-list checks do not substitute for a script-backed duel; record real duel
outcomes separately before treating the executor as competitively tuned.

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
