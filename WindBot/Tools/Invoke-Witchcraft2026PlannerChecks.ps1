[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BotExecutable,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
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
$harness = Join-Path $outputPath 'Witchcraft2026PlannerChecks.exe'
& $compilerPath /nologo /target:exe /platform:x64 /r:System.Core.dll `
    /r:System.Web.Extensions.dll "/out:$harness" `
    (Join-Path $PSScriptRoot 'Witchcraft2026PlannerChecks.cs')
if ($LASTEXITCODE -ne 0) {
    throw 'Witchcraft 2026 planner harness compilation failed.'
}
$report = Join-Path $outputPath 'report.json'
& $harness $botPath $report
if ($LASTEXITCODE -ne 0) {
    throw "Witchcraft 2026 planner checks failed (exit $LASTEXITCODE); inspect $outputPath"
}
Write-Output "Receipt: $report"
