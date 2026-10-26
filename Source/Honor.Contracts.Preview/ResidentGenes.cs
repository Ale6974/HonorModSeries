using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

/// <summary>Read original native tracker/lists; no gene APIs that add/remove/re-express or duplicate Deep saving.</summary>
internal static class ResidentGenes
{
    private static readonly System.Reflection.FieldInfo Xenotype = AccessTools.Field(typeof(Pawn_GeneTracker), "xenotype");
    private static readonly System.Reflection.FieldInfo Cache = AccessTools.Field(typeof(Pawn_GeneTracker), "cachedGenes");
    internal static string Proof(Pawn p)
    {
        if (p.genes == null) return ResidentGenetics.None;
        if (!ModsConfig.BiotechActive || p.genes.GetType() != typeof(Pawn_GeneTracker) || p.genes.pawn != p ||
            Xenotype == null || Xenotype.IsStatic || Xenotype.FieldType != typeof(XenotypeDef) ||
            Cache == null || Cache.IsStatic || Cache.FieldType != typeof(List<Gene>))
            throw new ResidentMoveBlocked("RE_GenesUnavailable");
        // Verify the base faction/need-component boundaries before allowing previously excluded genetic residents.
        foreach (var method in new[] {
            AccessTools.Method(typeof(Pawn), nameof(Pawn.SetFaction), [typeof(Faction), typeof(Pawn)]),
            AccessTools.Method(typeof(PawnComponentsUtility), nameof(PawnComponentsUtility.AddAndRemoveDynamicComponents), [typeof(Pawn), typeof(bool)]),
            AccessTools.Method(typeof(Pawn_NeedsTracker), nameof(Pawn_NeedsTracker.AddOrRemoveNeedsAsAppropriate), []),
            AccessTools.Method(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.EnablesNeed), [typeof(NeedDef)]),
            AccessTools.Method(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.DisablesNeed), [typeof(NeedDef)]),
            AccessTools.Method(typeof(Gene), nameof(Gene.GetUniqueLoadID), []),
            AccessTools.PropertyGetter(typeof(Gene), nameof(Gene.Active)),
            AccessTools.PropertyGetter(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.GenesListForReading)),
            AccessTools.PropertyGetter(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.Endogenes)),
            AccessTools.PropertyGetter(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.Xenogenes)) })
            if (method == null || Harmony.GetPatchInfo(method)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_GenesUnavailable");
        if (p.genes.Endogenes == null || p.genes.Xenogenes == null || p.genes.Endogenes.Count > 256 || p.genes.Xenogenes.Count > 256 ||
            p.genes.Endogenes.Count + p.genes.Xenogenes.Count > 256)
            throw new ResidentMoveBlocked("RE_GenesUnavailable");
        var original = (XenotypeDef)Xenotype.GetValue(p.genes);
        if (original != null && (original.GetType() != typeof(XenotypeDef) || DefDatabase<XenotypeDef>.GetNamedSilentFail(original.defName) != original) ||
            p.genes.iconDef != null && (p.genes.iconDef.GetType() != typeof(XenotypeIconDef) || DefDatabase<XenotypeIconDef>.GetNamedSilentFail(p.genes.iconDef.defName) != p.genes.iconDef))
            throw new ResidentMoveBlocked("RE_GenesUnavailable");
        var all = p.genes.Endogenes.Concat(p.genes.Xenogenes).ToArray();
        // A null cache may be materialized by native needs handling. Never populate/repair it for inspection.
        if (Cache.GetValue(p.genes) is List<Gene> cached && !cached.SequenceEqual(p.genes.Xenogenes.Concat(p.genes.Endogenes)))
            throw new ResidentMoveBlocked("RE_GenesUnavailable");
        if (all.Any(g => g == null || g.GetType() != typeof(Gene) || g.pawn != p || g.loadID < 0 || g.def == null ||
                g.def.GetType() != typeof(GeneDef) || g.def.geneClass != typeof(Gene) || DefDatabase<GeneDef>.GetNamedSilentFail(g.def.defName) != g.def ||
                g.overriddenByGene != null && !all.Contains(g.overriddenByGene)))
            throw new ResidentMoveBlocked("RE_GenesUnavailable");
        var rows = p.genes.Endogenes.Select(g => Describe(g, false)).Concat(p.genes.Xenogenes.Select(g => Describe(g, true)));
        try { return ResidentGenetics.Capture(p.GetUniqueLoadID(), original?.defName, p.genes.xenotypeName, p.genes.iconDef?.defName, p.genes.hybrid, rows); }
        catch (ArgumentException) { throw new ResidentMoveBlocked("RE_GenesUnavailable"); }
    }
    private static ResidentGene Describe(Gene g, bool xeno) => new(g.GetUniqueLoadID(), g.def.defName, xeno, g.overriddenByGene?.GetUniqueLoadID(),
        g.passionPreAdd.HasValue ? (int)g.passionPreAdd.Value : null);
}
