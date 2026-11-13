using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
namespace Honor.Contracts.Preview;

/// <summary>Original native work, reference-only alongside the native job tracker's sole deep save.</summary>
internal static class ResidentActiveWork
{
    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object a, object b) => ReferenceEquals(a,b);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }
    private static void KnownFamily(Type type)
    {
        if (type.Assembly != typeof(Pawn).Assembly || AccessTools.GetDeclaredMethods(type).Any(m => Harmony.GetPatchInfo(m)?.Owners.Any() == true && !WorkProgressHooks.IsExactRecipeObserver(m,Harmony.GetPatchInfo(m)))) throw new ArgumentException("Patched native work/toil family.");
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)) KnownFamily(nested);
    }
    internal static object Raw(object value, Type owner, string name)
    {
        var f = AccessTools.Field(owner, name);
        if (value == null || f?.DeclaringType != owner || f.IsStatic) throw new ArgumentException("Unknown original work field: " + owner.Name + "." + name);
        return f.GetValue(value);
    }
    internal static void Known(Type type, string name)
    {
        var methods = AccessTools.GetDeclaredMethods(type).Where(m => m.Name == name).ToArray();
        if (!NativeTransferBodies.WorkObservationBodies || type.Assembly != typeof(Pawn).Assembly || methods.Length == 0 || methods.Any(m => Harmony.GetPatchInfo(m)?.Owners.Any() == true))
            throw new ArgumentException("Unreviewed native work boundary: " + type.Name + "." + name);
    }
    internal static List<QueuedJob> Queue(Pawn pawn)
    {
        if (pawn.jobs?.jobQueue?.GetType() != typeof(JobQueue) || Raw(pawn.jobs.jobQueue, typeof(JobQueue), "jobs") is not List<QueuedJob> rows ||
            rows.Count > 32 || rows.Any(q => q?.GetType() != typeof(QueuedJob) || q.job == null) || rows.Distinct().Count() != rows.Count)
            throw new ArgumentException("Original bounded native queue required.");
        Known(typeof(JobQueue), "ExposeData"); Known(typeof(QueuedJob), "ExposeData");
        return rows;
    }
    internal static List<Job> Jobs(Pawn pawn) => new[] { pawn.jobs.curJob }.Concat(Queue(pawn).Select(q => q.job)).ToList();
    // Same-call memory check only. These runtime identities are NEVER serialized or used as load/recovery evidence.
    internal static string Memory(Pawn pawn)
    {
        var seen = new HashSet<object>(new ReferenceComparer()); int count = 0;
        object Read(object value, int depth = 0)
        {
            if (value == null) return null;
            if (++count > 20000 || depth > 12) throw new ArgumentException("Native work memory capacity.");
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string or decimal) return new { Type = type.FullName, Value = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) };
            if (!type.IsValueType && !seen.Add(value)) return new { Ref = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value) };
            if (value is Delegate callback) return callback.GetInvocationList().Select(d => new { Method = d.Method.MetadataToken, Module = d.Method.Module.ModuleVersionId, Target = Read(d.Target, depth + 1) }).ToArray();
            if (value is IList list && type.Assembly == typeof(List<>).Assembly)
            { if (list.Count > 10000) throw new ArgumentException("Native work list capacity."); return list.Cast<object>().Select(v => Read(v, depth + 1)).ToArray(); }
            if (type.IsValueType || type.Assembly == typeof(Pawn).Assembly && type.IsDefined(typeof(CompilerGeneratedAttribute), false))
                return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => new { f.Name, Value = Read(f.GetValue(value), depth + 1) }).ToArray();
            return new { Type = type.FullName, Ref = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value) };
        }
        object Fields(object value) => value == null ? null : value.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => new { f.Name, Value = Read(f.GetValue(value)) }).ToArray();
        return JsonSerializer.Serialize(new { Tracker = Fields(pawn.jobs), Jobs = Jobs(pawn).Select(Fields).ToArray(), Driver = Fields(pawn.jobs.curDriver),
            Toils = ((List<Toil>)Raw(pawn.jobs.curDriver, typeof(JobDriver), "toils")).Select(Fields).ToArray(), Path = Fields(pawn.pather), Mind = Fields(pawn.mindState), Settings = Fields(pawn.workSettings) });
    }
    private static object Value(object value, Pawn pawn, int depth = 0)
    {
        if (depth > 6) throw new ArgumentException("Native work collection depth.");
        if (value == null) return null;
        if (value is bool or int or long or string) return value;
        if (value is float f && RuntimeCompat.IsFinite(f)) return f;
        if (value.GetType().IsEnum) return Convert.ToInt32(value);
        if (value is IntVec3 cell) return new { cell.x, cell.y, cell.z };
        if (value is Rot4 rotation) return rotation.AsInt;
        if (value is LocalTargetInfo target) return new { Thing = target.Thing == null ? null : Thing(target.Thing, pawn), Cell = Value(target.Cell, pawn) };
        if (value is GlobalTargetInfo global && global.Equals(GlobalTargetInfo.Invalid)) return "invalid";
        if (value is Def def && def.GetType().Assembly == typeof(Pawn).Assembly)
        {
            var database = typeof(DefDatabase<>).MakeGenericType(def.GetType());
            var method = database.GetMethod("GetNamedSilentFail", BindingFlags.Public | BindingFlags.Static);
            if (method == null || method.Invoke(null, new object[] { def.defName }) != def) throw new ArgumentException("Unregistered original work definition.");
            return new { Type = def.GetType().FullName, def.defName, Driver = def is JobDef jobDef ? jobDef.driverClass?.FullName : null };
        }
        if (value is Thing thing) return Thing(thing, pawn);
        if (value is Bill bill) return WorktableEvidence.Identity(bill, pawn);
        if (value is ThingCountClass counted) return new { Thing = Thing(counted.thing, pawn), Count = (int)Raw(counted, typeof(ThingCountClass), "countInt") };
        if (value is IList list && list.Count <= 256) return list.Cast<object>().Select(v => Value(v, pawn, depth + 1)).ToArray();
        throw new ArgumentException("Unsupported native work value: " + value.GetType().FullName);
    }
    private static string Thing(Thing thing, Pawn pawn)
    {
        // Physical map proof separately retains every target and its own property authority.
        if (thing == pawn.carryTracker?.CarriedThing) { _ = Carry(pawn); return thing.GetUniqueLoadID(); }
        if (thing is Pawn or Corpse || thing.Destroyed || thing.Map != pawn.Map || !thing.Spawned || thing.GetType().Assembly != typeof(Pawn).Assembly ||
            thing.Faction != null && thing.Faction != pawn.Faction && thing.Faction != pawn.Map.Parent.Faction)
            throw new ArgumentException("Original same-map civilian work target required.");
        return thing.GetUniqueLoadID();
    }
    internal static Thing CarriedReference(Pawn pawn) { _ = Carry(pawn); return pawn.carryTracker?.CarriedThing; }
    private static object Carry(Pawn pawn)
    {
        var tracker = pawn.carryTracker;
        if (tracker?.GetType() != typeof(Pawn_CarryTracker) || Raw(tracker, typeof(Pawn_CarryTracker), "pawn") != pawn ||
            tracker.innerContainer?.GetType() != typeof(ThingOwner<Thing>) || tracker.innerContainer.Owner != tracker || tracker.innerContainer.Count > 1)
            throw new ArgumentException("Original native carry owner required.");
        Known(typeof(Pawn_CarryTracker), "ExposeData"); Known(typeof(Pawn_CarryTracker), "get_CarriedThing");
        if (tracker.innerContainer.Count == 0) return null;
        var t = tracker.innerContainer[0];
        if (t != tracker.CarriedThing) throw new ArgumentException("Original carry slot changed.");
        return SimpleCargo(t, tracker.innerContainer, pawn);
    }
    internal static object SimpleCargo(Thing t, ThingOwner owner, Pawn pawn)
    {
        if (t == null || t.Spawned || t.Map != null || t.holdingOwner != owner || t.Destroyed || t.Faction != null ||
            t.GetType() != typeof(Thing) && t.GetType() != typeof(ThingWithComps) || t.def?.category != ThingCategory.Item || t.stackCount < 1 ||
            t is ThingWithComps c && c.AllComps.Any(comp => comp?.GetType() != typeof(CompQuality) && comp?.GetType() != typeof(CompForbiddable)))
            throw new ArgumentException("Only original neutral basic work cargo is currently reviewed.");
        if (DefDatabase<ThingDef>.GetNamedSilentFail(t.def.defName) != t.def || t.Stuff != null && DefDatabase<ThingDef>.GetNamedSilentFail(t.Stuff.defName) != t.Stuff)
            throw new ArgumentException("Original carry definitions changed.");
        var comps = (t as ThingWithComps)?.AllComps ?? new List<ThingComp>();
        if ((t.def.comps?.Count ?? 0) != comps.Count || comps.Select(x => x.GetType()).Distinct().Count() != comps.Count ||
            comps.Any(x => x.parent != t || x.props == null || x.props.compClass != x.GetType() || !t.def.comps.Contains(x.props))) throw new ArgumentException("Original carry components changed.");
        foreach (var comp in comps) Known(comp.GetType(), "PostExposeData");
        return new { Id = t.GetUniqueLoadID(), Def = t.def.defName, Stuff = t.Stuff?.defName, t.stackCount, HP = t.HitPoints,
            Comps = comps.Select(comp => new { Type = comp.GetType().FullName, Fields = comp.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(f => !f.IsStatic).OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => new { f.Name, Value = Value(f.GetValue(comp), pawn) }).ToArray() }).ToArray() };
    }
    private static object Fields(object value, Type type, string names, Pawn pawn, GroundRestGiver giver = null)
        => names.Split(' ').OrderBy(n => n, StringComparer.Ordinal).Select(n => new { Name = n, Value = giver != null && n == "jobGiverKey" ? giver.Key : Value(Raw(value, type, n), pawn) }).ToArray();
    private static object JobState(Job job, Pawn pawn)
    {
        if (job?.GetType() != typeof(Job) || job.loadID < 0 || job.def == null || !DriverSupported(job.def.driverClass) || job.mote != null ||
            job.flying || job.swimming || job.def.driverClass != typeof(JobDriver_DoBill) && job.bill != null || job.def.driverClass == typeof(JobDriver_DoBill) && job.bill == null ||
            job.ability != null || job.verbToUse != null || job.lord != null || job.quest != null)
            throw new ArgumentException("New native hauling/basic production adapter only.");
        var giver = job.jobGiver == null && job.jobGiverThinkTree == null ? null : GroundRestGivers.Capture(job);
        if (giver == null && Raw(job, typeof(Job), "jobGiverKey") is not -1) throw new ArgumentException("Unknown original job giver cache.");
        Known(typeof(Job), "ExposeData"); Known(typeof(Job), "GetUniqueLoadID");
        if (job.def.driverClass == typeof(JobDriver_ConstructFinishFrame) && job.targetA.Thing?.GetType() != typeof(Frame)) throw new ArgumentException("Original frame finishing target required.");
        if (job.def.driverClass == typeof(JobDriver_HaulToContainer) && (job.targetB.Thing?.GetType() != typeof(Frame) || job.targetC.IsValid && job.targetC.Thing?.GetType() != typeof(Frame) ||
            job.targetQueueB?.Any(t => t.Thing?.GetType() != typeof(Frame)) == true)) throw new ArgumentException("Original frame supply destinations required.");
        return new { Fields = Fields(job, typeof(Job), ResidentGroundRest.JobFields, pawn, giver), Giver = giver };
    }
    private static bool DriverSupported(Type type) => type == typeof(JobDriver_HaulToCell) || type == typeof(JobDriver_DoBill) || type == typeof(JobDriver_ConstructFinishFrame) || type == typeof(JobDriver_HaulToContainer);
    private static object Settings(Pawn pawn)
    {
        if (pawn.workSettings == null) return null;
        if (pawn.workSettings.GetType() != typeof(Pawn_WorkSettings) || Raw(pawn.workSettings, typeof(Pawn_WorkSettings), "pawn") != pawn)
            throw new ArgumentException("Original work settings owner required.");
        Known(typeof(Pawn_WorkSettings), "ExposeData");
        var priorities = Raw(pawn.workSettings, typeof(Pawn_WorkSettings), "priorities");
        if (priorities == null) return new { Initialized = false };
        if (priorities.GetType() != typeof(DefMap<WorkTypeDef, int>) || Raw(priorities, priorities.GetType(), "values") is not List<int> values ||
            values.Count != DefDatabase<WorkTypeDef>.DefCount || values.Any(v => v is < 0 or > 4)) throw new ArgumentException("Original work priorities unavailable.");
        Known(priorities.GetType(), "ExposeData");
        return new { Definitions = DefDatabase<WorkTypeDef>.AllDefsListForReading.Select(d => new { d.defName, d.index }).ToArray(), Values = values.ToArray() };
    }
    private static object MindWork(Pawn pawn)
    {
        var priority=pawn.mindState?.priorityWork;
        if(priority?.GetType()!=typeof(PriorityWork) || Raw(priority,typeof(PriorityWork),"pawn")!=pawn || pawn.mindState.thinkData?.GetType()!=typeof(Dictionary<int,int>) || pawn.mindState.thinkData.Count>4096)
            throw new ArgumentException("Original bounded native priority/think history required.");
        Known(typeof(PriorityWork),"ExposeData"); Known(typeof(Pawn_MindState),"ExposeData");
        var cell=(IntVec3)Raw(priority,typeof(PriorityWork),"prioritizedCell");var giver=(WorkGiverDef)Raw(priority,typeof(PriorityWork),"prioritizedWorkGiver");
        var tick=(int)Raw(priority,typeof(PriorityWork),"prioritizeTick");
        if(cell.IsValid && !cell.InBounds(pawn.Map) || cell.IsValid!=(giver!=null) || tick<0 || tick>Find.TickManager.TicksGame) throw new ArgumentException("Original priority work scope/clock invalid.");
        return new { Priority=Fields(priority,typeof(PriorityWork),"prioritizedCell prioritizedWorkGiver prioritizeTick",pawn),
            Think=pawn.mindState.thinkData.OrderBy(p=>p.Key).Select(p=>new {p.Key,p.Value}).ToArray() };
    }
    internal static List<ReservationManager.Reservation> Reservations(Map map)
    {
        var manager = map.reservationManager;
        if (manager?.GetType() != typeof(ReservationManager) || Raw(manager, typeof(ReservationManager), "map") != map ||
            Raw(manager, typeof(ReservationManager), "reservations") is not List<ReservationManager.Reservation> rows || rows.Count > 10000 ||
            rows.Any(r => r?.GetType() != typeof(ReservationManager.Reservation)) || rows.Distinct().Count() != rows.Count)
            throw new ArgumentException("Original native reservation graph required.");
        Known(typeof(ReservationManager), "ExposeData"); Known(typeof(ReservationManager.Reservation), "ExposeData");
        return rows;
    }
    internal static object ReservationState(Map map)
    {
        if (map.Parent is not Settlement city || city.previouslyGeneratedInhabitants == null) throw new ArgumentException("Original native work town required.");
        return Reservations(map).Select((r, index) => {
            var pawn = Raw(r, typeof(ReservationManager.Reservation), "claimant") as Pawn;
            var job = Raw(r, typeof(ReservationManager.Reservation), "job") as Job;
            if (pawn?.Map != map || !pawn.Spawned || pawn.IsPrisoner || pawn.IsSlave || !city.previouslyGeneratedInhabitants.Contains(pawn) || !Jobs(pawn).Contains(job))
                throw new ArgumentException("Every reservation needs its original current/queued civilian job.");
            var target = (LocalTargetInfo)Raw(r, typeof(ReservationManager.Reservation), "target");
            if (!target.IsValid || target.Thing == null && !target.Cell.InBounds(map) || (int)Raw(r, typeof(ReservationManager.Reservation), "maxPawns") < 1 ||
                Raw(r, typeof(ReservationManager.Reservation), "stackCount") is not int count || count != -1 && count < 1) throw new ArgumentException("Original reservation limits/target invalid.");
            _ = JobState(job, pawn);
            return new { Index = index, Pawn = pawn.GetUniqueLoadID(), Job = job.GetUniqueLoadID(),
                Fields = Fields(r, typeof(ReservationManager.Reservation), "target layer maxPawns stackCount", pawn) };
        }).ToArray();
    }
    internal static List<PhysicalInteractionReservationManager.PhysicalInteractionReservation> PhysicalRows(Map map)
    {
        if (map?.physicalInteractionReservationManager?.GetType() != typeof(PhysicalInteractionReservationManager) ||
            Raw(map.physicalInteractionReservationManager, typeof(PhysicalInteractionReservationManager), "reservations") is not List<PhysicalInteractionReservationManager.PhysicalInteractionReservation> rows ||
            rows.Count > 10000 || rows.Any(r => r?.GetType() != typeof(PhysicalInteractionReservationManager.PhysicalInteractionReservation)) || rows.Distinct().Count() != rows.Count)
            throw new ArgumentException("Original physical ingredient claims required.");
        Known(typeof(PhysicalInteractionReservationManager), "ExposeData"); Known(typeof(PhysicalInteractionReservationManager.PhysicalInteractionReservation), "ExposeData");
        return rows;
    }
    internal static object PhysicalState(Map map)
    {
        if (map.Parent is not Settlement city || city.previouslyGeneratedInhabitants == null) throw new ArgumentException("Original physical work town required.");
        return PhysicalRows(map).Select((r,i) => {
            if (r.claimant?.Map != map || !r.claimant.Spawned || r.claimant.IsPrisoner || r.claimant.IsSlave || !city.previouslyGeneratedInhabitants.Contains(r.claimant) ||
                !Jobs(r.claimant).Contains(r.job) || !r.target.IsValid || r.target.Thing == null && !r.target.Cell.InBounds(map)) throw new ArgumentException("Original civilian physical claimant/job/target required.");
            _ = JobState(r.job, r.claimant);
            return new { Index = i, Pawn = r.claimant.GetUniqueLoadID(), Job = r.job.GetUniqueLoadID(), Target = Value(r.target, r.claimant) };
        }).ToArray();
    }
    internal static List<ThingCountTracker> EnrouteRows(Map map)
    {
        if (map?.enrouteManager?.GetType() != typeof(EnrouteManager) || Raw(map.enrouteManager, typeof(EnrouteManager), "map") != map ||
            Raw(map.enrouteManager, typeof(EnrouteManager), "enroute") is not List<ThingCountTracker> rows || rows.Count > 1000 ||
            rows.Any(r => r?.GetType() != typeof(ThingCountTracker)) || rows.Distinct().Count() != rows.Count ||
            Raw(map.enrouteManager, typeof(EnrouteManager), "lookup") is not Dictionary<IHaulEnroute, ThingCountTracker> lookup || lookup.Count != rows.Count ||
            rows.Any(r => r.parent?.GetType() != typeof(Frame) || ((Frame)r.parent).Map != map || !lookup.TryGetValue(r.parent,out var linked) || linked != r))
            throw new ArgumentException("Original frame enroute registry and lookup required.");
        Known(typeof(EnrouteManager), "ExposeData"); Known(typeof(ThingCountTracker), "ExposeData"); Known(typeof(ThingCountTracker.PawnCount), "ExposeData");
        return rows;
    }
    internal static object EnrouteState(Map map)
    {
        if (map.Parent is not Settlement city || city.previouslyGeneratedInhabitants == null) throw new ArgumentException("Original frame supply town required.");
        int total = 0; var seen = new HashSet<ThingCountTracker.PawnCount>();
        return EnrouteRows(map).Select((tracker,i) => {
            _ = ConstructionEvidence.Capture((Frame)tracker.parent);
            if (Raw(tracker, typeof(ThingCountTracker), "stuffCount") is not Dictionary<ThingDef,List<ThingCountTracker.PawnCount>> stuff || stuff.Count > 1000)
                throw new ArgumentException("Original enroute material counts required.");
            return new { Index=i, Frame=((Frame)tracker.parent).GetUniqueLoadID(), Stuff=stuff.OrderBy(s=>s.Key?.defName,StringComparer.Ordinal).Select(pair=> {
                if(pair.Key == null || DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key.defName)!=pair.Key || pair.Value==null || pair.Value.Count>128 || pair.Value.Select(r=>r?.pawn).Distinct().Count()!=pair.Value.Count)
                    throw new ArgumentException("Original distinct frame supply claimants required.");
                return new { Def=pair.Key.defName, Claims=pair.Value.Select(row=> {
                    var pawn=row?.pawn; var job=pawn?.jobs?.curJob;
                    if(++total>10000 || row?.GetType()!=typeof(ThingCountTracker.PawnCount) || !seen.Add(row) || row.count<=0 || pawn?.Map!=map || !pawn.Spawned || pawn.IsPrisoner || pawn.IsSlave ||
                        !city.previouslyGeneratedInhabitants.Contains(pawn) || job?.def.driverClass!=typeof(JobDriver_HaulToContainer) || job.targetA.Thing?.def!=pair.Key ||
                        job.targetB.Thing!=tracker.parent && job.targetC.Thing!=tracker.parent && job.targetQueueB?.Any(t=>ReferenceEquals(t.Thing,tracker.parent))!=true)
                        throw new ArgumentException("Original current frame supply job/material rights required.");
                    _=JobState(job,pawn); return new { Pawn=pawn.GetUniqueLoadID(), row.count };
                }).ToArray() };
            }).ToArray() };
        }).ToArray();
    }
    internal static Dictionary<Faction, PawnDestinationReservationManager.PawnDestinationSet> Destinations(Map map)
    {
        if (map?.pawnDestinationReservationManager?.GetType() != typeof(PawnDestinationReservationManager) ||
            Raw(map.pawnDestinationReservationManager, typeof(PawnDestinationReservationManager), "reservedDestinations") is not Dictionary<Faction, PawnDestinationReservationManager.PawnDestinationSet> groups ||
            groups.Count > 128 || groups.Any(g => g.Key == null || g.Value?.GetType() != typeof(PawnDestinationReservationManager.PawnDestinationSet) || g.Value.list == null))
            throw new ArgumentException("Original native destination groups required.");
        Known(typeof(PawnDestinationReservationManager), "ExposeData"); Known(typeof(PawnDestinationReservationManager.PawnDestinationSet), "ExposeData");
        Known(typeof(PawnDestinationReservationManager.PawnDestinationReservation), "ExposeData");
        return groups;
    }
    internal static object DestinationState(Map map)
    {
        if (map.Parent is not Settlement city || city.previouslyGeneratedInhabitants == null) throw new ArgumentException("Original destination town required.");
        var rows = new List<(Pawn Pawn, Job Job, IntVec3 Cell, bool Obsolete, int Order)>();
        var unique = new HashSet<PawnDestinationReservationManager.PawnDestinationReservation>();
        var owners = new HashSet<PawnDestinationReservationManager.PawnDestinationSet>();
        foreach (var group in Destinations(map))
        {
            if (!owners.Add(group.Value) || group.Value.list.Count > 10000) throw new ArgumentException("Aliased/oversized destination group.");
            foreach (var r in group.Value.list)
            {
                if (r?.GetType() != typeof(PawnDestinationReservationManager.PawnDestinationReservation) || !unique.Add(r) || unique.Count > 10000 ||
                    r.claimant?.Faction != group.Key || r.claimant.Map != map || !r.claimant.Spawned || r.claimant.IsPrisoner || r.claimant.IsSlave ||
                    !city.previouslyGeneratedInhabitants.Contains(r.claimant) || !Jobs(r.claimant).Contains(r.job) || !r.target.InBounds(map))
                    throw new ArgumentException("Each original destination requires its actual current civilian claimant/job/faction index.");
                _ = JobState(r.job, r.claimant);
                rows.Add((r.claimant, r.job, r.target, r.obsolete, rows.Count(x => x.Pawn == r.claimant)));
            }
        }
        // Cross-claimant list order is not a reservation right; each claimant's original order IS.
        return rows.OrderBy(r => r.Pawn.GetUniqueLoadID(), StringComparer.Ordinal).ThenBy(r => r.Order).Select(r => new { Pawn = r.Pawn.GetUniqueLoadID(), Job = r.Job.GetUniqueLoadID(), Cell = map.cellIndices.CellToIndex(r.Cell), r.Obsolete, r.Order }).ToArray();
    }
    internal static ActiveWorkRow Capture(Pawn pawn)
    {
        if (pawn?.jobs?.curJob == null) return null;
        if (!NativeTransferBodies.WorkObservationBodies || pawn.jobs.GetType() != typeof(Pawn_JobTracker) || !pawn.Spawned || pawn.jobs.startingNewJob ||
            pawn.jobs.curDriver == null || !DriverSupported(pawn.jobs.curDriver.GetType()) || pawn.jobs.curDriver.GetType() != pawn.jobs.curJob.def?.driverClass || pawn.jobs.curDriver.job != pawn.jobs.curJob || pawn.jobs.curDriver.pawn != pawn ||
            pawn.jobs.curDriver.ended || Raw(pawn.pather, typeof(Pawn_PathFollower), "curPathRequest") != null) throw new ArgumentException("Original native hauling driver without an in-flight path request required.");
        Known(typeof(JobDriver_HaulToCell), "ExposeData"); Known(typeof(JobDriver_HaulToCell), "MakeNewToils"); Known(typeof(JobDriver), "ExposeData");
        if (pawn.jobs.curDriver.GetType() == typeof(JobDriver_DoBill)) { Known(typeof(JobDriver_DoBill), "ExposeData"); Known(typeof(JobDriver_DoBill), "MakeNewToils"); }
        if (pawn.jobs.curDriver.GetType() == typeof(JobDriver_ConstructFinishFrame)) Known(typeof(JobDriver_ConstructFinishFrame), "MakeNewToils");
        if (pawn.jobs.curDriver.GetType() == typeof(JobDriver_HaulToContainer)) { Known(typeof(JobDriver_HaulToContainer), "MakeNewToils"); if (Raw(pawn.jobs.curDriver,typeof(JobDriver_HaulToContainer),"graveDigEffect") != null) throw new ArgumentException("Unknown transient container effect."); }
        Known(typeof(Pawn_JobTracker), "ExposeData"); Known(typeof(Pawn_PathFollower), "ExposeData");
        var jobs = Jobs(pawn);
        if (jobs.Distinct().Count() != jobs.Count) throw new ArgumentException("Original current/queued jobs overlap.");
        var driver = pawn.jobs.curDriver;
        foreach (var job in jobs)
        {
            var cached = Raw(job,typeof(Job),"cachedDriver"); var last = Raw(job,typeof(Job),"lastJobDriverMade");
            if (ReferenceEquals(job,pawn.jobs.curJob) ? cached != null && cached != driver || last != null && last != driver : cached != null || last != null)
                throw new ArgumentException("Original job has an unrelated or unsaved queued driver cache.");
        }
        foreach (var type in new[] { typeof(JobDriver), typeof(JobDriver_HaulToCell), typeof(JobDriver_HaulToContainer), typeof(JobDriver_DoBill), typeof(JobDriver_ConstructFinishFrame),
            typeof(Toils_Goto), typeof(Toils_Haul), typeof(Toils_Recipe), typeof(Toils_Reserve), typeof(Toils_Jump), typeof(Toils_JobTransforms), typeof(Toils_General), typeof(ToilMaker) }) KnownFamily(type);
        if (Raw(driver, typeof(JobDriver), "finalizerJobFactory") != null || driver.locomotionUrgencySameAs != null ||
            Raw(driver, typeof(JobDriver), "curToilIndex") is not int toil || toil < 0 || Raw(driver, typeof(JobDriver), "wantBeginNextToil") is not false)
            throw new ArgumentException("Original stable native work toil required.");
        // Native save owns toil reconstruction. Reject external callbacks, never copy/restart a driver.
        if (Raw(driver, typeof(JobDriver), "toils") is not List<Toil> toils || toils.Count > 128 || toil >= toils.Count || toils.Any(t => t == null || t.actor != pawn || t.inPool))
            throw new ArgumentException("Original work toils unavailable.");
        foreach (var callback in toils.SelectMany(t => typeof(Toil).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(f => f.GetValue(t))).OfType<Delegate>()
            .Concat(driver.globalFailConditions).Concat(driver.globalFinishActions))
            foreach (var d in callback.GetInvocationList()) if (d.Method.Module.Assembly != typeof(Pawn).Assembly || Harmony.GetPatchInfo(d.Method)?.Owners.Any() == true && !WorkProgressHooks.IsExactRecipeObserver(d.Method,Harmony.GetPatchInfo(d.Method)))
                throw new ArgumentException("External native work callback.");
        var saved = JsonSerializer.Serialize(new { Jobs = jobs.Select(j => JobState(j, pawn)).ToArray(), Tags = Queue(pawn).Select(q => q.tag.HasValue ? (int?)q.tag.Value : null).ToArray(),
            Driver = Fields(driver, typeof(JobDriver), ResidentGroundRest.DriverFields, pawn),
            Specific = driver.GetType() == typeof(JobDriver_HaulToCell) ? Fields(driver, typeof(JobDriver_HaulToCell), "forbiddenInitially", pawn) : driver.GetType() == typeof(JobDriver_DoBill) ? Fields(driver, typeof(JobDriver_DoBill), "workLeft billStartTick ticksSpentDoingRecipeWork", pawn) : null,
            Posture = (int)pawn.jobs.posture, Priorities = Settings(pawn), MindWork = MindWork(pawn), Carry = Carry(pawn),
            Path = Fields(pawn.pather, typeof(Pawn_PathFollower), "moving nextCell nextCellCostLeft nextCellCostTotal peMode cellsUntilClamor lastEnteredCellTick lastMovedTick debugDisabled curPathJobIsStale", pawn),
            Destination = pawn.pather.Moving ? Value(Raw(pawn.pather, typeof(Pawn_PathFollower), "destination"), pawn) : null,
            Reservations = ReservationState(pawn.Map), Destinations = DestinationState(pawn.Map), Physical = PhysicalState(pawn.Map), Enroute = EnrouteState(pawn.Map) });
        var row = new ActiveWorkRow(pawn.GetUniqueLoadID(), jobs.Select(j => j.GetUniqueLoadID()).ToArray(), saved); _ = ActiveWork.Proof(row); return row;
    }
    internal static void Validate(ResidentExecutionRecord record)
    {
        if (record.Profile != 16) { if (record.ActiveWorkJobs != null || record.ActiveWorkTrees != null || record.ActiveWorkCarry != null) throw new ArgumentException("Old clinical scope cannot acquire active work references."); return; }
        var original = LoadedMapPreservation.Restore(record.LoadedMapScope).ActiveWorkV1;
        var current = record.Pawns.Where(p => p.CurJob != null).Select(Capture).ToArray();
        if (original == null || JsonSerializer.Serialize(current) != JsonSerializer.Serialize(original) || record.ActiveWorkJobs == null ||
            !record.ActiveWorkJobs.SequenceEqual(record.Pawns.Where(p => p.CurJob != null).SelectMany(Jobs)) || record.ActiveWorkTrees == null ||
            !record.ActiveWorkTrees.SequenceEqual(record.ActiveWorkJobs.Select(j => j.jobGiverThinkTree).Where(t => t != null).Distinct().OrderBy(t => t.defName, StringComparer.Ordinal)) || record.ActiveWorkCarry == null ||
            !record.ActiveWorkCarry.SequenceEqual(record.Pawns.Select(CarriedReference).Where(t => t != null)))
            throw new ArgumentException("Original work changed/progressed; retain its journals, never reset/restart.");
    }
}
