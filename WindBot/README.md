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

## Witchcraft 2026 executor

`Deck=Witchcraft2026` is a separate test-registration executor; the existing
`Deck=Witchcraft` implementation and deck remain unchanged. Its versioned
`Decks/AI_Witchcraft2026.ydk` is an exact card-list copy of the user-selected
`D:/Games/MDPro4/Deck/Witchcraft.ydk` imported on 2026-11-05 (40 Main, 15 Extra,
7 Side; MDPro4 appearance/accessory metadata is not a WindBot runtime input). The
runtime identities and Lua behavior were checked against
`D:/Games/MDPro4/Data/locales/en-US/cards.cdb` and
`D:/Games/MDPro4/Data/script.zip`; the zero-byte
`D:/Games/MDPro4/Data/cards.cdb` is not a valid input.

The 2026-11-06 rewrite uses the RDA separation of observed state, resource-checked
model transitions and actual offers. Complete guide rollouts are evaluated
before beam pruning, then compared with alternative routes and stopping.
Only the first action is committed, including its source/effect, target, cost,
materials and zones. Every next offer is replanned from authoritative state.

The search shares a 180 ms deadline with cancellation, depth 30, beam width 64,
5,000 expanded nodes and 256 candidates per expansion. Scoring considers
functional interaction, usable spell/discard resources including modeled End
Phase returns, and Maxx "C" draws. Duplicate hard-once-per-name bosses do not
count as duplicate interaction. This is bounded heuristic lookahead, not a
global-optimum guarantee or an opponent simulator.

The supported guide goes through Walpurgis; Genni -> Schmietta -> Pittore ->
Potterie -> Marka with explicit Deck costs; Dark Magician of Destruction/Gaze/
Dragoon before Terracottan's lock; Genni copying Creation; Terracottan recovering
Celebration and fusing Pupils; Schmietta sending Madame Verre when needed;
Pupils copying Celebration for Rilliona; Rilliona's separate three-body trigger;
Celebration into Vice; Edel reviving Dragoon with Vice recovering Distortion.
Alternatives include direct Haine/Verre control, Maid Verre access and other
resource-valid Witchcrafter fusions. No search, material or recruit is free in
the model: transitions move counted identities between actual zones.

Generic optional summon-trigger prompts are distinguished from named effects.
Own-turn fast-effect windows do not spend Celebration before Terra's next
ignition action. Rilliona selects three planned recruits when offered; Pupils
requests an available Extra Monster Zone. Resolution uses chain activation
snapshots, and opponent summons cannot spend the bot's normal summon. Logs
record alternative endpoint scores, selected costs/materials and the forecast.

The decision-quality follow-up adds fieldless Lacrima -> Genni copying Lacrima ->
Marka/Dragoon or Maid Verre access, Maid Genni's hand summon and explicit
banish/recruit choices, additional low-monster recruits, and Terracottan's monster
recovery. A missing one-of in the guide no longer terminates the ladder. Independent
field access precedes a normal summon when those two planned actions commute;
both reordered actions are checked again. This is not a blanket ban on normal
summoning: fieldless Lacrima can require a Witchcrafter on board first.

`Witchcraft2026Tactics.cs` separates immediate responses from long combo forecasts.
Its bounded reverse-chain prediction covers the verified removal/negation links,
already-paid costs in the observed state, disappearing targets and Widow Anchor's
surviving-target requirement. Destroying an activated quick-play spell is not
treated as negating it. Vice chooses again from actual candidates at resolution;
duplicate removal can still be useful if the earlier removal is negated. Unknown
effects are logged as unknown, not assigned invented resolution semantics.
Dragoon compares threat value with discard and reserved-interruption cost; optional
searches on an established board can be allowed to lose to Ash. Known face-up Zero
adds a reserve opportunity cost; its activated two-recruit/removal effect receives
higher priority. This does not predict what an opponent would have done in an
alternate history. Quick low-monster/Maid/Lacrima responses compare resource-valid
one-step outcomes against Pass, and cannot use Walpurgis's Deck cost on the opponent's
turn or count next-own-End-Phase spell returns as immediately available fuel.

Maxx "C" still charges each forecast summon, but an observed lethal-damage threat
can justify a defensive upgrade; the survival bonus is awarded once to a functional
boss/durable stop, not to each extra body. These are documented heuristics, not
calibrated win probabilities or a complete opponent battle simulation. The search
depth, beam, node and deadline budgets are unchanged.

For an empty opposing field, Main Phase lethal uses current attack-position,
unspent attackers and a legal Battle Phase transition. It does not assume removal
of blockers or safety through unknown backrow. Battle decisions recheck the engine's
actual attackable/direct-attack offers and take the largest direct attack first
when that produces the shortest lethal sequence. Hidden hand responses remain
unknown. Blocked-board lethal search, lingering attack restrictions not represented
in Main Phase observations, generic Link/Synchro continuations, all copied spells,
every floodgate and side decking remain unimplemented. Chain tests below are
synthetic callback/state tests, not native replays of the two human duels.

Confirmed reference-log causes: `150713` had no modeled Lacrima or Maid Genni hand
line and only three recruit targets; its fixed Maxx penalty could suppress the
remaining branch. `153253` used a one-selection Maid handler for a two-selection
effect, permitting Haine to be banished; Haine spent a spell on an already resolving
Anchor and Vice then removed that same spell before Haine resolved. Vice's removal
is non-targeting, despite the movement log's generic selection wording. Dragoon's
old handler accepted every enemy chain; there was no lethal gate before strategic
fusion/search. The new diagnostics record Pass, marginal gain/cost, chain snapshots,
reverse-resolution assumptions, selected resources and bounded candidate summaries.
Accepted prompt choices are bound to the actual chain-link index, not merely the
card name; two effects from the same Vice in one chain cannot overwrite each
other's resolution context. Haine and Seed use callback-owned selections instead
of leaving a target queue behind a generic selection override.

Run focused model/callback fixtures after a Debug build:

