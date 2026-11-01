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
            AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.AllPawnsUnspawned)),
            AccessTools.PropertyGetter(typeof(ListerThings), nameof(ListerThings.AllThings)),
            AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Faction)),
            AccessTools.Method(typeof(Map), nameof(Map.GetDirectlyHeldThings), []),
            AccessTools.Method(typeof(Map), nameof(Map.GetChildHolders), [typeof(List<IThingHolder>)]),
            AccessTools.Method(typeof(MinifiedThing), nameof(MinifiedThing.GetChildHolders), [typeof(List<IThingHolder>)]),
            AccessTools.Method(typeof(ThingOwnerUtility), nameof(ThingOwnerUtility.AppendThingHoldersFromThings), [typeof(List<IThingHolder>), typeof(IList<Thing>)]),
            AccessTools.Method(typeof(TransporterUtility), nameof(TransporterUtility.IncomingTransporterPreventingMapRemoval), [typeof(Map)]) })
            if (read == null || Harmony.GetPatchInfo(read)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        var method = AccessTools.Method(typeof(Settlement), nameof(WorldObject.SetFaction), [typeof(Faction)]);
        if (city?.GetType() != typeof(Settlement) || !city.HasMap || city.Map?.GetType() != typeof(Map) || city.Map.Parent != city || !Find.Maps.Contains(city.Map) ||
            city.Map.IsPlayerHome || city.Map.mapPawns?.GetType() != typeof(MapPawns) || city.Map.listerThings?.GetType() != typeof(ListerThings) || city.Map.terrainGrid == null || city.Map.roofGrid == null ||
            TransporterUtility.IncomingTransporterPreventingMapRemoval(city.Map) ||
            city.previouslyGeneratedInhabitants?.Any(p => p?.ownership?.OwnedBed != null) == true || method?.DeclaringType != typeof(WorldObject) ||
            Harmony.GetPatchInfo(method)?.Owners.Any(x => x != "honor.series.contracts.preview") == true)
            throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
    }
    private static NeutralMapThing Evidence(Thing t, Map map, bool held = false)
    {
        if (t == null || t.def == null || t.Destroyed || (held ? t.Spawned || t.Map != null || t.holdingOwner == null : !t.Spawned || t.Map != map || t.holdingOwner != null && t.holdingOwner != map.GetDirectlyHeldThings()) ||
            t.Faction != null || t is Pawn or Corpse || t is IThingHolder && t.GetType() != typeof(MinifiedThing) ||
            t.GetType().Assembly != typeof(Thing).Assembly || t is ThingWithComps c && c.AllComps.Any(comp => comp == null || comp is IThingHolder ||
                comp.GetType() != typeof(CompQuality) && comp.GetType() != typeof(CompForbiddable) && comp.GetType() != typeof(CompColorable) && comp.GetType() != typeof(CompArt) &&
                (t.GetType() != typeof(Building_Storage) || comp.GetType() != typeof(CompStyleable))) ||
            t.GetType() != typeof(Building) && t.GetType() != typeof(Building_Storage) && t.GetType() != typeof(Plant) && t.GetType() != typeof(Filth) && t.GetType() != typeof(Thing) && t.GetType() != typeof(ThingWithComps) && t.GetType() != typeof(MinifiedThing))
            throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        if (AccessTools.PropertyGetter(t.GetType(), nameof(Thing.Faction))?.DeclaringType != typeof(Thing)) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        return new(t.GetUniqueLoadID(), t.def.defName, t.Stuff?.defName, t.stackCount, t.HitPoints, held ? -1 : map.cellIndices.CellToIndex(t.Position), t.Rotation.AsInt,
            t.TryGetQuality(out var q) ? (int)q : null);
    }
    private static ThingOwner Container(MinifiedThing holder)
    {
        var method = AccessTools.Method(typeof(MinifiedThing), nameof(MinifiedThing.GetDirectlyHeldThings), []);
        if (method?.DeclaringType != typeof(MinifiedThing) || Harmony.GetPatchInfo(method)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        var container = holder.GetDirectlyHeldThings();
        if (holder.stackCount != 1 || container?.GetType() != typeof(ThingOwner<Thing>) || container.Owner != holder || container.Count != 1 || container[0]?.holdingOwner != container)
            throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        return container;
    }
    internal static LoadedMapRecord Capture(Settlement city)
    {
        Ready(city); var map = city.Map; var things = map.listerThings.AllThings.OrderBy(t => t?.GetUniqueLoadID(), StringComparer.Ordinal).ToList();
        if (things.Count > 10000 || map.Size.x is < 1 or > 500 || map.Size.z is < 1 or > 500) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        var references = new List<Thing>(); var held = new List<NeutralMapHeld>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        var storage = new List<NeutralMapStorage>();
        NeutralMapThing Collect(Thing t, string parent, int depth)
        {
            if (t == null || t is Pawn or Corpse || depth > 8 || ids.Count >= 10000 || !ids.Add(t.GetUniqueLoadID())) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
            var container = t.GetType() == typeof(MinifiedThing) ? Container((MinifiedThing)t) : null;
            // Validate the entire child chain before quality helpers can inspect a package's InnerThing.
            if (container != null) Collect(container[0], t.GetUniqueLoadID(), depth + 1);
            var row = Evidence(t, map, parent != null); references.Add(t);
            if (t.GetType() == typeof(Building_Storage)) storage.Add(StorageMapEvidence.Capture((Building_Storage)t, map));
            if (parent != null) held.Add(new(parent, 0, row));
            return row;
        }
        var evidence = things.Select(t => Collect(t, null, 0)).ToArray();
        // Validate known holders BEFORE the native recursive pawn query can visit any ship/provider/unknown component.
        var holders = new List<IThingHolder>(); map.GetChildHolders(holders);
        if (holders.Any(h => h is not MinifiedThing m || m.GetType() != typeof(MinifiedThing) || !things.Contains(m))) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        var direct = map.GetDirectlyHeldThings();
        if (direct != null && (direct.Count != things.Count || direct.Any(t => !things.Contains(t)))) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        if (map.mapPawns.AllPawns.Count != 0) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        var grid = map.AllCells.Select(c => new { Terrain = map.terrainGrid.TerrainAt(c)?.defName, Under = map.terrainGrid.UnderTerrainAt(c)?.defName, Roof = map.roofGrid.RoofAt(c)?.defName }).ToArray();
        string gridProof = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(grid))));
        string stock = CityStockPreservation.CaptureLoadedStock(city, out int format);
        if (stock == null) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        string snapshot = held.Count == 0 ? LoadedMapPreservation.Capture("territory/" + city.GetUniqueLoadID(), map.GetUniqueLoadID(), map.Size.x, map.Size.z, gridProof, stock, format, evidence)
            : LoadedMapPreservation.CapturePacked("territory/" + city.GetUniqueLoadID(), map.GetUniqueLoadID(), map.Size.x, map.Size.z, gridProof, stock, format, evidence, held);
        if (storage.Count > 0) snapshot = LoadedMapPreservation.CaptureStorage(snapshot, storage);
        return new() { Map = map, Things = references.OrderBy(t => t.GetUniqueLoadID(), StringComparer.Ordinal).ToList(), Snapshot = snapshot };
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
