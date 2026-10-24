param()
Add-Type -AssemblyName System.Drawing
$repo = Split-Path $PSScriptRoot -Parent
foreach ($name in @('Core','Society','Nobility','Diplomacy','Oaths','Series')) {
    $path = if ($name -eq 'Series') { Join-Path $repo 'release/Collection/About/Preview.png' } else { Join-Path $repo "Mods/Honor.$name/About/Preview.png" }
    New-Item -ItemType Directory (Split-Path $path) -Force | Out-Null
    $bitmap=[Drawing.Bitmap]::new(1280,720);$graphics=[Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(19,24,29))
        $graphics.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $gold=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(220,182,109))
        $white=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(240,239,232))
        $gray=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(163,176,185))
        $line=[Drawing.Pen]::new([Drawing.Color]::FromArgb(220,182,109),3)
        $big=[Drawing.Font]::new('Segoe UI',110,[Drawing.FontStyle]::Bold)
        $mid=[Drawing.Font]::new('Segoe UI',48,[Drawing.FontStyle]::Regular)
        $small=[Drawing.Font]::new('Segoe UI',24,[Drawing.FontStyle]::Regular)
        $graphics.DrawRectangle($line,40,40,1200,640)
        $graphics.DrawString('HONOR',$big,$white,92,92)
        $graphics.DrawString('MOD SERIES',$small,$gold,106,295)
        $graphics.DrawLine($line,106,340,1170,340)
        $graphics.DrawString($name.ToUpperInvariant(),$mid,$gold,102,370)
        $sub = switch ($name) { Core {'Honor. Challenge. Duel.'} Society {'Apologies. Witnesses. Family.'} Nobility {'Royal titles. Proxy champions.'} Diplomacy {'World quests. Representative duels.'} Oaths {'Ceremonies. Promises. Consequences.'} Series {'The complete modular collection.'} }
        $graphics.DrawString($sub,$small,$white,108,470)
        $graphics.DrawString('RIMWORLD 1.7  /  EN + KO  /  EXPERIMENTAL 0.2.1',$small,$gray,108,584)
        $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
    } finally {
        foreach ($item in @($big,$mid,$small,$gold,$white,$gray,$line,$graphics,$bitmap)) { $item.Dispose() }
    }
}
