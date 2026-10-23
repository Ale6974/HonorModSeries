param([string]$Destination)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (!$Destination) { $Destination = Join-Path (Split-Path $repoRoot -Parent) 'Honor.Core-MVP.zip' }
$source = Join-Path $repoRoot 'Mods/Honor.Core'
$dll = Join-Path $source '1.7/Assemblies/Honor.Core.dll'
if (!(Test-Path -LiteralPath $dll)) { throw 'Build first.' }
$stage = Join-Path $repoRoot ('artifacts/package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $stage 'Honor.Core') -Recurse
$docTarget = Join-Path $stage 'Honor.Core/Docs'
New-Item -ItemType Directory -Path $docTarget | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $docTarget
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination (Join-Path $docTarget 'docs') -Recurse
$foreignDlls = Get-ChildItem -LiteralPath $stage -Recurse -Filter *.dll | Where-Object { $_.Name -notin @('Honor.Core.dll','Honor.Rules.dll') }
if ($foreignDlls) { throw 'Unexpected DLL in package.' }
Compress-Archive -LiteralPath (Join-Path $stage 'Honor.Core') -DestinationPath $Destination -Force
Get-FileHash -LiteralPath $Destination -Algorithm SHA256
