using HarmonyLib;
using Honor.Core;
using Honor.Rules;
using RimWorld;
using Verse;

namespace Honor.Nobility;

public sealed class NobilityMod : Mod
{
    public NobilityMod(ModContentPack content) : base(content)
    {
        if (!ModsConfig.RoyaltyActive) { Log.Error("[Honor.Nobility] Royalty DLC is required; disabled."); return; }
        HonorApi.SensitivityModifiers.Add(Multiplier);
        HonorApi.PendingDelayTicks = Math.Max(HonorApi.PendingDelayTicks, 600);
        new Harmony("honor.series.nobility").PatchAll();
    }
    public static float Multiplier(Pawn pawn)
    { int rank = pawn?.royalty?.MostSeniorTitle?.def.seniority ?? -1; return rank < 0 ? 1 : 1.1f + Math.Min(rank, 10) * .05f; }
    public static bool Appoint(DuelRecord duel, Pawn principal, Pawn champion) => principal.royalty?.MostSeniorTitle != null &&
        champion != principal && champion.Faction == principal.Faction && principal.Map?.GetComponent<DuelMapComponent>().AssignProxy(duel, principal, champion) == true;
}
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
internal static class ProxyCommands
{
    static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        foreach (var g in __result) yield return g;
        var p = __instance;
        if (p.Map == null || p.Faction != Faction.OfPlayer || p.royalty?.MostSeniorTitle == null) yield break;
        var duel = p.Map.GetComponent<DuelMapComponent>().Duels.FirstOrDefault(d => d.Phase == DuelPhase.Pending && d.Involves(p));
        if (duel == null) yield break;
        yield return new Command_Action { defaultLabel = "HN_Proxy".Translate(), defaultDesc = "HN_Desc".Translate(), action = () => {
            var options = p.Map.mapPawns.FreeColonistsSpawned.Where(c => c != p && DuelMapComponent.Eligible(c) && !duel.Involves(c))
                .Select(c => new FloatMenuOption(c.LabelShort, () => {
                    if (!NobilityMod.Appoint(duel, p, c)) Messages.Message("HN_Failed".Translate(), MessageTypeDefOf.RejectInput);
                })).ToList();
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
            else Messages.Message("HN_Failed".Translate(), MessageTypeDefOf.RejectInput);
        } };
    }
}
