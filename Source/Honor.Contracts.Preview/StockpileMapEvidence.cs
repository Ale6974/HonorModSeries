using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

internal static class StockpileMapEvidence
{
    internal static (List<Zone> References, NeutralMapStockpile[] Evidence, NeutralMapGrowing[] Growing) Capture(Map map)
    {
        foreach (var method in new[] {
            AccessTools.PropertyGetter(typeof(ZoneManager), nameof(ZoneManager.AllZones)),
            AccessTools.Method(typeof(ZoneManager), nameof(ZoneManager.ZoneAt), [typeof(IntVec3)]),
            AccessTools.Method(typeof(Zone), nameof(Zone.GetUniqueLoadID), []),
            AccessTools.PropertyGetter(typeof(Zone), nameof(Zone.Map)),
            AccessTools.PropertyGetter(typeof(Zone), nameof(Zone.Hidden)),
            AccessTools.Method(typeof(Zone_Stockpile), nameof(Zone_Stockpile.AllSlotCellsList), []),
            AccessTools.PropertyGetter(typeof(SlotGroup), nameof(SlotGroup.CellsList)),
            AccessTools.PropertyGetter(typeof(SlotGroup), "Map"),
            AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.ThingsListAt), [typeof(IntVec3)]),
            AccessTools.PropertyGetter(typeof(SlotGroup), nameof(SlotGroup.HeldThings)) })
            if (method == null || Harmony.GetPatchInfo(method)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_StockpileMapUnavailable");
        if (map.zoneManager?.GetType() != typeof(ZoneManager) || map.zoneManager.map != map) throw new ResidentMoveBlocked("RE_StockpileMapUnavailable");
        var zones = map.zoneManager.AllZones;
        if (zones == null || zones.Count > 1000 || zones.Any(z => (z?.GetType() != typeof(Zone_Stockpile) && z?.GetType() != typeof(Zone_Growing)) || z.zoneManager != map.zoneManager) ||
            zones.Distinct().Count() != zones.Count || zones.Select(z => z.GetUniqueLoadID()).Distinct().Count() != zones.Count)
            throw new ResidentMoveBlocked("RE_StockpileMapUnavailable");
        var baseLabel = AccessTools.Field(typeof(Zone), "baseLabel");
        if (baseLabel == null || baseLabel.IsStatic || baseLabel.FieldType != typeof(string)) throw new ResidentMoveBlocked("RE_StockpileMapUnavailable");
        var growing = new List<NeutralMapGrowing>();
        var plantField = AccessTools.Field(typeof(Zone_Growing), "plantDefToGrow");
        if (plantField == null || plantField.IsStatic || plantField.FieldType != typeof(ThingDef)) throw new ResidentMoveBlocked("RE_GrowingMapUnavailable");
        var expectedGrid = new Dictionary<IntVec3, Zone>(); var rows = new List<NeutralMapStockpile>();
        foreach (var original in zones)
        {
            if (original.GetType() == typeof(Zone_Growing))
            {
                var crop = (Zone_Growing)original;
                if (crop.cells == null || crop.cells.Count is < 1 or > 10000 || crop.cells.Any(c => !c.InBounds(map) || expectedGrid.ContainsKey(c)) ||
                    crop.cells.Distinct().Count() != crop.cells.Count || expectedGrid.Count + crop.cells.Count > 100000) throw new ResidentMoveBlocked("RE_GrowingMapUnavailable");
                foreach (var cell in crop.cells) expectedGrid.Add(cell, crop);
                // PlantDefToGrow initializes missing settings. Preserve its saved null without calling that getter.
                var plant = (ThingDef)plantField.GetValue(crop);
                string cropBase = (string)baseLabel.GetValue(crop); var tint = crop.color;
                if (crop.label?.Length > 10000 || cropBase?.Length > 10000 || plant != null && DefDatabase<ThingDef>.GetNamedSilentFail(plant.defName) != plant ||
                    !RuntimeCompat.IsFinite(tint.r) || !RuntimeCompat.IsFinite(tint.g) || !RuntimeCompat.IsFinite(tint.b) || !RuntimeCompat.IsFinite(tint.a)) throw new ResidentMoveBlocked("RE_GrowingMapUnavailable");
                string cropProof = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
                    crop.label, BaseLabel = cropBase, tint.r, tint.g, tint.b, tint.a, crop.Hidden, Plant = plant?.defName, crop.allowSow, crop.allowCut
                }))));
                growing.Add(new(crop.GetUniqueLoadID(), cropProof, crop.cells.Select(map.cellIndices.CellToIndex).OrderBy(i => i).ToArray()));
                continue;
            }
            var zone = (Zone_Stockpile)original;
            // Zone.Cells shuffles the original list. Read its saved backing list without that callback.
            if (zone.cells == null || zone.cells.Count is < 1 or > 10000 || zone.slotGroup?.GetType() != typeof(SlotGroup) || zone.slotGroup.parent != zone ||
                zone.cells.Any(c => !c.InBounds(map) || expectedGrid.ContainsKey(c)) || zone.cells.Distinct().Count() != zone.cells.Count || expectedGrid.Count + zone.cells.Count > 100000)
                throw new ResidentMoveBlocked("RE_StockpileMapUnavailable");
            foreach (var cell in zone.cells) expectedGrid.Add(cell, zone);
            string originalBase = (string)baseLabel.GetValue(zone);
            var color = zone.color;
            if (originalBase?.Length > 10000 || !RuntimeCompat.IsFinite(color.r) || !RuntimeCompat.IsFinite(color.g) || !RuntimeCompat.IsFinite(color.b) || !RuntimeCompat.IsFinite(color.a))
                throw new ResidentMoveBlocked("RE_StockpileMapUnavailable");
            string settings = StorageMapEvidence.Configuration(zone.settings, zone, zone.label, null);
            string config = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
                Settings = settings, BaseLabel = originalBase, color.r, color.g, color.b, color.a, zone.Hidden
            }))));
            var goods = zone.slotGroup.HeldThings.Take(10001).ToArray();
            if (goods.Length > 10000 || goods.Distinct().Count() != goods.Length || goods.Any(t => t == null || !t.Spawned || t.Map != map || !zone.cells.Contains(t.Position)))
                throw new ResidentMoveBlocked("RE_StockpileMapUnavailable");
            rows.Add(new(zone.GetUniqueLoadID(), config, zone.cells.Select(map.cellIndices.CellToIndex).OrderBy(i => i).ToArray(),
                goods.Select(t => t.GetUniqueLoadID()).OrderBy(id => id, StringComparer.Ordinal).ToArray()));
        }
        // Pair both directions of the manager's saved zones and native lookup grid; never rebuild it here.
        foreach (var cell in map.AllCells)
        {
            expectedGrid.TryGetValue(cell, out var expected);
            if (!ReferenceEquals(map.zoneManager.ZoneAt(cell), expected)) throw new ResidentMoveBlocked("RE_StockpileMapUnavailable");
        }
        return (zones.OrderBy(z => z.GetUniqueLoadID(), StringComparer.Ordinal).ToList(), rows.ToArray(), growing.ToArray());
    }
}
