using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

internal static class StockpileMapEvidence
{
    internal static (List<Zone> References, NeutralMapStockpile[] Evidence) Capture(Map map)
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
        if (zones == null || zones.Count > 1000 || zones.Any(z => z?.GetType() != typeof(Zone_Stockpile) || z.zoneManager != map.zoneManager) ||
            zones.Distinct().Count() != zones.Count || zones.Select(z => z.GetUniqueLoadID()).Distinct().Count() != zones.Count)
            throw new ResidentMoveBlocked("RE_StockpileMapUnavailable");
        var baseLabel = AccessTools.Field(typeof(Zone), "baseLabel");
        if (baseLabel == null || baseLabel.IsStatic || baseLabel.FieldType != typeof(string)) throw new ResidentMoveBlocked("RE_StockpileMapUnavailable");
        var expectedGrid = new Dictionary<IntVec3, Zone>(); var rows = new List<NeutralMapStockpile>();
        foreach (Zone_Stockpile zone in zones)
        {
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
        return (zones.OrderBy(z => z.GetUniqueLoadID(), StringComparer.Ordinal).ToList(), rows.ToArray());
    }
}
