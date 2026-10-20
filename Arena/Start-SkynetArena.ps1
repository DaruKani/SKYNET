param(
    [ValidateRange(1, 65535)]
    [int]$Port = 3000,
    [string]$Database = '',
    [switch]$Build
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Install Node.js 24 LTS, then run npm ci in the Arena directory.'
}
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'node_modules') -PathType Container)) {
    throw 'Dependencies are missing. Run npm ci in the Arena directory first.'
}
Push-Location -LiteralPath $PSScriptRoot
$previousPort = $env:SKYNET_ARENA_PORT
$previousDatabase = $env:SKYNET_ARENA_DATABASE
try {
    if ($Build -or -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'dist/index.html'))) {
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Skynet Arena frontend build failed.' }
    }
    $env:SKYNET_ARENA_PORT = [string]$Port
    if ($Database) { $env:SKYNET_ARENA_DATABASE = $Database }
    Write-Host "Skynet Arena - open http://127.0.0.1:$Port"
    Write-Host 'Keep this console open. Press Ctrl+C to stop Arena and its owned duel processes.'
    & node server/index.js
    if ($LASTEXITCODE -ne 0) { throw "Skynet Arena exited with code $LASTEXITCODE." }
}
finally {
    $env:SKYNET_ARENA_PORT = $previousPort
    $env:SKYNET_ARENA_DATABASE = $previousDatabase
    Pop-Location
}
