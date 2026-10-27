using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

public sealed record PoliticalChangeReview(Faction Destination, AllegianceCommand Command, FollowerTransferPlan Followers, string Stamp, UnionWarConsent Wars = null, Game Context = null)
{
    public UnionPeaceConsent Peace { get; init; }
}

/// <summary>Coordinates one reviewed allegiance change and all live NPC subjects; never resumes automatically on load.</summary>
public sealed class PoliticalTransitionLedger : GameComponent
{
    private string snapshot;
    private List<string> warHistory = [];
    public AllegianceTransitionLedger Journal { get; private set; } = new();
    public bool RestoreFailed { get; private set; }
    public bool Executing { get; private set; }
    private bool sharingPeace;
    internal bool PeaceSharingBlocked => Journal.Pending && (Journal.Current.Peace != null || Journal.Current.Wars?.IncomingCharters != null) && (!Executing || !sharingPeace);
    internal void VerifyPeace()
    {
        UnionPeaceCouncil.Verify(Politics.RecordedAuthority, Journal.Current);
        UnionWarCouncil.VerifyCurrentCouncil(Politics.RecordedAuthority, Journal.Current);
    }
    internal bool BlocksApartFromTownLosses => RestoreFailed || Journal.Pending || Current.Game.GetComponent<CharterProtectionLedger>().Executing || Current.Game.GetComponent<LeaderJoinLedger>().BlocksChanges || Current.Game.GetComponent<SubtreeExitLedger>().BlocksChanges || Current.Game.GetComponent<LeaderRelocationLedger>().BlocksChanges ||
        Current.Game.GetComponent<TerritoryAdministrationLedger>().RestoreFailed || Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared);
    public bool BlocksChanges => BlocksApartFromTownLosses || Charters.RestoreFailed || Charters.UnresolvedTownLosses;
    private PlayerPoliticsLedger Politics => Current.Game.GetComponent<PlayerPoliticsLedger>();
    private TerritoryAdministrationLedger Territories => Current.Game.GetComponent<TerritoryAdministrationLedger>();
    private LiveCharterLedger Charters => Current.Game.GetComponent<LiveCharterLedger>();
    public PoliticalTransitionLedger(Game game) { }
    internal string RenewalHistoryToken => JsonSerializer.Serialize(warHistory);
    internal (AllegianceTransitionLedger Journal, List<string> History) StageCharterRenewal(PlayerCharterRenewal receipt)
    {
        Ready();
        if (BlocksChanges || Journal.Export() != receipt.TransitionBefore || Politics.Model.Export() != receipt.Before)
            throw new ArgumentException("Political renewal scope changed.");
        var next = AllegianceTransitionLedger.Restore(receipt.TransitionAfter); var history = warHistory.ToList();
        if (Journal.Current?.Wars != null || Journal.Current?.Peace != null) history.Add(Journal.Export());
        UnionWarAdmission.ValidateHistory(history, next.Current); return (next, history);
    }
    internal void CommitCharterRenewal(AllegianceTransitionLedger prepared, List<string> history) { Journal = prepared; warHistory = history; }
    internal void ValidateCharterRenewalHistory(IEnumerable<PlayerCharterRenewal> renewals)
    {
        foreach (var r in renewals)
            if (AllegianceTransitionLedger.Restore(r.TransitionBefore).Current?.Wars != null && !warHistory.Contains(r.TransitionBefore))
                throw new ArgumentException("Original war admission must remain archived across charter renewal.");
    }
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) snapshot = Journal.Export();
        Scribe_Values.Look(ref snapshot, "honorPoliticalTransitionV1");
        Scribe_Collections.Look(ref warHistory, "honorUnionWarHistoryV1", LookMode.Value);
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        warHistory ??= [];
        try { if (snapshot != null) Journal = AllegianceTransitionLedger.Restore(snapshot); UnionWarAdmission.ValidateHistory(warHistory, Journal.Current); }
        catch (Exception e) when (e is ArgumentException or JsonException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Political transition restore rejected; raw intent retained. " + e.GetType().Name); }
    }
    public override void FinalizeInit()
    {
        if (RestoreFailed || Journal.Current == null) return;
        var c = Journal.Current;
        if (c.PreparedTick > Find.TickManager.TicksGame || c.AllegianceTick > Find.TickManager.TicksGame || c.FinishedTick > Find.TickManager.TicksGame ||
            c.WarResults?.Any(r => r.ObservedTick > Find.TickManager.TicksGame) == true || c.WarReaffirmations?.Any(w => w.Tick > Find.TickManager.TicksGame) == true ||
            c.PeaceReaffirmations?.Any(p => p.Tick > Find.TickManager.TicksGame) == true ||
            c.LostFollowers?.Any(l => l.ResolvedTick > Find.TickManager.TicksGame) == true ||
            c.TownLosses?.Any(l => l.ResolvedTick > Find.TickManager.TicksGame) == true ||
            PlayerAllegiance.Restore(c.Before).NativePlayer != Faction.OfPlayer.GetUniqueLoadID() ||
            Journal.Pending && !Journal.IsBefore(Politics.Model) && !Journal.IsAfter(Politics.Model))
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Political transition identity/clock/history mismatch; changes blocked."); }
    }
    private void Ready()
    {
        Territories.ValidatePoliticalLosses(Journal);
        if (Territories.HasPendingTownLosses) throw new ArgumentException("Explicitly reconcile observed town losses before resuming politics.");
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (Current.Game.GetComponent<LeaderJoinLedger>().BlocksChanges || Current.Game.GetComponent<SubtreeExitLedger>().BlocksChanges || Current.Game.GetComponent<LeaderRelocationLedger>().BlocksChanges || RestoreFailed || Politics.RestoreFailed || Charters.RestoreFailed || Charters.UnresolvedTownLosses || Territories.RestoreFailed ||
            Current.Game.GetComponent<RealmAdministrationLedger>().RestoreFailed || payments.RestoreFailed ||
            payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared))
            throw new ArgumentException("Resolve unavailable journals and pending physical deliveries.");
    }
    private void FutureRelations(Faction target, FollowerTransferPlan plan, UnionWarConsent wars = null)
    {
        var administration = Current.Game.GetComponent<RealmAdministrationLedger>().OwnedFaction;
        string released = ReleasedMigrationSource(target);
        foreach (var follower in plan.Entries)
        {
            var owner = Find.FactionManager.AllFactionsListForReading.SingleOrDefault(f => f.GetUniqueLoadID() == follower.Observation.Owner);
            if (owner == null || owner != target && owner != administration && owner.GetUniqueLoadID() != released &&
                (target.HostileTo(owner) || wars?.Targets.Any(t => t.Enemy == owner.GetUniqueLoadID()) == true))
                throw new ArgumentException("Future diplomacy would make a follower source hostile; this transfer needs a later wartime adapter.");
        }
    }
    internal string ReleasedMigrationSource(Faction target)
    {
        if (RestoreFailed || Charters.RestoreFailed || target == null) return null;
        var before = Politics.State;
        if (Journal.Pending)
        {
            if (!Journal.IsBefore(Politics.Model) && !Journal.IsAfter(Politics.Model) ||
                PlayerAllegiance.Restore(Journal.Current.After).State.Authority != target.GetUniqueLoadID()) return null;
            before = PlayerAllegiance.Restore(Journal.Current.Before).State;
        }
        return AllegianceChangeSource.ReleasedMigrationSource(Charters.Book, before, target.GetUniqueLoadID(), Find.TickManager.TicksGame);
    }
    private string Stamp(Faction root, AllegianceCommand command, FollowerTransferPlan plan, UnionWarConsent wars = null, UnionPeaceConsent peace = null) => JsonSerializer.Serialize(new {
        Command = command, Allegiance = Politics.Model.Export(), Intent = Journal.Export(), Followers = plan.Stamp, Relations = Politics.ReviewToken(root), Wars = wars, Peace = peace });
    public PoliticalChangeReview Review(bool joinUnion, bool acceptWars = false)
    {
        Ready(); if (Journal.Pending) throw new ArgumentException("A transition already needs attention.");
        Charters.EnsurePlayerIdentity();
        var wars = acceptWars ? UnionWarCouncil.Review(joinUnion ? Politics.UnionCandidate : Politics.LiegeCandidate,
            authorityKind: joinUnion ? AdmissionAuthorityKind.UnionCouncil : AdmissionAuthorityKind.FeudalLeader) : null;
        var command = Politics.PlanCommand(joinUnion, wars);
        var root = Find.FactionManager.AllFactionsListForReading.Single(f => f.GetUniqueLoadID() == command.Authority);
        var followers = Territories.ReviewFollowersFor(root); FutureRelations(root, followers, wars);
        if (wars?.AuthorityKind == AdmissionAuthorityKind.UnionCouncil)
        {
            UnionPeaceCouncil.CheckRepresentative(root);
            wars = UnionWarAdmission.BindIncoming(wars, followers);
        }
        var peace = joinUnion && wars == null ? UnionPeaceCouncil.Review(root, followers.Moves) : null;
        if (peace?.Accepted == false) throw new ArgumentException("Membership council declined admission.");
        return new(root, command, followers, Stamp(root, command, followers, wars, peace), wars, Current.Game) { Peace = peace };
    }
    public void Start(PoliticalChangeReview reviewed)
    {
        if (reviewed?.Command == null || reviewed.Context != Current.Game) { Reject(); return; }
        try
        {
            var current = Review(reviewed.Command.Action == AllegianceAction.JoinUnion, reviewed.Wars != null);
            if (current.Stamp != reviewed.Stamp) throw new ArgumentException("Review changed.");
            var next = AllegianceTransitionLedger.Restore(Journal.Export());
            if (current.Peace != null) next.PreparePeace(Guid.NewGuid().ToString("N"), Politics.Model, current.Command, current.Followers, Territories.Ledger, current.Peace);
            else next.Prepare(Guid.NewGuid().ToString("N"), Politics.Model, current.Command, current.Followers, Territories.Ledger, current.Wars);
            var history = warHistory.ToList();
            if (Journal.Current?.Wars != null || Journal.Current?.Peace != null) history.Add(Journal.Export());
            UnionWarAdmission.ValidateHistory(history, next.Current);
            Journal = next; warHistory = history;
            Executing = true;
            Politics.Model.Apply(current.Command);
            Journal.ConfirmAllegiance(Politics.Model, Find.TickManager.TicksGame);
            Finish(current.Destination, current.Wars, current.Peace);
        }
        catch (Exception e)
        { Interrupted(e); }
        finally { Executing = false; }
    }
    private void CheckRetainedSource()
    {
        var before = PlayerAllegiance.Restore(Journal.Current.Before).State;
        var command = JsonSerializer.Deserialize<PlayerAllegianceSave>(Journal.Current.After).Commands.Last();
        AllegianceChangeSource.Validate(Charters.Book, before, command, Current.Game.GetComponent<TributePaymentLedger>().Ledger);
    }
    private void SameFollowers(FollowerTransferPlan plan)
    {
        Territories.ValidatePoliticalLosses(Journal);
        if (!Journal.MatchesSurvivingFollowers(plan.Entries.Select(e => e.Charter)))
            throw new ArgumentException("Active subjects changed during the transition; do not move an unreviewed group.");
    }
    internal string ReviewTownLossContext()
    {
        if (RestoreFailed || Executing || Journal.Current?.Phase != AllegianceTransitionPhase.AllegianceApplied || !Journal.IsAfter(Politics.Model))
            throw new ArgumentException("Only a paused applied political intent can retain a follower loss.");
        CheckRetainedSource();
        if (Politics.TransitionAuthority().GetUniqueLoadID() != PlayerAllegiance.Restore(Journal.Current.After).State.Authority)
            throw new ArgumentException("Political destination changed.");
        return Journal.Export();
    }
    internal AllegianceTransitionLedger StageTownLoss(PendingTownLossArchive archive, CharterBook book, TerritoryTransferLedger transfers, string id, long now)
    {
        ReviewTownLossContext();
        var next = AllegianceTransitionLedger.Restore(Journal.Export()); next.AcknowledgeFollowerLoss(archive, book, transfers, id, now); return next;
    }
    // All validation occurs on copies before the common book/transfer/archive/political swap; no native callbacks between assignments.
    internal void CommitTownLoss(AllegianceTransitionLedger prepared) => Journal = prepared;
    public PoliticalChangeReview ReviewRemaining()
    {
        Ready();
        if (!Journal.Pending || !Journal.IsAfter(Politics.Model)) throw new ArgumentException("No applied transition to resume; unchanged preparation may be cancelled.");
        CheckRetainedSource();
        var root = Politics.TransitionAuthority();
        var wars = Journal.Current.Wars == null ? null : UnionWarCouncil.ReviewForTransition(root, Journal.Current);
        if (wars != null && !UnionWarAdmission.SameWars(Journal.Current.Wars, wars)) throw new ArgumentException("War admission scope changed.");
        Politics.CheckTransitionAdmission(root, wars);
        var followers = Territories.ReviewFollowersFor(root); SameFollowers(followers); FutureRelations(root, followers, wars);
        var peace = Journal.Current.Peace == null ? null : UnionPeaceCouncil.Review(root, Journal.Current.Peace.IncomingCharters, Journal.Current);
        if (peace?.Accepted == false) throw new ArgumentException("Current membership council declined recovery.");
        return new(root, null, followers, Stamp(root, null, followers, wars, peace), wars, Current.Game) { Peace = peace };
    }
    public void Resume(PoliticalChangeReview reviewed)
    {
        try
        {
            var current = ReviewRemaining();
            if (reviewed == null || reviewed.Context != Current.Game || reviewed.Command != null || current.Stamp != reviewed.Stamp) throw new ArgumentException("Recovery review changed.");
            Executing = true;
            if (Journal.Current.Phase == AllegianceTransitionPhase.Prepared) Journal.ConfirmAllegiance(Politics.Model, Find.TickManager.TicksGame);
            Finish(current.Destination, current.Wars, current.Peace);
        }
        catch (Exception e) { Interrupted(e); }
        finally { Executing = false; }
    }
    private void Finish(Faction root, UnionWarConsent wars = null, UnionPeaceConsent peace = null)
    {
        Ready(); CheckRetainedSource();
        if (Journal.Current.Peace != null) { Journal.ReaffirmPeace(peace, Find.TickManager.TicksGame); VerifyPeace(); }
        UnionWarCouncil.Apply(root, Journal, wars);
        UnionWarCouncil.Verify(root, Journal);
        sharingPeace = true;
        try { Politics.ApplyTransitionDiplomacy(root); } finally { sharingPeace = false; }
        VerifyPeace();
        var plan = Territories.ReviewFollowersFor(root); SameFollowers(plan); FutureRelations(root, plan);
        Territories.ApplyTransitionFollowers(plan, root);
        CheckRetainedSource();
        if (Politics.TransitionAuthority() != root) throw new ArgumentException("Actual destination changed during city execution.");
        sharingPeace = true;
        try { Politics.Synchronize(); } finally { sharingPeace = false; }
        VerifyPeace();
        UnionWarCouncil.Verify(root, Journal);
        var final = Territories.ReviewFollowersFor(root);
        Journal.Complete(Politics.Model, final, Find.TickManager.TicksGame);
        Messages.Message(Journal.LostCharters.Any() ? "PS_CompleteWithLoss".Translate(Journal.LostCharters.Count()) : "PS_Complete".Translate(), MessageTypeDefOf.NeutralEvent);
    }
    public void CancelUnchanged()
    {
        try
        {
            Ready(); Journal.CancelUnchanged(Politics.Model, Territories.Ledger, Find.TickManager.TicksGame);
            Messages.Message("PS_Cancelled".Translate(), MessageTypeDefOf.NeutralEvent);
        }
        catch (ArgumentException) { Reject(); }
    }
    private static void Reject() => Messages.Message("PS_Rejected".Translate(), MessageTypeDefOf.RejectInput);
    private static void Interrupted(Exception error)
    {
        Log.Warning("[Honor.Contracts.Preview] Coordinated political change incomplete; per-stage/city records retained. " + error.GetType().Name);
        Messages.Message("PS_Incomplete".Translate(), MessageTypeDefOf.RejectInput); Find.TickManager.Pause();
    }
}
