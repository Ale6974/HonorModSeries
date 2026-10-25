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
        // SetFaction also calls SetGuestStatus(null). Validate both dispatch paths, even for ordinary residents.
        var factionDispatch = AccessTools.Method(typeof(Ideo), nameof(Ideo.Notify_MemberChangedFaction), [typeof(Pawn), typeof(Faction), typeof(Faction)]);
        var guestDispatch = AccessTools.Method(typeof(Ideo), nameof(Ideo.Notify_MemberGuestStatusChanged), [typeof(Pawn)]);
        if (factionDispatch == null || guestDispatch == null || factionDispatch.ReturnType != typeof(void) || guestDispatch.ReturnType != typeof(void) || Harmony.GetPatchInfo(factionDispatch)?.Owners.Any() == true ||
            Harmony.GetPatchInfo(guestDispatch)?.Owners.Any() == true)
            throw new ResidentMoveBlocked("RE_FaithCallbacksUnavailable");
        var rows = new List<ResidentFaithPrecept>();
        foreach (var precept in faith.PreceptsListForReading)
        {
            if (precept == null || precept.def == null || precept.GetType().Assembly != typeof(Pawn).Assembly)
                throw new ArgumentException("Unsupported custom ideology precept.");
            var callback = AccessTools.Method(precept.GetType(), nameof(Precept.Notify_MemberChangedFaction), [typeof(Pawn), typeof(Faction), typeof(Faction)]);
            if (callback == null || callback.DeclaringType != typeof(Precept) && callback.DeclaringType != typeof(Precept_RoleSingle))
                throw new ResidentMoveBlocked("RE_FaithCallbacksUnavailable");
            if (Harmony.GetPatchInfo(callback)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_FaithCallbacksUnavailable");
            var guestCallback = AccessTools.Method(precept.GetType(), nameof(Precept.Notify_MemberGuestStatusChanged), [typeof(Pawn)]);
            if (guestCallback == null || guestCallback.DeclaringType != typeof(Precept) || guestCallback.ReturnType != typeof(void) ||
                Harmony.GetPatchInfo(guestCallback)?.Owners.Any() == true)
                throw new ResidentMoveBlocked("RE_FaithCallbacksUnavailable");
            if (precept is Precept_Role role && role.IsAssigned(pawn)) throw new ResidentMoveBlocked("RE_FaithRoleUnavailable");
            rows.Add(new(precept.GetUniqueLoadID(), precept.def.defName, precept.GetType().FullName));
        }
        return ResidentFaith.Capture(faith.GetUniqueLoadID(), certainty, rows, pawn.ideo.PreviousIdeos?.Select(i => i?.GetUniqueLoadID()) ?? [], false);
    }
}
