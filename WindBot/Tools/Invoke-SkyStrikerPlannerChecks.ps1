[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BotExecutable,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$CardDatabase,
    [string]$Compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$botPath = (Resolve-Path -LiteralPath $BotExecutable).Path
$compilerPath = (Resolve-Path -LiteralPath $Compiler).Path
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputPath) {
    throw 'Use a new output directory; existing receipts are never overwritten.'
}
New-Item -ItemType Directory -Path $outputPath | Out-Null
$harness = Join-Path $outputPath 'SkyStrikerPlannerChecks.exe'
& $compilerPath /nologo /target:exe /platform:x64 /r:System.Core.dll `
    /r:System.Web.Extensions.dll "/out:$harness" `
    (Join-Path $PSScriptRoot 'SkyStrikerPlannerChecks.cs')
if ($LASTEXITCODE -ne 0) {
    throw 'Sky Striker planner harness compilation failed.'
}
$report = Join-Path $outputPath 'report.json'
if ($CardDatabase) {
    $databasePath = (Resolve-Path -LiteralPath $CardDatabase).Path
    $nativeDirectory = Join-Path $outputPath 'x64'
    New-Item -ItemType Directory -Path $nativeDirectory | Out-Null
    Copy-Item -LiteralPath (Join-Path (Split-Path $botPath) 'x64/sqlite3.dll') `
        -Destination (Join-Path $nativeDirectory 'sqlite3.dll')
    & $harness $botPath $report $databasePath
} else {
    & $harness $botPath $report
}
if ($LASTEXITCODE -ne 0) {
    throw "Sky Striker planner checks failed (exit $LASTEXITCODE); inspect $outputPath"
}
Write-Output "Receipt: $report"
