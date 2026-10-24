param([Parameter(Mandatory)][string]$RimWorldDir, [switch]$Activate,
      [ValidateSet('Core','Society','Nobility','Diplomacy','Oaths')][string[]]$Modules = @('Core','Society','Nobility','Diplomacy','Oaths'))
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$gameRoot = (Resolve-Path -LiteralPath $RimWorldDir).Path
if (Get-Process -Name RimWorld -ErrorAction SilentlyContinue) { throw 'Close RimWorld before installation.' }
$backupDir = Join-Path $repoRoot ('.local/install-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
foreach ($module in $Modules) {
    $source = Join-Path $repoRoot "Mods/Honor.$module"
    $target = Join-Path $gameRoot "mods/Honor.$module"
    if (!(Test-Path -LiteralPath (Join-Path $source "1.7/Assemblies/Honor.$module.dll"))) { throw "Build $module first." }
    if (Test-Path -LiteralPath $target) {
        [xml]$about = Get-Content -LiteralPath (Join-Path $target 'About/About.xml') -Raw
        if ($about.ModMetaData.packageId -ne "honor.series.$($module.ToLowerInvariant())") { throw 'Refusing to replace a different mod.' }
        $resolved = (Resolve-Path -LiteralPath $target).Path
        if ((Split-Path $resolved -Parent) -ne (Join-Path $gameRoot 'mods')) { throw 'Unexpected target path.' }
        Move-Item -LiteralPath $resolved -Destination (Join-Path $backupDir "Honor.$module")
    }
    Copy-Item -LiteralPath $source -Destination $target -Recurse
    Write-Output "Installed: $target"
}
if ($Activate) {
    $config = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Config/ModsConfig.xml'
    if (!(Test-Path -LiteralPath $config)) { throw 'ModsConfig.xml missing. Enable the modules in the game mod list.' }
    Copy-Item -LiteralPath $config -Destination (Join-Path $backupDir 'ModsConfig.xml')
    $doc = [xml]::new(); $doc.PreserveWhitespace = $true; $doc.Load($config)
    $mods = $doc.ModsConfigData.activeMods
    if (!@($mods.li).Contains('brrainz.harmony')) {
        $node = $doc.CreateElement('li'); $node.InnerText = 'brrainz.harmony'; $mods.PrependChild($node) | Out-Null
    }
    foreach ($module in $Modules) {
        if ($module -eq 'Nobility' -and !@($mods.li).Contains('ludeon.rimworld.royalty')) { Write-Output 'Nobility installed but left inactive: enable Royalty first.'; continue }
        $id = "honor.series.$($module.ToLowerInvariant())"
        if (!@($mods.li).Contains($id)) { $node = $doc.CreateElement('li'); $node.InnerText = $id; $mods.AppendChild($node) | Out-Null }
    }
    $doc.Save($config)
    Write-Output "Activated eligible Honor modules. Previous configuration: $backupDir"
}
