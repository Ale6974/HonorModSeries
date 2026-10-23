param([string]$DotnetPath = 'dotnet')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    if (!(Test-Path -LiteralPath 'GamePaths.props')) { throw 'Copy GamePaths.props.example to GamePaths.props and set your local 1.7 / Harmony paths.' }
    & $DotnetPath build Source/Honor.Core/Honor.Core.csproj -c Release
    if ($LASTEXITCODE) { throw 'Core build failed.' }
    & $DotnetPath run --project Tests/Honor.Rules.Tests -c Release
    if ($LASTEXITCODE) { throw 'Rule tests failed.' }
    & $DotnetPath build Tests/Honor.InGame/Honor.InGame.csproj -c Release
    if ($LASTEXITCODE) { throw 'In-game test build failed.' }
    Get-ChildItem Mods,TestMods -Recurse -Filter *.xml | ForEach-Object {
        [xml](Get-Content -LiteralPath $_.FullName -Raw) | Out-Null
    }
    [xml]$english = Get-Content 'Mods/Honor.Core/1.7/Languages/English/Keyed/Honor.xml' -Raw
    [xml]$korean = Get-Content 'Mods/Honor.Core/1.7/Languages/Korean/Keyed/Honor.xml' -Raw
    $englishKeys = @($english.LanguageData.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object Name)
    $koreanKeys = @($korean.LanguageData.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object Name)
    if (Compare-Object $englishKeys $koreanKeys) { throw 'English/Korean translation keys differ.' }
    foreach ($key in $englishKeys) {
        $left = @([regex]::Matches($english.SelectSingleNode("LanguageData/$key").InnerText, '\{\d+\}') | ForEach-Object Value | Sort-Object)
        $right = @([regex]::Matches($korean.SelectSingleNode("LanguageData/$key").InnerText, '\{\d+\}') | ForEach-Object Value | Sort-Object)
        if (($left -join ',') -ne ($right -join ',')) { throw "Translation placeholders differ: $key" }
    }
    Write-Output "PASS: $($englishKeys.Count) matching English/Korean keys and format placeholders."
    Write-Output 'PASS: build, rule tests and XML syntax. Run Test-InGame.ps1 for engine validation.'
} finally { Pop-Location }
