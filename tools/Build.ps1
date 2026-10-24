param([string]$DotnetPath = 'dotnet', [ValidateSet('1.6','1.7')][string]$GameVersion = '1.7')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    if (!(Test-Path -LiteralPath 'GamePaths.props')) { throw 'Copy GamePaths.props.example to GamePaths.props and set your local 1.7 / Harmony paths.' }
    & $DotnetPath build Source/Honor.Core/Honor.Core.csproj -c Release "-p:RimWorldVersion=$GameVersion"
    if ($LASTEXITCODE) { throw 'Core build failed.' }
    & $DotnetPath run --project Tests/Honor.Rules.Tests -c Release "-p:RimWorldVersion=$GameVersion"
    if ($LASTEXITCODE) { throw 'Rule tests failed.' }
    & $DotnetPath run --project Tests/Honor.Contracts.Tests -c Release "-p:RimWorldVersion=$GameVersion"
    if ($LASTEXITCODE) { throw 'Contract rule tests failed.' }
    & $DotnetPath build Source/Honor.Contracts.Preview/Honor.Contracts.Preview.csproj -c Release "-p:RimWorldVersion=$GameVersion"
    if ($LASTEXITCODE) { throw 'Contracts preview build failed.' }
    if ($GameVersion -eq '1.7') {
        & $DotnetPath build Tests/Honor.InGame/Honor.InGame.csproj -c Release
        if ($LASTEXITCODE) { throw 'In-game test build failed.' }
    }
    foreach ($module in 'Society','Nobility','Diplomacy','Oaths') {
        & $DotnetPath build "Source/Honor.$module/Honor.$module.csproj" -c Release "-p:RimWorldVersion=$GameVersion"
        if ($LASTEXITCODE) { throw "$module build failed." }
    }
    if ($GameVersion -eq '1.7') {
        & $DotnetPath build Tests/Honor.SeriesTests/Honor.SeriesTests.csproj -c Release
        if ($LASTEXITCODE) { throw 'Series test build failed.' }
    }
    Get-ChildItem Mods,TestMods -Recurse -Filter *.xml | ForEach-Object {
        [xml](Get-Content -LiteralPath $_.FullName -Raw) | Out-Null
    }
    foreach ($mod in @((Get-ChildItem Mods -Directory)) + @(Get-Item TestMods/Honor.Contracts.Preview)) {
    [xml]$english = Get-Content "$($mod.FullName)/Common/Languages/English/Keyed/Honor.xml" -Raw
    [xml]$korean = Get-Content "$($mod.FullName)/Common/Languages/Korean/Keyed/Honor.xml" -Raw
    $englishKeys = @($english.LanguageData.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object Name)
    $koreanKeys = @($korean.LanguageData.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object Name)
    if (Compare-Object $englishKeys $koreanKeys) { throw 'English/Korean translation keys differ.' }
    foreach ($key in $englishKeys) {
        $left = @([regex]::Matches($english.SelectSingleNode("LanguageData/$key").InnerText, '\{\d+\}') | ForEach-Object Value | Sort-Object)
        $right = @([regex]::Matches($korean.SelectSingleNode("LanguageData/$key").InnerText, '\{\d+\}') | ForEach-Object Value | Sort-Object)
        if (($left -join ',') -ne ($right -join ',')) { throw "Translation placeholders differ: $key" }
    }
    Write-Output "PASS: $($englishKeys.Count) matching English/Korean keys and format placeholders."
    }
    foreach ($factionFile in 'FreeCityUnion','RealmAdministration') {
        [xml]$factionEnglish = Get-Content "TestMods/Honor.Contracts.Preview/Common/Languages/English/DefInjected/FactionDef/$factionFile.xml" -Raw
        [xml]$factionKorean = Get-Content "TestMods/Honor.Contracts.Preview/Common/Languages/Korean/DefInjected/FactionDef/$factionFile.xml" -Raw
        [xml]$factionDef = Get-Content "TestMods/Honor.Contracts.Preview/Common/Defs/FactionDefs/$factionFile.xml" -Raw
        foreach ($field in 'label','description','fixedName','pawnSingular','pawnsPlural','leaderTitle') {
            $key = "$($factionDef.Defs.FactionDef.defName).$field"
            if (!$factionEnglish.LanguageData.SelectSingleNode($key) -or !$factionKorean.LanguageData.SelectSingleNode($key)) { throw "Faction translation missing: $key" }
            if ($factionEnglish.LanguageData.SelectSingleNode($key).InnerText -ne $factionDef.Defs.FactionDef.SelectSingleNode($field).InnerText) { throw "Faction English fallback differs: $key" }
        }
        Write-Output "PASS: six $factionFile FactionDef fields have matching English fallback and Korean translations."
    }
    $symbols = Join-Path $repoRoot ('artifacts/symbols-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $symbols -Force | Out-Null
    Get-ChildItem Mods -Recurse -File -Filter *.pdb | ForEach-Object {
        $destination = Join-Path $symbols $_.FullName.Substring($repoRoot.Length + 1)
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Move-Item -LiteralPath $_.FullName -Destination $destination
    }
    Write-Output 'PASS: build, rule tests and XML syntax. Run Test-InGame.ps1 / Test-Series.ps1 for engine validation.'
} finally { Pop-Location }
