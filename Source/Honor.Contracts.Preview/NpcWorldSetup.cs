using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed record NpcWorldReview(int Percent, string Stamp, IReadOnlyList<NpcWorldLink> Links);

public sealed partial class LiveCharterLedger
{
    private bool npcWorldInitialized;
    private long npcWorldTick = -1;
    private List<string> npcWorldRecords = [];
    private void ExposeNpcWorld()
    {
        Scribe_Values.Look(ref npcWorldInitialized, "honorNpcWorldInitialized");
        Scribe_Values.Look(ref npcWorldTick, "honorNpcWorldTick", -1L);
        Scribe_Collections.Look(ref npcWorldRecords, "honorNpcWorldRecords", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) npcWorldRecords ??= [];
    }
    private void ValidateNpcWorld()
    {
        if (!npcWorldInitialized && (npcWorldTick != -1 || npcWorldRecords.Count != 0) || npcWorldInitialized &&
            (npcWorldTick < 0 || npcWorldTick > Book.LastTick || npcWorldRecords.Count is < 1 or > 30 ||
                npcWorldRecords.Distinct(StringComparer.Ordinal).Count() != npcWorldRecords.Count || npcWorldRecords.Any(id =>
                    Book.Charter(id).NpcAuthority == null || Book.Charter(id).PlayerSignedTick != npcWorldTick || Book.Charter(id).NpcSignedTick != npcWorldTick)))
            throw new ArgumentException("Invalid NPC world initialization registry.");
    }
    public override void FinalizeInit()
    {
        if (!RestoreFailed && reparentHistory.Records.Count > 0)
        {
            try
            {
                var transfers = context.GetComponent<TerritoryAdministrationLedger>(); var payments = context.GetComponent<TributePaymentLedger>();
                if (transfers.RestoreFailed || payments.RestoreFailed) throw new ArgumentException("Original reparent peers unavailable.");
                reparentHistory.ValidatePeers(transfers.Ledger, payments.Ledger);
            }
            catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
            { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Reparent peer history rejected; original data retained. " + e.GetType().Name); }
        }
        FinalizeNpcTerminations();
        ValidateOwnershipReturnClock();
        ValidatePlayerRenewalLineage();
        ValidateLawfulRestore();
        if (!RestoreFailed && (Book.LastTick > Find.TickManager.TicksGame || npcWorldInitialized && npcWorldTick > Find.TickManager.TicksGame))
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Charter/initial NPC registry clock mismatch; source records retained."); }
    }
    private (NpcWorldReview Review, CharterBook Book, List<TerritoryBinding> Bindings) PrepareNpcWorld(int percent)
    {
        var transfers = Current.Game.GetComponent<TerritoryAdministrationLedger>(); var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (npcWorldInitialized || RestoreFailed || transfers.RestoreFailed || payments.RestoreFailed ||
            Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges || transfers.Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) ||
            payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared)) throw new ArgumentException("World initialization unavailable.");
        var cities = Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => !s.Destroyed).ToArray();
        bool Eligible(Settlement s)
        {
            var f = s.Faction;
            return f != null && !f.IsPlayer && !f.Hidden && !f.temporary && !f.defeated && f.HasGoodwill && f.def.humanlikeFaction &&
                !f.def.permanentEnemy && !(f.def.royalTitleTags?.Count > 0) && !f.HostileTo(Faction.OfPlayer) &&
                f.def.defName != UnionPoliticsLedger.UnionDefName && f.def.defName != RealmAdministrationLedger.DefName &&
                f.leader != null && !f.leader.Dead && f.leader.Faction == f;
        }
        var byId = cities.Where(Eligible).ToDictionary(s => "territory/" + s.GetUniqueLoadID(), StringComparer.Ordinal);
        var links = NpcWorldHierarchy.Plan(byId.Select(pair => new NpcWorldCity(pair.Key, pair.Value.Faction.GetUniqueLoadID(), pair.Value.Faction.leader.GetUniqueLoadID())),
            Book.Entities.Select(e => e.NativeOwner), percent);
        if (links.Count == 0) throw new ArgumentException("No eligible unregistered NPC realm towns selected.");
        long now = Find.TickManager.TicksGame;
        var copy = CharterBook.Restore(Book.Export()); var proposed = new List<TerritoryBinding>(bindings); var observed = Observe().ToList();
        foreach (string owner in links.Select(l => l.Owner).Distinct(StringComparer.Ordinal))
        {
            var faction = byId[links.First(l => l.Owner == owner).City].Faction; string root = "faction/" + owner;
            copy.Register(new(root, owner, false), now);
            proposed.Add(new() { Identity = root, OriginalFaction = faction, FactionGroup = true });
            observed.Add(new(root, owner, true, false, true));
        }
        foreach (var link in links)
        {
            var city = byId[link.City]; copy.Register(new(link.City, link.Owner, false), now);
            proposed.Add(new() { Identity = link.City, Settlement = city, OriginalFaction = city.Faction });
            observed.Add(new(link.City, link.Owner, true, false, true));
        }
        foreach (var link in links)
        {
            string id = "npc-initial/" + link.City;
            copy.OfferNpc(id, new(link.Liege, link.City, 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m),
                new(link.Representative, link.Representative), now, PoliticalWorld.Day * 3, observed);
            copy.SignNpc(id, link.Liege, link.Representative, now, observed); copy.SignNpc(id, link.City, link.Representative, now, observed);
        }
        string stamp = JsonSerializer.Serialize(new { Before = Book.Export(), After = copy.Export(), Tick = now, Links = links,
            Transfers = transfers.Ledger.Export() });
        return (new(percent, stamp, links), copy, proposed);
    }
    public NpcWorldReview ReviewNpcWorld(int percent) => PrepareNpcWorld(percent).Review;
    public void ApplyNpcWorld(NpcWorldReview review)
    {
        Execute(() => {
            if (review == null) throw new ArgumentException("Review first.");
            var current = PrepareNpcWorld(review.Percent);
            if (current.Review.Stamp != review.Stamp) throw new ArgumentException("World/representative/contract changed.");
            var records = current.Review.Links.Select(l => "npc-initial/" + l.City).ToList();
            Book = current.Book; bindings = current.Bindings; npcWorldRecords = records;
            npcWorldTick = Find.TickManager.TicksGame; npcWorldInitialized = true;
            Messages.Message("NW_Applied".Translate(records.Count), MessageTypeDefOf.NeutralEvent);
        });
    }
    public string NpcWorldCityLabel(string city) => Find.WorldObjects.AllWorldObjects.OfType<Settlement>()
        .FirstOrDefault(s => "territory/" + s.GetUniqueLoadID() == city)?.Label ?? "HCL_Missing".Translate();
    public string NpcWorldOwnerLabel(string owner) => Find.FactionManager.AllFactionsListForReading
        .FirstOrDefault(f => f.GetUniqueLoadID() == owner)?.Name ?? "HCL_Missing".Translate();
    public string NpcWorldSpeakerLabel(string owner) => Find.FactionManager.AllFactionsListForReading
        .FirstOrDefault(f => f.GetUniqueLoadID() == owner)?.leader?.LabelShortCap ?? "HCL_Missing".Translate();
}