```powershell
.\WindBot\Tools\Invoke-Witchcraft2026PlannerChecks.ps1 `
  -BotExecutable WindBot/bin/Debug/WindBot.exe `
  -Database D:/Games/MDPro4/Data/locales/en-US/cards.cdb `
  -OutputDirectory Artifacts/witchcraft-planner-my-change
```

The Sky-Striker-style launcher is unchanged: open a compatible room and run
`Tools/Run-Witchcraft2026.bat`. It builds Debug and writes a timestamped
decision log beside the launcher. Use `--build-only` to compile without joining.

```bat
WindBot\Tools\Run-Witchcraft2026.bat 127.0.0.1 7911 "D:\Games\MDPro4\Data\locales\en-US\cards.cdb" ""
```

Run the native route checker with Python 3.11+ and explicit host/data inputs:

```powershell
.\WindBot\Tools\Invoke-Witchcraft2026NativeChecks.ps1 `
  -BotExecutable WindBot/bin/Debug/WindBot.exe `
  -Python C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe `
  -ServerLibrary D:/Games/MDPro4/MDPro4_Data/Plugins/x86_64/ygoserver.dll `
  -DataRoot D:/Games/MDPro4/Data `
  -OutputDirectory Artifacts/witchcraft-native-my-change
```

This permutes the selected deck without changing its multiset, runs two isolated
no-shuffle/no-deck-check duels in different client launch slots, and checks actual
card movements through the first own Main Phase. Default is the reported
Madame/Marka/Edel/Creation/Maid Verre opening. Exercise the no-Madame branch with
`-Opening @('32353566','14558127','14558127','23434538','6071005')`.
The opponent is passive Default; wins are not competitive evidence. Launch slot
is not play order, and the native core RNG seed is unavailable.

Initial rewrite validation on 2026-11-06 included 23 model/callback checks, Debug/Release builds,
44/44 native route assertions per opening (four duels total), and a separate
two-duel Ash-interruption probe. The preserved baseline passes only 14/44 route
assertions with the reported opening despite completing both duels. Winning the
passive fixture does not prove that a combo was played correctly.
Receipts are recorded in ignored workspace `Artifacts/`.
The decision-quality follow-up passes 48 focused checks, Debug/Release builds,
the launcher's `--build-only` path, and independent C# 6 compilation of all three
Witchcraft source files. Final receipts are `Artifacts/witchcraft-decision-accepted-*`.
Six passive native duels pass FieldAccess 6/6, FieldlessLacrima 16/16 and
FieldlessMaid 12/12 assertions. The preserved pre-change runtime passes only
4/16 Lacrima and 4/12 Maid assertions (completion/no-mismatch only), despite wins.
The guide pair records 39/44 strict milestones: one duel completes all 22; the
other completes the prefix then takes a verified lethal before the last five
setup actions. Those five strict failures remain visible in the receipt.
`-AllowVerifiedLethal` separately accepts only this shorter-win case after the
complete guide prefix, with actual attacks and a win before any further Main Phase
or strategic action. Without that explicit switch the strict checker still fails.
Earlier failed receipts are retained; none are replaced by the final runs.
Two additional native Ash-interruption duels complete with a frozen 40-Ash test
opponent (no-deck-check fixture only): Bifrost is negated and Lacrima responds to
the exposed Ash activation before the bot continues. This tests a real response
window, not the original mixed Maxx-C/field-destruction duel or matchup strength.

To reproduce, use the native command above with a fresh output directory and:

- `-Scenario FieldAccess -Opening @('59106048','95245544','58139997','73664385','32236916')`
- `-Scenario FieldlessLacrima -Opening @('59106048','95245544','58139997','73664385','14558127')`
- `-Scenario FieldlessMaid -Opening @('59106048','95245544','58139997','7656689','14558127')`
- `-AllowVerifiedLethal` for the default guide/shorter-win acceptance comparison.

Priorities still outstanding, in impact order: broader native hostile-chain and
Maxx-C recovery fixtures; a richer public-threat/usable-interruption evaluator;
blocked-board lethal and more card-effect coverage; frozen Arena matchup comparison.
No new Arena strength result or universal chain intelligence is claimed. Neither
successful passive duels nor focused assertions establish competitive strength.

### Opponent-aware foundation (2026-11-08)

This is a working, bounded foundation, **not universal card understanding**. It
implements the latest adopted opponent-awareness prompt using the five supplied
`20261108` logs. It does not change the deck or the `.bat` entry point.

| Evidence / actual alternatives | Previous cause | Implemented comparison / remaining uncertainty |
| --- | --- | --- |
| `113011`, 11:32:58–11:33:50: Hollie Sue or Case for K9 | Removal mostly used ATK (`10 + ATK/500`) versus a flat Spell value; Verre fired whenever the opponent chained with a live monster. | Case has an activation-only search, not a repeatable search. Destroying it prevents that search **but** permits a separate destroyed-card Quick-Play Set trigger. Hollie has conditional control/recruit modes with hidden reveal costs. Both alternatives now report their actual factors; no fixed Case-over-Hollie oracle is asserted. The original log also shows Seed subsequently bouncing Case. |
| `113011`, 11:34:06–11:34:21: Izuna resolving, Jokul field search unused | Haine preferred Izuna's slightly higher ATK; no separate hand/field usage history. | Izuna's committed send survives destruction. Removing Jokul denies its unused ignition search. Fixture scores: Jokul 9.3 vs Izuna 3.4; when Jokul is also spent, ranking reverses. Native chains independently verify Izuna's send and the surviving Jokul search. |
| `113011`, 11:32:58: Verre versus Case activation | The handler did not compare effect coverage or reservation cost at all. | Case itself is outside Verre's coverage. With only conditional Hollie effects, hold (1.6 versus cost 7); on an actual Hollie control activation, respond (18 versus 7). Revealing the costs establishes that live activation, without inventing future hand contents. Hollie's previous-turn use is correctly reset, not carried into this turn. |
| `113011`, 11:40:27–11:41:20: rebuilding into C104 and Jacks | Uninterrupted combo endpoints were scored without visible response availability. | C104 needs the publicly observed Number 104 material; Jacks needs material and a hand/GY monster activation. Supported routes report an uninterrupted branch and a single-response sensitivity floor. A used/disabled/removed response is excluded on re-observation. This is not full opponent search and does not guarantee that the proposed recovery survives. |
| `111215`, 11:17:36–11:18:09: Schmietta, Mastery, Gaze, Distortion, Verre | Mastery was discarded for recruiting, then Impulse stopped the recruit and Ash stopped the GY send. No independent-access insurance distinguished the Spell costs. | Equal-endpoint recruiting now prefers retaining unused Mastery with remaining Maid targets over stranded Gaze. The denied branch pays tribute/discard, creates no recruited monster, and exposes surviving Mastery as a new legal model route. Impulse was facedown and Ash hidden before use: neither is assumed known. No claim that this beats both interruptions. |

