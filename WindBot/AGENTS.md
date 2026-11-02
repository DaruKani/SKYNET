# Normal WindBot contributor instructions

This folder preserves the normal WindBot executor architecture. Its solution is
`WindBot.sln`, its main output is `WindBot.exe`, and `BotWrapper` builds `Bot.exe`.
The enclosing workspace's SKYNET-specific policy and training constraints apply
to the separate training project, not to replacing this executor registry.

For new executors, use the RDA-first structure in
`Templates/RdaPlannerExecutor.cs.template` and the current `TheCrimsonKingExecutor`
as the reference. Read `Guides/ExecutorAuthoring.md` for current API contracts and
the combo-fixture/Arena workflow. Implement each deck's card model and prompt
handling explicitly; the template itself provides no card coverage. Existing
executors remain supported and do not need wholesale rewrites for narrow fixes.

- Put deck logic in `Game/AI/Decks`; preserve `[Deck]` names and `AddExecutor`
  priority. Keep the original selection/callback lifecycle together.
- Read only information provided to this client. Unknown opponent cards are
  valid. Query remaining own-deck cards with `HasInDeck`/`GetCardCountInDeck`.
- Use the server's offered cards and response indices. Respect selection bounds,
  cancellation, chain activation/resolution context, and per-duel reset events.
- Keep protocol parsing and shared client state in their existing owners.
- Follow the enclosing workspace's C# 6, four-space/Allman, and UTF-8/CRLF rules.
- New core source requires explicit project inclusion. Deck executors, enum
  files, deck resources, and dialogs already use project wildcards.
- Build Debug and Release Any CPU using Visual Studio/MSBuild. Runtime checks
  require an explicitly selected card database and a compatible duel host.
- Use `Tools/Invoke-RdaPlannerChecks.ps1` for RDA scheduling and model regressions.
  Preserve a baseline runtime before changing planner policy; compare actual
  script-backed duels separately from model replay and Arena match rankings.
- Dialogs and `BotWrapper/bot.conf` are supplied resource snapshots; preserve
  their provenance and intentional names. Never overwrite the external sources.
- Use the existing README for documentation. Preserve license and attribution.

## Architecture and ownership

WindBot is an automated YGOPro client using the YGOPro/YGOSharp/SRVPro protocol.
It is a classic, non-SDK .NET Framework 4.8 solution with Any CPU and x86 targets.
`WindBot.csproj` builds the client as `WindBot.exe`;
`BotWrapper/BotWrapper.csproj` builds the external game launcher as `Bot.exe`.
Use Visual Studio/MSBuild rather than assuming modern SDK tools or APIs.

The normal runtime path is:

1. `Program.cs` reads arguments/configuration, initializes `cards.cdb` and the
   deck registry, then starts a single client or the HTTP server.
2. `Game/GameClient.cs` connects, joins the room and forwards packets.
3. `Game/GameBehavior.cs` parses messages, updates `Duel`, `ClientField` and
   `ClientCard`, asks `GameAI` for decisions, and validates/encodes responses.
4. `Game/GameAI.cs` dispatches legal offers to the executor in priority order.
5. `Game/AI/DecksManager.cs` discovers unique `[Deck]` registrations and constructs
   a separate executor for each duel.
6. `Game/AI/Executor.cs` defines callbacks and the ordered `CardExecutor` list;
   deck implementations normally inherit `DefaultExecutor`.

HTTP server requests have independent clients, duel state and executors. Keep
mutable duel state on instances. Shared search infrastructure must be bounded
and synchronized; it must never contain another duel's mutable card state.

- `Game/`: protocol-driven duel state and main/battle phase action models.
- `Game/AI/`: shared decisions, selection helpers and card extensions.
- `Game/AI/Decks/`: deck-specific routes, priorities and temporary state.
- `Game/AI/Enums/`: genuinely shared card classifications.
- `YGOSharp.Network/`: framed TCP messages and CTOS/STOC transport.
- `YGOSharp.OCGWrapper/` and `.Enums/`: database models and protocol/rule enums.
- `Decks/`, `Dialogs/` and `BotWrapper/`: runtime decks, dialogs and launcher.

Do not put deck exceptions into protocol parsing. Promote a helper into
`DefaultExecutor`, `AIUtil`, `CardExtension` or shared enums only when it has
real cross-deck use. Keep card definitions separate from dynamic duel properties.

## Ordered decisions and bounded planning

`GameAI` traverses registered handlers first, then offered candidates; the first
matching handler returning `true` wins. Registration order is behavior. Put
specific urgent handlers before general fallbacks. `AddExecutor(type, cardId)`
accepts every matching legal offer, so use it only when that is always suitable.
Phase changes and surrender can have no card context.

The engine owns legality. Decide whether an offered action is worthwhile and
which offered targets to use. Check strategic restrictions such as resource
conflicts, negation and lethal LP costs where needed. Never fabricate actions.
Rejected candidates must not leave selections, usage flags or resource changes.
Accepted activation, paid cost and successful resolution are distinct events.

The client cannot execute future Lua resolution or see the engine's private
state. An RDA-style model is an explicitly implemented prediction. Reconcile it
with authoritative observations after responses and interruptions; never write
predictions into protocol-owned state. Use one deadline across queueing and all
search phases, with cancellation and node/frontier/memory limits. Retire plans
when their duel, turn or relevant observed state changes.

## Context, effects and hidden information

