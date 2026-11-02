---
name: windbot-executor-builder
description: Implement or audit normal WindBot/YGOSharp deck executors, selection callbacks and per-duel AI state. Use for Game/AI/Decks work and executor imports; SKYNET policy/training and MDPro3 presentation have separate owners.
---

# WindBot executor work

Locate the user-selected bot project and read its contributor instructions. In
the MDPro4 AI workspace the normal bot is under `WindBot/`; it is separate from
`SKYNET/`. Read `WindBot/Guides/ExecutorAuthoring.md` for current API contracts,
complete behavior imports and the fixture/Arena validation workflow. In another
checkout, verify the equivalent APIs directly rather than searching sibling repos.

For a new executor in this workspace, start with the maintained
`WindBot/Templates/RdaPlannerExecutor.cs.template` and the live
`TheCrimsonKingExecutor` reference. Preserve their observed-state/model/action
separation, then implement the new deck's semantics and prompts. The scaffold
does not provide card coverage. Keep predicted model state separate from actual
duel state, and bound queueing plus all search phases with one deadline and
cancellation. Existing executors need not be rewritten to adopt a narrow fix.

Start with the requested decision and following prompts. Search numeric card IDs
in `DefaultExecutor` and relevant maintained executors, then verify the matching
Lua script. Copy the full behavior and reset lifecycle; a source's popularity or
recent timestamp is not correctness evidence. Record exact import revisions.

Candidate `Card`/`ActivateDescription` fields are not callback context. Distinguish
activation costs/targets (`GetCurrentChainCard`) from resolution (immutable
`GetCurrentSolvingChainInfo` snapshots). Mandatory triggers can bypass activation
handlers. Avoid duplicate preselection and callback ownership. Return offered
cards/options and legal place masks; own-deck identities come from `HasInDeck` or
`GetCardCountInDeck`, never hidden `Bot.Deck` slots.

For broad reuse discovery, the optional
[`find_reusable_helpers.py`](scripts/find_reusable_helpers.py) accepts the actual
bot directory via `--repo-root`. It is a lexical inventory, not a quality ranking.
Do not run it or read every executor for a narrow fix.

Validate the changed decision, its interruption and reset. Distinguish focused
synthetic fixtures, native duel completion and frozen Arena strength comparisons.
Record the executable/deck/script inputs actually tested and any skipped cases.
Do not infer that Arena injects combo hands or that a policy seed seeds the core.
Keep implementation and reporting proportional to the requested change.
