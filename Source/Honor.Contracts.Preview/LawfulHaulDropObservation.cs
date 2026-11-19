using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
using Verse.AI;

namespace Honor.Contracts.Preview;

// Evidence only. Native map and carry owners retain sole ownership of the actual Thing.
internal sealed record OriginalDropCall(TerritoryTransferReference City, Game Game, Pawn Actor,
    Thing Cargo, Map Map, IntVec3 Cell, int Quantity, string Start);

[HarmonyPatch]
internal static class LawfulHaulDropObservation
{
    private static bool Prepare() => NativeTransferBodies.HaulFactionBodies5013;
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(Pawn_CarryTracker),
        nameof(Pawn_CarryTracker.TryDropCarriedThing),
        [typeof(IntVec3),typeof(ThingPlaceMode),typeof(Thing).MakeByRefType(),typeof(Action<Thing,int>)]);

    internal static bool SoleObserver()
    {
        var patches = Harmony.GetPatchInfo(TargetMethod());
        return patches != null && patches.Transpilers.Count == 0 && patches.Finalizers.Count == 0 &&
            patches.Prefixes.Count == 1 && patches.Postfixes.Count == 1 &&
            patches.Prefixes[0].PatchMethod == AccessTools.Method(typeof(LawfulHaulDropObservation),nameof(Prefix)) &&
            patches.Postfixes[0].PatchMethod == AccessTools.Method(typeof(LawfulHaulDropObservation),nameof(Postfix));
    }

    private static void Prefix(Pawn_CarryTracker __instance, IntVec3 dropLoc, ThingPlaceMode mode,
        out OriginalDropCall __state)
    {
        __state = null;
        try
        {
            if (mode != ThingPlaceMode.Direct || Current.ProgramState != ProgramState.Playing ||
                __instance?.pawn?.jobs?.curDriver?.GetType() != typeof(JobDriver_HaulToCell)) return;
            __state = Current.Game.GetComponent<TerritoryAdministrationLedger>().BeginOriginalDrop(__instance, dropLoc);
        }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Original haul input unavailable: " + e.GetType().Name); }
    }

    private static void Postfix(bool __result, Thing resultingThing, OriginalDropCall __state)
    {
        if (__state == null) return;
        try
        {
            var call = __state;
            if (call.Game != Current.Game || call.City.OriginalHaulDropStart != call.Start ||
                call.City.OriginalHaulDropReturn != null) return;
            bool sameStack = __result && ReferenceEquals(resultingThing,call.Cargo) &&
                call.Actor.carryTracker.CarriedThing == null && call.Cargo.Spawned &&
                call.Cargo.Map == call.Map && call.Cargo.Position == call.Cell && call.Cargo.stackCount == call.Quantity;
            call.City.OriginalHaulDropReturn = JsonSerializer.Serialize(new OriginalDropOutput(
                WorkProgress.Hash(call.Start), __result, sameStack, Find.TickManager.TicksGame));
        }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Original haul output unavailable: " + e.GetType().Name); }
    }
}

