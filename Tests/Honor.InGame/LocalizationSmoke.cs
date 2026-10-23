using Honor.Core;
using RimWorld;
using Verse;

namespace Honor.InGame;

internal static class LocalizationSmoke
{
    internal static void Run()
    {
        try
        {
            bool korean = GenCommandLine.CommandLineArgPassed("honorExpectKorean");
            string language = LanguageDatabase.activeLanguage.folderName;
            if (GenCommandLine.CommandLineArgPassed("honorExpectBaseOnly"))
                Check(!ModsConfig.RoyaltyActive && !ModsConfig.IdeologyActive && !ModsConfig.BiotechActive &&
                    !ModsConfig.AnomalyActive && !ModsConfig.OdysseyActive, "all DLC are disabled for base-game test");
            if (GenCommandLine.TryGetCommandLineArg("honorExpectedRoot", out var expectedRoot))
                Check(Path.GetFullPath(LoadedModManager.GetMod<HonorMod>().Content.RootDir).TrimEnd(Path.DirectorySeparatorChar)
                    .Equals(Path.GetFullPath(expectedRoot).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase), "installed local mod is loaded");
            Check(language.StartsWith(korean ? "Korean" : "English"), "game language: " + language);
            Check(HonorDefOf.Honor_Need.label == (korean ? "명예" : "honor"), "need label");
            Check(HonorDefOf.Honor_Need.description.Contains(korean ? "단계 하락" : "Only events"), "need description");
            Check(HonorDefOf.Honor_Duel.reportString.Contains(korean ? "결투" : "honor duel"), "job report");
            Check(HonorDefOf.Honor_Obsessed.degreeDatas[0].label == (korean ? "명예 집착" : "honor obsessed"), "obsessed trait");
            Check(HonorDefOf.Honor_Honorable.degreeDatas[0].label == (korean ? "명예로움" : "honorable"), "honorable trait");
            Check(HonorDefOf.Honor_Vile.degreeDatas[0].label == (korean ? "비열함" : "vile"), "vile trait");
            Check(HonorDefOf.Honor_Respected.stages[0].label.Contains(korean ? "승리자" : "victor"), "social memory");
            Check(HonorDefOf.Honor_Dishonorable.stages[0].label == (korean ? "불명예스러운 살해" : "dishonorable killing"), "dishonor memory");
            Check("Honor_Arena".Translate().ToString() == (korean ? "결투장 지정" : "Set duel arena"), "keyed UI");
            Check("Honor_DevInsult".Translate().ToString() == (korean ? "개발: 명예 모욕" : "DEV: Honor insult"), "developer gizmo");
            Check("Honor_Challenge".Translate("A", "B").ToString().Contains(korean ? "모욕" : "insult"), "formatted notification");
            if (ModsConfig.IdeologyActive)
            {
                Check(DefDatabase<IssueDef>.GetNamed("Honor_DuelRule").label == (korean ? "명예 결투" : "honor duels"), "ideology issue");
                Check(DefDatabase<PreceptDef>.GetNamed("Honor_FirstBlood").label == (korean ? "선혈전" : "first blood"), "first blood precept");
                Check(DefDatabase<PreceptDef>.GetNamed("Honor_Surrender").label == (korean ? "항복전" : "surrender"), "surrender precept");
                Check(DefDatabase<PreceptDef>.GetNamed("Honor_Death").label == (korean ? "사망전" : "to the death"), "death precept");
            }
            var translationErrors = LanguageDatabase.activeLanguage.loadErrors
                .Concat(LanguageDatabase.activeLanguage.defInjections.SelectMany(p => p.loadErrors)).ToList();
            Check(!translationErrors.Any(e => e.Contains("Honor_", StringComparison.OrdinalIgnoreCase)), "no Honor translation diagnostics");
            if (korean)
            {
                var honorInjections = LanguageDatabase.activeLanguage.defInjections.SelectMany(p => p.injections.Values)
                    .Where(i => i.DefName.StartsWith("Honor_", StringComparison.Ordinal)).ToList();
                Check(honorInjections.Count >= 11 && honorInjections.All(i => i.injected), "all Honor Def translations applied");
                if (!ModsConfig.IdeologyActive)
                    Check(!honorInjections.Any(i => i.DefName is "Honor_DuelRule" or "Honor_FirstBlood" or "Honor_Surrender" or "Honor_Death"), "DLC-only translations excluded");
            }
            Log.Message($"[Honor.Tests] INFO: upstream translation diagnostics={translationErrors.Count}; Honor diagnostics=0.");
            Log.Message("[Honor.Tests] PASS: localization " + language);
        }
        catch (Exception e) { Log.Error("[Honor.Tests] FAIL: localization " + e); }
    }
    private static void Check(bool passed, string label)
    {
        if (!passed) throw new Exception(label);
        Log.Message("[Honor.Tests] ASSERT PASS: localization " + label);
    }
}
