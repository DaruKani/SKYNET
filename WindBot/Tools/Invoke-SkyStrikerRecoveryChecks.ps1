param(
    [Parameter(Mandatory = $true)][string]$BotExecutable,
    [Parameter(Mandatory = $true)][string]$CardDatabase,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$binary = (Resolve-Path -LiteralPath $BotExecutable).Path
$database = (Resolve-Path -LiteralPath $CardDatabase).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new evidence directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
Copy-Item -LiteralPath (Split-Path $binary) -Destination (Join-Path $output 'runtime') -Recurse
$runtime = Join-Path $output 'runtime'
$exe = Join-Path $runtime 'SkyStrikerRecoveryChecks.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:x64 `
    /r:System.Core.dll /r:System.Web.Extensions.dll "/r:$binary" "/out:$exe" `
    (Join-Path $PSScriptRoot 'SkyStrikerRecoveryChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Recovery harness compilation failed.' }
Push-Location $runtime
try {
    & $exe $database (Join-Path $output 'report.json') | Tee-Object -FilePath (Join-Path $output 'decisions.log')
    $result = $LASTEXITCODE
} finally { Pop-Location }
Get-FileHash -Algorithm SHA256 -LiteralPath $binary, $database | Format-Table -AutoSize
if ($result -ne 0) { throw "Recovery checks failed: $output/report.json" }
