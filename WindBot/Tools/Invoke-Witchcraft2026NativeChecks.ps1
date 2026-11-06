[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BotExecutable,
    [Parameter(Mandatory = $true)][string]$Python,
    [Parameter(Mandatory = $true)][string]$ServerLibrary,
    [Parameter(Mandatory = $true)][string]$DataRoot,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$Deck = (Join-Path $PSScriptRoot '../Decks/AI_Witchcraft2026.ydk'),
    [string[]]$Opening = @('21522601', '6071005', '58139997', '57916305', '22623509')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$botPath = (Resolve-Path -LiteralPath $BotExecutable).Path
$pythonPath = (Resolve-Path -LiteralPath $Python).Path
$serverPath = (Resolve-Path -LiteralPath $ServerLibrary).Path
$dataPath = (Resolve-Path -LiteralPath $DataRoot).Path
$deckPath = (Resolve-Path -LiteralPath $Deck).Path
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputPath) { throw 'Use a fresh evidence directory.' }
New-Item -ItemType Directory -Path $outputPath | Out-Null
$main = [Collections.Generic.List[string]]::new()
$tail = [Collections.Generic.List[string]]::new()
$extra = $false
foreach ($line in Get-Content -LiteralPath $deckPath) {
    if ($line -eq '#extra') { $extra = $true }
    if ($extra) { $tail.Add($line) }
    elseif ($line -match '^\d+$') { $main.Add($line) }
}
# Exact user-reported hand; reorder only, never add/remove identities.
if ($Opening.Count -ne 5) { throw 'Supply exactly five opening identities.' }
foreach ($id in $opening) {
    if (-not $main.Remove($id)) { throw "Selected deck is missing opening card $id" }
}
# Keep the possible sixth draw from accidentally supplying Madame Verre to
# the no-Madame fixture. This is another permutation, not a card-list change.
if ($main.Remove('32236916')) { $main.Insert(0, '32236916') }
$fixture = Join-Path $outputPath 'reported-hand.ydk'
[IO.File]::WriteAllLines($fixture, @('#fixed opening; original card multiset', '#main') +
    $opening + $main.ToArray() + $tail.ToArray(), [Text.UTF8Encoding]::new($false))
$native = Join-Path $PSScriptRoot '../../SKYNET/Tools/Skynet/native_smoke.py'
$duels = Join-Path $outputPath 'native'
& $pythonPath $native --server $serverPath --data-root $dataPath --bot-exe $botPath `
    --policy Witchcraft2026 --opponent-policy Default --deck $fixture `
    --games 2 --timeout 90 --output-dir $duels
if ($LASTEXITCODE -ne 0) { throw 'Native completion failed; retain logs for diagnosis.' }
$checks = @()
for ($number = 1; $number -le 2; $number++) {
    $seat = ($number - 1) % 2
    $log = Join-Path $duels ('duel-{0:D2}/client-{1}.log' -f $number, $seat)
    $text = Get-Content -LiteralPath $log -Raw
    # Check actual engine movements, not the planner's forecast or the win marker.
    $events = @(
        "Witchcrafter Genni from Hand move to MonsterZone",
        "Witchcrafter Creation from Deck move to Grave",
        "Witchcrafter Celebration from Deck move to Grave",
        "Witchcrafter Distortion from Deck move to Grave",
        "Witchcrafter Confusion Confession from Deck move to Grave",
        "Dark Magician of Destruction from Extra move to MonsterZone",
        "Gaze of Timaeus from Deck move to Hand",
        "Red-Eyes Dark Dragoon from Extra move to MonsterZone",
        "Witchcrafter Terracottan from Hand move to MonsterZone",
        "Red-Eyes Dark Dragoon from MonsterZone move to Grave",
        "Witchcrafter Pupils from Extra move to MonsterZone",
        "Witchcrafter Madame Rilliona from Extra move to MonsterZone"
    )
    $cursor = 0
    foreach ($event in $events) {
        $position = $text.IndexOf($event, $cursor, [StringComparison]::Ordinal)
        $checks += [ordered]@{ duel = $number; assertion = $event; passed = ($position -ge 0) }
        if ($position -ge 0) { $cursor = $position + $event.Length }
    }
    $rilliona = $cursor
    foreach ($name in @('Witchcrafter Haine', 'Witchcrafter Edel', 'Genni, the Maid of Endymion')) {
        $pattern = [regex]::Escape($name) + ' from (Hand|Deck) move to MonsterZone'
        $checks += [ordered]@{ duel = $number; assertion = "Rilliona recruits $name";
            passed = [regex]::IsMatch($text.Substring($rilliona), $pattern) }
    }
    foreach ($event in @(
        'Witchcrafter Vice-Madame from Extra move to MonsterZone',
        'Witchcrafter Edel from MonsterZone move to Grave',
        'Witchcrafter Distortion from Grave move to Hand',
        'Red-Eyes Dark Dragoon from Grave move to MonsterZone',
        'Witchcrafter Distortion from Hand move to SpellZone'
    )) {
        $position = $text.IndexOf($event, $cursor, [StringComparison]::Ordinal)
        $checks += [ordered]@{ duel = $number; assertion = $event; passed = ($position -ge 0) }
        if ($position -ge 0) { $cursor = $position + $event.Length }
    }
    $firstAction = $text.IndexOf('Witchcraft2026 action:', [StringComparison]::Ordinal)
    $firstEnd = if ($firstAction -ge 0) { $text.IndexOf('(Go to End)', $firstAction, [StringComparison]::Ordinal) } else { -1 }
    $checks += [ordered]@{ duel = $number; assertion = 'route finishes in first own Main Phase';
        passed = ($firstAction -ge 0 -and ($firstEnd -lt 0 -or $cursor -lt $firstEnd)) }
    $checks += [ordered]@{ duel = $number; assertion = 'no material mismatch';
        passed = (-not $text.Contains('material mismatch')) }
}
$receipt = [ordered]@{
    purpose = 'Native fixed-opening combo regression against passive Default; not Arena or competitive strength'
    sourceDeck = $deckPath
    sourceDeckSha256 = (Get-FileHash -LiteralPath $deckPath -Algorithm SHA256).Hash
    fixedOpening = $opening
    multisetUnchanged = $true
    nativeProvenance = (Join-Path $duels 'result.json')
    assertions = $checks
}
$receipt | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $outputPath 'route-checks.json') -Encoding UTF8
$failed = @($checks | Where-Object { -not $_.passed })
Write-Output ("Route assertions: {0}/{1} passed." -f ($checks.Count - $failed.Count), $checks.Count)
if ($failed.Count -gt 0) { throw 'Native route assertions failed; inspect route-checks.json and raw logs.' }