public sealed class NpcWorldSetupWindow : Window
{
    private int percent = 20;
    private NpcWorldReview review;
    private Vector2 scroll;
    private float height = 1200;
    public override Vector2 InitialSize => new(850, 760);
    public NpcWorldSetupWindow() { forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override void DoWindowContents(Rect rect)
    {
        var ledger = Current.Game.GetComponent<LiveCharterLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, 20000));
        list.Label("NW_Warning".Translate());
        if (list.ButtonText("NW_Percent".Translate(percent)))
            Find.WindowStack.Add(new FloatMenu(new[] { 20, 50, 100 }.Select(p => new FloatMenuOption(p + "%", () => { percent = p; review = null; })).ToList()));
        if (list.ButtonText("NW_Review".Translate()))
        {
            try { review = ledger.ReviewNpcWorld(percent); }
            catch (ArgumentException) { review = null; Messages.Message("NW_Rejected".Translate(), MessageTypeDefOf.RejectInput); }
        }
        if (review != null)
        {
            foreach (string owner in review.Links.Select(l => l.Owner).Distinct(StringComparer.Ordinal))
                list.Label("NW_Representative".Translate(ledger.NpcWorldOwnerLabel(owner), ledger.NpcWorldSpeakerLabel(owner)));
            foreach (var link in review.Links)
                list.Label("NS_Link".Translate(link.Liege.StartsWith("faction/") ? ledger.NpcWorldOwnerLabel(link.Owner) : ledger.NpcWorldCityLabel(link.Liege),
                    ledger.NpcWorldCityLabel(link.City), ledger.NpcWorldOwnerLabel(link.Owner)));
            if (list.ButtonText("NW_Confirm".Translate())) { ledger.ApplyNpcWorld(review); review = null; }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
