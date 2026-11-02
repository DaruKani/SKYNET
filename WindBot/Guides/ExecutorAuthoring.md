# Writing and improving WindBot executors

This is the maintained guide for the normal `WindBot/` project. The source files
linked below define the current contracts. SKYNET has a separate policy and
training lifecycle; adding an executor here does not teach SKYNET that deck.

## Use the RDA structure for new executors

Start new work from the maintained
[RDA planner scaffold](../Templates/RdaPlannerExecutor.cs.template). The live
[TheCrimsonKingExecutor](../Game/AI/Decks/TheCrimsonKingExecutor.cs) is the complete
RDA reference and is newer than the supplied desktop template. Use its separation
of observed state, a deck-specific model/search, and execution of current legal
offers. Fill in the new deck's card semantics and callback lifecycle explicitly;
copying RDA's cards, target priorities or simulated scores does not implement
another deck. The scaffold has no `[Deck]` registration or card model and is not
compiled into the bot until it is deliberately completed and integrated.

The model is a prediction. After every actual response, reconcile with observed
state and replan as needed. Its search must share one deadline including queue
wait and all phases, respect cancellation, and cap nodes/frontier resources.
Keep state immutable or isolated per search. Preserve short tactical handlers
for decisions that do not need a planner. Existing imported executors remain
available; convert them only as a bounded, measured change.

[Invoke-RdaPlannerChecks.ps1](../Tools/Invoke-RdaPlannerChecks.ps1) runs fixed-hand
model and scheduling checks with explicit binary, database and deck inputs and
a unique output directory. Those checks establish behavior of the RDA model and
its lifetime limits. They do not execute Lua or prove a stronger duel player.

## Start with one observable behavior

For a new executor, identify the actual `.ydk`, supported rule set, each important
card's matching Lua script, and the source revision being imported. For a repair,
start with the failed decision and its following prompts. Record an expected
action or board outcome and a counterexample, such as an interrupted search or
an unavailable material. A published executor or a high win count is useful
evidence, not proof that all of its branches are correct.

Search the numeric card ID in [DefaultExecutor](../Game/AI/DefaultExecutor.cs),
then in a relevant [deck executor](../Game/AI/Decks). Reuse the complete behavior:
registration order, handler, targets/materials/options, fields, and lifecycle
resets. Do not copy a large helper family without its callers. Historical
"golden executor" lists are discovery hints, not an authoritative ranking.

Use the configured `cards.cdb` and the scripts actually loaded by the test host.
Card text and old compiled text dumps cannot establish prompt order or legality.
`Util.GetStringId(cardId, offset)` encodes a prompt-string offset; it is not a
printed effect number. Verify the script call using that value, and the matching
`texts.str{offset + 1}` when needed. A description of `-1` is unknown context,
not permission to assume effect zero.

## Keep the dispatcher contract intact

| Owner | Contract to verify |
| --- | --- |
| [DecksManager](../Game/AI/DecksManager.cs) | A unique `[Deck("ExternalName", "AI_FileName", "Level")]` registers an executor and selects `Decks/AI_FileName.ydk`. `Normal` enters the default random pool; `Test` does not. |
| [GameAI](../Game/GameAI.cs) | Ordered `AddExecutor` handlers compete over offered candidates; the first accepted action wins. Reordering handlers changes behavior. |
| [Executor](../Game/AI/Executor.cs) | `Card`, `Type`, `ActivateDescription` and `CurrentTiming` describe the currently evaluated candidate. They are not later callback context. |
| [GameBehavior](../Game/GameBehavior.cs) | Parses authoritative protocol state and encodes responses. A deck executor must not parse packets or rewrite this state to make its plan succeed. |
| [ClientField](../Game/ClientField.cs) | Use `HasInDeck` and `GetCardCountInDeck` for own-deck identities. `Bot.Deck` contains hidden slots; only its count is usable directly. |

Evaluate strategic suitability, not a second copy of engine legality. A legal
option may still be strategically bad. Conversely, mandatory effects and mandatory
selection counts need a valid response even when the outcome is undesirable.