Implementation ownership:

- `Game/AI/OpponentAwareness.cs` is the opt-in shared semantics/usage/threat service;
  `WindBot.csproj` explicitly includes it. Only Witchcraft uses it in this update.
  Reviewed profiles cover Jokul, Izuna, Hollie Sue, Case, C104 and Jacks. They record
  effect identity/location, shared or separate usage groups, timing, supported
  outcomes, source-presence dependence, material conditions and uncertain costs.
  Shared outcome weights replace card-name ranking. Unknown cards retain a bounded
  uncertainty allowance, not zero threat or a claimed certain catastrophe.
- `Witchcraft2026Executor.cs` observes chain snapshots after their producer appends
  them, records solved/negated events, resets turn/instance state and gates Verre.
  `Witchcraft2026Outcomes.cs` also resets instance state when set facedown.
  Activation, resolution notification and negated/disabled notification are distinct.
  The current protocol's combined negation list does **not** distinguish activation
  negation from effect negation: activation-only oath availability is marked
  uncertain. A resolution notification is not proof that every operation succeeded.
  Ambiguous descriptions remain unidentified; copied effects are not guessed.
- `Witchcraft2026Tactics.cs` shares the service for Haine/Vice removal, Verre coverage
  and negate reservation, retaining the existing verified protection/chain forecast.
  Destruction is not negation. At Vice resolution, re-rank only actual offered
  targets. Diagnostics include remaining/committed effects, benefits, costs,
  uncertainty and the chosen alternative; no post-hoc fabricated rationale.
- `Witchcraft2026Resilience.cs` and `Witchcraft2026StrategicPlanner.cs` add copied
  public-response snapshots, known paid-cost failure states, independent access
  insurance and a one-response sensitivity floor. The 45% blend is an explicit
  risk heuristic, **not a probability**. It does not consume the same hypothetical
  negate once per edge or write predicted results to Duel. Existing 180 ms/depth/
  beam/node/cancellation limits are unchanged. Logs show the vulnerable edge,
  response class, floor and commitment cost next to the uninterrupted route.

New checks and exact commands (use fresh output names):

```powershell
./WindBot/Tools/Invoke-WitchcraftOpponentChecks.ps1 `
  -BotExecutable WindBot/bin/Debug/WindBot.exe `
  -Database D:/Games/MDPro4/Data/locales/en-US/cards.cdb `
  -OutputDirectory Artifacts/witchcraft-opponent-my-check

& C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe `
  WindBot/Tools/witchcraft_opponent_native.py `
  --core D:/Games/MDPro4/MDPro4_Data/Plugins/x86_64/ocgcore.dll `
  --database D:/Games/MDPro4/Data/locales/en-US/cards.cdb `
  --scripts D:/Games/MDPro4/Data/script.zip `
  --output Artifacts/witchcraft-opponent-my-core.json
```

`WitchcraftOpponentChecks.cs` exercises the shared service, actual executor
handlers/selection/lifecycle and planner costs. Counterexamples cover spent Jokul,
new turns/instances, shared Hollie modes, missing C104 material, already-negated
links, unknown descriptions, duplicate observations and unknown effects. Synthetic
search/draw profiles on an unrelated card demonstrate reversible rankings using
the same logic. Real Izuna has no repeatable unused field ignition after its
on-summon send; that requested counterexample is not fabricated.

`witchcraft_opponent_native.py` uses the installed native effects for Case, Izuna
and Jokul, with a fixture-only targeting destruction response above them. Seven
assertions verify actual reversed chains: Case search fails, its floating Set
works, Izuna's send works despite destruction, then Jokul searches. This is an
isolated engine fixture, not WindBot defeating K9. `witchcraft_outcome_native.py`
retains its original 16-assertion default and adds an explicit fixture/responder
seam. Unexpected prompts fail rather than receive guessed responses. Initial
fixture runs exposed missing placement/no-tribute option handling and a core
confirmation yield; their incomplete receipts are retained.

Evidence (separate kinds, not a strength claim):

- New focused results: `Artifacts/witchcraft-opponent-accepted-focused/report.json`,
  41/41, including actual lifecycle and ambiguity handling.
- Existing Witchcraft: `witchcraft-opponent-accepted-existing/report.json`, 90/90.
  RDA: `witchcraft-opponent-accepted-rda/report.json`, 14/14. Sky Striker:
  `witchcraft-opponent-accepted-sky/report.json`, 92/92. There were pre-existing uncommitted
  Sky Striker edits; they were neither changed nor included in this update.
- Native effect chains: `witchcraft-opponent-core-05.json`, 7/7. Existing core
  mechanics: `witchcraft-opponent-core-regression.json`, 16/16. Receipts hash the
  core, CDB, script archive and fixture. The configured scripts remain SHA-256
  `7638c79ebcc7fbfa03980a3cf32375558d2633fd053de2b8f8c2ecff372885cb`.
- Native full-bot guide: latest `witchcraft-opponent-accepted-guide`, two completed
  passive Default duels, **39/44 strict** milestones: the first took separately
  verified early lethal; all five strict failures remain in that receipt. Earlier
  `witchcraft-opponent-final-guide` reached **44/44**, and the initial
  `witchcraft-opponent-guide-01` reached **39/44**. No test threshold was weakened. The host
  RNG is not controlled by the policy seed; neither result proves playing strength.
