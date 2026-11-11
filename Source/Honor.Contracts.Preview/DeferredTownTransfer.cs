using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using HarmonyLib;
namespace Honor.Contracts.Preview;
public sealed class DeferredTownRecord : IExposable
{
    public string Journal;
    public Settlement City;
    public Map Map;
    public Faction Source, Target;
    public List<Pawn> Before, People;
    public void ExposeData()
    {
        Scribe_Values.Look(ref Journal, "originalDeferredTownV1");
        Scribe_References.Look(ref City, "originalTown"); Scribe_References.Look(ref Map, "originalMap");
        Scribe_References.Look(ref Source, "originalSource"); Scribe_References.Look(ref Target, "originalTarget");
        Scribe_Collections.Look(ref Before, "originalRoster", LookMode.Reference); Scribe_Collections.Look(ref People, "originalPeople", LookMode.Reference);
    }
    public void Release() { City = null; Map = null; Source = null; Target = null; Before = null; People = null; }
}
public sealed partial class TerritoryAdministrationLedger
{
    private List<DeferredTownRecord> deferredTowns;
    private bool progressingDeferredTown;
    private bool DeferredReserves(Pawn pawn) => !progressingDeferredTown && deferredTowns?.Any(r => DeferredTownTransfer.Restore(r.Journal).Phase is DeferredTownPhase.Waiting or DeferredTownPhase.Started && r.People?.Contains(pawn) == true) == true;
    private void ExposeDeferredTowns()
    {
        Scribe_Collections.Look(ref deferredTowns, "honorDeferredTownTransfersV1", LookMode.Deep);
        if (Scribe.mode != LoadSaveMode.PostLoadInit || deferredTowns == null) return;
        try
        {
            if (deferredTowns.Count > 128 || deferredTowns.Any(r => r == null)) throw new ArgumentException("Deferred archive capacity.");
            foreach (var r in deferredTowns) _ = DeferredTownTransfer.Restore(r.Journal);
            if (deferredTowns.Select(r => DeferredTownTransfer.Restore(r.Journal).Id).Distinct().Count() != deferredTowns.Count ||
                deferredTowns.Count(r => DeferredTownTransfer.Restore(r.Journal).Phase is DeferredTownPhase.Waiting or DeferredTownPhase.Started) > 1) throw new ArgumentException("Overlapping original deferred transfers.");
        }
        catch (Exception e) when (e is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Deferred town raw journal retained after restore failure: " + e.GetType().Name); }
    }
    private void ValidateDeferredTowns()
    {
        foreach (var r in deferredTowns ?? [])
        {
            var s = DeferredTownTransfer.Restore(r.Journal);
            if (s.Tick > Find.TickManager.TicksGame) throw new ArgumentException("Deferred clock is in the future.");
            if (s.Phase == DeferredTownPhase.Applied)
            {
                var receipt = Ledger.Transfer(s.Transfer);
                if (receipt.Phase != TerritoryTransferPhase.Applied || receipt.Charter != s.Charter || receipt.Territory != s.Town || receipt.FromOwner != s.Source || receipt.TargetOwner != s.Target || receipt.PreparedTick < s.Tick || s.PreviousTransfers.Contains(receipt.Id))
                    throw new ArgumentException("Deferred outcome lacks its positive independent applied receipt.");
                r.Release();
            }
        }
    }
    private void DeferredContext(DeferredTownRecord r, bool initial)
    {
        EnrollmentReady();
        foreach (var read in new[] { AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.CurJob)), AccessTools.PropertyGetter(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.Moving)),
            AccessTools.PropertyGetter(typeof(JobQueue), nameof(JobQueue.Count)), AccessTools.Method(typeof(Job), nameof(Job.GetUniqueLoadID)) })
            if (read == null || read.DeclaringType.Assembly != typeof(Pawn).Assembly || Harmony.GetPatchInfo(read)?.Owners.Any() == true) throw new ArgumentException("Unknown native work read boundary.");
        var s = DeferredTownTransfer.Restore(r.Journal);
        if (s.Tick > Find.TickManager.TicksGame) throw new ArgumentException("Future deferred intent.");
        if (!NativeTransferBodies.WorkObservationBodies || r.City == null || r.City.GetType() != typeof(Settlement) || r.City.Destroyed || !Find.WorldObjects.AllWorldObjects.Contains(r.City) ||
            !r.City.HasMap || r.City.Map != r.Map || r.Map?.Parent != r.City || r.Map.GetType() != typeof(Map) || r.Map.IsPlayerHome || !Find.Maps.Contains(r.Map) || r.City.Faction != r.Source || r.Source == r.Target ||
            r.Source == null || r.Target == null || r.Target.IsPlayer || !Find.FactionManager.AllFactionsListForReading.Contains(r.Target) || r.Source.HostileTo(r.Target) || r.Target.HostileTo(r.Source) ||
            s.Town != "territory/" + r.City.GetUniqueLoadID() || s.Map != r.Map.GetUniqueLoadID() || s.Source != r.Source.GetUniqueLoadID() || s.Target != r.Target.GetUniqueLoadID() ||
            r.Before == null || r.People == null || r.Before.Any(p => p == null) || r.People.Any(p => p == null || p.Destroyed || p.Dead || p.Faction != r.Source) ||
            !r.Before.Select(p => p.GetUniqueLoadID()).SequenceEqual(s.Before) || !r.People.Select(p => p.GetUniqueLoadID()).SequenceEqual(s.People.Select(p => p.Id)) ||
            r.City.previouslyGeneratedInhabitants == null || !r.City.previouslyGeneratedInhabitants.SequenceEqual(r.Before)) throw new ArgumentException("Original deferred town/person/source references changed.");
        var c = Charters.Book.Charter(s.Charter); var b = Charters.Binding(c.Terms.Subject);
        if (c.Phase != RecordedCharterPhase.Active || !Charters.Book.PlayerManaged(c) || IndependentlyLostTerritory(c.Terms.Subject) ||
            !Ledger.VerifiedOwner(c.Terms.Subject, Charters.Book.Entity(c.Terms.Subject).NativeOwner, s.Source) || b.PlayerGroup || b.FactionGroup || b.Settlement != r.City ||
            Current.Game.GetComponent<SubtreeExitLedger>().Scope(s.Charter) != null || Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority() != r.Target)
            throw new ArgumentException("Original ordinary active charter/target changed.");
        var actualPeople = r.Before.Concat(ResidentMapActors.SpawnedRoster(r.Map).Where(p => p != null && p.Faction == r.Source)).Distinct().OrderBy(p => p.GetUniqueLoadID(), StringComparer.Ordinal).ToList();
        if (!actualPeople.SequenceEqual(r.People)) throw new ArgumentException("Deferred resident scope changed; never add late people silently.");
        DeferredTownTransfer.Review(r.Journal, EnrollmentDigest(Charters.Book.Export()), NpcDecisionAuthority.Read(r.Source), actualPeople.Select(p => p.GetUniqueLoadID()));
        if (!Ledger.Transfers.Select(t => t.Id).SequenceEqual(s.PreviousTransfers)) throw new ArgumentException("Original physical ledger changed while waiting.");
        if (initial && r.People.Any(p => ReservesResident(p) || Current.Game.GetComponent<LeaderJoinLedger>().ReservesResident(p) || Current.Game.GetComponent<LeaderRelocationLedger>().ReservesResident(p)))
            throw new ArgumentException("Another execution owns these people.");
    }
    private DeferredTownRecord ReviewDeferredTown(string charter)
    {
        EnrollmentReady();
        if (!NativeTransferBodies.WorkObservationBodies) throw new ArgumentException("Unreviewed native work bodies.");
        if ((deferredTowns?.Count ?? 0) >= 128 || deferredTowns?.Any(r => DeferredTownTransfer.Restore(r.Journal).Phase is DeferredTownPhase.Waiting or DeferredTownPhase.Started) == true ||
            residentEnrollments?.Any(r => !ResidentEnrollment.Restore(r.Journal).Verified) == true) throw new ArgumentException("Resolve the existing intent first.");
        var c = Charters.Book.Charter(charter); var city = Charters.Binding(c.Terms.Subject).Settlement;
        if (city?.HasMap != true || city.previouslyGeneratedInhabitants == null || city.previouslyGeneratedInhabitants.Any(p => p == null)) throw new ArgumentException("Current loaded NPC town required.");
        var source = Charters.SubcharterRepresentative(c.Terms.Subject); var target = Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority();
        var people = city.previouslyGeneratedInhabitants.Concat(ResidentMapActors.SpawnedRoster(city.Map).Where(p => p?.Faction == source)).Distinct().OrderBy(p => p.GetUniqueLoadID(), StringComparer.Ordinal).ToList();
        if (people.Count > 128 || people.Any(p => p == null || p.GetType() != typeof(Pawn) || p.jobs?.GetType() != typeof(Pawn_JobTracker) || p.jobs.curJob != null && p.jobs.curJob.GetType() != typeof(Job))) throw new ArgumentException("Unsupported work owner.");
        var r = new DeferredTownRecord { City = city, Map = city.Map, Source = source, Target = target, Before = city.previouslyGeneratedInhabitants.ToList(), People = people };
        // Read the native stored job, never StopAll/EndCurrentJob/ClearReservation/StartJob or duplicate deep-save its driver/queue.
        foreach (var p in people)
            if (p.jobs.jobQueue?.GetType() != typeof(JobQueue) || p.pather != null && p.pather.GetType() != typeof(Pawn_PathFollower)) throw new ArgumentException("Unknown native work tracker.");
        var jobRead = AccessTools.Method(typeof(Job), nameof(Job.GetUniqueLoadID));
        var currentJobRead = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.CurJob));
        if (jobRead == null || currentJobRead == null || Harmony.GetPatchInfo(jobRead)?.Owners.Any() == true || Harmony.GetPatchInfo(currentJobRead)?.Owners.Any() == true) throw new ArgumentException("Unknown job observation.");
        var evidence = people.Select(p => new DeferredTownPerson(p.GetUniqueLoadID(), p.jobs.curJob?.GetUniqueLoadID(), EnrollmentDigest(System.Text.Json.JsonSerializer.Serialize(ResidentBaselineStore.Evidence(p))))).ToArray();
        r.Journal = DeferredTownTransfer.Prepare("deferred-" + Guid.NewGuid().ToString("N"), charter, "territory/" + city.GetUniqueLoadID(), city.Map.GetUniqueLoadID(), source.GetUniqueLoadID(), target?.GetUniqueLoadID(),
            EnrollmentDigest(Charters.Book.Export()), NpcDecisionAuthority.Read(source), r.Before.Select(p => p.GetUniqueLoadID()), Ledger.Transfers.Select(t => t.Id), evidence, Find.TickManager.TicksGame);
        DeferredContext(r, true); return r;
    }
    private void EnqueueDeferredTown(DeferredTownRecord r)
    {
        try { DeferredContext(r, true); deferredTowns ??= []; if (deferredTowns.Any(x => DeferredTownTransfer.Restore(x.Journal).Phase is DeferredTownPhase.Waiting or DeferredTownPhase.Started)) throw new ArgumentException("Intent already pending."); deferredTowns.Add(r); Messages.Message("RE_DeferredQueued".Translate(), MessageTypeDefOf.NeutralEvent); }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Deferred town not enqueued: " + e.GetType().Name); Messages.Message("RE_EnrollmentBlocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
    public override void GameComponentTick()
    {
        if (Current.Game != ownerGame || Current.ProgramState != ProgramState.Playing || RestoreFailed || progressingDeferredTown || Find.TickManager.TicksGame % 250 != 0) return;
        foreach (var r in (deferredTowns ?? []).Where(r => DeferredTownTransfer.Restore(r.Journal).Phase == DeferredTownPhase.Waiting).ToArray()) ProgressDeferredTown(r);
    }
    private void ProgressDeferredTown(DeferredTownRecord r)
    {
        progressingDeferredTown = true;
        try
        {
            try { DeferredContext(r, false); }
            catch (Exception) { r.Journal = DeferredTownTransfer.Stop(r.Journal); Messages.Message("RE_DeferredStopped".Translate(), MessageTypeDefOf.NeutralEvent); return; }
            // Native work and reservation managers keep owning/saving all ongoing progress while we wait.
            if (r.People.Any(p => p.jobs.curJob != null || p.jobs.curDriver != null || p.jobs.startingNewJob || p.jobs.jobQueue.Count != 0 || p.pather?.Moving == true)) return;
            try { ResidentMapActors.RequireEmptyReservations(r.Map); }
            catch (ArgumentException) { return; }
            ResidentEnrollmentRecord enrollment = null;
            TerritoryAdministrationReview reviewed = null;
            try
            {
                if (ResidentMapActors.SpawnedRoster(r.Map).Any(p => p?.Faction == r.Source && !r.Before.Contains(p))) enrollment = ReviewEnrollment(DeferredTownTransfer.Restore(r.Journal).Charter);
                else reviewed = Review(DeferredTownTransfer.Restore(r.Journal).Charter);
                if (reviewed != null && reviewed.TargetIdentity != r.Target.GetUniqueLoadID()) throw new ArgumentException("Target changed.");
            }
            catch (ArgumentException) { return; }
            var original = DeferredTownTransfer.Restore(r.Journal);
            r.Journal = DeferredTownTransfer.Begin(r.Journal, EnrollmentDigest(Charters.Book.Export()), NpcDecisionAuthority.Read(r.Source), r.People.Select(p => p.GetUniqueLoadID()));
            if (enrollment != null)
            {
                ApplyEnrollment(enrollment, true);
                if (!ResidentEnrollment.Restore(enrollment.Journal).Verified) throw new ArgumentException("Original registration did not positively finish.");
                reviewed = Review(original.Charter);
            }
            if (reviewed.TargetIdentity != original.Target) throw new ArgumentException("Current full target review changed.");
            _ = Apply(reviewed);
            ReconcileDeferredTown(r, false);
        }
        catch (Exception e) { StopUnpromotedIntent(r); Log.Warning("[Honor.Contracts.Preview] Deferred town execution stopped; original native/enrollment journals retained: " + e.GetType().Name); Messages.Message("RE_DeferredStopped".Translate(), MessageTypeDefOf.NeutralEvent); }
        finally { progressingDeferredTown = false; }
    }
    private void StopUnpromotedIntent(DeferredTownRecord r)
    {
        if (Current.Game != ownerGame || RestoreFailed) return;
        var s = DeferredTownTransfer.Restore(r.Journal);
        // Stop only this scheduling intent, never mark a native migration as cancelled or complete.
        // Native execution is records-first; any existing matching receipt keeps its original recovery ownership.
        if (s.Phase == DeferredTownPhase.Started && !Ledger.Transfers.Any(t => !s.PreviousTransfers.Contains(t.Id) && t.Charter == s.Charter && t.Territory == s.Town && t.FromOwner == s.Source && t.TargetOwner == s.Target))
            r.Journal = DeferredTownTransfer.Stop(r.Journal);
    }
    private void ReconcileDeferredTown(DeferredTownRecord r, bool notify)
    {
        try
        {
            if (RestoreFailed || Current.Game != ownerGame) throw new ArgumentException("Original game required.");
            var s = DeferredTownTransfer.Restore(r.Journal);
            var candidates = Ledger.Transfers.Where(t => !s.PreviousTransfers.Contains(t.Id) && t.Charter == s.Charter && t.Territory == s.Town && t.FromOwner == s.Source && t.TargetOwner == s.Target && t.PreparedTick >= s.Tick).ToArray();
            if (candidates.Length != 1) { StopUnpromotedIntent(r); throw new ArgumentException("No unique independent original migration receipt."); }
            r.Journal = DeferredTownTransfer.Applied(r.Journal, candidates[0]); r.Release();
            Messages.Message("RE_DeferredCompleted".Translate(), MessageTypeDefOf.NeutralEvent);
        }
        catch (ArgumentException) { if (notify) Messages.Message("RE_DeferredRecover".Translate(), MessageTypeDefOf.RejectInput); }
    }
    internal void DrawDeferredTowns(Listing_Standard list, string charter)
    {
        if (RestoreFailed) return;
        if (list.ButtonText("RE_DeferredReview".Translate()))
        {
            try { var r = ReviewDeferredTown(charter); Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RE_DeferredConfirm".Translate(r.City.Label, r.Target.Name, r.People.Count, r.Source.Name, r.Source.leader.Name?.ToStringFull), () => EnqueueDeferredTown(r))); }
            catch (ArgumentException) { Messages.Message("RE_EnrollmentBlocked".Translate(), MessageTypeDefOf.RejectInput); }
        }
        foreach (var r in (deferredTowns ?? []).Where(r => DeferredTownTransfer.Restore(r.Journal).Charter == charter).ToArray())
        {
            var s = DeferredTownTransfer.Restore(r.Journal); list.Label(("RE_Deferred_" + s.Phase).Translate());
            if (s.Phase == DeferredTownPhase.Waiting && list.ButtonText("RE_DeferredCancel".Translate())) { r.Journal = DeferredTownTransfer.Cancel(r.Journal); r.Release(); }
            if (s.Phase == DeferredTownPhase.Started && list.ButtonText("RE_DeferredReconcile".Translate())) ReconcileDeferredTown(r, true);
        }
    }
}