Only queue a selection or mark a use once the handler accepts the action. Rejected
handlers must not leave pending selections or consumed resources behind. A
`SpellSet` handler accepts the current `Card`; `AI.SelectCard(otherCard)` does not
change which candidate is being set. Phase changes and surrender may have no
card context.

Treat unknown opponent `Id == 0` and `Data == null` as normal. Do not infer the
identity of a hidden slot, or query an engine's private state. Read dynamic attack,
level, position and location from current observations, not immutable database
definitions. Per-duel mutable state belongs to the executor instance.

## Route each prompt at its actual stage

| Stage | Reliable source context |
| --- | --- |
| Activation cost or target selection | `Duel.GetCurrentChainCard()` while the chain is being built; check controller, identity, hint and actual candidate locations. |
| Effect resolution | `Duel.GetCurrentSolvingChainInfo()`; prefer its `ActivatePlayer`, `IsActivateCode`, `ActivateLocation` and other activation snapshots. |
| Non-chain prompt | Callback arguments plus explicitly maintained, narrowly scoped state for that actual prompt. |

See [Duel](../Game/Duel.cs) and [ChainInfo](../Game/ChainInfo.cs). During resolution,
the newest chain card may be a different effect. Do not fall back to
`Util.GetLastChainCard()` or the executor's candidate `Card` to guess ownership.
Use `RelatedCard` only when a live object comparison is actually needed.
If ownership is unknown, return the base/null fallback instead of inventing it.

Mandatory triggers can bypass activation handlers. Put their required choices in
the callback that receives the actual prompt. For each effect with several
prompts, verify the Lua hint, candidate zones, option values, and order of requests.
Give an effect either a matching preselection queue or a callback that owns that
prompt; a callback response does not consume a duplicate queued selection.

Return distinct objects from the offered candidate list. Respect `min`/`max`;
an empty cancellation is allowed only when the prompt is cancelable. Use
`Util.CheckSelectCount(preferred, cards, min, max)` when its fill/truncate behavior
matches the intended policy; it is not a substitute for choosing a meaningful
fallback order. The helper cannot prove a strategy, and the dispatcher still
validates the result. Return an offered option's index for `OnSelectOption`.

`OnSelectPlace` must return a legal bitmask intersecting its `available` argument.
Calling `AI.SelectPlace` inside that override affects a future queued preference,
not the current response. Inspect [GameAI.OnSelectPlace](../Game/GameAI.cs) when
testing placement. Do not copy zone logic for a different controller or zone type.

## Keep state small and tied to observed events

An accepted activation is not a resolved effect. A cost may already be paid even
if the effect is later negated. Model those states separately when the distinction
changes a later decision. Clear temporary prompt context when consumed or retired;
clear per-chain state at chain end and per-turn state at the correct new turn.
Call the base hook when overriding lifecycle behavior it owns.

[DefaultExecutor](../Game/AI/DefaultExecutor.cs) already owns player-hint tracking,
Infinite Impermanence zone-hint tracking, and their relevant resets. Reuse these
facts instead of introducing a second predictive tracker by default. Do not clear
protocol-owned lists such as `Duel.LastChainTargets` from a deck hook merely to
hide stale context; trace the producer and consumer first.

For combos, use current board/resource facts to decide whether an available action
advances the route. Re-evaluate after interruption. Record only the missing state
that cannot be recovered from observations. A bounded planner is a separate design
choice requiring explicit state, cancellation and resource limits; mutable
WindBot predicates cannot be evaluated as if they were pure planner scores.

## Register and package the whole executor

Add its unique `[Deck]` registration, `.ydk`, and requested roster entry together.
`Game/AI/Decks/*.cs`, `Decks/*.ydk` and `Dialogs/*.json` already use project globs;
new C# files in other directories need explicit project inclusion. Preserve
existing executor and deck names when importing a source.

The roster lives in [BotWrapper/bot.conf](../BotWrapper/bot.conf). An entry selects
the exact registered `Deck` and an existing dialog stem, for example
`Dialog=Universal.EN`. Keep custom authoring and source provenance separate from
the original resource snapshot hashes. An experimental executor can be listed
explicitly without marking its registration `Normal`.

