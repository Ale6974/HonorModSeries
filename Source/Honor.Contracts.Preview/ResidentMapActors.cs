using System.Collections;
using System.Reflection;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

internal static class ResidentMapActors
{
    // Metadata-read installed assembly whose map faction-change body and reservation/event boundaries were audited.
    private static readonly Guid AuditedModule = new("20545414-6845-4afb-b0ca-63bce8627a4c");
    internal static ResidentMapBinding Binding(Pawn p) => new(p.GetUniqueLoadID(), p.Spawned ? p.Map.GetUniqueLoadID() : null,
        p.Spawned ? p.Map.cellIndices.CellToIndex(p.Position) : -1, p.Spawned ? p.Rotation.AsInt : 0);

    internal static void RequireHolders(Pawn p)
    {
        foreach (var pair in new (object Value, Type Type)[] { (p, typeof(Pawn)), (p.inventory, typeof(Pawn_InventoryTracker)),
            (p.carryTracker, typeof(Pawn_CarryTracker)), (p.equipment, typeof(Pawn_EquipmentTracker)), (p.apparel, typeof(Pawn_ApparelTracker)) })
        {
            if (pair.Value == null) continue;
            if (pair.Value.GetType() != pair.Type) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
            if (pair.Value != p)
            {
                var owner = AccessTools.Field(pair.Type, "pawn");
                if (owner?.FieldType != typeof(Pawn) || owner.GetValue(pair.Value) != p) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
            }
            var methods = pair.Type.GetInterfaceMap(typeof(IThingHolder)).TargetMethods;
            if (methods.Any(m => m.DeclaringType.Assembly != typeof(Pawn).Assembly || Harmony.GetPatchInfo(m)?.Owners.Any() == true))
                throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        }
    }

