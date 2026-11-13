using System.Collections;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace Honor.Contracts.Preview;

internal static class MapAssetRecords
{
    private static readonly Guid[] AuditedModules = [new("bfbab472-0f01-4378-acdf-3bd92e3a67f8"), new("6c268a95-d1a8-4d6f-ad81-e5dcf6949c0e"), new("3523c04c-c298-43ae-9571-20caaef22acc")];
    private static bool Component(Type type, bool shelf, bool container = false, bool worktable = false, bool workframe = false) => workframe && type == typeof(CompForbiddable) || worktable && WorktableEvidence.Component(type) || type == typeof(CompStyleable) || (shelf || container) && type == typeof(CompQuality) || container && (type == typeof(CompHackable) || type == typeof(CompGlower));
    private static void Native(Type type, string name)
    {
        var methods = AccessTools.GetDeclaredMethods(type).Where(m => m.Name == name).ToArray();
        if (type.Assembly != typeof(Thing).Assembly || methods.Length == 0 || methods.Any(m => Harmony.GetPatchInfo(m)?.Owners.Any() == true)) throw new ResidentMoveBlocked("RE_AssetUnavailable");
    }
    private static object Field(object owner, string name, Type expected)
    {
        var f = AccessTools.Field(owner.GetType(), name);
        if (f == null || f.FieldType != expected) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        return f.GetValue(owner);
    }
    internal static void VerifyHolderRead(Thing thing)
    {
        if (!NativeTransferBodies.ExtendedReadBodies) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        Native(typeof(QualityUtility), "TryGetQuality"); Native(typeof(CompQuality), "get_Quality");
        if (thing.GetType() == typeof(MinifiedThing)) { Native(typeof(MinifiedThing), "get_InnerThing"); Native(typeof(MinifiedThing), "GetDirectlyHeldThings"); }
        if (thing is ThingWithComps comps && (Field(thing, "compQuality", typeof(CompQuality)) != comps.AllComps.OfType<CompQuality>().SingleOrDefault() ||
            Field(thing, "compStyleable", typeof(CompStyleable)) != comps.AllComps.OfType<CompStyleable>().SingleOrDefault())) throw new ResidentMoveBlocked("RE_AssetUnavailable");
    }
    internal static bool NeedsRepairScope(Thing t, bool ownedStorage = false, bool ownedContainers = false)
    {
        if (t?.GetType() != typeof(Building) && (!NativeTransferBodies.WorkObservationBodies || t?.GetType() != typeof(Frame)) && (!ownedStorage || t?.GetType() != typeof(Building_Storage)) && (!ownedContainers || !ContainerMapEvidence.Supported(t))) return false;
        if (t.def == null) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        var getter = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MaxHitPoints));
        if (!AuditedModules.Contains(typeof(Thing).Module.ModuleVersionId) || getter == null || Harmony.GetPatchInfo(getter)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        return t.HitPoints < t.MaxHitPoints;
    }
    private static void Boundary(Thing t, Map map, bool repairAssets, bool ownedStorage, IReadOnlyCollection<Pawn> reviewedPawns, bool ownedContainers, bool heldAssets, bool ownedForest)
    {
        bool held = heldAssets && !t.Spawned && t.Map == null && t.holdingOwner != null;
        bool package = ownedForest && t.GetType() == typeof(MinifiedThing);
        bool shelf = ownedStorage && t.GetType() == typeof(Building_Storage);
        bool worktable = NativeTransferBodies.WorkObservationBodies && WorktableEvidence.Supported(t) && reviewedPawns?.Any(p => p?.jobs?.curJob != null && p.jobs.curJob.def != JobDefOf.LayDown) == true;
        if (worktable) _ = WorktableEvidence.Capture((Building_WorkTable)t);
        bool workframe = NativeTransferBodies.WorkObservationBodies && t.GetType() == typeof(Frame) && reviewedPawns?.Any(p => p?.jobs?.curJob != null && p.jobs.curJob.def != JobDefOf.LayDown) == true;
        if (workframe) _ = ConstructionEvidence.Capture((Frame)t);
        if (held && t is Building heldBuilding && (heldBuilding.def?.building == null || heldBuilding.def.building.isNaturalRock || heldBuilding.def == ThingDefOf.CaravanPackingSpot)) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        bool container = ownedContainers && ContainerMapEvidence.Supported(t);
        if ((container || held || package) && !NativeTransferBodies.ExtendedReadBodies) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        if (!AuditedModules.Contains(typeof(Thing).Module.ModuleVersionId) || (reviewedPawns == null ? map.mapPawns.AllPawns.Count != 0 : map.mapPawns.AllPawns.Count != reviewedPawns.Count || map.mapPawns.AllPawns.Any(p => !reviewedPawns.Contains(p))) || map.lordManager?.lords?.Count != 0 ||
            !package && !container && !worktable && !workframe && t.GetType() != typeof(Building) && (!ownedStorage || t.GetType() != typeof(Building_Storage)) && t.GetType() != typeof(Thing) && t.GetType() != typeof(ThingWithComps) ||
            !t.def.CanHaveFaction || t.def.GetType() != typeof(ThingDef) || DefDatabase<ThingDef>.GetNamedSilentFail(t.def.defName) != t.def ||
            t is IThingHolder && !container && !package && !workframe || t is IAttackTarget || t.questTags?.Count > 0 || (!held && (!t.Spawned || t.Map != map)) || t.Faction == null || t.Faction.IsPlayer ||
            t is ThingWithComps c && (c.AllComps.Count > (worktable ? 5 : workframe ? 3 : container ? 4 : shelf || held || package ? 2 : 1) || c.AllComps.Any(x => x == null || !Component(x.GetType(), shelf || held || package || worktable || workframe, container, worktable, workframe) || x.parent != t) || c.AllComps.Select(x => x.GetType()).Distinct().Count() != c.AllComps.Count)) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        if ((t.def.comps?.Count ?? 0) != ((t as ThingWithComps)?.AllComps.Count ?? 0)) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        foreach (var getter in new[] { AccessTools.PropertyGetter(typeof(ThingDef), nameof(ThingDef.CanHaveFaction)), AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MaxHitPoints)) })
            if (getter == null || getter.DeclaringType.Assembly != typeof(Thing).Assembly || Harmony.GetPatchInfo(getter)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        var dispatch = AccessTools.Method(t.GetType(), nameof(Thing.SetFaction), [typeof(Faction), typeof(Pawn)]);
        if (dispatch?.DeclaringType != (t is Building ? typeof(Building) : typeof(Thing)) || Harmony.GetPatchInfo(dispatch)?.Owners.Any() == true ||
            t.def.comps?.Any(c => c == null || !Component(c.compClass, shelf || held || package || worktable || workframe, container, worktable, workframe) || c.GetType().Assembly != typeof(Thing).Assembly) == true ||
            RimWorld.QuestGen.QuestGen.quest != null) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        if (package) Native(typeof(MinifiedThing), "get_InnerThing");
        if (held || package) Native(typeof(QualityUtility), "TryGetQuality");
        if ((shelf || container || held || package || workframe || worktable) && t is ThingWithComps)
        {
            var comps = ((ThingWithComps)t).AllComps;
            if (Field(t, "compQuality", typeof(CompQuality)) != comps.OfType<CompQuality>().SingleOrDefault() || Field(t, "compStyleable", typeof(CompStyleable)) != comps.OfType<CompStyleable>().SingleOrDefault()) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            Native(typeof(QualityUtility), "TryGetQuality"); Native(typeof(CompQuality), "get_Quality");
        }
        Native(typeof(Quest), "get_State"); Native(typeof(Quest), "get_TicksUntilExpiry"); Native(typeof(QuestManager), "get_QuestsListForReading");
        if (Find.QuestManager.QuestsListForReading.Any(q => q == null || q.State is QuestState.Ongoing or QuestState.NotYetAccepted)) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        Native(typeof(Thing), "SetFaction"); Native(typeof(QuestUtility), "SendQuestTargetSignals");
        var events = Field(map, "events", AccessTools.TypeByName("Verse.MapEvents"));
        if (events == null || events.GetType() != AccessTools.TypeByName("Verse.MapEvents") || Field(events, "map", typeof(Map)) != map) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        Native(events.GetType(), "Notify_ThingFactionChanged");
        foreach (string name in new[] { "ThingFactionChanged", "FactionAdded", "FactionRemoved" })
        {
            var f = AccessTools.Field(events.GetType(), name);
            if (f == null || !typeof(Delegate).IsAssignableFrom(f.FieldType) || f.GetValue(events) != null) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        }
        bool activeWork = reviewedPawns?.Any(p => p?.jobs?.curJob != null && p.jobs.curJob.def != JobDefOf.LayDown) == true;
        ResidentMapActors.RequireEmptyReservations(map, typeof(Thing).Module.ModuleVersionId, !activeWork && reviewedPawns?.Any(p => p?.jobs?.curJob != null) == true, activeWork);
        var designations = map.designationManager;
        Native(typeof(DesignationManager), "get_AllDesignations");
        if (designations?.GetType() != typeof(DesignationManager) || designations.AllDesignations.Any()) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        if (t is Building b && !held)
        {
            if (b.def.building == null || b.def.building.isNaturalRock || b.def == ThingDefOf.CaravanPackingSpot || !repairAssets && b.HitPoints != b.MaxHitPoints || map.listerBuildings?.GetType() != typeof(ListerBuildings) ||
                map.listerBuildings.allBuildingsColonist.Contains(b) || map.listerBuildings.allBuildingsNonColonist.Count(x => ReferenceEquals(x, b)) != 1 ||
                ((ICollection)Field(map.listerBuildings, "scopes", typeof(List<ListerBuildings.TrackingScope>))).Count != 0 ||
                ((ICollection)Field(map.listerBuildings, "reinstallationMap", typeof(Dictionary<Thing, Blueprint_Install>))).Count != 0) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            Native(typeof(Building), "SetFaction"); Native(typeof(ListerBuildings), "Add"); Native(typeof(ListerBuildings), "Remove");
            foreach (var pair in new[] { ("listerBuildingsRepairable", typeof(ListerBuildingsRepairable)), ("listerArtificialBuildingsForMeditation", typeof(ListerArtificialBuildingsForMeditation)), ("listerBuildingWithTagInProximity", typeof(ListerBuildingWithTagInProximity)) })
            {
                var value = Field(map, pair.Item1, pair.Item2);
                if (value?.GetType() != pair.Item2) throw new ResidentMoveBlocked("RE_AssetUnavailable");
                Native(pair.Item2, "Notify_BuildingSpawned"); Native(pair.Item2, "Notify_BuildingDeSpawned");
                var ownerMap = AccessTools.Field(pair.Item2, "map");
                if (ownerMap != null && ownerMap.GetValue(value) != map) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            }
            var repair = map.listerBuildingsRepairable;
            var lists = (Dictionary<Faction, List<Thing>>)Field(repair, "repairables", typeof(Dictionary<Faction, List<Thing>>));
            var sets = (Dictionary<Faction, HashSet<Thing>>)Field(repair, "repairablesSet", typeof(Dictionary<Faction, HashSet<Thing>>));
            if (lists.Values.Any(l => l == null) || sets.Values.Any(l => l == null)) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            if (repairAssets)
            {
                var owners = lists.Keys.Concat(sets.Keys).Distinct().ToArray();
                if (owners.Any(f => f == null || !Find.FactionManager.AllFactionsListForReading.Contains(f))) throw new ResidentMoveBlocked("RE_AssetUnavailable");
                MapAssetRepairs.Verify(b.Faction.GetUniqueLoadID(), b.HitPoints, new(b.MaxHitPoints, b.def.building.repairable), owners.Select(f =>
                    new MapAssetRepairMembership(f.GetUniqueLoadID(), lists.TryGetValue(f, out var list) ? list.Count(x => ReferenceEquals(x, b)) : 0, sets.TryGetValue(f, out var set) && set.Contains(b))));
            }
            else if (lists.Values.Any(l => l.Contains(b)) || sets.Values.Any(l => l.Contains(b))) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            Native(typeof(ListerBuildingsRepairable), "UpdateBuilding"); Native(typeof(ListerBuildingsRepairable), "ListFor"); Native(typeof(ListerBuildingsRepairable), "HashSetFor");
            Native(typeof(MeditationUtility), "CountsAsArtificialBuilding"); Native(typeof(MapDrawer), "MapMeshDirty"); Native(typeof(MapDrawer), "SectionAt"); Native(typeof(MapDrawer), "SectionCoordsAt");
            if (map.mapDrawer?.GetType() != typeof(MapDrawer) || Field(map.mapDrawer, "map", typeof(Map)) != map) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        }
    }
    internal static MapAsset Capture(Thing t, Settlement city, TerritoryTransferReference original, bool repairAssets = false, bool ownedStorage = false, IReadOnlyCollection<Pawn> reviewedPawns = null, bool ownedContainers = false, bool heldAssets = false, bool ownedForest = false, Faction independentOwner = null)
    {
        Boundary(t, city.Map, repairAssets, ownedStorage, reviewedPawns, ownedContainers, heldAssets, ownedForest);
        string owner = t.Faction.GetUniqueLoadID();
        if (original != null)
        {
            if (original.LoadedMap.Map != city.Map || !original.LoadedMap.Things.Any(x => ReferenceEquals(x, t))) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            MapAssetTransfers.Pair(original.LoadedMap.AssetJournal, original.LoadedMap.Snapshot, original.From.GetUniqueLoadID(), original.Target.GetUniqueLoadID());
            if (MapAssetTransfers.ExpectedOwner(original.LoadedMap.AssetJournal, t.GetUniqueLoadID()) != owner) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            owner = original.From.GetUniqueLoadID();
        }
        else if (t.Faction != (independentOwner??city.Faction)) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        var style = (t as ThingWithComps)?.AllComps.OfType<CompStyleable>().SingleOrDefault();
        var preceptGetter = AccessTools.PropertyGetter(typeof(CompStyleable), nameof(CompStyleable.SourcePrecept));
        if (preceptGetter == null || Harmony.GetPatchInfo(preceptGetter)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        var precept = style?.SourcePrecept;
        if (style?.styleDef != null && DefDatabase<ThingStyleDef>.GetNamedSilentFail(style.styleDef.defName) != style.styleDef ||
            precept != null && (precept.GetType() != typeof(Precept_ThingStyle) || precept.ideo == null || !Find.IdeoManager.IdeosListForReading.Contains(precept.ideo) || !precept.ideo.PreceptsListForReading.Contains(precept))) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        var paint = t is Building ? (ColorDef)Field(t, "paintColorDef", typeof(ColorDef)) : null;
        if (paint != null && DefDatabase<ColorDef>.GetNamedSilentFail(paint.defName) != paint) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        string configuration = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            Class = t.GetType().FullName, Paint = paint?.defName, ChangeTerrain = t is Building building ? (bool?)building.canChangeTerrainOnDestroyed : null,
            Style = style == null ? null : new { Def = style.styleDef?.defName, Precept = precept?.GetUniqueLoadID(), Ideo = precept?.ideo.GetUniqueLoadID(), style.everSeenByPlayer }
        }))));
        return new(t.GetUniqueLoadID(), owner, configuration) { Repair = repairAssets && t is Building b ? new(b.MaxHitPoints, b.def.building.repairable) : null };
    }
    internal static void Prepare(LoadedMapRecord r, Faction source, Faction target)
    {
        if (LoadedMapPreservation.Restore(r.Snapshot).OwnedAssets == null) return;
        if (r.AssetJournal != null || source == null || target == null || source.IsPlayer || target.IsPlayer) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        r.AssetJournal = MapAssetTransfers.Prepare(r.Snapshot, source.GetUniqueLoadID(), target.GetUniqueLoadID());
    }
    internal static void Apply(TerritoryTransferReference r)
    {
        if (r.LoadedMap?.AssetJournal == null) return;
        var game = Current.Game;
        MapAssetTransfers.Pair(r.LoadedMap.AssetJournal, r.LoadedMap.Snapshot, r.From.GetUniqueLoadID(), r.Target.GetUniqueLoadID());
        foreach (var asset in LoadedMapPreservation.Restore(r.LoadedMap.Snapshot).OwnedAssets)
        {
            if (r.CustodyAgreementV1 != null) Current.Game.GetComponent<TerritoryAdministrationLedger>().RequireCurrentCustody(r);
            LoadedMapRecords.Verify(r.City, r.LoadedMap);
            var call = MapAssetTransfers.Restore(r.LoadedMap.AssetJournal).Calls.Single(c => c.Id == asset.Id);
            if (call.Returned) continue;
            var thing = r.LoadedMap.Things.Single(t => t.GetUniqueLoadID() == asset.Id);
            if (Current.Game != game || Current.ProgramState != ProgramState.Playing || r.Target.IsPlayer || !Find.FactionManager.AllFactionsListForReading.Contains(r.Target) ||
                r.From.HostileTo(r.Target) || r.Target.HostileTo(r.From)) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            r.LoadedMap.AssetJournal = MapAssetTransfers.Begin(r.LoadedMap.AssetJournal, asset.Id, thing.Faction?.GetUniqueLoadID());
            thing.SetFaction(r.Target);
            if (Current.Game != game || Current.ProgramState != ProgramState.Playing) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            r.LoadedMap.AssetJournal = MapAssetTransfers.Returned(r.LoadedMap.AssetJournal, asset.Id, thing.Faction?.GetUniqueLoadID());
            LoadedMapRecords.Verify(r.City, r.LoadedMap);
            if(r.WorkAssetVerifiedV2!=null && !r.WorkAssetVerifiedV2.Contains(asset.Id))r.WorkAssetVerifiedV2.Add(asset.Id);
        }
        MapAssetTransfers.Pair(r.LoadedMap.AssetJournal, r.LoadedMap.Snapshot, r.From.GetUniqueLoadID(), r.Target.GetUniqueLoadID(), true);
    }
    internal static void ApplyProgressNext(TerritoryTransferReference r)
    {
        var ledger=Current.Game.GetComponent<TerritoryAdministrationLedger>();var game=Current.Game;
        ledger.RequireCurrentCustody(r);
        var map=LoadedMapPreservation.Restore(r.LoadedMap.Snapshot);
        MapAssetTransfers.Pair(r.LoadedMap.AssetJournal,r.LoadedMap.Snapshot,r.From.GetUniqueLoadID(),r.Target.GetUniqueLoadID());
        var call=MapAssetTransfers.Restore(r.LoadedMap.AssetJournal).Calls.First(c=>!c.Returned);
        if(call.Started)throw new ArgumentException("Unknown original asset call never replays.");
        var asset=map.OwnedAssets.Single(a=>a.Id==call.Id);var row=map.Things.SingleOrDefault(t=>t.Id==call.Id);
        var thing=r.LoadedMap.Things.SingleOrDefault(t=>t!=null&&t.GetUniqueLoadID()==call.Id);
        if(row==null || thing==null || thing.GetType()!=typeof(Thing) && thing.GetType()!=typeof(ThingWithComps) && thing.GetType()!=typeof(Building) ||
            thing is IThingHolder || thing.Faction!=r.From || thing.Destroyed || !thing.Spawned || thing.Map!=r.City.Map || thing.def.defName!=row.Def || thing.Stuff?.defName!=row.Stuff ||
            thing.stackCount!=row.Count || thing.HitPoints!=row.HitPoints || thing.Rotation.AsInt!=row.Rotation || r.City.Map.cellIndices.CellToIndex(thing.Position)!=row.Cell)
            throw new ArgumentException("Same unchanged original basic pending asset required.");
        var peers=(r.LoadedMap.MapResidents??[]).Concat(r.LoadedMap.Visitors??[]).ToArray();
        if(Capture(thing,r.City,r,map.AssetRepairV1==1,reviewedPawns:peers)!=asset)throw new ArgumentException("Original pending asset configuration/rights changed.");
        ledger.RequireCurrentCustody(r);
        r.LoadedMap.AssetJournal=MapAssetTransfers.Begin(r.LoadedMap.AssetJournal,call.Id,thing.Faction.GetUniqueLoadID());
        thing.SetFaction(r.Target);
        if(Current.Game!=game || Current.ProgramState!=ProgramState.Playing)throw new ArgumentException("Changed game during original asset call.");
        r.LoadedMap.AssetJournal=MapAssetTransfers.Returned(r.LoadedMap.AssetJournal,call.Id,thing.Faction?.GetUniqueLoadID());
        if(thing.Destroyed || !thing.Spawned || thing.Map!=r.City.Map || thing.def.defName!=row.Def || thing.Stuff?.defName!=row.Stuff || thing.stackCount!=row.Count ||
            thing.HitPoints!=row.HitPoints || thing.Rotation.AsInt!=row.Rotation || r.City.Map.cellIndices.CellToIndex(thing.Position)!=row.Cell ||
            Capture(thing,r.City,r,map.AssetRepairV1==1,reviewedPawns:peers)!=asset)throw new ArgumentException("Original asset returned but preservation verification failed.");
        if(!r.WorkAssetVerifiedV2.Contains(asset.Id))r.WorkAssetVerifiedV2.Add(asset.Id);
    }
}