Build from the workspace root with Visual Studio/MSBuild:

```powershell
msbuild WindBot/WindBot.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU"
msbuild WindBot/WindBot.sln /t:Build /p:Configuration=Release /p:Platform="Any CPU"
```

Use C# 6-compatible code and the current .NET Framework 4.8 APIs. Follow the
project's Allman braces and four-space indentation. Build only the explicitly
selected and reviewed import; a source inventory is not compilation approval.

## Test correctness before comparing strength

| Evidence | What it establishes | What it does not establish |
| --- | --- | --- |
| Debug/Release build and registration check | APIs, resources and names integrate. | Prompt correctness or strength. |
| Focused C# fixture under ignored `Artifacts/` | A specified decision/prompt/reset produces the expected result. | Full Lua resolution or engine legality. |
| Controlled native duel with matching scripts/CDB | Actual offers and prompt sequence complete for that scenario. | General coverage or a reproducible core RNG seed. |
| Frozen Arena comparison | Observed results against recorded opponents and settings. | Correctness of an individual combo or a universal win rate. |

For a callback regression, instantiate the executor and actual client-state types
with explicit inputs. Exercise the accepted action and its prompt through the
dispatcher when practical; testing a private helper alone can miss selection
queues or fallback behavior. Include the interrupted/canceled path and the reset
before a second duel, turn or chain. Use real `ClientCard` candidates from the
fixture, and an explicitly configured database for metadata-dependent checks.

A useful combo fixture specifies the opening hand or board, offered actions,
the expected intermediate result, the final resource/board goal, and one result
that must not happen. Include interruption at a relevant step. Synthetic state
and offered candidates are test inputs, not evidence that a native engine would
offer the same actions. Do not add hidden opponent identities to the bot's view.

[Arena](../../Arena/README.md) currently schedules matches and collects rankings.
Its UI does not inject arbitrary hands or board states, and it does not assert
combo milestones. Use focused fixtures for those checks, then use Arena to assess
the working executor across opponents and both play orders. The existing
[native smoke tool](../../SKYNET/Tools/Skynet/native_smoke.py) can run explicitly
selected executable/deck inputs in isolated processes, using no-shuffle fixtures;
it is a completion check, not an arbitrary-board scenario editor. Its policy seed
is not a duel/core seed. The host's RNG seed remains unknown.

Preserve the previous binary and its decks/dialogs as a separate baseline before
building a candidate. Record source revision, executable hash, deck hashes,
CDB/script version, host/protocol settings, seat, timeouts, completion and disconnect
counts, and the server-reported result. Run a small completion pilot before a
larger frozen comparison. Investigate stalled prompts and bad selections before
tuning priorities from win rates. Arena's best-of-three match totals are not
individual-duel totals. A successful process exit is not a win.

Report exactly what ran, which fixtures passed, and which behaviors remain
unverified. Do not promote a checkpoint or claim a stronger executor from a
build or protocol smoke test alone.

## Retiring the old desktop guides

The former `GPT Guide` handbook and prompts are superseded for current API
guidance. Their deck notes, source variants and three RDA replay files remain
historical evidence. [The inventory](legacy-guide-inventory.json) records every
file and hash. A verified local archive preserves them before any desktop cleanup.

This guide replaces obsolete hidden-deck scans, candidate-context callback
fallbacks, hard-coded "golden" rankings and mandatory large audit scaffolds.
It retains the useful ideas: complete behavior reuse, prompt/script evidence,
observed combo milestones, lifecycle hygiene and binary/test provenance. Keep the
small [executor skill](../Skills/windbot-executor-builder/SKILL.md) as an entry point
to this guide. Its maintained source, metadata and lexical helper script live in
`WindBot/Skills/windbot-executor-builder/`; install that folder as
`%USERPROFILE%/.codex/skills/windbot-executor-builder` after backing up an existing
installation. Unrelated MDPro3 skills
still serve different subsystems and are not replaced by it.