    private static void Unpatched(Type type, string name)
    {
        if (type?.Assembly != typeof(Pawn).Assembly) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        var methods = AccessTools.GetDeclaredMethods(type).Where(m => m.Name == name).ToArray();
        if (methods.Length == 0 || methods.Any(m => Harmony.GetPatchInfo(m)?.Owners.Any() == true)) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
    }
    private static void EmptyManager(object manager, Map expectedMap, bool destinations = false)
    {
        if (manager == null || manager.GetType().Assembly != typeof(Pawn).Assembly) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        string[] names = manager.GetType().FullName switch {
            "Verse.AI.ReservationManager" or "Verse.PhysicalInteractionReservationManager" or "Verse.AI.AttackTargetReservationManager" => ["reservations"],
            "Verse.PawnDestinationReservationManager" => ["reservedDestinations"],
            "Verse.AI.EnrouteManager" => ["enroute", "lookup"],
            _ => throw new ResidentMoveBlocked("RE_MapResidentUnavailable")
        };
        var mapField = AccessTools.Field(manager.GetType(), "map");
        if (mapField != null && (mapField.FieldType != typeof(Map) || mapField.GetValue(manager) != expectedMap)) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        Unpatched(manager.GetType(), "ReleaseAllClaimedBy");
        foreach (string name in names)
        {
            var f = AccessTools.Field(manager.GetType(), name);
            if (f == null || !f.FieldType.IsGenericType || f.FieldType.GetGenericTypeDefinition() != typeof(List<>) && f.FieldType.GetGenericTypeDefinition() != typeof(Dictionary<,>) ||
                f.GetValue(manager) is not ICollection collection) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
            if (destinations && f.Name == "reservedDestinations" && collection is IDictionary dictionary)
            {
                foreach (object value in dictionary.Values)
                {
                    var list = value == null ? null : AccessTools.Field(value.GetType(), "list");
                    if (value?.GetType().Assembly != typeof(Pawn).Assembly || list?.GetValue(value) is not ICollection entries || entries.Count != 0)
                        throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
                }
            }
            else if (collection.Count != 0) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        }
    }
    internal static void RequireEmptyReservations(Map source, Guid? auditedModule = null)
    {
        if (typeof(Pawn).Module.ModuleVersionId != (auditedModule ?? AuditedModule) || source == null) throw new ResidentMoveBlocked("RE_AssetUnavailable");
        foreach (var map in Find.Maps)
            foreach (string field in new[] { "reservationManager", "pawnDestinationReservationManager", "enrouteManager", "physicalInteractionReservationManager", "attackTargetReservationManager" })
            {
                var f = AccessTools.Field(typeof(Map), field);
                if (f == null) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
                var value = f.GetValue(map);
                if (value?.GetType() != f.FieldType) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
                EmptyManager(value, map, field == "pawnDestinationReservationManager");
            }
    }
    internal static void Require(Pawn p)
    {
        if (typeof(Pawn).Module.ModuleVersionId != AuditedModule) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        if (p?.Spawned != true || p.Map?.Parent is not Settlement city || p.Map.IsPlayerHome || !Find.Maps.Contains(p.Map) ||
            city.previouslyGeneratedInhabitants == null || city.previouslyGeneratedInhabitants.Count(x => ReferenceEquals(x, p)) != 1 ||
            p.IsPrisoner || p.IsSlave || p.Drafted || p.CurJob != null || p.jobs == null || p.jobs.jobQueue.Count != 0 || p.jobs.startingNewJob ||
            p.pather == null || p.pather.Moving || p.mindState?.Active != true || p.workSettings != null || p.trader != null || p.mindState.wantsToTradeWithColony ||
            p.ownership?.OwnedBed != null || p.ownership?.AssignedDeathrestCasket != null || p.ownership?.AssignedThrone != null ||
            p.health?.surgeryBills?.Count != 0 || p.AllComps.Any(c => c is IThingHolder) ||
            p.inventory?.innerContainer.Any(t => t is IThingHolder || t is ThingWithComps c && c.AllComps.Any(x => x is IThingHolder)) == true)
            throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        RequireHolders(p);
        if (p.jobs.GetType().FullName != "Verse.AI.Pawn_JobTracker" || p.pather.GetType().FullName != "Verse.AI.Pawn_PathFollower" || p.mindState.GetType().FullName != "Verse.AI.Pawn_MindState") throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        foreach (object tracker in new object[] { p.jobs, p.pather, p.mindState, p.needs, p.relations })
        {
            var owner = AccessTools.Field(tracker.GetType(), "pawn");
            if (owner?.FieldType != typeof(Pawn) || owner.GetValue(tracker) != p) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        }
        if (p.mindState.traderDismissed || p.mindState.hasQuest || p.mindState.duty != null || p.mindState.enemyTarget != null || p.mindState.meleeThreat != null ||
            p.mindState.knownExploder != null || p.mindState.lastMannedThing != null || p.mindState.breachingTarget != null || p.mindState.forcedGotoPosition.IsValid ||
            p.mindState.exitMapAfterTick != -99999 || p.mindState.thinkData == null || p.mindState.thinkData.Count != 0)
            throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        // IsPrioritized can call Clear on expiry. Inspect saved fields instead; never expire or clear a work order here.
        var priority = p.mindState.priorityWork;
        var priorityOwner = AccessTools.Field(typeof(PriorityWork), "pawn");
        var priorityCell = AccessTools.Field(typeof(PriorityWork), "prioritizedCell");
        var priorityGiver = AccessTools.Field(typeof(PriorityWork), "prioritizedWorkGiver");
        if (priority?.GetType() != typeof(PriorityWork) || priorityOwner?.FieldType != typeof(Pawn) || priorityOwner.GetValue(priority) != p ||
            priorityCell?.FieldType != typeof(IntVec3) || priorityCell.GetValue(priority) is not IntVec3 cell || cell.IsValid ||
            priorityGiver?.FieldType != typeof(WorkGiverDef) || priorityGiver.GetValue(priority) != null) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        var equipment = p.equipment?.AllEquipmentListForReading ?? [];
        var apparel = p.apparel?.WornApparel ?? [];
        if (equipment.Cast<Thing>().Concat(apparel).Any(t => t is IThingHolder || t is ThingWithComps c && c.AllComps.Any(x => x is IThingHolder))) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        var spawnedField = AccessTools.Field(typeof(MapPawns), "pawnsSpawned");
        var ownerMap = AccessTools.Field(typeof(MapPawns), "map");
        var factionsField = AccessTools.Field(typeof(MapPawns), "pawnsInFactionSpawned");
        var factions = factionsField?.GetValue(p.Map.mapPawns);
        var listsField = factions == null ? null : AccessTools.Field(factions.GetType(), "pawnList");
        var nullField = factions == null ? null : AccessTools.Field(factions.GetType(), "nullFactionPawns");
        if (ownerMap?.FieldType != typeof(Map) || ownerMap.GetValue(p.Map.mapPawns) != p.Map || spawnedField?.FieldType != typeof(List<Pawn>) || spawnedField.GetValue(p.Map.mapPawns) is not List<Pawn> spawned || spawned.Count(x => ReferenceEquals(x, p)) != 1 ||
            factions?.GetType().FullName != "Verse.MapPawns+FactionDictionary" || listsField?.FieldType != typeof(Dictionary<Faction, List<Pawn>>) ||
            listsField.GetValue(factions) is not Dictionary<Faction, List<Pawn>> lists || nullField?.GetValue(factions) is not List<Pawn> withoutFaction || withoutFaction.Contains(p) ||
            lists.Values.Any(list => list == null || list.Any(x => x == null)) || !lists.TryGetValue(p.Faction, out var own) || own.Count(x => ReferenceEquals(x, p)) != 1 ||
            lists.Values.Sum(list => list.Count(x => ReferenceEquals(x, p))) != 1) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        foreach (var pair in new[] { (typeof(MapPawns), "RegisterPawn"), (typeof(MapPawns), "DeRegisterPawn"), (typeof(Pawn), "SetFaction"),
            (typeof(Pawn), "ClearMind_NewTemp"), (typeof(Pawn), "ClearAllReservations"), (typeof(Thing), "SetFaction"), (typeof(DesignationManager), "RemoveAllDesignationsOn") }) Unpatched(pair.Item1, pair.Item2);
        Unpatched(typeof(MapPawns), "DoListChangedNotifications");
        Unpatched(factions.GetType(), "GetPawnList");
        Unpatched(p.pather.GetType(), "StopDead"); Unpatched(p.jobs.GetType(), "StopAll");
        Unpatched(p.mindState.GetType(), "Reset");
        Unpatched(typeof(PawnComponentsUtility), "AddAndRemoveDynamicComponents");
        Unpatched(p.needs.GetType(), "AddOrRemoveNeedsAsAppropriate");
        Unpatched(p.relations.GetType(), "Notify_ChangedFaction");
        var drafted = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.Drafted));
        var active = AccessTools.PropertyGetter(p.mindState.GetType(), "Active");
        if (drafted == null || active == null || Harmony.GetPatchInfo(drafted)?.Owners.Any() == true || Harmony.GetPatchInfo(active)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        foreach (string name in new[] { "Moving" })
        {
            var getterMethod = AccessTools.PropertyGetter(p.pather.GetType(), name);
            if (getterMethod == null || Harmony.GetPatchInfo(getterMethod)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        }
        RequireEmptyReservations(p.Map);
        var getter = AccessTools.PropertyGetter(typeof(DesignationManager), "AllDesignations");
        if (getter == null || Harmony.GetPatchInfo(getter)?.Owners.Any() == true || p.Map.designationManager?.GetType() != typeof(DesignationManager) ||
            getter.Invoke(p.Map.designationManager, null) is not IEnumerable designations || designations.Cast<object>().Any()) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        // SetFaction can emit these events. An unknown subscriber is not a preservation permission.
        var eventField = AccessTools.Field(typeof(Map), "events"); var events = eventField?.GetValue(p.Map);
        if (events?.GetType().FullName != "Verse.MapEvents" || events.GetType().Assembly != typeof(Pawn).Assembly) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        Unpatched(events.GetType(), "Notify_ThingFactionChanged");
        foreach (var pair in new[] { ("autoSlaughterManager", "Notify_PawnChangedFaction"), ("attackTargetsCache", "UpdateTarget") })
        {
            var f = AccessTools.Field(typeof(Map), pair.Item1); var value = f?.GetValue(p.Map);
            if (value?.GetType() != f?.FieldType) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
            Unpatched(value.GetType(), pair.Item2);
        }
        foreach (string name in new[] { "ThingFactionChanged", "FactionAdded", "FactionRemoved" })
        {
            var field = AccessTools.Field(events.GetType(), name);
            if (field == null || !typeof(Delegate).IsAssignableFrom(field.FieldType) || field.GetValue(events) != null) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        }
    }
}
