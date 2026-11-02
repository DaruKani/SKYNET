[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BotExecutable,
    [Parameter(Mandatory = $true)][string]$Database,
    [Parameter(Mandatory = $true)][string]$Deck,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$Compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    [ValidateRange(30, 300)][int]$TimeoutSeconds = 90
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$botPath = (Resolve-Path -LiteralPath $BotExecutable).Path
$databasePath = (Resolve-Path -LiteralPath $Database).Path
$deckPath = (Resolve-Path -LiteralPath $Deck).Path
$compilerPath = (Resolve-Path -LiteralPath $Compiler).Path
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputPath) { throw 'Use a fresh output directory; receipts are never overwritten.' }
New-Item -ItemType Directory -Path $outputPath | Out-Null
$botDirectory = Split-Path -Parent $botPath
Copy-Item -LiteralPath $botPath -Destination (Join-Path $outputPath 'WindBot.exe')
foreach ($architecture in @('x64', 'x86')) {
    $nativePath = Join-Path $botDirectory ($architecture + '\sqlite3.dll')
    New-Item -ItemType Directory -Path (Join-Path $outputPath $architecture) | Out-Null
    Copy-Item -LiteralPath $nativePath -Destination (Join-Path $outputPath ($architecture + '\sqlite3.dll'))
}
New-Item -ItemType Directory -Path (Join-Path $outputPath 'Dialogs') | Out-Null
Copy-Item -LiteralPath (Join-Path $botDirectory 'Dialogs/default.json') -Destination (Join-Path $outputPath 'Dialogs/default.json')
$harness = Join-Path $outputPath 'VoicelessChecks.exe'
& $compilerPath /nologo /target:exe /platform:x64 /r:System.Core.dll /r:System.Web.Extensions.dll "/r:$botPath" "/out:$harness" (Join-Path $PSScriptRoot 'VoicelessChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Voiceless fixture compilation failed.' }
$report = Join-Path $outputPath 'report.json'
$arguments = @($databasePath, $deckPath, $report) | ForEach-Object { '"' + $_ + '"' }
$process = Start-Process -FilePath $harness -ArgumentList $arguments -WorkingDirectory $outputPath -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $outputPath 'stdout.txt') -RedirectStandardError (Join-Path $outputPath 'stderr.txt')
try {
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill()
        $process.WaitForExit()
        throw "Owned Voiceless fixture exceeded $TimeoutSeconds seconds."
    }
    $process.WaitForExit()
    Get-Content -LiteralPath (Join-Path $outputPath 'stdout.txt')
    $errors = Get-Content -LiteralPath (Join-Path $outputPath 'stderr.txt') -Raw
    if ($errors) { Write-Output $errors }
    if ($process.ExitCode -ne 0) { throw "Voiceless checks failed (exit $($process.ExitCode)); inspect $outputPath" }
    Write-Output "Receipt: $report"
}
finally { $process.Dispose() }
