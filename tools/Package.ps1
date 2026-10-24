param([string]$Destination, [switch]$IncludeContractsPreview)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (!$Destination) { $Destination = Join-Path $repoRoot 'artifacts/HonorModSeries-0.2.2.zip' }
& (Join-Path $PSScriptRoot 'Test-PackageLayout.ps1')
$stage = Join-Path $repoRoot ('artifacts/package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($name in 'Core','Society','Nobility','Diplomacy','Oaths') {
    $source=Join-Path $repoRoot "Mods/Honor.$name"
    if (!(Test-Path -LiteralPath (Join-Path $source "1.7/Assemblies/Honor.$name.dll"))) { throw "Build $name first." }
    Copy-Item -LiteralPath $source -Destination (Join-Path $stage "Honor.$name") -Recurse
}
if ($IncludeContractsPreview) {
    Copy-Item -LiteralPath (Join-Path $repoRoot 'TestMods/Honor.Contracts.Preview') -Destination (Join-Path $stage 'Honor.Contracts.Preview') -Recurse
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/DEPENDENCIES.md') -Destination $stage
$allowed=@('Honor.Core.dll','Honor.Rules.dll','Honor.Society.dll','Honor.Nobility.dll','Honor.Diplomacy.dll','Honor.Oaths.dll')
if ($IncludeContractsPreview) { $allowed += @('Honor.Contracts.Preview.dll','Honor.Contracts.Rules.dll','Microsoft.Bcl.AsyncInterfaces.dll',
    'System.Buffers.dll','System.IO.Pipelines.dll','System.Memory.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll',
    'System.Text.Encodings.Web.dll','System.Text.Json.dll','System.Threading.Tasks.Extensions.dll') }
if (Get-ChildItem -LiteralPath $stage -Recurse -Filter *.dll | Where-Object Name -NotIn $allowed) { throw 'Unexpected DLL in package.' }
Get-ChildItem -LiteralPath $stage -Recurse -File -Filter *.pdb | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $Destination -Force
Get-FileHash -LiteralPath $Destination -Algorithm SHA256
