using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed class TerritoryTransferReference : IExposable
{
    public string Id;
    public Settlement City;
    public Faction From, Target;
    public ResidentExecutionRecord Residents;
    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id"); Scribe_References.Look(ref City, "city");
        Scribe_References.Look(ref From, "from"); Scribe_References.Look(ref Target, "target");
        Scribe_Deep.Look(ref Residents, "residentExecution");
    }
}
public sealed record TerritoryAdministrationReview(string Charter, string City, string From, string Target,
    TerritoryTransferPurpose Purpose, string Stamp, string TargetIdentity, int Residents = 0);

/// <summary>Optional actual NPC city ownership execution. Never assigns a city to the native player faction.</summary>
public sealed partial class TerritoryAdministrationLedger : GameComponent
{
    private string snapshot;
    private List<TerritoryTransferReference> references = [];
    public TerritoryTransferLedger Ledger { get; private set; } = new();
    public bool RestoreFailed { get; private set; }
    private LiveCharterLedger Charters => Current.Game.GetComponent<LiveCharterLedger>();
    public TerritoryAdministrationLedger(Game game) { }
    private string AdministrationId => Current.Game.GetComponent<RealmAdministrationLedger>().OwnedFaction?.GetUniqueLoadID();
    internal bool ReservesResident(Pawn pawn) => references?.Any(r => r?.Residents?.Pawns?.Contains(pawn) == true &&
        (RestoreFailed || r.Residents.Started || Ledger.Transfers.Any(t => t.Id == r.Id && t.Phase == TerritoryTransferPhase.Prepared))) == true;
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) snapshot = Ledger.Export();
        Scribe_Values.Look(ref snapshot, "honorTerritoryTransfersV1");
        Scribe_Collections.Look(ref references, "honorTerritoryTransferReferences", LookMode.Deep);
        ExposePendingTownLosses();
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        references ??= [];
        try
        {
            if (snapshot != null) Ledger = TerritoryTransferLedger.Restore(snapshot);
            if (references.Any(r => r == null) || references.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != references.Count ||
                references.Count != Ledger.Transfers.Count || references.Any(r => Ledger.Transfers.All(t => t.Id != r.Id)))
                throw new ArgumentException("Invalid transfer references.");
        }
        catch (Exception e) when (e is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Territory execution restore rejected; original journal retained. " + e.GetType().Name); }
    }
    public override void FinalizeInit()
    {
        if (RestoreFailed) return;
        try
        {
            if (Charters.RestoreFailed || Ledger.LastTick > Find.TickManager.TicksGame || Charters.Book.LastTick > Find.TickManager.TicksGame)
                throw new ArgumentException("Invalid source book or clock.");
            foreach (var t in Ledger.Transfers)
            {
                var charter = Charters.Book.Charter(t.Charter); var entity = Charters.Book.Entity(t.Territory);
                var binding = Charters.Binding(t.Territory); var reference = references.Single(r => r.Id == t.Id);
                if (entity.Player || binding.PlayerGroup || binding.FactionGroup || entity.NativeOwner != t.BaselineOwner ||
                    charter.Terms.Subject != t.Territory || !Charters.Book.Entity(charter.Terms.Liege).Player && charter.NpcAuthority == null ||
                    !charter.PlayerSignedTick.HasValue || !charter.NpcSignedTick.HasValue ||
                    Math.Max(charter.PlayerSignedTick.Value, charter.NpcSignedTick.Value) > t.PreparedTick ||
                    binding.Settlement != reference.City || reference.City != null && "territory/" + reference.City.GetUniqueLoadID() != t.Territory ||
                    reference.From != null && reference.From.GetUniqueLoadID() != t.FromOwner ||
                    reference.Target != null && (reference.Target.GetUniqueLoadID() != t.TargetOwner || reference.Target.IsPlayer) ||
                    (t.ResidentProof == null) != (reference.Residents == null) ||
                    reference.Residents != null && reference.Residents.Set().Proof != t.ResidentProof ||
                    t.ReleasedRoot != null && (!Current.Game.GetComponent<SubtreeExitLedger>().Proves(t) || reference.Target != null && reference.Target.def.defName != UnionPoliticsLedger.UnionDefName) ||
                    t.Purpose == TerritoryTransferPurpose.ReleasedToUnion && t.ReleasedRoot == null && (charter.Phase != RecordedCharterPhase.Released ||
                        !charter.PlayerReleaseTick.HasValue || !charter.NpcReleaseTick.HasValue ||
                        Math.Max(charter.PlayerReleaseTick.Value, charter.NpcReleaseTick.Value) > t.PreparedTick ||
                        reference.Target != null && reference.Target.def.defName != UnionPoliticsLedger.UnionDefName))
                    throw new ArgumentException("Execution history/source mismatch.");
                if (reference.Residents != null)
                {
                    if (t.Phase == TerritoryTransferPhase.Prepared) ResidentExecution.ValidateReferences(reference.Residents, reference.City, t.ResidentProof);
                    else ResidentExecution.ReleaseReferences(reference.Residents);
                }
            }
            pendingTownLosses.ValidatePair(Charters.Book, Ledger);
            politicalTownLosses.ValidatePair(Charters.Book, Ledger);
            ValidatePoliticalLosses(Current.Game.GetComponent<PoliticalTransitionLedger>().Journal);
            if (pendingTownLosses.Records.Any(r => r.ObservedTick > Find.TickManager.TicksGame || r.ResolvedTick > Find.TickManager.TicksGame))
                throw new ArgumentException("Future native destruction record.");
            if (politicalTownLosses.Records.Any(r => r.ObservedTick > Find.TickManager.TicksGame || r.ResolvedTick > Find.TickManager.TicksGame))
                throw new ArgumentException("Future independent political loss.");
        }
        catch (Exception e) when (e is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Territory execution/source mismatch; sharing blocked and records retained."); }
    }
    internal string OwnerLabel(string id) => Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.GetUniqueLoadID() == id)?.Name
        ?? "CT_Missing".Translate();
    private static Settlement[] Cities() => Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => !s.Destroyed).ToArray();
    private static bool Eligible(Faction faction, string releasedSource = null) => faction != null && !faction.IsPlayer && !faction.Hidden && !faction.temporary &&
        !faction.defeated && faction.HasGoodwill && faction.def.humanlikeFaction && !faction.def.permanentEnemy &&
        !(faction.def.royalTitleTags?.Count > 0) && (!faction.HostileTo(Faction.OfPlayer) || faction.GetUniqueLoadID() == releasedSource);
    internal static TerritoryTransferObservation Observe(Settlement city, string territory, Faction target, string releasedSource = null, int residentProfile = 6)
    {
        var all = Cities(); bool exists = city != null && all.Contains(city); var owner = exists ? city.Faction : null;
        int format = 0;
        string proof = exists ? CityStockPreservation.Capture(city, out format) : null;
        string residentProof = exists && city.previouslyGeneratedInhabitants?.Count > 0 ? ResidentExecution.Proof(city, residentProfile, target) : null;
        return new(territory, owner?.GetUniqueLoadID(), all.Count(s => s.Faction == owner), exists, owner?.IsPlayer == true,
            exists && city.HasMap, !exists || proof == null || city.EverVisited,
            exists && city.def.canHaveFaction && Eligible(owner, releasedSource), Eligible(target), target?.IsPlayer == true,
            HasInhabitants: !exists || city.previouslyGeneratedInhabitants == null || city.previouslyGeneratedInhabitants.Count > 0, StockProof: proof, StockFormat: format, ResidentProof: residentProof);
    }
    private (RecordedCharter Charter, Settlement City, Faction Target, TerritoryTransferObservation Observation, TerritoryTransferPurpose Purpose) Context(string id, Faction proposed = null)
    {
        if (RestoreFailed || Charters.RestoreFailed || Current.Game.GetComponent<RealmAdministrationLedger>().RestoreFailed || Current.Game.GetComponent<TributePaymentLedger>().HasPending(id))
            throw new ArgumentException("Resolve unavailable journals and pending deliveries.");
        var charter = Charters.Book.Charter(id); var binding = Charters.Binding(charter.Terms.Subject);
        if (binding.PlayerGroup || binding.FactionGroup || binding.Settlement == null || IndependentlyLostTerritory(charter.Terms.Subject)) throw new ArgumentException("Only single surviving NPC city subjects.");
        bool released = charter.Phase == RecordedCharterPhase.Released;
        string subtree = Current.Game.GetComponent<SubtreeExitLedger>().Scope(id);
        var union = Current.Game.GetComponent<UnionPoliticsLedger>();
        var target = released || subtree != null ? (!union.RestoreFailed ? union.Union : null) : proposed ?? Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority();
        if (target == null || !Find.FactionManager.AllFactionsListForReading.Contains(target))
            throw new ArgumentException("Configure a live union or player shared authority first.");
        string releasedSource = Current.Game.GetComponent<PoliticalTransitionLedger>().ReleasedMigrationSource(target);
        Charters.Book.ValidateCurrent(id, Charters.ObserveForMigration(releasedSource));
        var observed = Observe(binding.Settlement, binding.Identity, target, releasedSource);
        if (observed.Owner != target.GetUniqueLoadID() && observed.HasInhabitants && observed.ResidentProof == null)
        {
            Messages.Message("SX_CityBlocked".Translate(binding.Settlement.Label, ResidentExecution.BlockReasonKey(binding.Settlement, target).Translate()), MessageTypeDefOf.RejectInput);
            throw new ArgumentException("Resident preservation unavailable.");
        }
        return (charter, binding.Settlement, target, observed, released || subtree != null ? TerritoryTransferPurpose.ReleasedToUnion : TerritoryTransferPurpose.ActiveCharter);
    }
    public TerritoryAdministrationReview Review(string id) => ReviewForTarget(id, null);
    private TerritoryAdministrationReview ReviewForTarget(string id, Faction target)
    {
        var c = Context(id, target); var clone = TerritoryTransferLedger.Restore(Ledger.Export());
        clone.Prepare("review-" + Guid.NewGuid().ToString("N"), Charters.Book, id, c.Observation, c.Target.GetUniqueLoadID(),
            c.Purpose == TerritoryTransferPurpose.ActiveCharter ? c.Target.GetUniqueLoadID() : null,
            c.Purpose == TerritoryTransferPurpose.ReleasedToUnion ? c.Target.GetUniqueLoadID() : null, c.Purpose, Find.TickManager.TicksGame, AdministrationId,
            Current.Game.GetComponent<SubtreeExitLedger>().Scope(id));
        return new(id, c.City.Label, c.City.Faction.Name, c.Target.Name, c.Purpose,
            string.Join("/", id, c.City.GetUniqueLoadID(), c.City.Faction.GetUniqueLoadID(), c.Target.GetUniqueLoadID(), Charters.Book.LastTick, Ledger.LastTick, c.Purpose, c.Observation.StockProof, c.Observation.ResidentProof),
            c.Target.GetUniqueLoadID(), c.City.previouslyGeneratedInhabitants?.Count ?? 0);
    }
    public bool Apply(TerritoryAdministrationReview reviewed, bool notify = true) => ApplyForTarget(reviewed, notify, null);
    private bool ApplyForTarget(TerritoryAdministrationReview reviewed, bool notify, Faction target)
    {
        if (reviewed == null) { Reject(); return false; }
        try
        {
            nativeExecutionDepth++;
            var transition = Current.Game.GetComponent<PoliticalTransitionLedger>();
            if (transition.BlocksChanges && !transition.Executing && !Current.Game.GetComponent<SubtreeExitLedger>().Executing) throw new ArgumentException("Resolve coordinated transition first.");
            var current = ReviewForTarget(reviewed.Charter, target);
            if (current.Stamp != reviewed.Stamp) throw new ArgumentException("Reviewed state changed.");
            var c = Context(reviewed.Charter, target); string id = Guid.NewGuid().ToString("N");
            var residents = c.Observation.HasInhabitants ? ResidentExecution.Capture(c.City) : null;
            if (residents?.Set().Proof != c.Observation.ResidentProof) throw new ArgumentException("Resident preflight changed.");
            Ledger.Prepare(id, Charters.Book, c.Charter.Id, c.Observation, c.Target.GetUniqueLoadID(),
                c.Purpose == TerritoryTransferPurpose.ActiveCharter ? c.Target.GetUniqueLoadID() : null,
                c.Purpose == TerritoryTransferPurpose.ReleasedToUnion ? c.Target.GetUniqueLoadID() : null, c.Purpose, Find.TickManager.TicksGame, AdministrationId,
                Current.Game.GetComponent<SubtreeExitLedger>().Scope(c.Charter.Id));
            var reference = new TerritoryTransferReference { Id = id, City = c.City, From = c.City.Faction, Target = c.Target, Residents = residents };
            references.Add(reference);
            if (residents != null) ResidentExecution.Apply(residents, c.City, reference.From, c.Target, c.Observation.ResidentProof);
            c.City.SetFaction(c.Target);
            CityStockPreservation.InvalidateMaterial(c.City);
            if (residents != null) ResidentExecution.Verify(residents, c.City, reference.From, c.Target, c.Observation.ResidentProof);
            Ledger.Confirm(id, Observe(c.City, c.Charter.Terms.Subject, c.Target), Find.TickManager.TicksGame);
            if (residents != null) ResidentExecution.ReleaseReferences(residents);
            if (notify) Messages.Message("CT_Applied".Translate(), MessageTypeDefOf.NeutralEvent);
            return true;
        }
        catch (Exception e)
        { Log.Warning("[Honor.Contracts.Preview] Territory execution incomplete; recorded intent retained. " + e.GetType().Name); Reject(); Find.TickManager.Pause(); return false; }
        finally { nativeExecutionDepth--; }
    }
    public FollowerTransferPlan ReviewFollowers() => ReviewFollowersFor(Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority());
    internal FollowerTransferPlan ReviewFollowersFor(Faction target)
    {
        if (RestoreFailed || Charters.RestoreFailed) throw new ArgumentException("Execution or consent journal unavailable.");
        var entries = Charters.Book.PlayerFollowers().Select(c => {
                var context = Context(c.Id, target);
                if (context.Target != target) throw new ArgumentException("Authority changed during review.");
                return new FollowerCandidate(c.Id, context.Observation);
            }).ToArray();
        return FollowerTransfers.Review(Charters.Book, Ledger, entries, target.GetUniqueLoadID(), Find.TickManager.TicksGame, AdministrationId);
    }
    internal void ApplyTransitionFollowers(FollowerTransferPlan reviewed, Faction target)
    {
        var transition = Current.Game.GetComponent<PoliticalTransitionLedger>();
        if (!transition.Executing || !transition.Journal.Pending || transition.Journal.Current == null ||
            Current.Game.GetComponent<PlayerPoliticsLedger>().State.Authority != target.GetUniqueLoadID())
            throw new ArgumentException("No authorized coordinated execution.");
        var current = ReviewFollowersFor(target);
        if (current.Stamp != reviewed.Stamp) throw new ArgumentException("Transition follower plan changed.");
        foreach (string id in current.Moves)
        {
            var single = ReviewForTarget(id, target);
            if (single.TargetIdentity != current.Target || !ApplyForTarget(single, false, target)) throw new ArgumentException("Follower execution interrupted.");
        }
    }
    public string FollowerLabel(string charterId) => Charters.Label(Charters.Book.Charter(charterId).Terms.Subject);
    public void OpenPreservationAudit(string charterId = null)
    {
        if (Charters.RestoreFailed) { Reject(); return; }
        try
        {
            if (charterId != null)
            {
                var binding = Charters.Binding(Charters.Book.Charter(charterId).Terms.Subject);
                if (binding.PlayerGroup || binding.FactionGroup) throw new ArgumentException("No single NPC subject.");
                Find.WindowStack.Add(new CityPreservationAuditWindow(binding.Settlement));
                return;
            }
            var options = Charters.Book.PlayerFollowers()
                .Select(c => new FloatMenuOption(FollowerLabel(c.Id), () => OpenPreservationAudit(c.Id))).ToList();
            if (options.Count == 0) { Messages.Message("CA_Empty".Translate(), MessageTypeDefOf.NeutralEvent); return; }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        catch (ArgumentException) { Reject(); }
    }
    public void ApplyFollowers(FollowerTransferPlan reviewed)
    {
        if (Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges) { Reject(); return; }
        if (reviewed == null) { Reject(); return; }
        try
        {
            var current = ReviewFollowers();
            if (current.Stamp != reviewed.Stamp) throw new ArgumentException("Follower plan changed; review again.");
            foreach (string id in current.Moves)
            {
                var single = Review(id);
                if (single.TargetIdentity != current.Target || !Apply(single, false)) throw new ArgumentException("Follower movement interrupted or authority changed.");
            }
            var final = ReviewFollowers();
            if (final.Target != current.Target || final.Moves.Count > 0) throw new ArgumentException("Follower set or authority changed during execution.");
            Messages.Message("FB_Complete".Translate(current.Moves.Count), MessageTypeDefOf.NeutralEvent);
        }
        catch (ArgumentException)
        { Messages.Message("FB_Incomplete".Translate(), MessageTypeDefOf.RejectInput); Find.TickManager.Pause(); }
    }
    public void Recover(string id)
    {
        if (RestoreFailed || Charters.RestoreFailed) { Reject(); return; }
        if (pendingTownLosses.HasPending(id)) { Messages.Message("PL_Pending".Translate(), MessageTypeDefOf.RejectInput); return; }
        try
        {
            nativeExecutionDepth++;
            var item = Ledger.Transfer(id); var reference = references.Single(r => r.Id == id);
            if (reference.City == null || reference.City.Faction == null || "territory/" + reference.City.GetUniqueLoadID() != item.Territory ||
                reference.From == null || reference.From.GetUniqueLoadID() != item.FromOwner || reference.Target == null ||
                reference.Target.GetUniqueLoadID() != item.TargetOwner || reference.Target.IsPlayer)
                throw new ArgumentException("Cannot establish original transfer references.");
            var actual = Observe(reference.City, item.Territory, reference.Target, residentProfile: reference.Residents?.Profile ?? 3);
            if (reference.Residents != null)
            {
                if (item.Phase != TerritoryTransferPhase.Prepared || reference.Residents.Set().Proof != item.ResidentProof ||
                    reference.City.Faction != reference.From && reference.City.Faction != reference.Target)
                    throw new ArgumentException("No matching pending resident execution.");
                if (!reference.Residents.Started && reference.City.Faction == reference.From)
                {
                    ResidentExecution.ValidateReferences(reference.Residents, reference.City, item.ResidentProof);
                    Ledger.CancelUnmoved(id, actual, Find.TickManager.TicksGame);
                }
                else
                {
                    ValidateResidentRecovery(item, reference);
                    ResidentExecution.Apply(reference.Residents, reference.City, reference.From, reference.Target, item.ResidentProof);
                    if (reference.City.Faction == reference.From) reference.City.SetFaction(reference.Target);
                    CityStockPreservation.InvalidateMaterial(reference.City);
                    ResidentExecution.Verify(reference.Residents, reference.City, reference.From, reference.Target, item.ResidentProof);
                    Ledger.Confirm(id, Observe(reference.City, item.Territory, reference.Target, residentProfile: reference.Residents.Profile), Find.TickManager.TicksGame);
                }
                ResidentExecution.ReleaseReferences(reference.Residents);
                return;
            }
            if (reference.City.Faction == reference.From) Ledger.CancelUnmoved(id, actual, Find.TickManager.TicksGame);
            else
            {
                if (reference.City.Faction != reference.Target) throw new ArgumentException("Unrelated current owner.");
                CityStockPreservation.InvalidateMaterial(reference.City);
                Ledger.Confirm(id, actual, Find.TickManager.TicksGame);
            }
        }
        catch (Exception e)
        { Log.Warning("[Honor.Contracts.Preview] Territory recovery incomplete; original resident/stock intent retained. " + e.GetType().Name); Reject(); Find.TickManager.Pause(); }
        finally { nativeExecutionDepth--; }
    }
    private void ValidateResidentRecovery(TerritoryTransfer item, TerritoryTransferReference reference)
    {
        var charter = Charters.Book.Charter(item.Charter);
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (payments.RestoreFailed || payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) ||
            Current.Game.GetComponent<PlayerPoliticsLedger>().RestoreFailed || Current.Game.GetComponent<UnionPoliticsLedger>().RestoreFailed ||
            Current.Game.GetComponent<RealmAdministrationLedger>().RestoreFailed)
            throw new ArgumentException("Recovery authority unavailable.");
        string releasedSource = Current.Game.GetComponent<PoliticalTransitionLedger>().ReleasedMigrationSource(reference.Target);
        var observations = Charters.ObserveForMigration(releasedSource).Select(o => o.Id == item.Territory
            ? new TerritoryObservation(item.Territory, item.BaselineOwner, true, false, true) : o).ToArray();
        Faction destination;
        if (item.Purpose == TerritoryTransferPurpose.ActiveCharter)
        {
            if (charter.Phase != RecordedCharterPhase.Active || !Charters.Book.PlayerManaged(charter)) throw new ArgumentException("Original active consent changed.");
            destination = Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryRecoveryAuthority(observations);
        }
        else
        {
            destination = Current.Game.GetComponent<UnionPoliticsLedger>().Union;
            if (item.ReleasedRoot != null ? !Current.Game.GetComponent<SubtreeExitLedger>().Proves(item) :
                charter.Phase != RecordedCharterPhase.Released || !charter.PlayerReleaseTick.HasValue || !charter.NpcReleaseTick.HasValue)
                throw new ArgumentException("Original release intent changed.");
        }
        if (destination != reference.Target || !Eligible(destination) || !Eligible(reference.From, releasedSource) ||
            !Find.FactionManager.AllFactionsListForReading.Contains(destination)) throw new ArgumentException("Current recovery destination or diplomacy changed.");
        if (reference.From.HostileTo(destination) && reference.From.GetUniqueLoadID() != releasedSource ||
            item.Purpose == TerritoryTransferPurpose.ReleasedToUnion && Find.FactionManager.AllFactionsListForReading.Any(f => f != Faction.OfPlayer && f != reference.From && f != destination &&
                reference.From.HostileTo(f) && !destination.HostileTo(f))) throw new ArgumentException("Unreviewed wartime admission.");
        Charters.Book.ValidateCurrent(item.Charter, observations);
        if (Charters.Book.Charters.Any(c => Charters.Book.Phase(c, Find.TickManager.TicksGame) == RecordedCharterPhase.Offered && c.Terms.Subject == item.Territory))
            throw new ArgumentException("Competing current city offer.");
        var stock = Observe(reference.City, item.Territory, destination, releasedSource);
        if (stock.HasMap || !stock.Exists || stock.StockFormat != item.StockFormat || stock.StockProof != item.StockProof ||
            stock.StockProof == null && stock.Visited || stock.Owner == item.FromOwner && stock.SourceSettlements < 2 && item.FromOwner != AdministrationId)
            throw new ArgumentException("Original stock/map/source-base preservation changed.");
    }
    private static void Reject() => Messages.Message("CT_Unavailable".Translate(), MessageTypeDefOf.RejectInput);
}

