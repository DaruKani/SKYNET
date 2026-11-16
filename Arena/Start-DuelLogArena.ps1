param(
    [ValidateRange(1, 65535)]
    [int]$Port = 3000,
    [string]$TraceDirectory = '',
    [switch]$SkipWindBotBuild,
    [switch]$SkipArenaBuild
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$windBotRoot = Join-Path $workspaceRoot 'WindBot'
$windBotRuntime = Join-Path $windBotRoot 'bin\Release'
$windBotExecutable = Join-Path $windBotRuntime 'WindBot.exe'
$botConfPath = Join-Path $windBotRoot 'BotWrapper\bot.conf'
$botTool = Join-Path $windBotRoot 'Tools\bot_tool.py'
$databaseSource = Join-Path $workspaceRoot 'DeckWorkshop\Data\standard\cards.cdb'
$databaseDestination = Join-Path $windBotRuntime 'cards.cdb'
$arenaLauncher = Join-Path $PSScriptRoot 'Start-SkynetArena.ps1'
$arenaUrl = "http://127.0.0.1:$Port"

function Get-ExplicitTraceDirectory {
    param([string]$RequestedDirectory)

    $candidate = $RequestedDirectory
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $candidate = Join-Path $workspaceRoot 'Artifacts\windbot-duel-traces'
    }
    if (-not [IO.Path]::IsPathRooted($candidate)) {
        throw 'TraceDirectory must be an absolute non-root directory.'
    }
    $fullPath = [IO.Path]::GetFullPath($candidate)
    $root = [IO.Path]::GetPathRoot($fullPath)
    $normalizedPath = $fullPath.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $normalizedRoot = $root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if ([string]::IsNullOrWhiteSpace($root) -or
        [string]::Equals($normalizedPath, $normalizedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'TraceDirectory must be an absolute non-root directory.'
    }
    return $fullPath
}

function Test-ArenaReady {
    try {
        $system = Invoke-RestMethod "$arenaUrl/api/system" -TimeoutSec 2
        return $system.endpoints.web -eq $arenaUrl
    }
    catch {
        return $false
    }
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit code $LASTEXITCODE)"
    }
}

function Start-ArenaIfNeeded {
    if (Test-ArenaReady) {
        Write-Host "Using the Arena instance already running at $arenaUrl"
        return
    }

    $arguments = @(
        '-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-File', ('"' + $arenaLauncher + '"'),
        '-Port', [string]$Port,
        '-InstallDependencies'
    )
    if (-not $SkipArenaBuild) {
        $arguments += '-Build'
    }
    Write-Host "Starting Arena in a separate console at $arenaUrl ..."
    Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -WindowStyle Normal
    for ($attempt = 0; $attempt -lt 360 -and -not (Test-ArenaReady); $attempt++) {
        Start-Sleep -Milliseconds 500
    }
    if (-not (Test-ArenaReady)) {
        throw "Arena did not become ready within 180 seconds: $arenaUrl"
    }
}

function Enable-StructuredTraces {
    param([string]$OutputDirectory)

    $public = Invoke-RestMethod "$arenaUrl/api/settings" -TimeoutSec 5
    $settings = $public.settings
    $enabled = 0
    foreach ($name in @('current', 'old')) {
        $instance = $settings.windbots.$name
        if ($instance.mode -ne 'local') {
            Write-Host "Skipping $name agent because its mode is '$($instance.mode)'."
            continue
        }
        $instance.runtimeDir = $windBotRuntime
        $instance.botConfPath = $botConfPath
        $instance.traceEnabled = $true
        $instance.traceDirectory = $OutputDirectory
        $enabled++
    }
    if ($enabled -eq 0) {
        throw 'No Local WindBot agent is configured. Select Local WindBot under Settings -> Agents and retry.'
    }
    $body = $settings | ConvertTo-Json -Depth 20
    [void](Invoke-RestMethod "$arenaUrl/api/settings" -Method Put -ContentType 'application/json' -Body $body -TimeoutSec 15)
    Write-Host "Structured duel traces enabled for $enabled Local WindBot agent(s)."
}

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Install Node.js 24 LTS, then double-click Duel Log Arena.bat again.'
}
if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    throw 'Python 3 is required to build WindBot.'
}
if (-not (Test-Path -LiteralPath $botTool -PathType Leaf)) {
    throw "WindBot build tool is missing: $botTool"
}
if (-not (Test-Path -LiteralPath $arenaLauncher -PathType Leaf)) {
    throw "Arena launcher is missing: $arenaLauncher"
}

$resolvedTraceDirectory = Get-ExplicitTraceDirectory $TraceDirectory
[void](New-Item -ItemType Directory -Path $resolvedTraceDirectory -Force)

if (-not $SkipWindBotBuild) {
    Write-Host 'Building normal WindBot Release with structured trace support...'
    Invoke-CheckedCommand python @($botTool, 'build', '--configuration', 'Release') 'WindBot Release build failed. Close any running WindBot server and retry'
}
if (-not (Test-Path -LiteralPath $windBotExecutable -PathType Leaf)) {
    throw "WindBot Release runtime is missing: $windBotExecutable"
}
if (-not (Test-Path -LiteralPath $databaseDestination -PathType Leaf)) {
    if (-not (Test-Path -LiteralPath $databaseSource -PathType Leaf)) {
        throw "WindBot cards.cdb is missing and no versioned Standard database is available: $databaseSource"
    }
    Copy-Item -LiteralPath $databaseSource -Destination $databaseDestination
}

Start-ArenaIfNeeded
Enable-StructuredTraces $resolvedTraceDirectory
Write-Host "Duel logs will be written below: $resolvedTraceDirectory"
Write-Host 'Each Arena run is separated by experiment, room and participant.'
Start-Process $arenaUrl
