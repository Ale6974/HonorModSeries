param([string]$Destination)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (!$Destination) { $Destination = Join-Path (Split-Path $repoRoot -Parent) 'HonorModSeries-0.2.1.zip' }
$stage = Join-Path $repoRoot ('artifacts/package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($name in 'Core','Society','Nobility','Diplomacy','Oaths') {
    $source=Join-Path $repoRoot "Mods/Honor.$name"
    if (!(Test-Path -LiteralPath (Join-Path $source "1.7/Assemblies/Honor.$name.dll"))) { throw "Build $name first." }
    Copy-Item -LiteralPath $source -Destination (Join-Path $stage "Honor.$name") -Recurse
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/DEPENDENCIES.md') -Destination $stage
$allowed=@('Honor.Core.dll','Honor.Rules.dll','Honor.Society.dll','Honor.Nobility.dll','Honor.Diplomacy.dll','Honor.Oaths.dll')
if (Get-ChildItem -LiteralPath $stage -Recurse -Filter *.dll | Where-Object Name -NotIn $allowed) { throw 'Unexpected DLL in package.' }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $Destination -Force
Get-FileHash -LiteralPath $Destination -Algorithm SHA256
