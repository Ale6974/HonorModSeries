using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

/// <summary>Observation only: never assign an ideology/role or write certainty during a territorial move.</summary>
internal static class ResidentIdeology
{
    private static readonly System.Reflection.FieldInfo Certainty = AccessTools.Field(typeof(Pawn_IdeoTracker), "certaintyInt");
    internal static string Proof(Pawn pawn)
    {
        var faith = pawn.Ideo;
        if (faith == null) return ResidentFaith.None;
        if (!ModsConfig.IdeologyActive || faith.GetType() != typeof(Ideo) || pawn.ideo == null || pawn.ideo.GetType() != typeof(Pawn_IdeoTracker) ||
            Certainty == null || Certainty.IsStatic || Certainty.FieldType != typeof(float) ||
            faith.PreceptsListForReading == null || faith.PreceptsListForReading.Count > 256 || faith.HasPrecept(PreceptDefOf.AgeReversal_Demanded))
            throw new ArgumentException("Unsupported resident ideology or age-reversal obligation.");
        float certainty = (float)Certainty.GetValue(pawn.ideo); // The public getter can write a baby's certainty; observation must not do that.
        if (!RuntimeCompat.IsFinite(certainty) || certainty is < 0 or > 1) throw new ArgumentException("Invalid stored certainty.");
        var rows = new List<ResidentFaithPrecept>();
        foreach (var precept in faith.PreceptsListForReading)
        {
            if (precept == null || precept.def == null || precept.GetType().Assembly != typeof(Pawn).Assembly)
                throw new ArgumentException("Unsupported custom ideology precept.");
            var callback = AccessTools.Method(precept.GetType(), nameof(Precept.Notify_MemberChangedFaction), [typeof(Pawn), typeof(Faction), typeof(Faction)]);
            if (callback == null || callback.DeclaringType != typeof(Precept) && callback.DeclaringType != typeof(Precept_RoleSingle))
                throw new ArgumentException("Unverified ideology faction-change callback.");
            if (Harmony.GetPatchInfo(callback)?.Owners.Any() == true) throw new ArgumentException("Patched ideology callback is not verified for resident migration.");
            if (precept is Precept_Role role && role.IsAssigned(pawn)) throw new ArgumentException("Ideology role holders require separate role/leadership preservation.");
            rows.Add(new(precept.GetUniqueLoadID(), precept.def.defName, precept.GetType().FullName));
        }
        return ResidentFaith.Capture(faith.GetUniqueLoadID(), certainty, rows, pawn.ideo.PreviousIdeos?.Select(i => i?.GetUniqueLoadID()) ?? [], false);
    }
}
