[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BotExecutable,
    [Parameter(Mandatory = $true)][string]$Database,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$Compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$botPath = (Resolve-Path -LiteralPath $BotExecutable).Path
$databasePath = (Resolve-Path -LiteralPath $Database).Path
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputPath) { throw 'Use a fresh receipt directory.' }
New-Item -ItemType Directory -Path $outputPath | Out-Null
Copy-Item -LiteralPath $botPath -Destination (Join-Path $outputPath 'WindBot.exe')
foreach ($arch in @('x64', 'x86')) {
    Copy-Item -LiteralPath (Join-Path (Split-Path $botPath) $arch) -Destination (Join-Path $outputPath $arch) -Recurse
}
$harness = Join-Path $outputPath 'WitchcraftOpponentChecks.exe'
& $Compiler /nologo /langversion:6 /target:exe /platform:x64 /r:System.Core.dll /r:System.Web.Extensions.dll "/r:$botPath" "/out:$harness" (Join-Path $PSScriptRoot 'WitchcraftOpponentChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Opponent harness compilation failed.' }
@{ bot = (Get-FileHash -LiteralPath $botPath).Hash; database = (Get-FileHash -LiteralPath $databasePath).Hash } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputPath 'inputs.json') -Encoding UTF8
Push-Location (Split-Path -Parent $botPath)
try { & $harness $databasePath (Join-Path $outputPath 'report.json') }
finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw "Opponent checks failed; inspect $outputPath" }
Write-Output "Receipt: $outputPath"
