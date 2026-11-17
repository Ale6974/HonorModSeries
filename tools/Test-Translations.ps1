$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    foreach ($mod in @((Get-ChildItem Mods -Directory)) + @(Get-Item TestMods/Honor.Contracts.Preview)) {
    [xml]$english = Get-Content "$($mod.FullName)/Common/Languages/English/Keyed/Honor.xml" -Raw
    [xml]$korean = Get-Content "$($mod.FullName)/Common/Languages/Korean/Keyed/Honor.xml" -Raw
    $englishKeys = @($english.LanguageData.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object Name)
    $koreanKeys = @($korean.LanguageData.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object Name)
    if (@($englishKeys | Group-Object | Where-Object Count -gt 1).Count -gt 0 -or @($koreanKeys | Group-Object | Where-Object Count -gt 1).Count -gt 0) {
        throw "Duplicate translation keys in $($mod.Name)."
    }
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
} finally { Pop-Location }