- Debug and Release builds pass (five pre-existing warnings). All five Witchcraft
  source files plus the shared service independently compile with `/langversion:6`;
  that subset check emits expected duplicate-type warnings against the runtime
  reference. The new focused harness also compiles as C# 6.
- The pre-build runtime is preserved in `witchcraft-opponent-baseline-a300a992`;
  executable SHA-256 `60c905be5548c97ccf1fe3e65a56dff6626f45de623484e24bbc0020ab4193b9`.
  This is the actual pre-existing working runtime, not a claimed clean-commit build.
  Arena has no configured SQLite/dedicated SRVPro here; loopback console refused
  connection. No Arena experiment or settings mutation was performed.

Limits / next strategic work: full adversarial route branching and controlled K9
replays, more independently verified effect profiles (including the Orcust and
other engines in `110635`, `105411`, `102215`), complete source-dependency and
floating/replacement coverage, publicly confirmed facedown interruptions, Case's
conditional team ATK aura, and response costs/targets beyond the supported subset.
The C104 sensitivity floor does not simulate its optional random hand loss/LP
halving; Jacks conservatively tests loss of an existing unprotected modeled body,
not a guaranteed future target. Hidden-cost Hollie responses remain conditional.
The remaining logs were triaged, not claimed fully repaired. Build success,
synthetic scores and passive wins do not prove competitive playing strength.

### Floodgate/outcome repair (2026-11-07)

The `Witchcraft2026-20261107-194227.log` failure was not simply a weak deck list.
The following confirmed defects were repaired without changing the deck,
registration, launcher, shared dispatcher or search depth:

| Log evidence | Before | Updated decision/model |
| --- | --- | --- |
| 19:46:16–19:46:27 | Vice selected protected Winda above Verre in the chain. | Effect-destruction protection is conditional on controller, face-up presence and current negation; a lower Verre link is not applied early. Reverse-chain fixtures cover both orders. |
| 19:49:27–19:49:43 | After one special summon under Winda, Pupils/Celebration still forecast Fusion. The script could activate its destruction mode instead, losing Vice/Marka without removing Winda. | Every modeled summon is filtered by observed per-source limits. Pupils can choose its separate search mode. Forced destruction selection evaluates a friendly/enemy pair's actual removals and minimizes friendly loss; absent Fusion options invalidate the expectation, never invent a cancellation. |
| 19:53:27–19:54:55 | Newly summoned Anoyatyllis/Winda did not constrain future modeled summons. | Re-observation includes restriction source, active/disabled state and event count. Spell hand/GY summons are distinguished from Deck/Extra origins and monster-effect copies. Verre has a resource-paid planning branch to disable opposing restrictions first. |
| 19:45:30–19:45:59 | Distortion destroyed hand Scheiren and enabled its GY Fusion. | Negate-and-destroy forecasts include the destination and known possible GY trigger. With no identified urgent target the response is reserved for an actual follow-up offer. Public already-spent triggers and urgent threats are distinguished; hidden Fusion materials are not guessed. |
| 19:54:39–19:55:30 | Gaze Dragoon counted as permanent. | The observed card instance and modeled board track next-turn End expiry. Leaving/returning or being set clears its lifetime. Evaluation compares current interaction with the projected post-expiry board; it remains a two-horizon heuristic, not a win probability. |
| 19:55:12–19:55:14 | Base battle selection allowed an equal-ATK Vice trade without evaluating its engine loss. | Compare lost defense, public GY recovery and resource-valid Main2 continuations after removing the restriction, under one bounded comparison budget. Equal trades can still be accepted when they unlock enough real follow-up. |
| 19:55:36 and 19:57:38 | Four-name Walpurgis search coverage missed remaining Pittore/Potterie. | Search considers all supported Witchcrafter monsters, including discarding the just-searched card. The earlier collapse has a modeled low-monster-to-Haine recovery; after Walpurgis is destroyed, Edel plus Mastery with both Maids outside Deck does not invent a route. |

`Witchcraft2026Outcomes.cs` owns verified semantic profiles, restriction/lifetime
observations, paired destruction and prediction reconciliation. The strategic
planner applies these constraints to all edges; tactics uses resolution-aware
protection and battle/negate outcomes; the executor binds them to actual callbacks
and legal offers. `reconcile` logs expected versus observed zone deltas,
restrictions, expiry and evaluation after a chain, then retires the forecast.
Concurrent chain effects can legitimately produce differences: the log labels
them `changed-or-interrupted`, not automatically an engine error. `hold audit`
separates generated/offered model actions from the engine's activation offers.
These checks do not write predictions into authoritative duel state.

The installed core independently verifies 16 mechanics assertions, including
protected versus disabled Winda, harmful paired destruction, per-source summon
limits (including while temporarily disabled and a newly arriving source), and
all six tested Anoyatyllis source/effect-class distinctions. This is a scripted
core probe, **not** a WindBot replay of the human's whole duel:

```powershell
& C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe `
  WindBot/Tools/witchcraft_outcome_native.py `
  --core D:/Games/MDPro4/MDPro4_Data/Plugins/x86_64/ocgcore.dll `
  --database D:/Games/MDPro4/Data/locales/en-US/cards.cdb `
  --scripts D:/Games/MDPro4/Data/script.zip `
  --output Artifacts/witchcraft-outcomes-core-my-change.json
