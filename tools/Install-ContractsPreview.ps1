param([Parameter(Mandatory)][string]$RimWorldDir)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$gameRoot = (Resolve-Path -LiteralPath $RimWorldDir).Path
if (Get-Process -Name RimWorld,RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'Close RimWorld before installation.' }
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'Version.txt'))) { throw 'Not a RimWorld installation.' }
$source = Join-Path $repoRoot 'TestMods/Honor.Contracts.Preview'
foreach ($assembly in 'Honor.Contracts.Preview','Honor.Contracts.Rules') {
    if (!(Test-Path -LiteralPath (Join-Path $source "1.7/Assemblies/$assembly.dll"))) { throw 'Build the preview first.' }
}
$modsRoot = Join-Path $gameRoot 'Mods'
New-Item -ItemType Directory -Path $modsRoot -Force | Out-Null
$resolvedMods = (Resolve-Path -LiteralPath $modsRoot).Path
$target = Join-Path $resolvedMods 'Honor.Contracts.Preview'
if (Test-Path -LiteralPath $target) {
    $resolvedTarget = (Resolve-Path -LiteralPath $target).Path
    if ((Split-Path $resolvedTarget -Parent) -ne $resolvedMods) { throw 'Unexpected target path.' }
    if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing a linked install target.' }
    [xml]$about = Get-Content -LiteralPath (Join-Path $target 'About/About.xml') -Raw
    if ($about.ModMetaData.packageId -ne 'honor.series.contracts.preview') { throw 'Refusing to replace another mod.' }
    New-Item -ItemType Directory -Path (Join-Path $repoRoot '.local') -Force | Out-Null
    $backup = Join-Path $repoRoot ('.local/contracts-preview-' + [guid]::NewGuid().ToString('N'))
    Move-Item -LiteralPath $resolvedTarget -Destination $backup
    Write-Output "Previous preview backed up: $backup"
}
Copy-Item -LiteralPath $source -Destination $target -Recurse
Write-Output "Installed optional development preview: $target"
Write-Output 'Activation unchanged. Requires Harmony 3 and Honor.Core 0.2.1+. No game process was started.'
