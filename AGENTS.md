# SKYNET contributor instructions

## Product and boundaries

SKYNET is an experimental deck-independent MDPro4 duel AI. Build `Skynet.sln`
with Visual Studio/MSBuild for .NET Framework 4.8; the output is `SKYNET.exe`.
The project is a classic non-SDK C# project with Any CPU and x86 configurations.
Use C# 6-compatible syntax for the supported legacy Mono toolchain; do not add
local functions, tuple types, inline out declarations, pattern matching or default
literals without validating that toolchain.

`Game/AI/Strategy` owns the policy, shared card semantics and learning session.
`Game/SkynetDecisionAdapter.cs` constructs legal offers and commits the selected
response. `GameBehavior` owns protocol parsing and client state; shared callbacks
and battle helpers remain in `Game/AI`. SKYNET constructs `SkynetAgent` directly:
there is no deck-executor registry or required class per deck. Do not restore the
archived specialized collection, BotWrapper or HTTP launcher.

The transport and general helpers derive from WindBot/YGOSharp. Keep the original
license and attribution. The ignored `LegacyWindBot` directory preserves the
previous local workspace; it is not source for this build or publication.

## Decision correctness

- The server owns legality. Compare offered actions; never fabricate a card,
  response index or target. Respect min/max and cancelability on selections.
- Opponent hidden identities are unavailable. Id=0/Data=null are valid. Never
  infer a real hidden card from a slot, inspect an engine's private state, or
  include hidden opponent identities in features or logs.
- Query own-deck contents through HasInDeck/GetCardCountInDeck, never the IDs in
  Bot.Deck's hidden placeholder slots. Counts alone may be read directly.
- Scoring must be pure. No pending selections, usage flags or consumed resources
  on rejected candidates. Only the accepted action is committed.
- Unknown optional effects are not safe training candidates. Mandatory effects
  must remain selectable even when unsupported or strategically undesirable.
- Card knowledge must be checked against the matching Lua script. StringId
  offsets are prompt-string offsets, not printed effect paragraph numbers.
- OnSelectCard uses GetCurrentChainCard during activation and
  GetCurrentSolvingChainInfo during resolution. Prefer immutable activation
  snapshots over live card identities; never use current executor candidate
  fields as callback context. Mandatory triggers can bypass activation callbacks.
- Avoid duplicate preselection queues when a callback already handles a prompt.
- Keep per-duel state on instances and reset it at the proper turn/chain/start
  boundary. No shared mutable agent state.

## Learning and evidence

Training is opt-in. Frozen inference never writes episodes/checkpoints. Only a
server-reported completed outcome supplies a reward; disconnections are not
losses. Preserve draw=2 when converting player perspective. Publish unique
completed episode files atomically. Validate schema, bounded finite features,
model identity and stable deck/encounter holdout cohorts. Candidate training and
fresh frozen evaluation are separate; never automatically promote a checkpoint
or claim strength from protocol smoke tests.

`Tools/Skynet/native_smoke.py` runs bounded native fixtures with owned processes.
Never kill unrelated processes or overwrite user game settings. Core RNG seed
is currently unknown; a policy seed must not be mislabeled as a duel seed.

## Validation and collaboration

For protocol, common state or selection changes, build Debug Any CPU. Release is
also checked in CI. Run the focused C# and Python harnesses as relevant:

```powershell
msbuild Skynet.sln /p:Configuration=Debug /p:Platform="Any CPU"
msbuild Tests/Skynet.Tests.csproj /p:Configuration=Debug
.\Tests\bin\Debug\Skynet.Tests.exe <cards.cdb>
python Tools/Skynet/test_learning.py
```

Without a database, five policy fixtures are explicitly skipped. Builds,
regressions, real duels, Unity lifetime checks and visual acceptance are separate
evidence; report what actually ran. Normal Unity compilation is not a player or
bundle build. Follow the companion MDPro4 instructions when editing that repo.

Use four-space indentation, Allman braces and UTF-8/CRLF for C#. Avoid unrelated
formatting. New core source requires explicit project inclusion. Preserve user
changes and use explicit staged paths. The owner requests a commit with a clear
title and descriptive body for each completed update and pushes to the private
SKYNET repository; companion game commits remain local unless separately asked.
Update README with capabilities and actual limits. Do not add standalone design
documents unless requested. Read AGENTS.local.md/AGENTS.user.md when present.