```

The tool uses the core's 76-byte/16-ushort-setcode reader ABI and checks populated
metadata in Lua. The obsolete managed wrapper's packed-setcode ABI is not valid
for this DLL. Initial probe development failures (ABI, placement/position waits,
adjustment timing) were corrected; successful receipt is
`Artifacts/witchcraft-outcomes-core-05.json`. Inputs are hashed there. No installed
scripts, databases, game settings or unrelated processes are changed.

Validation receipts under `Artifacts/witchcraft-outcomes-*` distinguish:

- `accepted-focused/report.json`: 90 deterministic model/callback checks, including
  the original guide, legal recovery versus no recovery, chain order and resets.
- `accepted-guide/route-checks.json`: the original 39/44 strict guide milestones;
  the first duel takes separately verified early lethal after the prefix, and
  the second completes all 22. The five strict failures remain recorded.
- `access-01`, `maid-01`, `control-01`: two native duels each, respectively 6/6,
  12/12 and 22/22 movement assertions. The fieldless control route is Lacrima ->
  Genni copy -> Terra -> Pupils search -> Celebration/Vice, with both Fusions
  surviving the first Main Phase. Run the existing native command with
  `-Scenario FieldlessControl -Opening @('59106048','95245544','58139997','73664385','14558127')`.
- `fieldless-01`: the unchanged strict Marka/Dragoon fixture records **12/16**,
  because the temporary-boss-aware planner chooses the above control route.
  These four route-specific failures are not erased or counted as passes by the
  new, separately named scenario. Both duels complete without material mismatch.
- `rda-01` and `skystriker-01`: unchanged-bot scheduling/model/callback regressions
  pass. No shared bot/protocol source was edited. Debug and Release builds pass
  with five pre-existing warnings; all four Witchcraft2026 files compile as C# 6.

Baseline runtime was preserved before building in
`Artifacts/witchcraft-floodgate-baseline-9f3c588f` (executable SHA-256
`a55a8d70947d789a8f6aa4e085ca3b09e6db28e074f5ee5979dcaedfae83067a`).
Native receipts contain each candidate executable, host and data hash. Wins over
passive Default establish neither competitive strength nor the correctness of an
unexercised hostile chain. No configured Arena SQLite/dedicated SRVPro instance
or running loopback console was available; no Arena win-rate result is claimed.

Remaining priorities: (1) a full native hostile-chain fixture with changing
floodgates and delayed Gaze, not only isolated mechanics/callbacks and the supplied
log; (2) more verified protection/floating/replacement-effect profiles and exposed
usage counters, especially Bystreet's consumed shield and negated summon attempts;
(3) deeper opponent battle/response forecasting and an actual frozen Arena matchup
comparison. Unknown effects, hidden resources and unexposed protection counters
remain unknown. The paired-destruction fallback conservatively assumes a friendly
card can be lost rather than crediting an unverified remaining Bystreet shield.

All three changed executor/model/tactics files compile independently with C# 6.
A whole-solution C# 6 override still fails in pre-existing AIUtil/Albaz/Ryzeal
syntax; ordinary Debug/Release builds use the project's existing language setup.
Native hashes are recorded in each `native/result.json` (or `result.json`
for the interruption probe); route assertions are in `route-checks.json`.
Selected CDB SHA-256:
`1f4a9afbec2133fd9c051fb53e940aaeb9d5603af74a29dc7dae62ef630086c8`.
Script archive SHA-256:
`7638c79ebcc7fbfa03980a3cf32375558d2633fd053de2b8f8c2ecff372885cb`.

Arena's 142 automated tests, frontend build and six process-owner tests pass.
These are integration checks, not a frozen strength comparison. No Arena SQLite
configuration or dedicated SRVPro endpoint was available in this checkout, so no
Arena win-rate experiment is claimed. `BotWrapper/bot.conf` still exposes
**Witchcraft 2026 (Planner)** for a future configured comparison against frozen
baseline `a0360a76` (preserved executable SHA-256
`fcbc4d56648e1d68971ee56ddeb596f9915094d4aa3c4d47b2ce24a245bc3799`).

## Sky Striker 2026 executor

The original `Deck=SkyStriker` executor and `Decks/AI_SkyStriker.ydk` remain
unchanged. The separate `Deck=SkyStriker2026` entry loads the new ordinary WindBot
executor and `Decks/AI_SkyStriker2026.ydk`. Its 40-card Main Deck, 15-card Extra
Deck and 15-card Side Deck were imported from the user-supplied
`D:\Games\MDPro4\Deck\Sky striker.ydk` on 2026-11-04. The companion MDPro4 deck
`D:\Games\MDPro4\Deck\_Skystriker_2026.ydk` was refreshed from the same source at
the user's request; appearance and accessory metadata remain outside the versioned
runtime deck resource.

The latest 2026-11-04 refresh replaces Forbidden Droplet and Triple Tactics
Talent with two Pot of Desires, without changing the 15-card Extra or Side Deck.

The executor prioritizes an OTK when going second and a Zero-centered setup when
going first. It covers Raye/Roze access, Engage/Kagari recovery, Linkage battle
extension, Lemnisgate recycling, Zero/Amatsu/Widow Anchor interaction, the modern
Camellia/Azalea/Zeke links, the Fallen/Dogma/Ecclesia package and TY-PHON. The
listed Side Deck board breakers and counter traps have ordinary executor support.
All target, material, recycle and option choices remain restricted to candidates
offered by the duel server.

Engage now treats Raye, Hornet Drones and Roze as emergency starter searches only
when no live Sky Striker access is already available. Otherwise its search order
is goal-driven: Linkage for a prepared OTK, Widow Anchor or Maneuver cards for an
observed board, Shark Cannon for a known GY threat, and Lemnisgate or Multirole for
resource conversion. Lemnisgate separately scores its equal monster/Spell recycle
pairs, preserves Raye/Roze in the GY where possible, restores spent Extra Deck
Aces and high-value Spells, and pays four or six shuffled cards only when one or
two useful bounce targets exist. Its two optional prompts are handled separately:
the field effect will not bounce a resolving non-persistent Spell/Trap, while the
GY effect Link Summons only for a battle extension, defensive Kaina/Amatsu,
Kagari recovery, Zero setup or Shizuku end board.

Azalea accepts both the explicit effect description and MDPro4's description-less
optional summon callback. It plans a target before spending Link materials,
activates the summon trigger against the highest-value legal opposing card, and
accounts for losing itself when the GY contains three or fewer Spells. It may also
be summoned without a destruction target when a controlled opposing LIGHT/DARK
monster plus the bot's existing material produces the complete conservative
Azalea -> Linkage -> Kagari -> Linkage -> Zero lethal route. Its battle trigger
remains available whenever a legal Spell banish cost exists.

The four user-supplied 2026-11-03 and 2026-11-04 mirror logs are treated as opt-in
two-sided decision evidence, not as automatically promoted model checkpoints.
Public actions by either player may supply a positive demonstration or a negative
regression case; hidden `UnKnowCard` identities are never reconstructed. Negative
cases cover an established first-turn Zero searching redundant monster access,
Lemnisgate spending a single pair for no bounce or extension, and its GY Link
effect consuming Raye plus Roze for a targetless Azalea. Positive opponent examples
cover Hayate sending Engage, Kagari recovering Linkage, Shizuku selecting follow-up,
Zeke temporarily removing an opposing Ace, Shark Cannon denying Raye, multi-pair
Lemnisgate tempo bounces, and Widow Anchor converting a stolen Amatsu with Zeke
into an Azalea-led Linkage OTK. Only a line whose required cards and public state
are present is enabled; seeing the opponent perform it is not treated as proof that
unknown cards exist in the bot's hand or Deck.

The 2026-11-04 23:44 replay adds two further regression guards. An established
turn-one Zero/Ace board keeps Roze in hand instead of Normal Summoning it solely
to turn Roze into Kagari for an early Engage recovery. Lemnisgate no longer treats
an arbitrary Ace or Linkage in hand as sufficient reason to recycle a single pair.
It also refuses to chain the recycle effect over the bot's own unresolved effect.
MDPro4 presents the
recycle choice through repeated one-card `SelectUnselect` prompts, so the executor
now advances that selection incrementally instead of indexing a nonexistent full
monster/Spell candidate pair.

Main Monster Zone preservation is a shared executor invariant rather than a
single-combo exception. Whenever a Sky Striker Spell remains in hand/on the field
or a tracked Zero, Hornet or Linkage line still plans to use one, a Sky Striker
Link Summon must consume every current Main Monster Zone blocker and the existing
Extra Monster Zone Ace; otherwise the summon is rejected because the new Link
would occupy a Main Monster Zone. The selected Link placement is explicitly
restricted to an Extra Monster Zone. Normal Raye/Roze summons are likewise held
when an existing monster prevents that immediate clearance. The only temporary
exception is the tracked Hornet bridge whose following Zero summon consumes both
the bridge and Token. Main-Phase Linkage and untracked Lemnisgate GY Links cannot
create a second Ace beside an existing Extra Monster Zone Ace while a Striker
Spell plan remains live. This is a planning constraint rather than a permanent
ban: when Zero's defensive exit summons Raye and Roze, the executor records a
next-turn clearance step and Link Summons a fresh Zero with both monsters before
trying to spend another Sky Striker Spell.

The setup layer now uses a bounded RDA-style route search instead of deciding
each starter in isolation. An immutable observed snapshot covers the current
Main/Extra Monster Zones, own known hand/Deck/GY/Extra Deck resources, once-per-turn
usage, Spell threshold, opponent public field counts and the remaining own Deck
count. Pure transitions compare Engage/ROTA access, Raye-to-Zero, the two-Hornet
Kagari/bridge/Zero route, Raye+Roze Main Zone clearance, Ultimate Sky chained to
Linkage, setting the lowest-opportunity-cost hand card as ordinary Linkage fuel,
ordinary Linkage activation and Pot risk. A planned `set fuel -> Linkage -> Zero`
route is therefore one turn plan rather than two unrelated card predicates; live
interaction such as Shark Cannon on a public Raye is protected while a dead
Lemnisgate or Metalfoes Fusion can be committed as fuel. The best modeled route supplies only
its first recommendation; the normal executor still matches the actual legal
offer and owns every target, material and option prompt. Movement, chain, phase
and turn observations retire the cached recommendation and force a fresh plan.
Search is synchronous and capped at 35 ms, depth 8, beam width 72, 2,500 expanded
states and 6,000 generated candidates.

The 2026-11-06 planning update filters the first step against the current server
offers and includes that offer mask in cache identity. An unavailable search or
summon therefore triggers another route instead of reserving an impossible first
action. Intermediate search states can remain in the frontier, but a selected
endpoint cannot still have a pending Zero search or leave Main Zones occupied
while modeled Sky Striker Spells remain. When an evaluated action has no better
completed route, the executor preserves the planner's hold decision rather than
automatically accepting that action through fallback.

Endpoint scoring adds discounted next-turn starter/recovery access and prioritizes
an already prepared, empty-opponent-field Linkage battle line when its modeled
damage reaches the observed LP. That projection requires an available attacker,
Kagari, Zero, Raye and Roze access, unused summon resources and empty Main Zones.
It does not simulate opposing responses or guarantee lethal. Follow-up value is
an estimate from retained resources, not an opponent-turn simulation. Debug route
logs now show the expected final Link, Main Zone count, follow-up value and battle
damage; explicit hold decisions are logged too.

Resource transitions no longer count vanished Tokens as GY monsters or spend the
same hand fuel twice. Zero's model and actual selection callback both retain the
three-other-Spell threshold when choosing Deck versus GY Engage. Bridge availability
also respects this turn's recorded summons; tactical/card-specific handlers still
own emergency responses and all actual resolution prompts.

Triple Tactics Talent, Ultimate Sky and Kaina are handled explicitly even when a
future list chooses to include Talent. Talent prefers taking a high-value monster
only when it creates lethal or removes a major threat, otherwise it draws two
before using the hand-shuffle fallback; its control and revealed-hand prompts use
only server-offered cards. Ultimate Sky is set for the opponent's turn and its
dedicated hand/GY negate now requires a 3,600-point threat threshold instead of
answering every legal monster effect. Maxx "C", Mulcharmy Fuwalos and Droll on
the bot's turn receive explicit stop value, while Ash Blossom, Ghost Belle,
Ghost Ogre and Effect Veiler receive it only when they are answering a core
Sky Striker engine effect. The field-negate mode spends LP only for scored
face-up threats while preserving a safe LP reserve outside emergencies. On an
otherwise empty own-turn board, Ultimate Sky can also be activated from hand as
the explicit send card for a chained Linkage; that tracked recovery line summons
Zero instead of passing with both cards stranded in hand. Kaina is available as a
defensive Raye tag and Main Phase 2 Link, targeting the strongest opponent
monster that can still attack; it is also recognized throughout Link, Lemnisgate
and mirror-selection policy.

Droll & Lock Bird uses the public movement event that an opponent added a card
from the Main Deck to hand outside the Draw Phase. It no longer depends on the
last Chain player still being the opponent after that Chain has finished, so an
Engage, Zero or Linkage search opens the correct response window without reading
the identity of a hidden card.

Ash Blossom distinguishes an already-negated on-field effect from a monster that
left the field while paying its activation cost. It holds when the current
face-up field monster is still disabled or a tracked negate already covers that
chain link. It does not assume that an effect remains negated after its monster
has moved to another zone as cost; the normal Ash eligibility check still applies
there.

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
lethal defense, a losing battle, or a high-value target whose destruction repays
the temporary loss of Zero. The response is legal on either player's turn. In a
mirror it answers the opposing Zero's Quick Effect at activation, before that
effect reaches its resolution-only destruction choice; waiting until Zero becomes
the selected card would be too late to Chain. A next-turn Raye/Roze clearance
route lowers the value threshold but is not a prerequisite for escaping removal
or destroying a major threat. A mirror Raye in the opponent's Graveyard remains
a cost penalty rather than an absolute ban. Normal Spells that have already spent
their effects are worth zero. The optional destruction prompt is tied to the
target scored at activation instead of selecting an arbitrary face-up card after
Raye and Roze are summoned.

The 2026-11-05 replay also closes Lemnisgate's remaining no-bounce leak. Restoring
a spent Link Ace to the Extra Deck is not, by itself, permission to activate it:
one recycled Spell/Ace pair produces zero returns under the card script. A
one-pair activation is never used merely to stage Lemnisgate in the GY; ordinary
use requires two pairs for one worthwhile return or three pairs for two. The
post-recycle `StringId 1` prompt now correctly accepts those
returns instead of consulting the unrelated grave-Link condition. The same
live-value rule controls whether Engage prioritizes Lemnisgate.

The later 2026-11-05 mirror replay adds resource-exchange scoring on top of that
pair arithmetic. Lemnisgate now compares the public value of every planned return
with the opportunity cost of the exact Spells and monsters it would recycle. A
spent opposing Kagari is penalized when returning it to the Extra Deck restores a
mirror resource or enables a public Raye/Roze GY trigger; winning the pair count
alone is not enough. The 21:51 replay removes the former Hornet staging exception
entirely: the field effect is rejected when the opposing field is empty and also
requires a worthwhile opposing return. Two recycle pairs earn one return and
three pairs earn two; this floor-of-selected-cards-divided-by-three arithmetic is
checked before activation. Lemnisgate must reach the GY through Amatsu or another
real cost when the plan only wants its grave-Link effect. Its GY Link effect now
scores the actual extension, defensive interruption or recovery against the
cheapest legal material plus the spent GY card, rejecting thin conversions.

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
now branch-scores the resolution target: on the bot's Main Phase it revives a
chain-targeted Raye when the three-Spell bonus, empty Main Monster Zone and an
immediate tag/Link conversion are all present; otherwise it banishes. Widow Anchor can take
an otherwise non-threatening opposing Ace for the same purpose, Multirole is the
first face-up back-row removal target, and searches raise Shark Cannon, Widow
Anchor and Multirole ahead of generic follow-up when their mirror conditions are
present. It also holds an idle Widow Anchor against a live opposing Raye that can
tag away from the target, while retaining live-chain and emergency responses.
The source `Sky striker.ydk` remains unchanged; only the explicitly requested
`_Skystriker_2026.ydk` companion is synchronized outside the repository.

The opening setup now treats Engage and Reinforcement of the Army as access to
Raye or Hornet Drones. A lone Raye can tag directly into Zero. The legal Drones
route is Token into Kagari (Kagari, not Shizuku, recovers Drones), a second Token,
Kagari into Kaina with Amatsu/Hayate/Shizuku as fallbacks, then the bridge Link
plus the Token into Zero. The route no longer activates Lemnisgate merely to
recycle Drones/Kagari and stage itself in the GY. Outside the OTK
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

The pre-Battle planner now reserves that route before the first attack. If Widow
Anchor has moved an opposing Ace into the Main Monster Zone, it can bridge that
monster through Hayate to clear the zone instead of consuming the once-per-turn
Kagari early. Upstart Goblin is held when its 1000 LP award would move the
opponent out of the prepared lethal range, and a premature Zero Link Summon is
deferred to Main Phase 2 when its materials would deal more Battle Phase damage.

Pot of Desires is a conservative recovery action rather than an automatic
opener. Deterministic Engage/ROTA access resolves first, and the bot normally
requires Raye-or-Drones access, Linkage and Roze to be secured outside the Deck
before risking ten cards; the exception is a genuinely low-hand emergency with
no Ace access. Shark Cannon likewise chains to a Graveyard effect only when it
removes an explicit Graveyard target or a known self-moving Raye, Roze or
Camellia. Merely banishing an already-activated Graveyard card such as The Dragon
that Devours the Dogma is not treated as negating that effect.

After building, start it from the output directory with an explicit database and
compatible duel host, for example:

For the local MDPro4 setup, open a compatible room on port `7911`, then
double-click `Tools\Run-SkyStriker2026.bat`. The launcher locates Visual Studio
MSBuild, builds `Debug|Any CPU`, and joins `127.0.0.1:7911` with decision logging
enabled. Every non-build run creates
`WindBot\Tools\SkyStriker2026-yyyyMMdd-HHmmss.log` beside the launcher itself;
Ultimate Sky records accepted and rejected timing windows, chain score/threshold,
public field scores and the competing tactical response. It only reads the
external card database and does not modify MDPro4's deck or settings files. The
defaults and optional overrides are:

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
        Host=127.0.0.1 Port=7911 Debug=True `
        "LogPath=..\..\Tools\SkyStriker2026-manual.log"
}
finally { Pop-Location }
```

Run the offline fixed-state planner checks from the workspace root with a fresh
output directory:

```powershell
.\WindBot\Tools\Invoke-SkyStrikerPlannerChecks.ps1 `
  -BotExecutable WindBot/bin/Debug/WindBot.exe `
  -OutputDirectory Artifacts/skystriker-planner-checks/my-change `
  -CardDatabase D:/Games/MDPro4/Data/locales/en-US/cards.cdb
```

