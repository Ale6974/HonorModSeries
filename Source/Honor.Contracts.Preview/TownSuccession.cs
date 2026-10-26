using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed record LiveTownSuccessionReview(TownSuccessionPlan Plan, string Stamp, string Realm, string Representative, Game Context = null);

public sealed partial class LiveCharterLedger
{
    private string townLossSnapshot;
    private TownSuccessionArchive townLosses = new();
    internal IReadOnlyCollection<TownLossProof> TownLosses => townLosses.Records;
    internal bool TownLossReviewRoot(string id) => townLosses.IsReviewRoot(id);
    internal bool UnresolvedTownLosses => townLosses.HasUnresolved;
    private void ExposeTownLosses()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) townLossSnapshot = townLosses.Export();
        Scribe_Values.Look(ref townLossSnapshot, "honorUpperTownLossesV1");
    }
    private void ValidateTownLosses()
    {
        if (townLossSnapshot != null) townLosses = TownSuccessionArchive.Restore(townLossSnapshot);
        townLosses.ValidateBook(Book);
    }
    public LiveTownSuccessionReview ReviewTownSuccession(string root, string prefix = null)
    {
        if (!LossChangesAvailable()) throw new ArgumentException("Resolve unavailable or pending political/payment records.");
        var proof = townLosses.Record(root);
        foreach (var loss in townLosses.ScopeLosses(root))
        {
            var lostCity = Binding(loss.Territory).Settlement;
            if (loss.Kind == TownLossKind.Destroyed)
            {
                if (lostCity != null && !lostCity.Destroyed && Find.WorldObjects.AllWorldObjects.Contains(lostCity))
                    throw new ArgumentException("The previously destroyed object returned; review its new rights separately.");
            }
            else if (lostCity == null || lostCity.Destroyed || !Find.WorldObjects.AllWorldObjects.Contains(lostCity) ||
                lostCity.Faction?.GetUniqueLoadID() != loss.CapturingOwner)
                throw new ArgumentException("The observed hostile owner changed again; another review adapter is required.");
        }
        var authority = Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority();
        var decision = NpcDecisionAuthority.Read(authority);
        if (authority == null || authority.def == null || authority.IsPlayer || authority.def.defName == UnionPoliticsLedger.UnionDefName ||
            authority.def.royalTitleTags?.Count > 0 ||
            authority.GetUniqueLoadID() != proof.OwnerAtLoss || authority.leader == null || authority.leader.Dead ||
            authority.leader.Destroyed || authority.leader.Downed || authority.leader.InMentalState || authority.leader.IsPrisoner || authority.leader.IsSlave ||
            authority.leader.Faction != authority || authority.GoodwillWith(Faction.OfPlayer) < 50)
            throw new ArgumentException("Same surviving willing realm representative is required; collective union succession is not implemented.");
        var source = CharterBook.Restore(proof.Before); var original = source.Charter(root);
        if (!Book.Entity(original.Terms.Liege).Player && SubcharterRepresentative(original.Terms.Liege) != authority)
            throw new ArgumentException("The surviving higher liege is not represented by this realm.");
        long now = Find.TickManager.TicksGame;
        var plan = townLosses.Review(root, Book, prefix ?? Guid.NewGuid().ToString("N"),
            NpcRealmDecision.Consent(decision), now, Observe());
        var current = plan.Descendants.Select(Book.Charter).ToArray();
        var actual = new List<object>();
        foreach (var c in current)
        {
            if (SubcharterRepresentative(c.Terms.Subject) != authority) throw new ArgumentException("Every survivor must remain in the same actual realm.");
            var binding = Binding(c.Terms.Subject); var entity = Book.Entity(c.Terms.Subject);
            var transfers = Current.Game.GetComponent<TerritoryAdministrationLedger>();
            if (transfers.RestoreFailed || !transfers.Ledger.VerifiedOwner(entity.Id, entity.NativeOwner, binding.Settlement.Faction.GetUniqueLoadID()))
                throw new ArgumentException("Unverified native survivor ownership.");
            actual.Add(new { c.Id, Town = binding.Settlement.GetUniqueLoadID(), Owner = binding.Settlement.Faction.GetUniqueLoadID() });
        }
        string stamp = JsonSerializer.Serialize(new { Plan = plan, Archive = townLosses.Export(), Actual = actual,
            Authority = authority.GetUniqueLoadID(), Leader = authority.leader.GetUniqueLoadID(), Goodwill = authority.GoodwillWith(Faction.OfPlayer),
            Transfers = Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger.Export(), Observations = Observe() });
        return new(plan, stamp, authority.Name, authority.leader.LabelShortCap, Current.Game);
    }
    public void ApplyTownSuccession(LiveTownSuccessionReview reviewed)
    {
        Execute(() => {
            if (reviewed == null || reviewed.Context != Current.Game || reviewed.Plan?.Replacements == null || reviewed.Plan.Replacements.Count == 0 && reviewed.Plan.ScopePrefix == null ||
                reviewed.Plan.Replacements.Values.Any(v => string.IsNullOrWhiteSpace(v))) throw new ArgumentException("Review first.");
            string prefix = reviewed.Plan.ScopePrefix;
            if (prefix == null) { string sample = reviewed.Plan.Replacements.Values.First(); int separator = sample.LastIndexOf('/');
                if (separator < 1) throw new ArgumentException("Invalid succession identity."); prefix = sample.Substring(0, separator); }
            var fresh = ReviewTownSuccession(reviewed.Plan.Root, prefix);
            if (fresh.Stamp != reviewed.Stamp) throw new ArgumentException("Succession authority or source changed.");
            var result = townLosses.Commit(Book, fresh.Plan, Find.TickManager.TicksGame, Observe());
            Book = result.Book; townLosses = result.Archive;
            Messages.Message("LS_Completed".Translate(fresh.Plan.Replacements.Count), MessageTypeDefOf.NeutralEvent);
        }, resolvingLoss: true);
    }
}

