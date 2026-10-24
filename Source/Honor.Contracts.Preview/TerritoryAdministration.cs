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
    TerritoryTransferPurpose Purpose, string Stamp);

/// <summary>Optional actual NPC city ownership execution. Never assigns a city to the native player faction.</summary>
public sealed class TerritoryAdministrationLedger : GameComponent
{
    private string snapshot;
    private List<TerritoryTransferReference> references = [];
    public TerritoryTransferLedger Ledger { get; private set; } = new();
    public bool RestoreFailed { get; private set; }
    private LiveCharterLedger Charters => Current.Game.GetComponent<LiveCharterLedger>();
    public TerritoryAdministrationLedger(Game game) { }
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
        return new(territory, owner?.GetUniqueLoadID(), all.Count(s => s.Faction == owner), exists, owner?.IsPlayer == true,
            exists && city.HasMap, !exists || city.trader == null || city.EverVisited || city.previouslyGeneratedInhabitants?.Count > 0,
            exists && city.def.canHaveFaction && Eligible(owner), Eligible(target), target?.IsPlayer == true);
    }
    private (RecordedCharter Charter, Settlement City, Faction Target, TerritoryTransferObservation Observation, TerritoryTransferPurpose Purpose) Context(string id)
    {
        if (RestoreFailed || Charters.RestoreFailed || Current.Game.GetComponent<TributePaymentLedger>().HasPending(id))
            throw new ArgumentException("Resolve unavailable journals and pending deliveries.");
        var charter = Charters.Book.Charter(id); var binding = Charters.Binding(charter.Terms.Subject);
        if (binding.PlayerGroup || binding.FactionGroup || binding.Settlement == null) throw new ArgumentException("Only single NPC city subjects.");
        Charters.Book.ValidateCurrent(id, Charters.Observe());
        bool released = charter.Phase == RecordedCharterPhase.Released;
        var union = Current.Game.GetComponent<UnionPoliticsLedger>();
        var target = released ? (!union.RestoreFailed ? union.Union : null) : Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority();
        if (target == null || !Find.FactionManager.AllFactionsListForReading.Contains(target))
            throw new ArgumentException("Configure a live union or player shared authority first.");
        var observed = Observe(binding.Settlement, binding.Identity, target);
        return (charter, binding.Settlement, target, observed, released ? TerritoryTransferPurpose.ReleasedToUnion : TerritoryTransferPurpose.ActiveCharter);
    }
    public TerritoryAdministrationReview Review(string id)
    {
        var c = Context(id); var clone = TerritoryTransferLedger.Restore(Ledger.Export());
        clone.Prepare("review-" + Guid.NewGuid().ToString("N"), Charters.Book, id, c.Observation, c.Target.GetUniqueLoadID(),
            c.Purpose == TerritoryTransferPurpose.ActiveCharter ? c.Target.GetUniqueLoadID() : null,
            c.Purpose == TerritoryTransferPurpose.ReleasedToUnion ? c.Target.GetUniqueLoadID() : null, c.Purpose, Find.TickManager.TicksGame);
        return new(id, c.City.Label, c.City.Faction.Name, c.Target.Name, c.Purpose,
            string.Join("/", id, c.City.GetUniqueLoadID(), c.City.Faction.GetUniqueLoadID(), c.Target.GetUniqueLoadID(), Charters.Book.LastTick, Ledger.LastTick, c.Purpose));
    }
    public void Apply(TerritoryAdministrationReview reviewed)
    {
        if (reviewed == null) { Reject(); return; }
        try
        {
            var current = Review(reviewed.Charter);
            if (current.Stamp != reviewed.Stamp) throw new ArgumentException("Reviewed state changed.");
            var c = Context(reviewed.Charter); string id = Guid.NewGuid().ToString("N");
            Ledger.Prepare(id, Charters.Book, c.Charter.Id, c.Observation, c.Target.GetUniqueLoadID(),
                c.Purpose == TerritoryTransferPurpose.ActiveCharter ? c.Target.GetUniqueLoadID() : null,
                c.Purpose == TerritoryTransferPurpose.ReleasedToUnion ? c.Target.GetUniqueLoadID() : null, c.Purpose, Find.TickManager.TicksGame);
            references.Add(new() { Id = id, City = c.City, From = c.City.Faction, Target = c.Target });
            c.City.SetFaction(c.Target);
            Ledger.Confirm(id, Observe(c.City, c.Charter.Terms.Subject, c.Target), Find.TickManager.TicksGame);
            Messages.Message("CT_Applied".Translate(), MessageTypeDefOf.NeutralEvent);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        { Log.Warning("[Honor.Contracts.Preview] Territory execution incomplete; recorded intent retained. " + e.GetType().Name); Reject(); }
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
            else Ledger.Confirm(id, actual, Find.TickManager.TicksGame);
        }
        catch (ArgumentException) { Reject(); }
    }
    private static void Reject() => Messages.Message("CT_Unavailable".Translate(), MessageTypeDefOf.RejectInput);
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
