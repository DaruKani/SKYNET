# Normal WindBot contributor instructions

This folder preserves the normal WindBot executor architecture. Its solution is
`WindBot.sln`, its main output is `WindBot.exe`, and `BotWrapper` builds `Bot.exe`.
The enclosing workspace's SKYNET-specific policy and training constraints apply
to the separate training project, not to replacing this executor registry.

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
- Dialogs and `BotWrapper/bot.conf` are supplied resource snapshots; preserve
  their provenance and intentional names. Never overwrite the external sources.
- Use the existing README for documentation. Preserve license and attribution.
