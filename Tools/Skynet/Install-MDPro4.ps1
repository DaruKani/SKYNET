param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'

$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$gameDirectory = (Resolve-Path -LiteralPath $GameRoot).Path
$destination = [IO.Path]::GetFullPath((Join-Path $gameDirectory 'Data\Skynet'))
if (-not $destination.StartsWith($gameDirectory.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The installation must stay inside the requested MDPro4 directory.'
}
if (-not (Test-Path -LiteralPath (Join-Path $gameDirectory 'Data') -PathType Container)) {
    throw 'GameRoot must contain the MDPro4 Data directory.'
}

# Explicit product payload: no inherited outputs, local models, CDBs or QA data.
$payload = @{
    'SKYNET.exe' = "bin\$Configuration\SKYNET.exe"
    'SKYNET.exe.config' = "bin\$Configuration\SKYNET.exe.config"
    'x64\sqlite3.dll' = 'ThirdParty\sqlite\x64\sqlite3.dll'
    'x86\sqlite3.dll' = 'ThirdParty\sqlite\x86\sqlite3.dll'
    'Decks\AI_Skynet.ydk' = 'Decks\AI_Skynet.ydk'
    'Decks\AI_ExpertOldSchool.ydk' = 'Decks\AI_ExpertOldSchool.ydk'
    'Decks\AI_ExpertSwordsoul.ydk' = 'Decks\AI_ExpertSwordsoul.ydk'
    'Decks\AI_SkynetSwordsoul.ydk' = 'Decks\AI_SkynetSwordsoul.ydk'
    'skynet-logo.png' = 'Branding\skynet-logo.png'
    'LICENSE' = 'LICENSE'
}
foreach ($relative in $payload.Keys) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot $payload[$relative]) -PathType Leaf)) {
        throw "Build Skynet.sln ($Configuration) first: missing $($payload[$relative])"
    }
}
foreach ($relative in $payload.Keys) {
    $target = Join-Path $destination $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceRoot $payload[$relative]) -Destination $target -Force
}
$manifest = foreach ($relative in ($payload.Keys | Sort-Object)) {
    [PSCustomObject]@{
        path = $relative
        sha256 = (Get-FileHash -LiteralPath (Join-Path $destination $relative) -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'manifest.json') -Encoding utf8
Write-Output "Installed SKYNET to $destination"
Write-Output 'The MDPro4 SKYNET host adapter must be present. Select ocgcore, then SKYNET in Solo.'