public sealed class TownSuccessionWindow : Window
{
    private string root;
    private LiveTownSuccessionReview review;
    private Vector2 scroll;
    private float height = 1200;
    public TownSuccessionWindow() { forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override Vector2 InitialSize => new(850, 760);
    public override void DoWindowContents(Rect rect)
    {
        var ledger = Current.Game.GetComponent<LiveCharterLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, Mathf.Max(12000, height + 1000)));
        list.Label("LS_Warning".Translate());
        list.Label("ND_Limits".Translate());
        list.Label("HL_Limits".Translate());
        list.Label("LC_Warning".Translate());
        list.Label("LM_Warning".Translate());
        if (ledger.UnresolvedTownLosses) list.Label("LS_Freeze".Translate());
        if (ledger.RestoreFailed) list.Label("HCP_RestoreFailed".Translate());
        else
        {
            if (list.ButtonText("LS_Select".Translate(root == null ? "HCL_Missing".Translate() : ledger.TownLosses.Single(r => r.Root == root).Label)))
            {
                var options = ledger.TownLosses.Where(r => r.Resolution == null && ledger.TownLossReviewRoot(r.Root)).Select(r => new FloatMenuOption(r.Label, () => { root = r.Root; review = null; })).ToList();
                if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options)); else list.Label("LS_Empty".Translate());
            }
            if (root != null && list.ButtonText("LS_Review".Translate()))
            {
                try { review = ledger.ReviewTownSuccession(root); }
                catch (NpcDecisionBlocked e) { review = null; Messages.Message(e.Key.Translate(), MessageTypeDefOf.RejectInput); }
                catch (ArgumentException) { review = null; Messages.Message("LS_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
            }
            if (review != null)
            {
                if (review.Plan.ScopeRoots != null)
                {
                    list.Label("LC_Scope".Translate(review.Plan.ScopeRoots.Length));
                    foreach (string id in review.Plan.ScopeRoots) list.Label("LC_Loss".Translate(ledger.TownLosses.Single(r => r.Root == id).Label));
                }
                list.Label("LS_Plan".Translate(review.Realm, review.Representative, ledger.Label(review.Plan.Parent), review.Plan.Replacements.Count, review.Plan.Descendants.Length));
                foreach (var p in review.Plan.Replacements) list.Label(ledger.Label(ledger.Book.Charter(p.Key).Terms.Subject));
                if (list.ButtonText("LS_Confirm".Translate())) { ledger.ApplyTownSuccession(review); review = null; }
            }
            foreach (var proof in ledger.TownLosses.Where(r => r.Resolution != null)) list.Label("LS_History".Translate(proof.Label, proof.Resolution.Replacements.Count));
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