`SetCard` sets `Type`, `Card`, `ActivateDescription` and `CurrentTiming` for the
candidate currently being evaluated. Only the handler and its synchronous helpers
may rely on those fields. Selection and lifecycle callbacks must use their own
arguments, chain snapshots or narrowly maintained prompt state.

- `Bot`/`Enemy` are local `Duel.Fields[0]`/`Fields[1]`; use existing player-number
  conversion rather than assuming the bot occupies a particular server seat.
- Unknown identities (`Id == 0`, `Data == null`, `Name == null`) are normal.
  Never inspect or infer hidden opponent identities from slots or private state.
- `Bot.Deck` contains hidden placeholders. Read its count when appropriate, but
  use `HasInDeck`/`GetCardCountInDeck` for own-deck identities. An inactive tag
  teammate's deck is not a queryable source of card identities.
- Prefer existing field/card/helper APIs over repeated scans and raw bit masks.
  Read current levels, types, positions and negation from observed dynamic state.
- `Util.GetStringId(id, offset)` and Lua `aux.Stringid` encode `id * 16 + offset`.
  Offset zero corresponds to `texts.str1`; it is a prompt-string offset, not an
  effect paragraph number. Check the matching Lua call and database text.
- A description of `-1` means unknown. Distinguish effects by reliable location,
  event and phase context when available; do not silently interpret it as effect
  zero or use `ActivateDescription` to answer an effect's later option prompt.
- Maintain turn, chain, summon-attempt and usage state at the corresponding
  lifecycle hook, including `OnMove` when necessary. Preserve relevant base hooks.
  Do not clear `CurrentChain`, chain targets or other protocol-owned collections
  merely to hide stale callback context.

## Selection ownership

Selection queues are not inherently bound to a unique effect/chain link. Their
order must match the actual script's cost, target, material and resolution
prompts. Use the scripts loaded by the configured host, not an unpinned online
copy or printed text alone.

For predictable prompts, queue `SelectCard` before `SelectNextCard`/`SelectThirdCard`
only when accepting the action. For contextual prompts, use the matching callback.
Choose one owner for each prompt; returning a callback selection while leaving a
duplicate preselection queued can contaminate a later effect.

- At activation/cost/target time, identify the latest chain card with
  `GetCurrentChainCard`, and check its controller plus the actual hint/zones.
- During resolution, use `GetCurrentSolvingChainInfo` and its activation snapshot
  (`ActivatePlayer`, `IsActivateCode`, `ActivateLocation`, description). Use
  `RelatedCard` only when a live reference is needed. The newest chain link is
  not necessarily the link currently resolving.
- Mandatory triggers can bypass activation handlers and `OnSelectChain`, notably
  a single forced offer. Their actual selection callbacks must work without
  flags or queues that would have been set by an activation predicate.
- Return distinct objects from the offered list, respecting `min`/`max`. Empty
  cancellation requires `cancelable == true`. If a prompt is not owned by this
  effect, return the appropriate base/null fallback.
- Return an offered option's index from `OnSelectOption`. `OnSelectPlace` returns
  a mask intersecting `available`; queuing a future place is not that response.
- The accepted `SpellSet` candidate is the card being set; selecting a different
  card does not change it. Material choices must also obey the actual offered
  candidates and server constraints.

## Registration, core changes and validation

Keep `[Deck("ExternalName", "AI_FileName", "Level")]` unique and paired with
`Decks/AI_FileName.ydk`. Only `Level == "Normal"` enters the default random pool;
`Easy`, `NotFinished` and `Test` registrations are explicit choices. Update the
roster/dialog when requested, using this workspace's selected resource sources
and provenance; do not overwrite the external MDPro4_Data copies.

Deck, enum, `.ydk` and dialog globs already exist. Other new core source requires
explicit project inclusion. Read registration order, overrides and relevant
neighboring implementations before modifying an executor. Avoid unrelated
reordering or formatting. Use database fields rather than duplicating static
card data, except IDs and necessary strategic classifications.

Protocol changes require evidence for field order, integer width, controller
conversion and response encoding. Check enums, message registration, state
updates and response paths together. Preserve enum bitwise semantics. Review
multiple callers and fallback behavior when shared AI or selection code changes.

Build Debug and Release Any CPU for executor changes. Use `Debug=True` for action
and movement logs, recording the phase, chain, offered candidates and selected
handler. Runtime checks need the selected `cards.cdb`, SQLite libraries, decks
and dialogs. There is no standalone Tests project; do not restore one. Focused
harnesses, native Lua-backed fixtures and Arena comparisons establish different
facts; do not substitute a compilation or process exit for gameplay evidence.

Use C# 6 for new code: no local functions, tuple types, inline out declarations,
pattern matching or default literals. LINQ, lambdas, `?.` and `??` are available.
Use UTF-8/CRLF, four spaces and Allman braces; preserve unrelated existing BOMs.
Keep helpers purposeful. Preserve user changes, use explicit staged paths and
follow the enclosing workspace's commit/push instructions. Update existing docs
instead of adding unsolicited standalone design documents. Read applicable
`AGENTS.local.md` and `AGENTS.user.md` when present.

## Guidance origin

The detailed architecture and callback guidance above is translated and adapted
from the user-selected `D:/Programs/GitHub/windbot/AGENTS.md` (last recorded file
commit `3ac567cad95d0b2d3cedaefa14937e1660e72e33`, imported file SHA-256
`6261d7e4c834ae8bc96a2af3bf20c0140ddad1b2a4e04cad6a1567cfd6c15d27`).
This version retains our RDA planning, separate SKYNET/Arena ownership, explicit
resource imports, validation tools and repository commit/push requirements.