They verify the modeled Engage starter, Raye-to-Zero, Hornet/Kagari/Kaina bridge,
Ultimate Sky/Linkage recovery, set-fuel/Linkage/Zero route, deterministic access
before Pot and Raye/Roze
Main Zone clearance routes, plus Ultimate Sky's chain threat threshold and
latched option choice. They also lock Zero's target/pre-emptive response policy
and Lemnisgate's return arithmetic, field/GY net resource thresholds, Shark
Cannon's revive-versus-banish branch, mirror Raye Widow hold, and Ash preservation
against an already-covered chain link. The expanded checks cover unavailable
offers, stranded Main Zones, fuel conservation, Token disposal, prepared battle
resources and next-turn access. The optional explicit `-CardDatabase` also runs
the real Zero selection callback on synthetic offered cards, including successive
Deck/GY preference changes, and the Hornet handler's hold/replan behavior when
observed Extra Deck resources change. Without it those callback checks are skipped. These
checks do not execute Lua or prove engine legality or competitive improvement.

This remains a hybrid executor: bounded search covers the modeled setup/resource
routes, tracked stages own script-specific sequences, and competing interaction
uses a pure observed-state scoring layer before the normal executor commits an
offered action. It is not an exhaustive Lua simulator or a proof of lethal, does
not predict unknown opposing cards, and cards outside the modeled packages still
use ordinary WindBot handlers. Debug/Release compilation, planner checks and
deck-list checks do not substitute for a script-backed duel; record real duel
outcomes separately before treating the executor as competitively tuned.

