using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

internal static class StorageMapEvidence
{
    private static readonly System.Reflection.FieldInfo Root = AccessTools.Field(typeof(ThingFilter), "overrideRootDef");
    internal static NeutralMapStorage Capture(Building_Storage shelf, Map map)
    {
        if (shelf?.GetType() != typeof(Building_Storage) || !shelf.Spawned || shelf.Map != map || shelf.Faction != null || shelf.storageGroup != null ||
            shelf.settings?.GetType() != typeof(StorageSettings) || shelf.settings.owner != shelf || shelf.settings.filter?.GetType() != typeof(ThingFilter) ||
            shelf.slotGroup?.GetType() != typeof(SlotGroup) || shelf.slotGroup.parent != shelf)
            throw new ResidentMoveBlocked("RE_StorageMapUnavailable");
        foreach (var method in new[] {
            AccessTools.Method(typeof(Building_Storage), nameof(Building_Storage.AllSlotCellsList), []),
            AccessTools.Method(typeof(Building_Storage), nameof(Building_Storage.AllSlotCells), []),
            AccessTools.Method(typeof(Building_Storage), nameof(Building_Storage.GetStoreSettings), []),
            AccessTools.PropertyGetter(typeof(SlotGroup), nameof(SlotGroup.CellsList)),
            AccessTools.PropertyGetter(typeof(SlotGroup), nameof(SlotGroup.HeldThings)),
            AccessTools.PropertyGetter(typeof(StorageSettings), nameof(StorageSettings.Priority)),
            AccessTools.PropertyGetter(typeof(CompStyleable), nameof(CompStyleable.SourcePrecept)),
            AccessTools.PropertyGetter(typeof(ThingFilter), nameof(ThingFilter.AllowedThingDefs)),
            AccessTools.PropertyGetter(typeof(ThingFilter), nameof(ThingFilter.AllowedHitPointsPercents)),
            AccessTools.PropertyGetter(typeof(ThingFilter), nameof(ThingFilter.AllowedQualityLevels)),
            AccessTools.PropertyGetter(typeof(ThingFilter), nameof(ThingFilter.AllowedMentalBreakChance)),
            AccessTools.PropertyGetter(typeof(ThingFilter), nameof(ThingFilter.OnlySpecialFilters)),
            AccessTools.Method(typeof(ThingFilter), nameof(ThingFilter.Allows), [typeof(SpecialThingFilterDef)]) })
            if (method == null || Harmony.GetPatchInfo(method)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_StorageMapUnavailable");
        var filter = shelf.settings.filter;
        if (Root == null || Root.IsStatic || Root.FieldType != typeof(ThingCategoryDef)) throw new ResidentMoveBlocked("RE_StorageMapUnavailable");
        var root = (ThingCategoryDef)Root.GetValue(filter);
        if (root != null && DefDatabase<ThingCategoryDef>.GetNamedSilentFail(root.defName) != root) throw new ResidentMoveBlocked("RE_StorageMapUnavailable");
        var styles = shelf.AllComps.OfType<CompStyleable>().ToArray();
        if (styles.Length > 1) throw new ResidentMoveBlocked("RE_StorageMapUnavailable");
        var style = styles.SingleOrDefault();
        var precept = style?.SourcePrecept;
        if (style != null && (style.parent != shelf || style.styleDef != null && DefDatabase<ThingStyleDef>.GetNamedSilentFail(style.styleDef.defName) != style.styleDef) ||
            precept != null && (precept.GetType() != typeof(Precept_ThingStyle) || precept.ideo == null || !Find.IdeoManager.IdeosListForReading.Contains(precept.ideo) || !precept.ideo.PreceptsListForReading.Contains(precept)))
            throw new ResidentMoveBlocked("RE_StorageMapUnavailable");
        var allowed = filter.AllowedThingDefs.Take(30001).ToArray();
        var special = DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.Take(10001).ToArray();
        var hp = filter.AllowedHitPointsPercents; var quality = filter.AllowedQualityLevels; var mental = filter.AllowedMentalBreakChance;
        if (allowed.Length > 30000 || allowed.Any(d => d == null || DefDatabase<ThingDef>.GetNamedSilentFail(d.defName) != d) || special.Length > 10000 ||
            special.Any(d => d == null) ||
            !RuntimeCompat.IsDefined(shelf.settings.Priority) || !RuntimeCompat.IsFinite(hp.min) || !RuntimeCompat.IsFinite(hp.max) || hp.min < 0 || hp.max > 1 || hp.min > hp.max ||
            !RuntimeCompat.IsFinite(mental.min) || !RuntimeCompat.IsFinite(mental.max) || mental.min < 0 || mental.max > 1 || mental.min > mental.max || (int)quality.min < 0 || (int)quality.max > 6 || quality.min > quality.max ||
            shelf.label?.Length > 10000)
            throw new ResidentMoveBlocked("RE_StorageMapUnavailable");
        string configuration = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            shelf.label, Priority = (int)shelf.settings.Priority, Allowed = allowed.Select(d => d.defName).OrderBy(s => s, StringComparer.Ordinal).ToArray(),
            HpMin = hp.min, HpMax = hp.max, QualityMin = (int)quality.min, QualityMax = (int)quality.max, MentalMin = mental.min, MentalMax = mental.max,
            filter.OnlySpecialFilters, Root = root?.defName,
            Style = style == null ? null : new { Def = style.styleDef?.defName, Precept = precept?.GetUniqueLoadID(), Ideo = precept?.ideo.GetUniqueLoadID(), style.everSeenByPlayer },
            Special = special.OrderBy(d => d.defName, StringComparer.Ordinal).Select(d => new { d.defName, Allowed = filter.Allows(d) }).ToArray()
        }))));
        var cells = shelf.slotGroup.CellsList.Take(4097).ToArray();
        if (cells.Length is < 1 or > 4096 || cells.Any(c => !c.InBounds(map)) || cells.Distinct().Count() != cells.Length) throw new ResidentMoveBlocked("RE_StorageMapUnavailable");
        var contents = shelf.slotGroup.HeldThings.Take(10001).ToArray();
        if (contents.Length > 10000 || contents.Any(t => t == null || !t.Spawned || t.Map != map || !cells.Contains(t.Position)) || contents.Distinct().Count() != contents.Length)
            throw new ResidentMoveBlocked("RE_StorageMapUnavailable");
        return new(shelf.GetUniqueLoadID(), configuration, cells.Select(map.cellIndices.CellToIndex).OrderBy(i => i).ToArray(), contents.Select(t => t.GetUniqueLoadID()).OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }
}
