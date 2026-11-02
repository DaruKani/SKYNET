# WindBot

WindBot is a C# duel bot for [YGOPro](https://github.com/Fluorohydride/ygopro). It is compatible with [YGOSharp](https://github.com/IceYGO/ygosharp) and [SRVPro](https://github.com/mycard/srvpro) servers.

## This workspace

`WindBot/` is the normal deck-executor repository restored from
[`ElderLich/windbot-elder`](https://github.com/ElderLich/windbot-elder) at
`0aa00265d7b378f7e0aee59dedb298d4e356b9b3` (clean source checkout). The C# sources,
solution/project names, executors, decks, `WindBot.exe`, and `Bot.exe` retain their
original names and behavior. The separate training AI and Arena are described in
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
