param([Parameter(Mandatory)][string]$RimWorldDir, [switch]$Activate)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$gameRoot = (Resolve-Path -LiteralPath $RimWorldDir).Path
$source = Join-Path $repoRoot 'Mods/Honor.Core'
$target = Join-Path $gameRoot 'mods/Honor.Core'
if (Get-Process -Name RimWorld -ErrorAction SilentlyContinue) { throw 'Close RimWorld before installation.' }
if (!(Test-Path -LiteralPath (Join-Path $source '1.7/Assemblies/Honor.Core.dll'))) { throw 'Build first.' }
$backupDir = Join-Path $repoRoot ('.local/install-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
if (Test-Path -LiteralPath $target) {
    [xml]$about = Get-Content -LiteralPath (Join-Path $target 'About/About.xml') -Raw
    if ($about.ModMetaData.packageId -ne 'honor.series.core') { throw 'Refusing to replace a different mod.' }
    $resolved = (Resolve-Path -LiteralPath $target).Path
    if ((Split-Path $resolved -Parent) -ne (Join-Path $gameRoot 'mods')) { throw 'Unexpected target path.' }
    Move-Item -LiteralPath $resolved -Destination (Join-Path $backupDir 'Honor.Core')
}
Copy-Item -LiteralPath $source -Destination $target -Recurse
if ($Activate) {
    $config = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Config/ModsConfig.xml'
    if (!(Test-Path -LiteralPath $config)) { throw 'Game ModsConfig.xml missing. Enable Honor.Core in the game mod list.' }
    Copy-Item -LiteralPath $config -Destination (Join-Path $backupDir 'ModsConfig.xml')
    $doc = [xml]::new(); $doc.PreserveWhitespace = $true; $doc.Load($config)
    $mods = $doc.ModsConfigData.activeMods
    if (!@($mods.li).Contains('brrainz.harmony')) {
        $harmony = $doc.CreateElement('li'); $harmony.InnerText = 'brrainz.harmony'; $mods.PrependChild($harmony) | Out-Null
    }
    if (!@($mods.li).Contains('honor.series.core')) {
        $core = $doc.CreateElement('li'); $core.InnerText = 'honor.series.core'; $mods.AppendChild($core) | Out-Null
    }
    $doc.Save($config)
    Write-Output "Activated Honor.Core. Previous configuration backup: $backupDir"
}
Write-Output "Installed: $target"
