[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [Parameter(Mandatory = $true)][string]$ServerLibrary,
    [Parameter(Mandatory = $true)][string]$Database,
    [Parameter(Mandatory = $true)][string]$ScriptsArchive,
    [string]$Banlist,
    [string]$Compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-RequiredFile([string]$Filename, [string]$Extension) {
    $resolved = (Resolve-Path -LiteralPath $Filename).Path
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "Expected a file: $resolved"
    }
    if ([System.IO.Path]::GetExtension($resolved) -ine $Extension) {
        throw "Expected a $Extension file: $resolved"
    }
    return $resolved
}

$nativePath = Get-RequiredFile $ServerLibrary '.dll'
$databasePath = Get-RequiredFile $Database '.cdb'
$scriptsPath = Get-RequiredFile $ScriptsArchive '.zip'
$compilerPath = Get-RequiredFile $Compiler '.exe'
$banlistPath = if ($Banlist) { Get-RequiredFile $Banlist '.conf' } else { $null }
$sourcePath = Join-Path $PSScriptRoot 'SkynetDuelHost.cs'
$targetPath = [System.IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $targetPath) {
    throw "Use a new destination; existing staged runtimes are never overwritten: $targetPath"
}

[System.IO.Directory]::CreateDirectory($targetPath) | Out-Null
$dataPath = Join-Path $targetPath 'Data'
[System.IO.Directory]::CreateDirectory($dataPath) | Out-Null
$outputPath = Join-Path $targetPath 'SKYNET_DUEL_HOST.exe'
& $compilerPath /nologo /target:exe /platform:x64 /optimize+ "/out:$outputPath" $sourcePath
if ($LASTEXITCODE -ne 0) {
    throw "Duel host compilation failed with exit code $LASTEXITCODE"
}
Copy-Item -LiteralPath $nativePath -Destination (Join-Path $targetPath 'ygoserver.dll')
Copy-Item -LiteralPath $databasePath -Destination (Join-Path $targetPath 'cards.cdb')
Copy-Item -LiteralPath $scriptsPath -Destination (Join-Path $dataPath 'script.zip')
if ($banlistPath) {
    Copy-Item -LiteralPath $banlistPath -Destination (Join-Path $targetPath 'lflist.conf')
}

$inputs = [ordered]@{
    hostSource = $sourcePath
    compiler = $compilerPath
    nativeLibrary = $nativePath
    database = $databasePath
    scripts = $scriptsPath
}
if ($banlistPath) { $inputs['banlist'] = $banlistPath }
$inputRecords = foreach ($item in $inputs.GetEnumerator()) {
    [ordered]@{
        role = $item.Key
        path = $item.Value
        sha256 = (Get-FileHash -LiteralPath $item.Value -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$payloadPaths = @('SKYNET_DUEL_HOST.exe', 'ygoserver.dll', 'cards.cdb', 'Data/script.zip')
if ($banlistPath) { $payloadPaths += 'lflist.conf' }
$payload = foreach ($relativePath in $payloadPaths) {
    $filename = Join-Path $targetPath $relativePath
    [ordered]@{
        path = $relativePath
        bytes = (Get-Item -LiteralPath $filename).Length
        sha256 = (Get-FileHash -LiteralPath $filename -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$receipt = [ordered]@{
    schema = 'skynet-native-host-v1'
    builtAtUtc = [DateTime]::UtcNow.ToString('o')
    platform = 'windows-x64-net48'
    inputs = @($inputRecords)
    files = @($payload)
    nativeCalls = @('start_server')
    runtimeValidation = 'Not run. Compilation and staging do not establish protocol compatibility.'
    binding = 'Owned by the staged native library. This wrapper does not override its socket binding.'
}
$receiptPath = Join-Path $targetPath 'host-receipt.json'
[System.IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 8) + "`n", [System.Text.UTF8Encoding]::new($false))
Write-Output "Built and staged: $outputPath"
Write-Output "Receipt: $receiptPath"
Write-Output 'No server was started.'
