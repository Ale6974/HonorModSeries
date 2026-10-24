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
    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id"); Scribe_References.Look(ref City, "city");
        Scribe_References.Look(ref From, "from"); Scribe_References.Look(ref Target, "target");
    }
}
public sealed record TerritoryAdministrationReview(string Charter, string City, string From, string Target,
    TerritoryTransferPurpose Purpose, string Stamp, string TargetIdentity);

/// <summary>Optional actual NPC city ownership execution. Never assigns a city to the native player faction.</summary>
public sealed class TerritoryAdministrationLedger : GameComponent
{
    private string snapshot;
    private List<TerritoryTransferReference> references = [];
    public TerritoryTransferLedger Ledger { get; private set; } = new();
    public bool RestoreFailed { get; private set; }
    private LiveCharterLedger Charters => Current.Game.GetComponent<LiveCharterLedger>();
    public TerritoryAdministrationLedger(Game game) { }
    private string AdministrationId => Current.Game.GetComponent<RealmAdministrationLedger>().OwnedFaction?.GetUniqueLoadID();
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) snapshot = Ledger.Export();
        Scribe_Values.Look(ref snapshot, "honorTerritoryTransfersV1");
        Scribe_Collections.Look(ref references, "honorTerritoryTransferReferences", LookMode.Deep);
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
                    charter.Terms.Subject != t.Territory || !Charters.Book.Entity(charter.Terms.Liege).Player ||
                    !charter.PlayerSignedTick.HasValue || !charter.NpcSignedTick.HasValue ||
                    Math.Max(charter.PlayerSignedTick.Value, charter.NpcSignedTick.Value) > t.PreparedTick ||
                    binding.Settlement != reference.City || reference.City != null && "territory/" + reference.City.GetUniqueLoadID() != t.Territory ||
                    reference.From != null && reference.From.GetUniqueLoadID() != t.FromOwner ||
                    reference.Target != null && (reference.Target.GetUniqueLoadID() != t.TargetOwner || reference.Target.IsPlayer) ||
                    t.Purpose == TerritoryTransferPurpose.ReleasedToUnion && (charter.Phase != RecordedCharterPhase.Released ||
                        !charter.PlayerReleaseTick.HasValue || !charter.NpcReleaseTick.HasValue ||
                        Math.Max(charter.PlayerReleaseTick.Value, charter.NpcReleaseTick.Value) > t.PreparedTick ||
                        reference.Target != null && reference.Target.def.defName != UnionPoliticsLedger.UnionDefName))
                    throw new ArgumentException("Execution history/source mismatch.");
            }
        }
        catch (ArgumentException) { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Territory execution/source mismatch; sharing blocked and records retained."); }
    }
    internal string OwnerLabel(string id) => Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.GetUniqueLoadID() == id)?.Name
        ?? "CT_Missing".Translate();
    private static Settlement[] Cities() => Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => !s.Destroyed).ToArray();
    private static bool Eligible(Faction faction) => faction != null && !faction.IsPlayer && !faction.Hidden && !faction.temporary &&
        !faction.defeated && faction.HasGoodwill && faction.def.humanlikeFaction && !faction.def.permanentEnemy &&
        !(faction.def.royalTitleTags?.Count > 0) && !faction.HostileTo(Faction.OfPlayer);
    private static TerritoryTransferObservation Observe(Settlement city, string territory, Faction target)
    {
        var all = Cities(); bool exists = city != null && all.Contains(city); var owner = exists ? city.Faction : null;
        string proof = exists ? CityStockPreservation.Capture(city) : null;
        return new(territory, owner?.GetUniqueLoadID(), all.Count(s => s.Faction == owner), exists, owner?.IsPlayer == true,
            exists && city.HasMap, !exists || proof == null || city.EverVisited,
            exists && city.def.canHaveFaction && Eligible(owner), Eligible(target), target?.IsPlayer == true,
            HasInhabitants: !exists || city.previouslyGeneratedInhabitants == null || city.previouslyGeneratedInhabitants.Count > 0, StockProof: proof);
    }
    private (RecordedCharter Charter, Settlement City, Faction Target, TerritoryTransferObservation Observation, TerritoryTransferPurpose Purpose) Context(string id, Faction proposed = null)
    {
        if (RestoreFailed || Charters.RestoreFailed || Current.Game.GetComponent<TributePaymentLedger>().HasPending(id))
            throw new ArgumentException("Resolve unavailable journals and pending deliveries.");
        var charter = Charters.Book.Charter(id); var binding = Charters.Binding(charter.Terms.Subject);
        if (binding.PlayerGroup || binding.FactionGroup || binding.Settlement == null) throw new ArgumentException("Only single NPC city subjects.");
        Charters.Book.ValidateCurrent(id, Charters.Observe());
        bool released = charter.Phase == RecordedCharterPhase.Released;
        var union = Current.Game.GetComponent<UnionPoliticsLedger>();
        var target = released ? (!union.RestoreFailed ? union.Union : null) : proposed ?? Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority();
        if (target == null || !Find.FactionManager.AllFactionsListForReading.Contains(target))
            throw new ArgumentException("Configure a live union or player shared authority first.");
        var observed = Observe(binding.Settlement, binding.Identity, target);
        return (charter, binding.Settlement, target, observed, released ? TerritoryTransferPurpose.ReleasedToUnion : TerritoryTransferPurpose.ActiveCharter);
    }
    public TerritoryAdministrationReview Review(string id) => ReviewForTarget(id, null);
    private TerritoryAdministrationReview ReviewForTarget(string id, Faction target)
    {
        var c = Context(id, target); var clone = TerritoryTransferLedger.Restore(Ledger.Export());
        clone.Prepare("review-" + Guid.NewGuid().ToString("N"), Charters.Book, id, c.Observation, c.Target.GetUniqueLoadID(),
            c.Purpose == TerritoryTransferPurpose.ActiveCharter ? c.Target.GetUniqueLoadID() : null,
            c.Purpose == TerritoryTransferPurpose.ReleasedToUnion ? c.Target.GetUniqueLoadID() : null, c.Purpose, Find.TickManager.TicksGame, AdministrationId);
        return new(id, c.City.Label, c.City.Faction.Name, c.Target.Name, c.Purpose,
            string.Join("/", id, c.City.GetUniqueLoadID(), c.City.Faction.GetUniqueLoadID(), c.Target.GetUniqueLoadID(), Charters.Book.LastTick, Ledger.LastTick, c.Purpose, c.Observation.StockProof), c.Target.GetUniqueLoadID());
    }
    public bool Apply(TerritoryAdministrationReview reviewed, bool notify = true) => ApplyForTarget(reviewed, notify, null);
    private bool ApplyForTarget(TerritoryAdministrationReview reviewed, bool notify, Faction target)
    {
        if (reviewed == null) { Reject(); return false; }
        try
        {
            var transition = Current.Game.GetComponent<PoliticalTransitionLedger>();
            if (transition.BlocksChanges && !transition.Executing) throw new ArgumentException("Resolve coordinated transition first.");
            var current = ReviewForTarget(reviewed.Charter, target);
            if (current.Stamp != reviewed.Stamp) throw new ArgumentException("Reviewed state changed.");
            var c = Context(reviewed.Charter, target); string id = Guid.NewGuid().ToString("N");
            Ledger.Prepare(id, Charters.Book, c.Charter.Id, c.Observation, c.Target.GetUniqueLoadID(),
                c.Purpose == TerritoryTransferPurpose.ActiveCharter ? c.Target.GetUniqueLoadID() : null,
                c.Purpose == TerritoryTransferPurpose.ReleasedToUnion ? c.Target.GetUniqueLoadID() : null, c.Purpose, Find.TickManager.TicksGame, AdministrationId);
            references.Add(new() { Id = id, City = c.City, From = c.City.Faction, Target = c.Target });
            c.City.SetFaction(c.Target);
            CityStockPreservation.InvalidateMaterial(c.City);
            Ledger.Confirm(id, Observe(c.City, c.Charter.Terms.Subject, c.Target), Find.TickManager.TicksGame);
            if (notify) Messages.Message("CT_Applied".Translate(), MessageTypeDefOf.NeutralEvent);
            return true;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        { Log.Warning("[Honor.Contracts.Preview] Territory execution incomplete; recorded intent retained. " + e.GetType().Name); Reject(); return false; }
    }
    public FollowerTransferPlan ReviewFollowers() => ReviewFollowersFor(Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority());
    internal FollowerTransferPlan ReviewFollowersFor(Faction target)
    {
        if (RestoreFailed || Charters.RestoreFailed) throw new ArgumentException("Execution or consent journal unavailable.");
        var entries = Charters.Book.Charters.Where(c => c.Phase == RecordedCharterPhase.Active &&
            Charters.Book.Entity(c.Terms.Liege).Player && !Charters.Book.Entity(c.Terms.Subject).Player).Select(c => {
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
            var options = Charters.Book.Charters.Where(c => c.Phase == RecordedCharterPhase.Active &&
                Charters.Book.Entity(c.Terms.Liege).Player && !Charters.Book.Entity(c.Terms.Subject).Player)
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
        try
        {
            var item = Ledger.Transfer(id); var reference = references.Single(r => r.Id == id);
            if (reference.City == null || reference.City.Faction == null || "territory/" + reference.City.GetUniqueLoadID() != item.Territory ||
                reference.From == null || reference.From.GetUniqueLoadID() != item.FromOwner || reference.Target == null ||
                reference.Target.GetUniqueLoadID() != item.TargetOwner || reference.Target.IsPlayer)
                throw new ArgumentException("Cannot establish original transfer references.");
            var actual = Observe(reference.City, item.Territory, reference.Target);
            if (reference.City.Faction == reference.From) Ledger.CancelUnmoved(id, actual, Find.TickManager.TicksGame);
            else
            {
                if (reference.City.Faction != reference.Target) throw new ArgumentException("Unrelated current owner.");
                CityStockPreservation.InvalidateMaterial(reference.City);
                Ledger.Confirm(id, actual, Find.TickManager.TicksGame);
            }
        }
        catch (ArgumentException) { Reject(); }
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
