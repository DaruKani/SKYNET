[CmdletBinding()]
param(
    [string]$Python = 'python',
    [string]$MSBuild = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe',
    [string]$DataRoot = 'D:/Games/MDPro4/Data',
    [string]$Server = 'D:/Games/MDPro4/MDPro4_Data/Plugins/x86_64/ygoserver.dll',
    [string]$OutputDirectory,
    [int]$MirrorGames = 10
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repo ('Artifacts/skystriker-check-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputPath) { throw 'Use a new output directory.' }
& $Python -c 'import sys; sys.exit(0 if sys.version_info >= (3,11) else 1)'
if ($LASTEXITCODE -ne 0) { throw 'Native runner requires Python 3.11 or newer; pass -Python with its executable path.' }
Push-Location $repo
try {
    foreach ($configuration in @('Debug', 'Release')) {
        & $MSBuild 'WindBot/WindBot.sln' "/p:Configuration=$configuration" '/p:Platform=Any CPU' /verbosity:minimal /nologo
        if ($LASTEXITCODE -ne 0) { throw "$configuration build failed." }
    }
    & (Join-Path $PSScriptRoot 'Invoke-SkyStrikerPlannerChecks.ps1') `
        -BotExecutable 'WindBot/bin/Debug/WindBot.exe' `
        -CardDatabase (Join-Path $DataRoot 'locales/en-US/cards.cdb') `
        -OutputDirectory (Join-Path $outputPath 'checks')
    & $Python (Join-Path $PSScriptRoot 'skystriker_native_checks.py') `
        --bot-exe 'WindBot/bin/Debug/WindBot.exe' --data-root $DataRoot --server $Server `
        --deck 'WindBot/Decks/AI_SkyStriker2026.ydk' --output-dir (Join-Path $outputPath 'native') `
        --mirror-games $MirrorGames
    if ($LASTEXITCODE -ne 0) { throw "Native behavior checks failed. Inspect $outputPath" }
    Write-Output "PASS: build, focused checks and selected native regressions. Results: $outputPath"
    Write-Output 'This is not a competitive-strength or all-matchups certification.'
}
finally {
    Pop-Location
}
