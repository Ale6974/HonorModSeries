param(
    [Parameter(Mandatory)][string]$RimWorldDir,
    [ValidateSet('Gameplay','Checkpoint','Resume','English','Korean','Review','ReviewResume')][string]$Scenario = 'Gameplay',
    [string]$SaveDataDir,
    [switch]$BaseGameOnly,
    [int]$TimeoutSeconds = 240
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$gameRoot = (Resolve-Path -LiteralPath $RimWorldDir).Path
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'RimWorld.exe'))) { throw 'RimWorld.exe missing.' }
if (!(Test-Path -LiteralPath (Join-Path $repoRoot 'Mods/Honor.Core/1.7/Assemblies/Honor.Core.dll'))) { throw 'Build first.' }
$runId = [guid]::NewGuid().ToString('N')
$results = Join-Path $repoRoot "artifacts/$Scenario-$runId"
New-Item -ItemType Directory -Path $results -Force | Out-Null
if (!$SaveDataDir) { $SaveDataDir = Join-Path $results 'SaveData' }
$SaveDataDir = [IO.Path]::GetFullPath($SaveDataDir)
New-Item -ItemType Directory -Path $SaveDataDir -Force | Out-Null
if ($Scenario -in @('Resume','ReviewResume')) {
    $saveName = if ($Scenario -eq 'Resume') { 'HonorSeries' } else { 'HonorReviewResolved' }
    $activeSave = Join-Path $SaveDataDir "Saves/$saveName.rws"
    if (!(Test-Path -LiteralPath $activeSave)) { throw "$Scenario requires a prior matching run SaveDataDir with $saveName.rws." }
}
$temporaryModPaths = @()
$taskProcess = $null
$hiddenMods = @()
try {
    if (Get-Process -Name RimWorld -ErrorAction SilentlyContinue) { throw 'Close RimWorld before isolated series tests.' }
    foreach ($name in @('Core','Society','Nobility','Diplomacy','Oaths')) {
        $local = Join-Path $gameRoot "mods/Honor.$name"
        if (Test-Path -LiteralPath $local) {
            $resolved = (Resolve-Path -LiteralPath $local).Path
            if ((Split-Path $resolved -Parent) -ne (Join-Path $gameRoot 'mods')) { throw 'Unexpected mod path.' }
            $backup = Join-Path $results "installed-Honor.$name"
            Move-Item -LiteralPath $resolved -Destination $backup
            $hiddenMods += ,@($backup,$local)
        }
    }
    $entries = @(@('Core','Mods/Honor.Core'), @('Society','Mods/Honor.Society'), @('Oaths','Mods/Honor.Oaths'), @('Diplomacy','Mods/Honor.Diplomacy'), @('Tests','TestMods/Honor.SeriesTests'))
    if (!$BaseGameOnly) { $entries += ,@('Nobility','Mods/Honor.Nobility') }
    foreach ($entry in $entries) {
        $target = Join-Path $gameRoot "mods/HonorTest_$($entry[0])_$runId"
        if (Test-Path -LiteralPath $target) { throw "Target already exists: $target" }
        Copy-Item -LiteralPath (Join-Path $repoRoot $entry[1]) -Destination $target -Recurse
        $temporaryModPaths += $target
    }
    $runArgs = @('-Headless', '-NoFileLogCallstacks', '-mod=brrainz.harmony,honor.series.core,honor.series.society,honor.series.oaths,honor.series.diplomacy,honor.series.nobility,honor.series.seriestests', "-savedatafolder=`"$SaveDataDir`"")
    if ($BaseGameOnly) {
        $runArgs = @($runArgs | Where-Object { $_ -notlike '-mod=*' })
        $runArgs += '-honorExpectBaseOnly'
        $configDir = Join-Path $SaveDataDir 'Config'
        New-Item -ItemType Directory -Path $configDir -Force | Out-Null
        '<ModsConfigData><version>1.7.4985 rev1269</version><activeMods><li>brrainz.harmony</li><li>ludeon.rimworld</li><li>honor.series.core</li><li>honor.series.society</li><li>honor.series.oaths</li><li>honor.series.diplomacy</li><li>honor.series.seriestests</li></activeMods><knownExpansions><li>ludeon.rimworld.royalty</li><li>ludeon.rimworld.ideology</li><li>ludeon.rimworld.biotech</li><li>ludeon.rimworld.anomaly</li><li>ludeon.rimworld.odyssey</li></knownExpansions></ModsConfigData>' | Set-Content -LiteralPath (Join-Path $configDir 'ModsConfig.xml') -Encoding utf8
    }
    if ($Scenario -in @('English','Korean')) {
        $configDir = Join-Path $SaveDataDir 'Config'
        New-Item -ItemType Directory -Path $configDir -Force | Out-Null
        $langName = if ($Scenario -eq 'Korean') { 'Korean (한국어)' } else { 'English' }
        "<PrefsData><langFolderName>$langName</langFolderName></PrefsData>" | Set-Content -LiteralPath (Join-Path $configDir 'Prefs.xml') -Encoding utf8
        $runArgs += '-honorSeriesLanguage'
        if ($Scenario -eq 'Korean') { $runArgs += '-honorExpectKorean' }
    }
    elseif ($Scenario -eq 'Resume') { $runArgs += '-honorSeriesResume' }
    elseif ($Scenario -eq 'Review') { $runArgs += @('-quicktest','-honorSeriesReview') }
    elseif ($Scenario -eq 'ReviewResume') { $runArgs += '-honorSeriesReviewResume' }
    else {
        $runArgs += @('-quicktest','-honorSeries')
        if ($Scenario -eq 'Checkpoint') { $runArgs += '-honorSeriesCheckpoint' }
    }
    $stdout = Join-Path $results 'stdout.log'
    $taskProcess = Start-Process -FilePath (Join-Path $gameRoot 'RimWorld.exe') -WorkingDirectory $gameRoot -ArgumentList $runArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError (Join-Path $results 'stderr.log')
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $passed = $false
    do {
        Start-Sleep -Seconds 2
        $log = if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout -Raw } else { '' }
        if ($log -match '\[Honor.Tests\] FAIL:') { throw "Engine test failed. See $stdout" }
        $marker = if ($Scenario -eq 'Checkpoint') { '\[Honor.Tests\] CHECKPOINT PASS:' } else { '\[Honor.Tests\] PASS:' }
        if ($log -match $marker) { $passed = $true; break }
    } until ($taskProcess.HasExited -or [DateTime]::UtcNow -ge $deadline)
    if (!$passed) { throw "Engine test did not complete. See $stdout" }
    if ($log -match '\bERROR\b|XML error|Config error') { throw "Engine emitted errors despite test result. See $stdout" }
    Write-Output "PASS: $Scenario. Results: $results. SaveData: $SaveDataDir"
} finally {
    if ($taskProcess -and !$taskProcess.HasExited) { Stop-Process -Id $taskProcess.Id; $taskProcess.WaitForExit(10000) | Out-Null }
    foreach ($target in $temporaryModPaths) {
        $resolved = [IO.Path]::GetFullPath($target)
        $expected = [IO.Path]::GetFullPath((Join-Path $gameRoot 'mods')) + [IO.Path]::DirectorySeparatorChar
        if (!$resolved.StartsWith($expected, [StringComparison]::OrdinalIgnoreCase) -or
            !(Split-Path $resolved -Leaf).EndsWith($runId)) { throw "Unsafe cleanup target: $resolved" }
        $retired = Join-Path $results (Split-Path $resolved -Leaf)
        Move-Item -LiteralPath $resolved -Destination $retired
    }
    foreach ($entry in $hiddenMods) { Move-Item -LiteralPath $entry[0] -Destination $entry[1] }
}
