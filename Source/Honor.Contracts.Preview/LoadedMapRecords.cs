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
    public List<StorageGroup> Groups;
    public List<Zone> Stockpiles;
    public List<Zone> Growing;
    public List<Pawn> MapResidents;
    public List<Pawn> Visitors;
    public List<MapVisitorReference> BystanderReferencesV1;
    public string AssetJournal;
    public string Snapshot;
    public void ExposeData()
    {
        Scribe_References.Look(ref Map, "originalMap"); Scribe_Collections.Look(ref Things, "originalMapThings", LookMode.Reference);
        Scribe_Collections.Look(ref Groups, "originalStorageGroups", LookMode.Reference);
        Scribe_Collections.Look(ref Stockpiles, "originalStockpileZones", LookMode.Reference);
        Scribe_Collections.Look(ref Growing, "originalGrowingZones", LookMode.Reference);
        Scribe_Collections.Look(ref MapResidents, "originalNativeMapResidentsV1", LookMode.Reference);
        Scribe_Collections.Look(ref Visitors, "originalMapBystandersV1", LookMode.Reference);
        Scribe_Collections.Look(ref BystanderReferencesV1, "originalReadOnlyBystanderReferencesV1", LookMode.Deep);
        Scribe_Values.Look(ref AssetJournal, "originalOwnedAssetCallsV1");
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
    private static NeutralMapThing Evidence(Thing t, Map map, bool held = false, bool owned = false)
    {
        if (t == null || t.def == null || t.Destroyed || (held ? t.Spawned || t.Map != null || t.holdingOwner == null : !t.Spawned || t.Map != map || t.holdingOwner != null && t.holdingOwner != map.GetDirectlyHeldThings()) ||
            t.Faction != null && !owned || t is Pawn or Corpse || t is IThingHolder && t.GetType() != typeof(MinifiedThing) && !ContainerMapEvidence.Supported(t) ||
            t.GetType().Assembly != typeof(Thing).Assembly || t is ThingWithComps c && c.AllComps.Any(comp => comp == null || comp is IThingHolder ||
                comp.GetType() != typeof(CompQuality) && comp.GetType() != typeof(CompForbiddable) && comp.GetType() != typeof(CompColorable) && comp.GetType() != typeof(CompArt) &&
                (t.GetType() != typeof(Building_Storage) && !owned || comp.GetType() != typeof(CompStyleable)) &&
                (!ContainerMapEvidence.Supported(t) || comp.GetType() != typeof(CompHackable) && comp.GetType() != typeof(CompGlower))) ||
            !ContainerMapEvidence.Supported(t) && t.GetType() != typeof(Building) && t.GetType() != typeof(Building_Storage) && t.GetType() != typeof(Plant) && t.GetType() != typeof(Filth) && t.GetType() != typeof(Thing) && t.GetType() != typeof(ThingWithComps) && t.GetType() != typeof(MinifiedThing))
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
        var assetOriginal = Current.Game.GetComponent<TerritoryAdministrationLedger>().MapAssetOriginal(city);
        var ownedRows = new List<MapAsset>();
        var originalAssets = assetOriginal == null ? null : LoadedMapPreservation.Restore(assetOriginal.LoadedMap.Snapshot).OwnedAssets;
        bool heldAssets = assetOriginal != null && LoadedMapPreservation.Restore(assetOriginal.LoadedMap.Snapshot).HeldAssetsV1 == 1;
        bool ownedContainers = assetOriginal != null ? LoadedMapPreservation.Restore(assetOriginal.LoadedMap.Snapshot).OwnedContainersV1 == 1 :
            things.Any(t => ContainerMapEvidence.Supported(t) && t.Faction != null);
        bool ownedStorage = assetOriginal != null ? LoadedMapPreservation.Restore(assetOriginal.LoadedMap.Snapshot).OwnedStorageV1 == 1 :
            things.Any(t => t?.GetType() == typeof(Building_Storage) && t.Faction != null);
        bool repairAssets = assetOriginal != null ? LoadedMapPreservation.Restore(assetOriginal.LoadedMap.Snapshot).AssetRepairV1 == 1 :
            things.Any(t => t?.Faction != null && MapAssetRecords.NeedsRepairScope(t, ownedStorage, ownedContainers));
        var allActors = things.OfType<Pawn>().OrderBy(p => p.GetUniqueLoadID(), StringComparer.Ordinal).ToList();
        var actors = allActors.Where(p => city.previouslyGeneratedInhabitants?.Contains(p) == true).ToList();
        var visitors = allActors.Where(p => !actors.Contains(p)).ToList();
        bool civicAssets = assetOriginal != null ? LoadedMapPreservation.Restore(assetOriginal.LoadedMap.Snapshot).CivicAssetsV1 == 1 :
            actors.Count > 0 && things.Any(t => t is not Pawn && t.Faction != null);
        if (actors.Count > 128 || actors.Any(p => !p.Spawned || p.Map != map || city.previouslyGeneratedInhabitants == null || city.previouslyGeneratedInhabitants.Count(x => ReferenceEquals(x, p)) != 1)) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        var originalResidents = actors.Count == 0 ? null : Current.Game.GetComponent<TerritoryAdministrationLedger>().MapResidentOriginal(city);
        var actorRows = actors.Select(p => ResidentExecution.MapActor(p, originalResidents)).ToArray();
        if (visitors.Count > 128) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        var visitorRows = visitors.Select(ResidentMapVisitors.Capture).ToArray();
        var rootThings = things.Where(t => t is not Pawn).ToList();
        var references = new List<Thing>(); var held = new List<NeutralMapHeld>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        var storage = new List<NeutralMapStorage>(); var containers = new List<NeutralMapContainer>();
        NeutralMapThing Collect(Thing t, string parent, int depth, int slot = 0)
        {
            if (t == null || t is Pawn or Corpse || depth > 8 || ids.Count >= 10000 || !ids.Add(t.GetUniqueLoadID())) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
            bool owned = t.Faction != null || originalAssets?.Any(a => a.Id == t.GetUniqueLoadID()) == true;
            if (parent != null && owned && ContainerMapEvidence.Supported(t)) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            ThingOwner container;
            if (ContainerMapEvidence.Supported(t)) { var captured = owned && ownedContainers ? ContainerMapEvidence.CaptureOwned(t) : ContainerMapEvidence.Capture(t); container = captured.Owner; containers.Add(captured.Evidence); }
            else container = t.GetType() == typeof(MinifiedThing) ? Container((MinifiedThing)t) : null;
            // Validate the entire child chain before quality helpers can inspect a package's InnerThing.
            if (container != null) for (int i = 0; i < container.Count; i++) Collect(container[i], t.GetUniqueLoadID(), depth + 1, i);
            if (owned)
            {
                if (assetOriginal == null && allActors.Count > 0) civicAssets = true;
                if (parent != null && !heldAssets && assetOriginal != null || allActors.Count > 0 && !civicAssets) throw new ResidentMoveBlocked("RE_AssetUnavailable");
                if (parent != null) heldAssets = true;
                ownedRows.Add(MapAssetRecords.Capture(t, city, assetOriginal, repairAssets, ownedStorage, civicAssets ? allActors : null, ownedContainers, heldAssets));
            }
            var row = Evidence(t, map, parent != null, owned); references.Add(t);
            if (t.GetType() == typeof(Building_Storage)) storage.Add(owned ? StorageMapEvidence.CaptureOwned((Building_Storage)t, map, grouped: true) : StorageMapEvidence.Capture((Building_Storage)t, map, grouped: true));
            if (parent != null) held.Add(new(parent, slot, row));
            return row;
        }
        var evidence = rootThings.Select(t => Collect(t, null, 0)).ToArray();
        var groups = things.OfType<Building_Storage>().Select(s => s.storageGroup).Where(g => g != null).Distinct().ToArray();
        var groupEvidence = groups.Select(g => StorageMapEvidence.CaptureGroup(g, map, things)).ToArray();
        if (groups.Length > 0 && (map.storageGroups.StorageGroupsForReading.Count != groups.Length ||
            groups.Any(g => !g.members.Cast<Building_Storage>().OrderBy(s => s.GetUniqueLoadID(), StringComparer.Ordinal).SequenceEqual(
                things.OfType<Building_Storage>().Where(s => s.storageGroup == g).OrderBy(s => s.GetUniqueLoadID(), StringComparer.Ordinal)))))
            throw new ResidentMoveBlocked("RE_StorageMapUnavailable");
        var stockpiles = StockpileMapEvidence.Capture(map);
        // Validate known holders BEFORE the native recursive pawn query can visit any ship/provider/unknown component.
        var holders = new List<IThingHolder>(); map.GetChildHolders(holders);
        if (holders.Any(h => h is not Thing t || !things.Contains(t) || t.GetType() != typeof(MinifiedThing) && !ContainerMapEvidence.Supported(t) && (t is not Pawn p || !allActors.Contains(p)))) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        var direct = map.GetDirectlyHeldThings();
        if (direct != null && (direct.Count != things.Count || direct.Any(t => !things.Contains(t)))) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        if (map.mapPawns.AllPawns.Count != allActors.Count || map.mapPawns.AllPawns.Any(p => !allActors.Contains(p))) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        var grid = map.AllCells.Select(c => new { Terrain = map.terrainGrid.TerrainAt(c)?.defName, Under = map.terrainGrid.UnderTerrainAt(c)?.defName, Roof = map.roofGrid.RoofAt(c)?.defName }).ToArray();
        string gridProof = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(grid))));
        string stock = CityStockPreservation.CaptureLoadedStock(city, out int format);
        if (stock == null) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        string snapshot;
        if (containers.Count > 0)
        {
            string roots = LoadedMapPreservation.Capture("territory/" + city.GetUniqueLoadID(), map.GetUniqueLoadID(), map.Size.x, map.Size.z, gridProof, stock, format, evidence);
            snapshot = LoadedMapPreservation.CaptureContainers(roots, held, containers);
        }
        else snapshot = held.Count == 0 ? LoadedMapPreservation.Capture("territory/" + city.GetUniqueLoadID(), map.GetUniqueLoadID(), map.Size.x, map.Size.z, gridProof, stock, format, evidence)
            : LoadedMapPreservation.CapturePacked("territory/" + city.GetUniqueLoadID(), map.GetUniqueLoadID(), map.Size.x, map.Size.z, gridProof, stock, format, evidence, held);
        if (storage.Count > 0) snapshot = LoadedMapPreservation.CaptureStorage(snapshot, storage);
        if (groups.Length > 0) snapshot = LoadedMapPreservation.CaptureGroups(snapshot, groupEvidence);
        if (stockpiles.Evidence.Length > 0) snapshot = LoadedMapPreservation.CaptureStockpiles(snapshot, stockpiles.Evidence);
        if (stockpiles.Growing.Length > 0) snapshot = LoadedMapPreservation.CaptureGrowing(snapshot, stockpiles.Growing);
        if (actors.Count > 0) snapshot = LoadedMapPreservation.CaptureResidents(snapshot, actorRows, visitorRows);
        else if (visitors.Count > 0) snapshot = LoadedMapPreservation.CaptureBystanders(snapshot, visitorRows);
        if (ownedRows.Count > 0) snapshot = heldAssets ? LoadedMapPreservation.CaptureHeldAssets(snapshot, ownedRows, repairAssets, ownedStorage, ownedContainers) : ownedContainers ? LoadedMapPreservation.CaptureOwnedContainers(snapshot, ownedRows, repairAssets, ownedStorage) : civicAssets ? LoadedMapPreservation.CaptureCivicAssets(snapshot, ownedRows, repairAssets, ownedStorage) : ownedStorage ? LoadedMapPreservation.CaptureOwnedStorage(snapshot, ownedRows, repairAssets) : repairAssets ? LoadedMapPreservation.CaptureRepairAssets(snapshot, ownedRows) : LoadedMapPreservation.CaptureAssets(snapshot, ownedRows);
        var visitorReferences = actors.Count == 0 && visitors.Count > 0 ? visitors.Select(ResidentMapVisitors.References).ToList() : null;
        if (visitorReferences != null)
        {
            var custodyIds = new HashSet<string>(ids, StringComparer.Ordinal);
            foreach (var visitor in visitorReferences)
            {
                if (!custodyIds.Add(visitor.Pawn.GetUniqueLoadID())) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
                foreach (var item in visitor.Inventory.Concat(visitor.Equipment).Concat(visitor.Apparel))
                    if (!custodyIds.Add(item.GetUniqueLoadID()) || custodyIds.Count > 10000) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
            }
        }
        return new() { BystanderReferencesV1 = visitorReferences, Map = map, MapResidents = actors.Count == 0 ? null : actors, Visitors = visitors.Count == 0 ? null : visitors, Things = references.OrderBy(t => t.GetUniqueLoadID(), StringComparer.Ordinal).ToList(),
            Groups = groups.Length == 0 ? null : groups.OrderBy(g => g.GetUniqueLoadID(), StringComparer.Ordinal).ToList(),
            Stockpiles = stockpiles.Evidence.Length == 0 ? null : stockpiles.References.Where(z => z.GetType() == typeof(Zone_Stockpile)).ToList(),
            Growing = stockpiles.Growing.Length == 0 ? null : stockpiles.References.Where(z => z.GetType() == typeof(Zone_Growing)).ToList(), Snapshot = snapshot };
    }
    internal static void Verify(Settlement city, LoadedMapRecord original)
    {
        if (original == null || original.Map != city.Map || original.Things == null) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        var fresh = Capture(city);
        if (LoadedMapPreservation.Restore(original.Snapshot).ReadOnlyBystandersV1 == 1)
        {
            if (original.BystanderReferencesV1 == null || original.Visitors == null || original.BystanderReferencesV1.Count != original.Visitors.Count) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
            for (int i = 0; i < original.Visitors.Count; i++) ResidentMapVisitors.VerifyReferences(original.BystanderReferencesV1[i], original.Visitors[i]);
        }
        else if (original.BystanderReferencesV1 != null) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        if (fresh.Snapshot != original.Snapshot || !fresh.Things.SequenceEqual(original.Things)) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
        if (fresh.Groups == null ? original.Groups != null : original.Groups == null || !fresh.Groups.SequenceEqual(original.Groups)) throw new ResidentMoveBlocked("RE_StorageMapUnavailable");
        if (fresh.MapResidents == null ? original.MapResidents != null : original.MapResidents == null || !fresh.MapResidents.SequenceEqual(original.MapResidents)) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        if (fresh.Visitors == null ? original.Visitors != null : original.Visitors == null || !fresh.Visitors.SequenceEqual(original.Visitors)) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        if (fresh.Stockpiles == null ? original.Stockpiles != null : original.Stockpiles == null || !fresh.Stockpiles.SequenceEqual(original.Stockpiles)) throw new ResidentMoveBlocked("RE_StockpileMapUnavailable");
        if (fresh.Growing == null ? original.Growing != null : original.Growing == null || !fresh.Growing.SequenceEqual(original.Growing)) throw new ResidentMoveBlocked("RE_GrowingMapUnavailable");
    }
    internal static void VerifyScope(Settlement city, string scope)
    {
        if (Capture(city).Snapshot != scope) throw new ResidentMoveBlocked("RE_LoadedMapUnavailable");
    }
    internal static void Release(LoadedMapRecord r) { r.Map = null; r.Things?.Clear(); r.Groups?.Clear(); r.Stockpiles?.Clear(); r.Growing?.Clear(); r.MapResidents?.Clear(); r.Visitors?.Clear(); r.BystanderReferencesV1?.Clear(); }
}