public sealed class FollowerTransfersWindow : Window
{
    private FollowerTransferPlan review;
    private Vector2 scroll;
    private float height = 1000;
    public FollowerTransfersWindow() { forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override Vector2 InitialSize => new(850, 760);
    public override void DoWindowContents(Rect rect)
    {
        var ledger = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, 12000));
        list.Label("FB_Warning".Translate());
        list.Label("RE_Limits".Translate());
        ledger.DrawPendingTownLosses(list);
        if (list.ButtonText("CA_OpenAll".Translate())) ledger.OpenPreservationAudit();
        if (ledger.RestoreFailed) list.Label("HCP_RestoreFailed".Translate());
        else
        {
            if (list.ButtonText("FB_Review".Translate()))
            {
                try { review = ledger.ReviewFollowers(); }
                catch (ArgumentException) { review = null; Messages.Message("CT_Unavailable".Translate(), MessageTypeDefOf.RejectInput); }
            }
            if (review != null)
            {
                list.Label("FB_Plan".Translate(ledger.OwnerLabel(review.Target), review.Moves.Count, review.Entries.Count));
                foreach (var entry in review.Entries)
                    list.Label("FB_Entry".Translate(ledger.FollowerLabel(entry.Charter), ledger.OwnerLabel(entry.Observation.Owner),
                        (review.Moves.Contains(entry.Charter) ? "FB_Move" : "FB_Already").Translate()));
                if (review.Moves.Count > 0 && list.ButtonText("FB_Confirm".Translate())) { ledger.ApplyFollowers(review); review = null; }
            }
            foreach (var pending in ledger.Ledger.Transfers.Where(t => t.Phase == TerritoryTransferPhase.Prepared).ToArray())
            {
                list.Label("FB_Pending".Translate(ledger.FollowerLabel(pending.Charter)));
                if (list.ButtonText("CT_Recover".Translate())) { ledger.Recover(pending.Id); review = null; }
            }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}

public sealed class TerritoryAdministrationWindow : Window
{
    private readonly string charter;
    private TerritoryAdministrationReview review;
    private Vector2 scroll;
    private float height = 1000;
    public TerritoryAdministrationWindow(string id) { charter = id; forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override Vector2 InitialSize => new(850, 760);
    public override void DoWindowContents(Rect rect)
    {
        var ledger = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, 12000));
        list.Label("CT_Warning".Translate());
        list.Label("RE_Limits".Translate());
        Current.Game.GetComponent<LeaderRelocationLedger>().Draw(list, charter);
        Current.Game.GetComponent<LeaderJoinLedger>().Draw(list, charter);
        if (list.ButtonText("FM_Open".Translate())) Find.WindowStack.Add(new FactionMergerProposalWindow(charter));
        ledger.DrawPendingTownLosses(list, charter);
        if (list.ButtonText("CA_Open".Translate())) ledger.OpenPreservationAudit(charter);
        if (ledger.RestoreFailed) list.Label("HCP_RestoreFailed".Translate());
        else
        {
            if (list.ButtonText("CT_Review".Translate()))
            {
                try { review = ledger.Review(charter); }
                catch (ArgumentException) { review = null; Messages.Message("CT_Unavailable".Translate(), MessageTypeDefOf.RejectInput); }
            }
            if (review != null)
            {
                list.Label("CT_Proposal".Translate(review.City, review.From, review.Target));
                if (review.Residents > 0) list.Label("RE_Review".Translate(review.Residents));
                list.Label((review.Purpose == TerritoryTransferPurpose.ActiveCharter ? "CT_Active" : "CT_Released").Translate());
                if (list.ButtonText("CT_Confirm".Translate())) { ledger.Apply(review); review = null; }
            }
            foreach (var item in ledger.Ledger.Transfers.Where(t => t.Charter == charter).ToArray())
            {
                list.Label("CT_Record".Translate(ledger.OwnerLabel(item.FromOwner), ledger.OwnerLabel(item.TargetOwner), ("CT_" + item.Phase).Translate()));
                if (item.Phase == TerritoryTransferPhase.Prepared && list.ButtonText("CT_Recover".Translate())) ledger.Recover(item.Id);
            }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
