using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace Honor.Contracts.Preview;

/// <summary>Private copy of the audited native authority call; original global methods are never patched.</summary>
internal static class NativeWorkFactionCall
{
    private sealed class Context
    {
        internal Pawn Pawn; internal Faction Source, Target; internal Game Game; internal Map Map;
        internal Job Job; internal JobDriver Driver; internal JobQueue Queue;
        internal object Path, Request, Mind, Settings; internal List<QueuedJob> Entries;
        internal List<ReservationManager.Reservation> Reservations; internal bool MindPassed, DestinationPassed; internal string Memory;
        internal Dictionary<Faction, PawnDestinationReservationManager.PawnDestinationSet> DestinationGroups;
        internal PawnDestinationReservationManager.PawnDestinationReservation[] DestinationRows;
        internal List<PhysicalInteractionReservationManager.PhysicalInteractionReservation> Physical;
        internal Pawn_CarryTracker Carry; internal Thing Cargo; internal ThingOwner CargoOwner;
        internal List<ThingCountTracker> Enroute;
        internal void Same(Faction expected)
        {
            if (Current.Game != Game || Current.ProgramState != ProgramState.Playing || Pawn.Map != Map || !Pawn.Spawned || Pawn.Faction != expected ||
                Pawn.jobs.curJob != Job || Pawn.jobs.curDriver != Driver || Pawn.jobs.jobQueue != Queue || Pawn.mindState != Mind || Pawn.workSettings != Settings ||
                !ResidentActiveWork.Queue(Pawn).SequenceEqual(Entries) || !ResidentActiveWork.Reservations(Map).SequenceEqual(Reservations) ||
                !ResidentActiveWork.PhysicalRows(Map).SequenceEqual(Physical) || Pawn.carryTracker != Carry || Pawn.carryTracker.CarriedThing != Cargo || Pawn.carryTracker.innerContainer != CargoOwner ||
                !ResidentActiveWork.EnrouteRows(Map).SequenceEqual(Enroute) ||
                ResidentActiveWork.Raw(Pawn.pather, typeof(Pawn_PathFollower), "curPath") != Path || ResidentActiveWork.Raw(Pawn.pather, typeof(Pawn_PathFollower), "curPathRequest") != Request)
                throw new ArgumentException("Original native work identity changed during authority call.");
        }
    }
    [ThreadStatic] private static Context active;
    private static bool ready;
    internal static void Prepare()
    {
        ResidentActiveWork.Known(typeof(Pawn), "SetFaction"); ResidentActiveWork.Known(typeof(Pawn), "ClearMind_NewTemp");
        ResidentActiveWork.Known(typeof(PawnDestinationReservationManager), "ReleaseAllClaimedBy");
        if (Harmony.GetPatchInfo(AccessTools.Method(typeof(NativeWorkFactionCall), nameof(InvokeOriginal)))?.Owners.Any() == true) throw new ArgumentException("Work authority stand-in was externally patched.");
        if (ready) return;
        var original = AccessTools.Method(typeof(Pawn), nameof(Pawn.SetFaction), new[] { typeof(Faction), typeof(Pawn) });
        if (original?.DeclaringType != typeof(Pawn)) throw new ArgumentException("Original native authority dispatch required.");
        new ReversePatcher(new Harmony("honor.series.contracts.preview.workcopy"), original, new HarmonyMethod(AccessTools.Method(typeof(NativeWorkFactionCall), nameof(InvokeOriginal))))
            .Patch(HarmonyReversePatchType.Original);
        ready = true;
    }
    internal static void Execute(Pawn pawn, Faction target, Action returned)
    {
        Prepare();
        if (active != null || returned == null || target == null || target.IsPlayer || pawn?.Faction == null || pawn.Faction.IsPlayer || pawn.Faction == target)
            throw new ArgumentException("Independent NPC work authority call required.");
        var before = ResidentActiveWork.Capture(pawn);
        active = new() { Pawn = pawn, Source = pawn.Faction, Target = target, Game = Current.Game, Map = pawn.Map, Job = pawn.jobs.curJob,
            Driver = pawn.jobs.curDriver, Queue = pawn.jobs.jobQueue, Entries = ResidentActiveWork.Queue(pawn).ToList(),
            Reservations = ResidentActiveWork.Reservations(pawn.Map).ToList(), Mind = pawn.mindState, Settings = pawn.workSettings, Memory = ResidentActiveWork.Memory(pawn),
            Path = ResidentActiveWork.Raw(pawn.pather, typeof(Pawn_PathFollower), "curPath"), Request = ResidentActiveWork.Raw(pawn.pather, typeof(Pawn_PathFollower), "curPathRequest"),
            Enroute = ResidentActiveWork.EnrouteRows(pawn.Map).ToList(), Physical = ResidentActiveWork.PhysicalRows(pawn.Map).ToList(), Carry = pawn.carryTracker, Cargo = pawn.carryTracker.CarriedThing, CargoOwner = pawn.carryTracker.innerContainer,
            DestinationGroups = ResidentActiveWork.Destinations(pawn.Map), DestinationRows = ResidentActiveWork.Destinations(pawn.Map).Values.SelectMany(g => g.list).Where(r => r.claimant == pawn).ToArray() };
        try
        {
            active.Same(active.Source); InvokeOriginal(pawn, target, null);
            // Native normal return is an independent fact, retained even if later preservation verification fails.
            returned(); active.Same(target);
            if (!active.MindPassed || !active.DestinationPassed || active.Memory != ResidentActiveWork.Memory(pawn) || System.Text.Json.JsonSerializer.Serialize(before) != System.Text.Json.JsonSerializer.Serialize(ResidentActiveWork.Capture(pawn)))
                throw new ArgumentException("Native work-preserving call did not return the original work.");
        }
        finally { active = null; }
    }
    private static void KeepDestination(PawnDestinationReservationManager manager, Pawn pawn)
    {
        var c = active;
        if (c == null || c.DestinationPassed || c.Pawn != pawn || manager != c.Map.pawnDestinationReservationManager) throw new ArgumentException("No original work destination authority.");
        c.Same(c.Source); c.DestinationPassed = true;
        // Retain the same rows until the native body reaches its post-faction mind boundary.
    }
    private static void KeepMind(Pawn pawn, bool keepLaying, bool clearInspiration, bool clearMentalState, bool wasDowned)
    {
        var c = active;
        if (c == null || c.MindPassed || !c.DestinationPassed || c.Pawn != pawn || !keepLaying || clearInspiration || !clearMentalState || wasDowned) throw new ArgumentException("No original work continuation authority.");
        c.Same(c.Target);
        if (!ReferenceEquals(c.DestinationGroups, ResidentActiveWork.Destinations(c.Map))) throw new ArgumentException("Original destination registry replaced.");
        if (c.DestinationRows.Length != 0)
        {
            if (!c.DestinationGroups.TryGetValue(c.Source, out var from) || !from.list.Where(r => r.claimant == pawn).SequenceEqual(c.DestinationRows) ||
                c.DestinationGroups.Values.Where(g => g != from).Any(g => g.list.Any(r => r.claimant == pawn))) throw new ArgumentException("Original destination rows changed before reindex.");
            c.DestinationGroups.TryGetValue(c.Target, out var to);
            if (to == from) throw new ArgumentException("Aliased source/target destination groups.");
            to ??= new PawnDestinationReservationManager.PawnDestinationSet();
            // Allocate first. Then only field stores on the main thread; no callbacks, Reserve, Release or duplicate deep save.
            var sourceRows = from.list.Where(r => r.claimant != pawn).ToList();
            var targetRows = to.list.Concat(c.DestinationRows).ToList();
            if (!c.DestinationGroups.ContainsKey(c.Target)) c.DestinationGroups.Add(c.Target, to);
            from.list = sourceRows; to.list = targetRows;
            if (!to.list.Where(r => r.claimant == pawn).SequenceEqual(c.DestinationRows)) throw new ArgumentException("Original destination reindex failed.");
        }
        _ = ResidentActiveWork.DestinationState(c.Map); c.MindPassed = true;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InvokeOriginal(Pawn pawn, Faction target, Pawn recruiter)
    {
        // Harmony's ReversePatcher discovers this emitted local function by the stand-in method name.
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            var instructions = source.ToList(); int mind = 0, destination = 0;
            var clear = AccessTools.Method(typeof(Pawn), nameof(Pawn.ClearMind_NewTemp), new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
            var release = AccessTools.Method(typeof(PawnDestinationReservationManager), "ReleaseAllClaimedBy", new[] { typeof(Pawn) });
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(clear)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(NativeWorkFactionCall), nameof(KeepMind)); mind++; }
                if (instruction.Calls(release)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(NativeWorkFactionCall), nameof(KeepDestination)); destination++; }
            }
            if (mind != 1 || destination != 1) throw new ArgumentException("Audited native work call boundaries changed.");
            return instructions;
        }
        GC.KeepAlive((Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>>)Transpiler);
        throw new InvalidOperationException("Original work authority copy was not prepared.");
    }
}
