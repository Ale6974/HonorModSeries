using System.Collections;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld.Planet;
namespace Honor.Contracts.Preview;
internal static class ResidentGroundRest
{
    private const string JobFields = "ability attackDoorIfTargetLost bill biosculpterCycleKey canBashDoors canBashFences canUseRangedWeapon checkEncumbrance checkOverrideOnExpire collideWithPawns controlGroupTag count countQueue crawlingReportStringOverride def doUntilGatheringEnded draftedTend dutyTag endAfterTendedOnce endIfCantShootInMelee endIfCantShootTargetFromCurPos ensureReachable exitMapOnArrival expireRequiresEnemiesNearby expiryInterval failIfCantJoinOrCreateCaravan followRadius forceMaintainFacing forceSleep globalTarget haulDroppedApparel haulMode haulOpportunisticDuplicates ignoreDesignations ignoreForbidden ignoreJoyTimeAssignment ingestTotalCount interactableIndex interaction intervalScalingTarget isLearningDesire jobGiverKey jobGiverThinkTree killIncappedTarget loadID locomotionUrgency lookDirection lord maxNumMeleeAttacks maxNumStaticAttacks overeat overrideFacing placedThings plantDefToSow playerForced playerInterruptedForced preventFriendlyFire psyfocusTargetLast quest reactingToMeleeThreat reportStringOverride restUntilHealed ritualTag ropeToUnenclosedPens ropingPriority showSpeechBubbles source speechFaceSpectatorsIfPossible speechSoundFemale speechSoundMale startInvoluntarySleep startTick takeExtraIngestibles takeInventoryDelay targetA targetB targetC targetQueueA targetQueueB thingDefToCarry verbToUse wasOnMeditationTimeAssignment workGiverDef commTarget";
    private const string DriverFields = "asleep collideWithPawns curToilIndex ended locomotionUrgencySameAs nextToilIndex rotateToFace startTick ticksLeftThisToil uninstallWorkLeft wantBeginNextToil";
    private static object Raw(object value, Type type, string name)
    { var field = AccessTools.Field(type, name); if (value == null || field?.DeclaringType != type || field.IsStatic) throw new ArgumentException("Unknown native rest schema."); return field.GetValue(value); }
    private static void Known(Type type, string name)
    { var methods = AccessTools.GetDeclaredMethods(type).Where(m => m.Name == name).ToArray(); if (methods.Length == 0 || methods.Any(m => Harmony.GetPatchInfo(m)?.Owners.Any() == true)) throw new ArgumentException("Unknown rest read/save/continuation boundary."); }
    private static object Value(object v)
    {
        if (v == null) return null;
        if (v is bool or int or long or string) return v;
        if (v is float f && RuntimeCompat.IsFinite(f)) return f;
        if (v.GetType().IsEnum) return Convert.ToInt32(v);
        if (v is Rot4 rot) return rot.AsInt;
        if (v is LocalTargetInfo target && target.Thing == null) return new { target.Cell.x, target.Cell.y, target.Cell.z };
        if (v is GlobalTargetInfo global && global.Equals(GlobalTargetInfo.Invalid)) return "invalid";
        if (v is JobDef def && def == JobDefOf.LayDown && def.GetType() == typeof(JobDef) && DefDatabase<JobDef>.GetNamedSilentFail(def.defName) == def) return def.defName;
        // No queues, materials, bills, lord/quest/source/verbs or private target references.
        if (v is ICollection list && list.Count == 0) return new { Empty = true };
        throw new ArgumentException("Rest job contains unsupported work/property references.");
    }
    private static object Fields(object value, Type type, string names)
        => names.Split(' ').OrderBy(s => s, StringComparer.Ordinal).Select(name => new { Name = name, Value = Value(Raw(value, type, name)) }).ToArray();
    private static List<ReservationManager.Reservation> Reservations(Map map)
    {
        if (!NativeTransferBodies.WorkObservationBodies || map?.GetType() != typeof(Map) || map.reservationManager?.GetType() != typeof(ReservationManager) || Raw(map.reservationManager, typeof(ReservationManager), "map") != map || Raw(map.reservationManager, typeof(ReservationManager), "reservations") is not List<ReservationManager.Reservation> rows || rows.Count > 10000 || rows.Any(r => r?.GetType() != typeof(ReservationManager.Reservation)) || rows.Distinct().Count() != rows.Count) throw new ArgumentException("Original native cell reservations unavailable.");
        Known(typeof(ReservationManager), "ExposeData"); Known(typeof(ReservationManager.Reservation), "ExposeData"); Known(typeof(Thing), "GetUniqueLoadID"); Known(typeof(Job), "GetUniqueLoadID"); return rows;
    }
    internal static void RequireReservations(Map map, ICollection originalRows)
    {
        var rows = Reservations(map);
        if (!ReferenceEquals(rows, originalRows) || map.Parent is not RimWorld.Planet.Settlement city || city.previouslyGeneratedInhabitants == null) throw new ArgumentException("Original native reservation owner changed.");
        var people = new HashSet<Pawn>(); var cells = new HashSet<IntVec3>();
        var evidence = new List<GroundRestCell>();
        foreach (var row in rows)
        {
            var type = typeof(ReservationManager.Reservation);
            var pawn = Raw(row, type, "claimant") as Pawn; var job = Raw(row, type, "job") as Job;
            if (pawn?.GetType() != typeof(Pawn) || !pawn.Spawned || pawn.Map != map || pawn.IsPrisoner || pawn.IsSlave || pawn.GetLord() != null || !city.previouslyGeneratedInhabitants.Contains(pawn) || !people.Add(pawn) || job?.GetType() != typeof(Job) || job != pawn.jobs?.curJob || job.def != JobDefOf.LayDown || pawn.jobs.curDriver?.GetType() != typeof(JobDriver_LayDown) || Raw(row, type, "target") is not LocalTargetInfo target || target.Thing != null || target.Cell != pawn.Position || target != job.targetA || !cells.Add(target.Cell) || Raw(row, type, "layer") != null || Raw(row, type, "maxPawns") is not int max || max != 1 || Raw(row, type, "stackCount") is not int stack || stack != -1) throw new ArgumentException("Only the same original resident/job/current ground cell may remain reserved.");
            evidence.Add(new(pawn.GetUniqueLoadID(), job.GetUniqueLoadID(), map.GetUniqueLoadID(), map.cellIndices.CellToIndex(target.Cell), max, stack));
        }
        GroundRest.ValidateCells(evidence, people.Select(p => new GroundRestCell(p.GetUniqueLoadID(), p.jobs.curJob.GetUniqueLoadID(), map.GetUniqueLoadID(), map.cellIndices.CellToIndex(p.Position))));
    }
    private static object OwnReservations(Pawn pawn)
    {
        var rows = Reservations(pawn.Map);
        RequireReservations(pawn.Map, rows);
        return rows.Select((r, i) => new { Row = r, Index = i }).Where(r => ReferenceEquals(Raw(r.Row, typeof(ReservationManager.Reservation), "claimant"), pawn)).Select(r => new { r.Index, Job = pawn.jobs.curJob.GetUniqueLoadID(), Cell = pawn.Map.cellIndices.CellToIndex(pawn.Position), Max = 1, Stack = -1 }).ToArray();
    }
    internal static GroundRestRow Capture(Pawn pawn)
    {
        if (pawn == null) return null;
        if (!NativeTransferBodies.WorkObservationBodies) throw new ArgumentException("Unreviewed ongoing native work bodies.");
        Known(typeof(Pawn), "get_CurJob"); Known(typeof(JobQueue), "get_Count");
        if (pawn.CurJob == null) return null;
        if (!NativeTransferBodies.WorkObservationBodies || pawn.GetType() != typeof(Pawn) || !pawn.Spawned || pawn.IsPrisoner || pawn.IsSlave || pawn.Drafted || pawn.InMentalState || pawn.jobs?.GetType() != typeof(Pawn_JobTracker)) throw new ArgumentException("Reviewed spawned civilian rest only.");
        var job = pawn.jobs.curJob; var driver = pawn.jobs.curDriver;
        if (job?.GetType() != typeof(Job) || job.loadID < 0 || job.def != JobDefOf.LayDown || job.def.driverClass != typeof(JobDriver_LayDown) || driver?.GetType() != typeof(JobDriver_LayDown) || driver.pawn != pawn || driver.job != job || driver.ended || driver.locomotionUrgencySameAs != null || pawn.jobs.jobQueue?.GetType() != typeof(JobQueue) || pawn.jobs.jobQueue.Count != 0 || pawn.jobs.startingNewJob || job.targetA.Thing != null || job.targetA.Cell != pawn.Position || !job.targetB.Equals(LocalTargetInfo.Invalid) || !job.targetC.Equals(LocalTargetInfo.Invalid) || job.jobGiver != null || job.jobGiverThinkTree != null || Raw(job, typeof(Job), "jobGiverKey") is not int key || key != -1) throw new ArgumentException("Only same original ground job without queued or foreign work is reviewed.");
        if (Raw(driver, typeof(JobDriver_LayDown), "hasSavedValues") is not true || Raw(driver, typeof(JobDriver), "curToilIndex") is not int index || index < 0 || Raw(driver, typeof(JobDriver), "wantBeginNextToil") is not false || Raw(driver, typeof(JobDriver), "nextToilIndex") is not int next || next != -1) throw new ArgumentException("Rest driver has not reached a stable original toil.");
        foreach (var pair in new[] { (typeof(Job), "ExposeData"), (typeof(Job), "GetUniqueLoadID"), (typeof(JobDriver), "ExposeData"), (typeof(JobDriver), "SetupToils"), (typeof(JobDriver_LayDown), "ExposeData"), (typeof(JobDriver_LayDown), "MakeNewToils"), (typeof(JobDriver_LayDown), "LayDownToil"), (typeof(Pawn_JobTracker), "ExposeData"), (typeof(Pawn_JobTracker), "StopAll"), (typeof(Pawn_JobTracker), "ClearQueuedJobs"), (typeof(JobQueue), "Clear"), (typeof(JobQueue), "get_Count"), (typeof(RestUtility), "IsLayingForJobCleanup"), (typeof(RestUtility), "InBed"), (typeof(RestUtility), "CurrentBed"), (typeof(PawnUtility), "GetPosture"), (typeof(PawnPostureUtility), "Laying"), (typeof(Pawn), "get_CurJob"), (typeof(Toils_LayDown), "LayDown"), (typeof(Toils_Goto), "GotoCell") }) Known(pair.Item1, pair.Item2);
        if (!RestUtility.IsLayingForJobCleanup(pawn)) throw new ArgumentException("Native faction change would end this job.");
        // Reject unsaved nondefault transient work. Native saving alone owns reconstruction of cached drivers/toils.
        if (job.mote != null || job.flying || job.swimming || !job.showCarryingInspectLine || Raw(job, typeof(Job), "cachedDriver") is JobDriver cached && cached != driver || Raw(job, typeof(Job), "lastJobDriverMade") is JobDriver last && last != driver) throw new ArgumentException("Unknown transient rest job state.");
        if (Raw(driver, typeof(JobDriver), "finalizerJobFactory") != null) throw new ArgumentException("Rest finalizer changes native work ownership.");
        foreach (var delegates in new[] { driver.globalFailConditions.Cast<Delegate>(), driver.globalFinishActions.Cast<Delegate>() })
            if (delegates.Any(d => d == null || d.Method.Module.Assembly != typeof(Pawn).Assembly)) throw new ArgumentException("Custom native rest callback.");
        var toils = Raw(driver, typeof(JobDriver), "toils") as List<Toil>;
        if (toils == null || toils.Count > 8 || index >= toils.Count || toils.Any(t => t == null || t.actor != pawn || t.inPool)) throw new ArgumentException("Original native toils unavailable.");
        foreach (var toil in toils)
            foreach (var field in typeof(Toil).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.GetValue(toil) is Delegate d && d.Method.Module.Assembly != typeof(Pawn).Assembly) throw new ArgumentException("Unknown toil callback.");
        object Plan(object value, int depth = 0)
        {
            if (depth > 10) throw new ArgumentException("Native toil closure capacity.");
            if (value == null) return null;
            if (ReferenceEquals(value, pawn)) return "original-pawn";
            if (ReferenceEquals(value, driver)) return "original-driver";
            if (ReferenceEquals(value, job)) return "original-job";
            if (value is Toil toil) { int at = toils.IndexOf(toil); if (at < 0) throw new ArgumentException("External native toil."); return new { Toil = at }; }
            if (value is Delegate callback)
                return callback.GetInvocationList().Select(d => {
                    if (d.Method.Module.Assembly != typeof(Pawn).Assembly || Harmony.GetPatchInfo(d.Method)?.Owners.Any() == true) throw new ArgumentException("Unreviewed native rest callback.");
                    return new { Type = d.Method.DeclaringType.FullName, Token = d.Method.MetadataToken, Target = Plan(d.Target, depth + 1) };
                }).ToArray();
            if (value is IList list) { if (list.Count > 32) throw new ArgumentException("Native toil plan capacity."); return list.Cast<object>().Select(v => Plan(v, depth + 1)).ToArray(); }
            var type = value.GetType();
            if (type.Assembly == typeof(Pawn).Assembly && type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
                return new { Type = type.FullName, Fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(f => f.Name != "<>l__initialThreadId").OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => new { f.Name, Value = Plan(f.GetValue(value), depth + 1) }).ToArray() };
            return Value(value);
        }
        var plan = toils.Select(t => typeof(Toil).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => new { f.Name, Value = Plan(f.GetValue(t)) }).ToArray()).ToArray();
        string saved = JsonSerializer.Serialize(new { Job = Fields(job, typeof(Job), JobFields), Driver = Fields(driver, typeof(JobDriver), DriverFields), Rest = Fields(driver, typeof(JobDriver_LayDown), "canMoveOrCrawl hasSavedValues"), Posture = (int)pawn.jobs.posture, Toils = plan, Fail = Plan(driver.globalFailConditions), Finish = Plan(driver.globalFinishActions), Reservations = OwnReservations(pawn) });
        var row = new GroundRestRow(pawn.GetUniqueLoadID(), job.GetUniqueLoadID(), saved); _ = GroundRest.Proof(row); return row;
    }
    internal static void Validate(ResidentExecutionRecord record)
    {
        if (record.Profile != 14) { if (record.GroundRestJobs != null) throw new ArgumentException("Rest references cannot expand old clinical scopes."); return; }
        var original = LoadedMapPreservation.Restore(record.LoadedMapScope).GroundRestV1;
        if (original == null || record.GroundRestJobs == null || record.GroundRestJobs.Count != original.Length || record.GroundRestJobs.Where((j,i) => j == null || j.GetUniqueLoadID() != original[i].Job || record.Pawns.Single(p => p.GetUniqueLoadID() == original[i].Pawn).jobs?.curJob != j).Any()) throw new ArgumentException("Original native rest job references changed.");
        var current = record.Pawns.Where(p => p.CurJob != null).Select(Capture).ToArray();
        if (JsonSerializer.Serialize(current) != JsonSerializer.Serialize(original)) throw new ArgumentException("Original native work progressed/changed; do not reset or restart it.");
    }
}
