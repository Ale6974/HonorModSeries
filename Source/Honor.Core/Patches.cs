using HarmonyLib;
using RimWorld;
using Verse;
using Honor.Rules;

namespace Honor.Core;

[HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
internal static class InsultPatch
{
    private static void Postfix(Pawn ___pawn, Pawn recipient, InteractionDef intDef, bool __result)
    {
        if (__result && intDef == InteractionDefOf.Insult && HonorMod.PatchesReady)
            recipient.Map?.GetComponent<DuelMapComponent>().RegisterInsult(___pawn, recipient);
    }
}

[HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.CheckSocialFightStart))]
internal static class SocialFightPatch
{
    private static bool Prefix(Pawn ___pawn, Pawn initiator, InteractionDef interaction, ref bool __result)
    {
        if (interaction != InteractionDefOf.Insult ||
            ___pawn.Map?.GetComponent<DuelMapComponent>().CanPair(___pawn, initiator) != true) return true;
        __result = false; return false;
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
internal static class DuelDamageGuard
{
    private static bool Prefix(Pawn __instance, ref DamageInfo dinfo, out bool absorbed)
    {
        absorbed = false;
        var manager = __instance.Map?.GetComponent<DuelMapComponent>();
        if (manager == null || dinfo.Instigator is not Pawn attacker) return true;
        var duel = manager.RecentFor(attacker);
        if (duel == null) return true;
        // Prevent collateral, attacks during gathering and late in-flight bullets after the result.
        if (!duel.Active || duel.Phase != DuelPhase.Fighting || duel.Other(attacker) != __instance ||
            (duel.Mode != DuelMode.Death && __instance.Downed))
        { absorbed = true; return false; }
        return true;
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
internal static class DuelDamageResult
{
    private static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
    {
        var manager = (__instance.Map ?? (dinfo.Instigator as Pawn)?.Map)?.GetComponent<DuelMapComponent>();
        var duel = manager?.ActiveFor(__instance);
        if (duel == null || totalDamageDealt <= 0) return;
        if (__instance.Dead && dinfo.Instigator is Pawn killer && duel.Mode != DuelMode.Death)
            manager.Dishonor(duel, killer);
        if (dinfo.Instigator != duel.Other(__instance)) manager.Finish(duel, null, "outside damage");
        else manager.CheckEnd(duel);
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
internal static class HonorGizmos
{
    private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        foreach (var gizmo in __result) yield return gizmo;
        var pawn = __instance;
        if (pawn.Map == null || !DuelMapComponent.Eligible(pawn)) yield break;
        var manager = pawn.Map.GetComponent<DuelMapComponent>();
        yield return new Command_Action {
            defaultLabel = "Honor_Arena".Translate(), defaultDesc = "Honor_ArenaDesc".Translate(),
            action = () => Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), target => {
                if (manager.Duels.Any(d => d.Active)) return;
                manager.Arena = target.Cell;
                Messages.Message("Honor_ArenaSet".Translate(), MessageTypeDefOf.NeutralEvent);
            })
        };
        var pending = manager.Duels.FirstOrDefault(d => d.Phase == DuelPhase.Pending && d.Contains(pawn));
        if (pending != null)
            yield return new Command_Action { defaultLabel = "Honor_Begin".Translate(), defaultDesc = "Honor_BeginDesc".Translate(),
                action = () => { if (!manager.TryBegin(pending)) Messages.Message("Honor_NotReady".Translate(), pawn, MessageTypeDefOf.RejectInput); } };
        var active = manager.ActiveFor(pawn);
        if (active != null)
            yield return new Command_Action { defaultLabel = "Honor_Abort".Translate(), defaultDesc = "Honor_AbortDesc".Translate(),
                action = () => manager.Finish(active, null, "player safety stop") };
        if (Prefs.DevMode)
            yield return new Command_Action { defaultLabel = "Honor_DevInsult".Translate(), defaultDesc = "Honor_DevInsultDesc".Translate(),
                action = () => Find.Targeter.BeginTargeting(TargetingParameters.ForPawns(), target => {
                    if (target.Pawn != null && manager.CanRegister(pawn, target.Pawn)) manager.RegisterInsult(target.Pawn, pawn);
                }) };
    }
}
