param(
    [ValidateRange(1, 10000)]
    [int]$GamesPerOpponent = 1,
    [string[]]$OpponentDeck = @(),
    [ValidateRange(1, 65535)]
    [int]$Port = 3000,
    [switch]$SkipBuild,
    [switch]$SkipChecks,
    [switch]$PreflightOnly
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$windBotRoot = Join-Path $workspaceRoot 'WindBot'
$runtimeDirectory = Join-Path $windBotRoot 'bin\Release'
$databaseSource = Join-Path $workspaceRoot 'DeckWorkshop\Data\standard\cards.cdb'
$databaseDestination = Join-Path $runtimeDirectory 'cards.cdb'
$botTool = Join-Path $windBotRoot 'Tools\bot_tool.py'
$challengeTool = Join-Path $PSScriptRoot 'Tools\run_shaddoll2025_challenge.js'
$arenaUrl = "http://127.0.0.1:$Port"

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

function Test-ArenaReady {
    try {
        $system = Invoke-RestMethod "$arenaUrl/api/system" -TimeoutSec 2
        return $system.endpoints.web -eq $arenaUrl
    }
    catch {
        return $false
    }
}

if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    throw 'Python 3 is required to build and check WindBot.'
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js is required to start Arena.'
}
if (-not (Test-Path -LiteralPath $databaseSource -PathType Leaf)) {
    throw "The standard card database is missing: $databaseSource"
}

Push-Location -LiteralPath $workspaceRoot
try {
    if (-not $SkipBuild) {
        Write-Host 'Building WindBot Release...'
        Invoke-CheckedCommand python @($botTool, 'build', '--configuration', 'Release') 'WindBot Release build failed'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $runtimeDirectory 'WindBot.exe') -PathType Leaf)) {
        throw "WindBot Release runtime is missing: $runtimeDirectory"
    }
    Copy-Item -LiteralPath $databaseSource -Destination $databaseDestination -Force

    if (-not $SkipChecks) {
        $receiptRoot = Join-Path $workspaceRoot ('Artifacts\shaddoll-2025-launcher-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
        Write-Host "Writing Shaddoll receipts under $receiptRoot"
        Invoke-CheckedCommand python @(
            $botTool, 'check', '--suite', 'runtime',
            '--database', $databaseSource,
            '--deck', (Join-Path $windBotRoot 'Decks\AI_Shaddoll2025.ydk'),
            '--registration', 'Shaddoll2025',
            '--output', (Join-Path $receiptRoot 'runtime')
        ) 'Shaddoll runtime check failed'
        Invoke-CheckedCommand python @(
            $botTool, 'check', '--suite', 'shaddoll-2025',
            '--database', $databaseSource,
            '--deck', (Join-Path $windBotRoot 'Decks\AI_Shaddoll2025.ydk'),
            '--output', (Join-Path $receiptRoot 'focused')
        ) 'Shaddoll focused checks failed'
    }
}
finally {
    Pop-Location
}

if (-not (Test-ArenaReady)) {
    Write-Host "Starting Arena in a separate console at $arenaUrl ..."
    $arenaLauncher = Join-Path $PSScriptRoot 'Start-SkynetArena.ps1'
    $launcherArguments = @(
        '-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-File', ('"' + $arenaLauncher + '"'),
        '-Port', [string]$Port,
        '-InstallDependencies', '-Build', '-OpenBrowser'
    )
    Start-Process -FilePath 'powershell.exe' -ArgumentList $launcherArguments -WindowStyle Normal
    for ($attempt = 0; $attempt -lt 120 -and -not (Test-ArenaReady); $attempt++) {
        Start-Sleep -Milliseconds 500
    }
    if (-not (Test-ArenaReady)) {
        throw "Arena did not become ready within 60 seconds: $arenaUrl"
    }
}

$challengeArguments = @(
    $challengeTool,
    '--url', $arenaUrl,
    '--games', [string]$GamesPerOpponent
)
foreach ($deck in $OpponentDeck) {
    $challengeArguments += @('--opponent', $deck)
}
if ($PreflightOnly) {
    $challengeArguments += '--preflight-only'
}

Write-Host 'Checking Arena and preparing the Shaddoll2025 Normal challenge...'
& node @challengeArguments
if ($LASTEXITCODE -eq 2) {
    Write-Warning 'Arena is open, but its dedicated SRVPro settings are incomplete. Configure Settings -> SRVPro, save, then run this command again.'
    exit 2
}
if ($LASTEXITCODE -ne 0) {
    throw "Shaddoll2025 Arena challenge failed (exit code $LASTEXITCODE)"
}
