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
$serverTool = Join-Path $PSScriptRoot 'Tools\arena_server.py'
$serverRuntime = Join-Path $PSScriptRoot 'Artifacts\arena-server-v2'
$portablePython = Join-Path $PSScriptRoot 'Artifacts\tools\python313\python.exe'
$duelPort = 17911
$managementPort = 17922
$serverUsername = 'skynet-local'

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

function Get-DedicatedServerPassword {
    $credentialsPath = Join-Path $serverRuntime 'config\admin_user.json'
    if (-not (Test-Path -LiteralPath $credentialsPath -PathType Leaf)) {
        throw "The prepared duel-server credential is missing: $credentialsPath"
    }
    $credentials = Get-Content -LiteralPath $credentialsPath -Raw | ConvertFrom-Json
    $password = $credentials.users.$serverUsername.password
    if ([string]::IsNullOrWhiteSpace($password)) {
        throw "The prepared duel-server credential for '$serverUsername' is invalid."
    }
    return $password
}

function Test-DedicatedServerReady {
    param([string]$Password)

    try {
        $encodedUsername = [Uri]::EscapeDataString($serverUsername)
        $encodedPassword = [Uri]::EscapeDataString($Password)
        $response = Invoke-RestMethod "http://127.0.0.1:$managementPort/api/getroomscount?username=$encodedUsername&pass=$encodedPassword" -TimeoutSec 2
        if ($null -eq $response.count -or [string]::IsNullOrWhiteSpace([string]$response.serverInstanceId)) {
            return $false
        }
        return [long]$response.count -ge 0
    }
    catch {
        return $false
    }
}

function Start-DedicatedArenaServer {
    $manifest = Join-Path $serverRuntime 'skynet-server.json'
    if (-not (Test-Path -LiteralPath $serverTool -PathType Leaf)) {
        throw "The dedicated-server supervisor is missing: $serverTool"
    }
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
        throw "The prepared loopback duel server is missing: $serverRuntime"
    }
    if (-not (Test-Path -LiteralPath $portablePython -PathType Leaf)) {
        throw "The prepared portable Python runtime is missing: $portablePython"
    }

    $password = Get-DedicatedServerPassword
    if (Test-DedicatedServerReady $password) {
        Write-Host "Using the dedicated duel server already running on ports $duelPort/$managementPort"
        return $password
    }
    if (Test-LocalTcpPort $managementPort) {
        throw "Port $managementPort is occupied, but the expected authenticated SRVPro management API is unavailable. The launcher will not stop an unrelated process."
    }
    if (Test-LocalTcpPort $duelPort) {
        throw "Port $duelPort is occupied, so the prepared SRVPro cannot start. The launcher will not stop an unrelated process."
    }

    $lease = Join-Path $serverRuntime 'supervisor.lock'
    if (Test-Path -LiteralPath $lease -PathType Leaf) {
        $ownerId = 0
        [void][int]::TryParse((Get-Content -LiteralPath $lease -Raw).Trim(), [ref]$ownerId)
        if ($ownerId -gt 0 -and (Get-Process -Id $ownerId -ErrorAction SilentlyContinue)) {
            throw "The dedicated-server supervisor process $ownerId exists but its authenticated API is unavailable. Close that supervisor and retry."
        }
        Remove-Item -LiteralPath $lease -Force
    }
    $stopRequest = Join-Path $serverRuntime 'stop.request'
    if (Test-Path -LiteralPath $stopRequest -PathType Leaf) {
        Remove-Item -LiteralPath $stopRequest -Force
    }

    $nodePath = (Get-Command node -ErrorAction Stop).Source
    Write-Host "Starting the prepared loopback duel server on ports $duelPort/$managementPort ..."
    Start-Process -FilePath $portablePython -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -ArgumentList @(
        ('"' + $serverTool + '"'), 'serve',
        '--runtime-dir', ('"' + $serverRuntime + '"'),
        '--node', ('"' + $nodePath + '"'),
        '--minutes', '720'
    )
    for ($attempt = 0; $attempt -lt 120 -and -not (Test-DedicatedServerReady $password); $attempt++) {
        Start-Sleep -Milliseconds 500
    }
    if (-not (Test-DedicatedServerReady $password)) {
        throw "The dedicated duel server did not expose its authenticated management API on 127.0.0.1:$managementPort within 60 seconds."
    }
    Write-Host 'Dedicated SRVPro management API is ready.'
    return $password
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

function Set-LocalArenaSettings {
    param(
        [string]$OutputDirectory,
        [string]$ServerPassword
    )

    $public = Invoke-RestMethod "$arenaUrl/api/settings" -TimeoutSec 5
    $settings = $public.settings
    $settings.srvpros = @([pscustomobject]@{
        duelPort = $duelPort
        host = '127.0.0.1'
        id = 'srvpro-1'
        maxRooms = 2
        name = 'Local Duel Log Arena'
        password = $ServerPassword
        roomsPerSecond = 1
        statusPort = $managementPort
        username = $serverUsername
    })

    $settings.windbots.current.mode = 'local'
    $settings.windbots.current.port = 2399
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
    Write-Host "Arena now uses the prepared local SRVPro; structured duel traces are enabled for $enabled Local WindBot agent(s)."
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

$serverPassword = Start-DedicatedArenaServer
Start-ArenaIfNeeded
Set-LocalArenaSettings $resolvedTraceDirectory $serverPassword
Write-Host "Duel logs will be written below: $resolvedTraceDirectory"
Write-Host 'Each Arena run is separated by experiment, room and participant.'
Start-Process $arenaUrl
