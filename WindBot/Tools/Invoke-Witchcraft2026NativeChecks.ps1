[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BotExecutable,
    [Parameter(Mandatory = $true)][string]$Python,
    [Parameter(Mandatory = $true)][string]$ServerLibrary,
    [Parameter(Mandatory = $true)][string]$DataRoot,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$Deck = (Join-Path $PSScriptRoot '../Decks/AI_Witchcraft2026.ydk'),
    [ValidateSet('Guide', 'FieldAccess', 'FieldlessLacrima', 'FieldlessMaid', 'FieldlessControl')][string]$Scenario = 'Guide',
    [switch]$AllowVerifiedLethal,
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
$sixth = if ($Scenario -like 'Fieldless*') { '14558127' } else { '32236916' }
if ($main.Remove($sixth)) { $main.Insert(0, $sixth) }
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
$earlyLethal = @()
for ($number = 1; $number -le 2; $number++) {
    $seat = ($number - 1) % 2
    $log = Join-Path $duels ('duel-{0:D2}/client-{1}.log' -f $number, $seat)
    $text = Get-Content -LiteralPath $log -Raw
    if ($Scenario -ne 'Guide') {
        $start = $text.IndexOf('Witchcraft2026 action:', [StringComparison]::Ordinal)
        $end = if ($start -ge 0) { $text.IndexOf('(Go to End)', $start, [StringComparison]::Ordinal) } else { -1 }
        if ($end -lt 0 -and $start -ge 0) { $end = $text.IndexOf('Duel finished', $start, [StringComparison]::Ordinal) }
        $turn = if ($start -ge 0 -and $end -gt $start) { $text.Substring($start, $end - $start) } else { '' }
        $checks += [ordered]@{ duel = $number; assertion = 'first own main completed'; passed = ($turn.Length -gt 0) }
        if ($Scenario -eq 'FieldAccess') {
            $access = $turn.IndexOf('Rainbow Bridge Bifrost from Hand move to SpellZone')
            $normal = $turn.IndexOf('from Hand move to MonsterZone')
            $checks += [ordered]@{ duel = $number; assertion = 'Bifrost resolves before normal commitment';
                passed = ($access -ge 0 -and $normal -gt $access) }
        } elseif ($Scenario -eq 'FieldlessLacrima') {
            foreach ($event in @('Verre Magic - Lacrima of Light from Hand move to SpellZone',
                'Witchcrafter Genni from Deck move to Grave', 'Witchcrafter Genni from Grave move to Removed',
                'Verre Magic - Lacrima of Light from Grave move to Removed',
                'Witchcrafter Marka from Deck move to MonsterZone',
                'Red-Eyes Dark Dragoon from Extra move to MonsterZone')) {
                $checks += [ordered]@{ duel = $number; assertion = $event; passed = $turn.Contains($event) }
            }
        } elseif ($Scenario -eq 'FieldlessControl') {
            # A separate endpoint assertion, not a rewrite of the strict Marka/Dragoon
            # fixture above. Temporary Gaze valuation can select this lasting board.
            foreach ($event in @('Verre Magic - Lacrima of Light from Hand move to SpellZone',
                'Witchcrafter Genni from Deck move to Grave', 'Witchcrafter Genni from Grave move to Removed',
                'Verre Magic - Lacrima of Light from Grave move to Removed',
                'Witchcrafter Terracottan from Deck move to MonsterZone',
                'Witchcrafter Pupils from Extra move to MonsterZone',
                'Witchcrafter Celebration from Deck move to Hand',
                'Witchcrafter Vice-Madame from Extra move to MonsterZone')) {
                $checks += [ordered]@{ duel = $number; assertion = $event; passed = $turn.Contains($event) }
            }
            $checks += [ordered]@{ duel = $number; assertion = 'Pupils and Vice survive the first Main Phase';
                passed = (-not $turn.Contains('Witchcrafter Pupils from MonsterZone move to') -and
                    -not $turn.Contains('Witchcrafter Vice-Madame from MonsterZone move to')) }
        } else {
            foreach ($event in @('Genni, the Maid of Endymion from Hand move to MonsterZone',
                'Witchcrafter Pittore from MonsterZone move to Removed',
                'Verre, the Maid of Endymion from Deck move to MonsterZone',
                'Witchcrafter Walpurgis from Deck move to Hand')) {
                $checks += [ordered]@{ duel = $number; assertion = $event; passed = $turn.Contains($event) }
            }
        }
        $checks += [ordered]@{ duel = $number; assertion = 'no material mismatch'; passed = (-not $text.Contains('material mismatch')) }
        continue
    }
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
    # Preserve every strict assertion. A separately requested acceptance mode can
    # recognize a shorter verified win after the guide prefix, never any generic win.
    $tailNames = @('Witchcrafter Vice-Madame from Extra move to MonsterZone',
        'Witchcrafter Edel from MonsterZone move to Grave', 'Witchcrafter Distortion from Grave move to Hand',
        'Red-Eyes Dark Dragoon from Grave move to MonsterZone', 'Witchcrafter Distortion from Hand move to SpellZone')
    $duelFailures = @($checks | Where-Object { $_.duel -eq $number -and -not $_.passed })
    $lethalAt = $text.IndexOf('Witchcraft2026 lethal: potential=', [StringComparison]::Ordinal)
    if ($AllowVerifiedLethal -and $duelFailures.Count -gt 0 -and
        @($duelFailures | Where-Object { $_.assertion -notin $tailNames }).Count -eq 0 -and
        $lethalAt -gt $rilliona -and ($firstEnd -lt 0 -or $lethalAt -lt $firstEnd)) {
        $finish = $text.Substring($lethalAt)
        if ($finish.Contains('result: Win') -and $finish.Contains('direct attack!!') -and
            -not $finish.Contains('Witchcraft2026 action:') -and -not $finish.Contains('(Go to Main2)')) {
            $earlyLethal += $number
        }
    }
}
$receipt = [ordered]@{
    scenario = $Scenario
    purpose = 'Native fixed-opening combo regression against passive Default; not Arena or competitive strength'
    sourceDeck = $deckPath
    sourceDeckSha256 = (Get-FileHash -LiteralPath $deckPath -Algorithm SHA256).Hash
    fixedOpening = $opening
    multisetUnchanged = $true
    nativeProvenance = (Join-Path $duels 'result.json')
    assertions = $checks
    verifiedEarlyLethalDuels = $earlyLethal
}
$receipt | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $outputPath 'route-checks.json') -Encoding UTF8
$failed = @($checks | Where-Object { -not $_.passed })
Write-Output ("Route assertions: {0}/{1} passed." -f ($checks.Count - $failed.Count), $checks.Count)
if ($earlyLethal.Count -gt 0) { Write-Output "Separate shorter-win acceptance: duels $($earlyLethal -join ', '). Strict failures remain in receipt." }
if (@($failed | Where-Object { $_.duel -notin $earlyLethal }).Count -gt 0) {
    throw 'Native route assertions failed; inspect route-checks.json and raw logs.'
}