The first acceptance run on 2026-11-07 passed 69 focused checks but failed native
behavior; its unchanged evidence remains in
`Artifacts/skystriker-acceptance-20261107/acceptance-report.md`. The follow-up fixes
the Normal Roze/Raye and committed Linkage-fuel handler conflicts. An unresolved
own Linkage now reserves its summon/fuel without prohibiting later Battle Phase
chains. Pot's existing risk guard feeds the planner; resolved search/draw locks
also constrain projected actions, while Zero may still recover a grave Spell.

The planner can now reach the prepared lethal position from a normal summon via
Hayate, reserving Kagari and Zero. Raye preserves that reservation in the
post-summon fast-effect window, when the next Main Phase offers are not yet
available. Lemnisgate's incremental `SelectSameCount` callback completes balanced
pairs, so a two-pair plan does not accidentally pay three. Its GY effect is held
when the same normal Link can be performed for free in our Main Phase.

Run the complete regression workflow from the workspace root (no open MDPro4
room is needed):

```powershell
.\WindBot\Tools\Test-SkyStriker2026.ps1 `
  -Python 'C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' `
  -OutputDirectory Artifacts/skystriker-check-my-change
```

Or double-click `Tools/Test-SkyStriker2026.bat`. This is separate from the
`Run-SkyStriker2026.bat` room-join launcher. The test wrapper builds Debug/Release,
runs the focused suite with the selected database, and runs five controlled native
scenarios plus ten mirror duels. It requires Python 3.11+, .NET Framework 4.8
MSBuild and the explicitly selected MDPro4 data/server. Override `-DataRoot`,
`-Server`, `-MSBuild` and `-Python` if installed elsewhere. `-MirrorGames 0` skips
mirrors but is not the complete workflow. Existing output folders are rejected.