public sealed partial class TerritoryAdministrationLedger
{
    private static void RequireCurrentOriginalDrop(TerritoryTransfer t, TerritoryTransferReference r)
    {
        LawfulHaulDrop.Validate(t, r.Residents?.Profile, r.OriginalHaulDropStart, r.OriginalHaulDropReturn, Find.TickManager.TicksGame);
        if (r.OriginalHaulDropReturn == null || !JsonSerializer.Deserialize<OriginalDropOutput>(r.OriginalHaulDropReturn).SameStackAtDestination)
            throw new ArgumentException("Original whole cargo placement has no positive output.");
        var input = JsonSerializer.Deserialize<OriginalDropInput>(r.OriginalHaulDropStart);
        var map = r.City?.Map;
        var cargo = r.Residents.ActiveWorkCarry?.SingleOrDefault(thing => thing != null && thing.GetUniqueLoadID() == input.Cargo);
        if (map == null || map != r.LoadedMap?.Map || map.GetUniqueLoadID() != input.Map ||
            cargo == null || cargo.Destroyed || !cargo.Spawned || cargo.Map != map || cargo.holdingOwner != null && cargo.holdingOwner != map.GetDirectlyHeldThings() ||
            cargo.Faction != null || cargo.stackCount != input.Count || map.cellIndices.CellToIndex(cargo.Position) != input.Cell)
            throw new ArgumentException("Original delivered stack no longer matches its approved destination and quantity.");
        var actor = r.Residents.Pawns.Single(p => p != null && p.GetUniqueLoadID() == input.Pawn);
        var work = LoadedMapPreservation.Restore(t.LoadedMap).ActiveWorkV1.Single(row => row.Pawn == input.Pawn);
        using var saved = JsonDocument.Parse(work.Saved);
        if (saved.RootElement.GetProperty("Carry").GetRawText() != JsonSerializer.Serialize(ResidentActiveWork.SimpleCargoState(cargo, actor)))
            throw new ArgumentException("Delivered stack definition, health or components changed.");
    }
    // Retain a pending operation for review. Execution requires independent current authority.
    private static bool RetainsDeliveredHaul(TerritoryTransfer t, TerritoryTransferReference r)
    {
        if (t.Phase != TerritoryTransferPhase.Prepared || r.OriginalHaulDropReturn == null ||
            !JsonSerializer.Deserialize<OriginalDropOutput>(r.OriginalHaulDropReturn).SameStackAtDestination) return false;
        RequireCurrentOriginalDrop(t, r);
        var input = JsonSerializer.Deserialize<OriginalDropInput>(r.OriginalHaulDropStart);
        LoadedMapRecords.VerifyDeliveredContents(r, input.Cargo);
        var residents = r.Residents;
        var map = r.City.Map;
        if (map != residents.NativeMap || r.From == null || r.Target == null || r.Target.IsPlayer ||
            r.City.Faction != (r.CityCallReturned ? r.Target : r.From) || r.CityCallStarted != r.CityCallReturned ||
            residents.Pawns == null || residents.Pawns.Any(p => p == null || p.Destroyed || p.Dead || p.Map != map) ||
            !residents.Pawns.Select(p => p.GetUniqueLoadID()).SequenceEqual(residents.Set().Residents.Select(row => row.Id)) ||
            r.City.previouslyGeneratedInhabitants == null || residents.Pawns.Count != r.City.previouslyGeneratedInhabitants.Count ||
            !new HashSet<Pawn>(residents.Pawns).SetEquals(r.City.previouslyGeneratedInhabitants))
            throw new ArgumentException("Original pending town, map and resident roster required.");
        if (!Find.FactionManager.AllFactionsListForReading.Contains(r.From) ||
            !Find.FactionManager.AllFactionsListForReading.Contains(r.Target) || r.From.HostileTo(r.Target) || r.Target.HostileTo(r.From))
            throw new ArgumentException("Original realms and reciprocal peace required.");
        var visitors = r.LoadedMap.Visitors ?? [];
        var expectedVisitors = LoadedMapPreservation.Restore(t.LoadedMap).Visitors ?? [];
        if (!visitors.Select(ResidentMapVisitors.Capture).OrderBy(row => row.Id,StringComparer.Ordinal).SequenceEqual(expectedVisitors) ||
            !new HashSet<Pawn>(residents.Pawns.Concat(visitors)).SetEquals(map.mapPawns.AllPawns))
            throw new ArgumentException("Original mapped residents and bystanders required.");
        if (visitors.Count > 0) CheckMapVisitorPeace(r.City,r.Target);
        ResidentTransferCensus.RequireRecoverable(residents.AuthorityJournal, residents.CareJournal);
        for (int i = 0; i < residents.Pawns.Count; i++)
        {
            var call = ResidentTransferCensus.Call(residents.AuthorityJournal[i]);
            if (residents.Pawns[i].Faction != (call.Returned ? r.Target : r.From))
                throw new ArgumentException("Original resident affiliation and call history disagree.");
        }
        ResidentExecution.ValidateCareJournal(residents, r.From, r.Target);
        return true;
    }
    private static string CurrentDeliveredMapProof(TerritoryTransfer transfer, TerritoryTransferReference reference)
    {
        var input = JsonSerializer.Deserialize<OriginalDropInput>(reference.OriginalHaulDropStart);
        var residents = reference.Residents;
        var actor = residents.Pawns.Single(p => p.GetUniqueLoadID() == input.Pawn);
        var fresh = LoadedMapRecords.Capture(reference.City, deliveredActor: actor);
        LoadedMapRecords.VerifyMapRegistries(fresh, reference.LoadedMap);
        var current = LoadedMapPreservation.Restore(fresh.Snapshot);
        var original = LoadedMapPreservation.Restore(transfer.LoadedMap);
        // Only the delivering resident, its work and the new ground stack may differ.
        // Storage membership changes require separate evidence.
        var comparison = current with {
            Proof = original.Proof,
            Things = current.Things.Where(row => row.Id != input.Cargo).ToArray(),
            NativeResidents = current.NativeResidents.Select(row => row.Id == input.Pawn
                ? original.NativeResidents.Single(old => old.Id == row.Id) : row).ToArray(),
            ActiveWorkV1 = current.ActiveWorkV1?.Select(row => row.Pawn == input.Pawn
                ? original.ActiveWorkV1.Single(old => old.Pawn == row.Pawn) : row).ToArray()
        };
        if (current.Things.Count(row => row.Id == input.Cargo) != 1 ||
            current.ActiveWorkV1?.SingleOrDefault(row => row.Pawn == input.Pawn)?.Jobs.FirstOrDefault() != input.Job ||
            JsonSerializer.Serialize(comparison) != JsonSerializer.Serialize(original))
            throw new ArgumentException("Current map changed beyond the observed original ground placement.");
        return current.Proof;
    }
    private void FinishDeliveredCity(TerritoryTransfer t, TerritoryTransferReference r, Game game)
    {
        RequireCurrentCustody(r);
        var agreement = custodyAgreements.Record(r.CustodyAgreementV1);
        var residents = r.Residents;
        ResidentExecution.ValidateCensus(residents, r.ResidentCensusProof, t.Territory,t.FromOwner,t.TargetOwner,true);
        var roster = residents.Set().Residents;
        for (int i = 0; i < residents.Pawns.Count; i++)
        {
            var pawn = residents.Pawns[i];
            int? care = pawn.playerSettings == null ? null : (int)pawn.playerSettings.medCare;
            if (pawn.Faction != r.Target || care != roster[i].MedicalCare || residents.Surgery[i].Detached.Count != 0 ||
                ResidentSurgery.Proof(pawn, residents.Surgery[i]) != residents.Surgery[i].Set().Proof)
                throw new ArgumentException("Original restored resident affiliation, medical policy and surgery orders required.");
            ResidentAreas.CheckCurrent(residents.AreaRecords[i].Journal, ResidentAreaRecords.Entries(pawn));
        }
        var authority = ResidentCallAuthority(r);
        if (r.LoadedMap.AssetJournal != null)
        {
            // The city may already have returned on an earlier run. Never start a property call after it.
            if (r.CityCallStarted)
                MapAssetTransfers.Pair(r.LoadedMap.AssetJournal,t.LoadedMap,t.FromOwner,t.TargetOwner,true);
            else
                MapAssetRecords.Apply(r, authority, deliveredGroundStack: true);
        }
        authority();
        if (r.City.Faction == r.From)
        {
            TerritoryCallGuard.RequireNewCall(r.CityCallV1,r.CityCallStarted,r.CityCallReturned);
            r.CityCallStarted = true;
            r.City.SetFaction(r.Target);
            if (Current.Game != game || Current.ProgramState != ProgramState.Playing)
                throw new ArgumentException("Current game changed during lawful city return.");
            r.CityCallReturned = true;
        }
        TerritoryCallGuard.RequireStartedForTarget(r.CityCallV1,r.CityCallStarted,r.CityCallReturned);
        CityStockPreservation.InvalidateMaterial(r.City);
        authority();
        Ledger.ConfirmDeliveredCity(t.Id,agreement,CurrentDeliveredMapProof(t,r),r.City.Faction.GetUniqueLoadID(),
            r.CityCallStarted,r.CityCallReturned,Find.TickManager.TicksGame);
        ResidentExecution.ReleaseReferences(residents);
        LoadedMapRecords.Release(r.LoadedMap);
        ReleaseWorkObservationReferences(r);
    }
    internal OriginalDropCall BeginOriginalDrop(Pawn_CarryTracker carry, IntVec3 cell)
    {
        var pawn = carry.pawn;
        if (nativeExecutionDepth != 0 || RestoreFailed || Current.Game != ownerGame ||
            pawn.carryTracker != carry || pawn.Map == null || !cell.InBounds(pawn.Map)) return null;
        var political = ownerGame.GetComponent<PoliticalTransitionLedger>();
        if (!political.Journal.Pending || political.Journal.Current?.LawfulResidents == null) return null;
        var r = references.SingleOrDefault(row => row.Residents?.Profile == 16 && row.OriginalHaulDropStart == null &&
            row.LoadedMap?.Map == pawn.Map && row.Residents.Pawns.Contains(pawn) &&
            Ledger.Transfer(row.Id).Phase == TerritoryTransferPhase.Prepared);
        if (r == null) return null;
        var t = Ledger.Transfer(r.Id);
        var cargo = carry.CarriedThing;
        var job = pawn.CurJob;
        if (t.LawfulHaulParentProof == null || cargo == null || cargo.holdingOwner != carry.innerContainer ||
            cargo.stackCount <= 0 || job == null || r.Residents.ActiveWorkCarry?.Contains(cargo) != true ||
            r.Residents.ActiveWorkJobs?.Contains(job) != true) return null;
        var original = LoadedMapPreservation.Restore(t.LoadedMap).ActiveWorkV1.Single(row => row.Pawn == pawn.GetUniqueLoadID());
        if (original.Jobs.FirstOrDefault() != job.GetUniqueLoadID()) return null;
        using var saved = JsonDocument.Parse(original.Saved);
        if (!saved.RootElement.TryGetProperty("Carry", out var priorCargo) ||
            priorCargo.GetRawText() != JsonSerializer.Serialize(ResidentActiveWork.SimpleCargo(cargo,carry.innerContainer,pawn)) ||
            !LawfulHaulDropObservation.SoleObserver()) return null;
        political.RequireHaulCustodyParent(t);
        string start = JsonSerializer.Serialize(new OriginalDropInput(t.LawfulHaulParentProof, t.Id,
            pawn.Map.GetUniqueLoadID(), original.Pawn, job.GetUniqueLoadID(), cargo.GetUniqueLoadID(),
            cargo.stackCount, pawn.Map.cellIndices.CellToIndex(cell), Find.TickManager.TicksGame));
        var call = new OriginalDropCall(r, Current.Game, pawn, cargo, pawn.Map, cell, cargo.stackCount, start);
        r.OriginalHaulDropStart = start;
        return call;
    }
}
