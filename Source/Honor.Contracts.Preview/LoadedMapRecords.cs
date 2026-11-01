using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

public sealed class LoadedMapRecord : IExposable
{
    public Map Map;
    public List<Thing> Things = [];
    public string Snapshot;
    public void ExposeData()
    {
        Scribe_References.Look(ref Map, "originalMap"); Scribe_Collections.Look(ref Things, "originalMapThings", LookMode.Reference);
        Scribe_Values.Look(ref Snapshot, "originalNeutralMapV1");
    }
}

internal static class LoadedMapRecords
{
    private static void Ready(Settlement city)
    {
        if (UnionWarCouncil.HasExternalProvider) throw new ResidentMoveBlocked("WU_Provider");
        foreach (var read in new[] {
            AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.AllPawns)),
            AccessTools.PropertyGetter(typeof(ListerThings), nameof(ListerThings.AllThings)),
            AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Faction)),
            AccessTools.Method(typeof(TransporterUtility), nameof(TransporterUtility.IncomingTransporterPreventingMapRemoval), [typeof(Map)]) })
            if (read == null || Harmony.GetPatchInfo(read)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        var method = AccessTools.Method(typeof(Settlement), nameof(WorldObject.SetFaction), [typeof(Faction)]);
        if (city?.GetType() != typeof(Settlement) || !city.HasMap || city.Map?.GetType() != typeof(Map) || city.Map.Parent != city || !Find.Maps.Contains(city.Map) ||
            city.Map.IsPlayerHome || city.Map.mapPawns?.GetType() != typeof(MapPawns) || city.Map.listerThings?.GetType() != typeof(ListerThings) || city.Map.terrainGrid == null || city.Map.roofGrid == null ||
            city.Map.mapPawns.AllPawns.Count != 0 || TransporterUtility.IncomingTransporterPreventingMapRemoval(city.Map) ||
            city.previouslyGeneratedInhabitants?.Any(p => p?.ownership?.OwnedBed != null) == true || method?.DeclaringType != typeof(WorldObject) ||
            Harmony.GetPatchInfo(method)?.Owners.Any(x => x != "honor.series.contracts.preview") == true)
            throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
    }
    private static NeutralMapThing Evidence(Thing t, Map map)
    {
        if (t == null || t.def == null || t.Destroyed || !t.Spawned || t.Map != map || t.Faction != null || t is Pawn or Corpse or MinifiedThing or IThingHolder ||
            t.GetType().Assembly != typeof(Thing).Assembly || t is ThingWithComps c && c.AllComps.Any(comp => comp == null || comp is IThingHolder ||
                comp.GetType() != typeof(CompQuality) && comp.GetType() != typeof(CompForbiddable) && comp.GetType() != typeof(CompColorable) && comp.GetType() != typeof(CompArt)) ||
            t.GetType() != typeof(Building) && t.GetType() != typeof(Plant) && t.GetType() != typeof(Filth) && t.GetType() != typeof(Thing) && t.GetType() != typeof(ThingWithComps))
            throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        if (AccessTools.PropertyGetter(t.GetType(), nameof(Thing.Faction))?.DeclaringType != typeof(Thing)) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        return new(t.GetUniqueLoadID(), t.def.defName, t.Stuff?.defName, t.stackCount, t.HitPoints, map.cellIndices.CellToIndex(t.Position), t.Rotation.AsInt,
            t.TryGetQuality(out var q) ? (int)q : null);
    }
    internal static LoadedMapRecord Capture(Settlement city)
    {
        Ready(city); var map = city.Map; var things = map.listerThings.AllThings.OrderBy(t => t?.GetUniqueLoadID(), StringComparer.Ordinal).ToList();
        if (things.Count > 10000 || map.Size.x is < 1 or > 500 || map.Size.z is < 1 or > 500) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        var evidence = things.Select(t => Evidence(t, map)).ToArray();
        var grid = map.AllCells.Select(c => new { Terrain = map.terrainGrid.TerrainAt(c)?.defName, Under = map.terrainGrid.UnderTerrainAt(c)?.defName, Roof = map.roofGrid.RoofAt(c)?.defName }).ToArray();
        string gridProof = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(grid))));
        string stock = CityStockPreservation.CaptureLoadedStock(city, out int format);
        if (stock == null) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        string snapshot = LoadedMapPreservation.Capture("territory/" + city.GetUniqueLoadID(), map.GetUniqueLoadID(), map.Size.x, map.Size.z, gridProof, stock, format, evidence);
        return new() { Map = map, Things = things, Snapshot = snapshot };
    }
    internal static void Verify(Settlement city, LoadedMapRecord original)
    {
        if (original == null || original.Map != city.Map || original.Things == null) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        var fresh = Capture(city);
        if (fresh.Snapshot != original.Snapshot || !fresh.Things.SequenceEqual(original.Things)) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
    }
    internal static void VerifyScope(Settlement city, string scope)
    {
        if (Capture(city).Snapshot != scope) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
    }
    internal static void Release(LoadedMapRecord r) { r.Map = null; r.Things?.Clear(); }
}