For native checks only after a Debug build:

```powershell
python WindBot/Tools/skystriker_native_checks.py `
  --bot-exe WindBot/bin/Debug/WindBot.exe `
  --data-root D:/Games/MDPro4/Data `
  --server D:/Games/MDPro4/MDPro4_Data/Plugins/x86_64/ygoserver.dll `
  --deck WindBot/Decks/AI_SkyStriker2026.ydk `
  --output-dir Artifacts/skystriker-native-my-change --mirror-games 10
```

The five scenarios assert turn-two Linkage OTK, one-copy setup preserving Zero,
replanning after Droll, Hornet/Kaina/Zero with no targetless Lemnisgate, and normal
recovery after a wipe without wasting Lemnisgate's GY effect. Mirror audits inspect
both clients for unresolved duplicate Linkage, idle recommended-root deadlocks,
Lemnisgate planned/actual return counts and runtime/selection errors. They are
log-level regression checks, not a full legality or strategy oracle. Runtime,
database, scripts, server, decks, hashes and logs are retained under the output.
Only isolated owned duel processes are started; installed game files are unchanged.

These are no-shuffle/no-deck-check fixtures. The tested Main/Extra card multiset is
preserved, but controlled opponents deliberately use synthetic decks. The core RNG
seed is unknown, and same-version mirror wins are not a strength comparison. The
92 focused checks include actual handler/callback tests on synthetic observed
states, including positive Lemnisgate GY/Kaina defense and already-negated Ash;
those two positive/negative windows are not yet certified by native fixtures.
Passing this workflow means the selected regressions passed, not that every card,
matchup, interruption or future-turn plan is perfect.

Follow-up acceptance on 2026-11-07: Debug/Release built; 92/92 focused checks,
14/14 shared RDA checks, all five controlled native scenarios and all ten audited
mirrors passed. Native OTK and four-/six-card Lemnisgate returns were observed.
Final receipts, frozen inputs, failed intermediate runs and coverage limits are
under `Artifacts/skystriker-fix-20261107/acceptance-report.md` at workspace root.

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
