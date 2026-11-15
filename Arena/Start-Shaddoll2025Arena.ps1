param(
    [ValidateRange(1, 10000)]
    [int]$GamesPerOpponent = 1,
    [string[]]$OpponentDeck = @('Lightsworn'),
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
$serverTool = Join-Path $PSScriptRoot 'Tools\arena_server.py'
$serverRuntime = Join-Path $PSScriptRoot 'Artifacts\arena-server-v2'
$portablePython = Join-Path $PSScriptRoot 'Artifacts\tools\python313\python.exe'
$arenaUrl = "http://127.0.0.1:$Port"
$duelPort = 17911
$managementPort = 17922

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

function Test-LocalTcpPort {
    param([int]$TcpPort)
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $pending = $client.BeginConnect('127.0.0.1', $TcpPort, $null, $null)
        if (-not $pending.AsyncWaitHandle.WaitOne(500)) {
            return $false
        }
        $client.EndConnect($pending)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Start-DedicatedArenaServer {
    if (Test-LocalTcpPort $managementPort) {
        return
    }
    $manifest = Join-Path $serverRuntime 'skynet-server.json'
    $credentials = Join-Path $serverRuntime 'config\admin_user.json'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf) -or
        -not (Test-Path -LiteralPath $credentials -PathType Leaf)) {
        throw "The prepared loopback duel server is missing: $serverRuntime"
    }
    if (-not (Test-Path -LiteralPath $portablePython -PathType Leaf)) {
        throw "The prepared portable Python runtime is missing: $portablePython"
    }

    $lease = Join-Path $serverRuntime 'supervisor.lock'
    if (Test-Path -LiteralPath $lease -PathType Leaf) {
        $ownerId = 0
        [void][int]::TryParse((Get-Content -LiteralPath $lease -Raw).Trim(), [ref]$ownerId)
        if ($ownerId -gt 0 -and (Get-Process -Id $ownerId -ErrorAction SilentlyContinue)) {
            throw "The dedicated-server supervisor process $ownerId exists but port $managementPort is unavailable. Close that console and retry."
        }
        Remove-Item -LiteralPath $lease -Force
    }
    $stopRequest = Join-Path $serverRuntime 'stop.request'
    if (Test-Path -LiteralPath $stopRequest -PathType Leaf) {
        Remove-Item -LiteralPath $stopRequest -Force
    }

    $nodePath = (Get-Command node -ErrorAction Stop).Source
    Write-Host "Starting the loopback duel server on ports $duelPort/$managementPort ..."
    Start-Process -FilePath $portablePython -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -ArgumentList @(
        ('"' + $serverTool + '"'), 'serve',
        '--runtime-dir', ('"' + $serverRuntime + '"'),
        '--node', ('"' + $nodePath + '"'),
        '--minutes', '720'
    )
    for ($attempt = 0; $attempt -lt 120 -and -not (Test-LocalTcpPort $managementPort); $attempt++) {
        Start-Sleep -Milliseconds 500
    }
    if (-not (Test-LocalTcpPort $managementPort)) {
        throw "The dedicated duel server did not become ready on 127.0.0.1:$managementPort within 60 seconds."
    }
}

function Set-LocalArenaSettings {
    $credentialsPath = Join-Path $serverRuntime 'config\admin_user.json'
    $credentials = Get-Content -LiteralPath $credentialsPath -Raw | ConvertFrom-Json
    $password = $credentials.users.'skynet-local'.password
    if ([string]::IsNullOrWhiteSpace($password)) {
        throw 'The prepared duel-server credential is invalid.'
    }

    $public = Invoke-RestMethod "$arenaUrl/api/settings" -TimeoutSec 5
    $settings = $public.settings
    $settings.srvpros = @([pscustomobject]@{
        duelPort = $duelPort
        host = '127.0.0.1'
        id = 'srvpro-1'
        maxRooms = 2
        name = 'Local Shaddoll Arena'
        password = $password
        roomsPerSecond = 1
        statusPort = $managementPort
        username = 'skynet-local'
    })
    $settings.windbots.current.mode = 'local'
    $settings.windbots.current.port = 2399
    $settings.windbots.current.runtimeDir = $runtimeDirectory
    $settings.windbots.current.botConfPath = Join-Path $windBotRoot 'BotWrapper\bot.conf'
    $body = $settings | ConvertTo-Json -Depth 20
    [void](Invoke-RestMethod "$arenaUrl/api/settings" -Method Put -ContentType 'application/json' -Body $body -TimeoutSec 15)
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
            '--configuration', 'Release',
            '--database', $databaseSource,
            '--deck', (Join-Path $windBotRoot 'Decks\AI_Shaddoll2025.ydk'),
            '--registration', 'Shaddoll2025',
            '--output', (Join-Path $receiptRoot 'runtime')
        ) 'Shaddoll runtime check failed'
        Invoke-CheckedCommand python @(
            $botTool, 'check', '--suite', 'shaddoll-2025',
            '--configuration', 'Release',
            '--database', $databaseSource,
            '--deck', (Join-Path $windBotRoot 'Decks\AI_Shaddoll2025.ydk'),
            '--output', (Join-Path $receiptRoot 'focused')
        ) 'Shaddoll focused checks failed'
    }
}
finally {
    Pop-Location
}

Start-DedicatedArenaServer

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

Write-Host 'Applying the prepared local duel-server and WindBot settings...'
Set-LocalArenaSettings

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
