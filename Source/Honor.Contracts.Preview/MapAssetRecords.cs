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
    private static readonly Guid AuditedModule = new("bfbab472-0f01-4378-acdf-3bd92e3a67f8");
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
    private static void Boundary(Thing t, Map map)
    {
        if (typeof(Thing).Module.ModuleVersionId != AuditedModule || map.mapPawns.AllPawns.Count != 0 || map.lordManager?.lords?.Count != 0 ||
            t.GetType() != typeof(Building) && t.GetType() != typeof(Thing) && t.GetType() != typeof(ThingWithComps) ||
            !t.def.CanHaveFaction || t.def.GetType() != typeof(ThingDef) || DefDatabase<ThingDef>.GetNamedSilentFail(t.def.defName) != t.def ||
            t is IThingHolder || t is IAttackTarget || t.questTags?.Count > 0 || !t.Spawned || t.Map != map || t.Faction == null || t.Faction.IsPlayer ||
            t is ThingWithComps c && (c.AllComps.Count > 1 || c.AllComps.Any(x => x?.GetType() != typeof(CompStyleable) || x.parent != t))) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        if ((t.def.comps?.Count ?? 0) != ((t as ThingWithComps)?.AllComps.Count ?? 0)) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        foreach (var getter in new[] { AccessTools.PropertyGetter(typeof(ThingDef), nameof(ThingDef.CanHaveFaction)), AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MaxHitPoints)) })
            if (getter == null || getter.DeclaringType.Assembly != typeof(Thing).Assembly || Harmony.GetPatchInfo(getter)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        var dispatch = AccessTools.Method(t.GetType(), nameof(Thing.SetFaction), [typeof(Faction), typeof(Pawn)]);
        if (dispatch?.DeclaringType != (t is Building ? typeof(Building) : typeof(Thing)) || Harmony.GetPatchInfo(dispatch)?.Owners.Any() == true ||
            t.def.comps?.Any(c => c == null || c.compClass != typeof(CompStyleable) || c.GetType().Assembly != typeof(Thing).Assembly) == true ||
            RimWorld.QuestGen.QuestGen.quest != null) throw new ResidentMoveBlocked("RE_AssetUnavailable");
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
        ResidentMapActors.RequireEmptyReservations(map, AuditedModule);
        var designations = map.designationManager;
        Native(typeof(DesignationManager), "get_AllDesignations");
        if (designations?.GetType() != typeof(DesignationManager) || designations.AllDesignations.Any()) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        if (t is Building b)
        {
            if (b.def.building == null || b.def.building.isNaturalRock || b.def == ThingDefOf.CaravanPackingSpot || b.HitPoints != b.MaxHitPoints || map.listerBuildings?.GetType() != typeof(ListerBuildings) ||
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
            if (lists.Values.Any(l => l == null || l.Contains(b)) || sets.Values.Any(l => l == null || l.Contains(b))) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            Native(typeof(ListerBuildingsRepairable), "UpdateBuilding"); Native(typeof(ListerBuildingsRepairable), "ListFor"); Native(typeof(ListerBuildingsRepairable), "HashSetFor");
            Native(typeof(MeditationUtility), "CountsAsArtificialBuilding"); Native(typeof(MapDrawer), "MapMeshDirty"); Native(typeof(MapDrawer), "SectionAt"); Native(typeof(MapDrawer), "SectionCoordsAt");
            if (map.mapDrawer?.GetType() != typeof(MapDrawer) || Field(map.mapDrawer, "map", typeof(Map)) != map) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        }
    }
    internal static MapAsset Capture(Thing t, Settlement city, TerritoryTransferReference original)
    {
        Boundary(t, city.Map);
        string owner = t.Faction.GetUniqueLoadID();
        if (original != null)
        {
            if (original.LoadedMap.Map != city.Map || !original.LoadedMap.Things.Any(x => ReferenceEquals(x, t))) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            MapAssetTransfers.Pair(original.LoadedMap.AssetJournal, original.LoadedMap.Snapshot, original.From.GetUniqueLoadID(), original.Target.GetUniqueLoadID());
            if (MapAssetTransfers.ExpectedOwner(original.LoadedMap.AssetJournal, t.GetUniqueLoadID()) != owner) throw new ResidentMoveBlocked("RE_AssetUnavailable");
            owner = original.From.GetUniqueLoadID();
        }
        else if (t.Faction != city.Faction) throw new ResidentMoveBlocked("RE_AssetUnavailable");
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
        return new(t.GetUniqueLoadID(), owner, configuration);
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
        }
        MapAssetTransfers.Pair(r.LoadedMap.AssetJournal, r.LoadedMap.Snapshot, r.From.GetUniqueLoadID(), r.Target.GetUniqueLoadID(), true);
    }
}
