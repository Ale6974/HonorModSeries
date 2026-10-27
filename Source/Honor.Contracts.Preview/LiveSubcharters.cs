using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed record LiveSubcharterReview(NpcSubcharterPlan Plan, string Stamp, int RetainedDescendants, string Realm, string Representative)
{
    [System.Text.Json.Serialization.JsonIgnore] public Game Context { get; init; }
}

public sealed partial class LiveCharterLedger
{
    internal Faction SubcharterRepresentative(string party)
    {
        var binding = Binding(party);
        if (binding.PlayerGroup || binding.FactionGroup || binding.Settlement == null || binding.Settlement.Destroyed ||
            !Find.WorldObjects.AllWorldObjects.Contains(binding.Settlement)) throw new ArgumentException("No live NPC city party.");
        var faction = binding.Settlement.Faction;
        if (faction == null || faction.IsPlayer || faction.defeated || faction.Hidden || !faction.HasGoodwill || faction.HostileTo(Faction.OfPlayer) ||
            faction.leader == null || faction.leader.Dead || faction.leader.Faction != faction || faction.GoodwillWith(Faction.OfPlayer) < 50)
            throw new ArgumentException("No willing current NPC realm representative.");
        return faction;
    }
    public LiveSubcharterReview ReviewSubcharter(string previous, string parent, string created = null)
    {
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null) throw new NpcDecisionBlocked("ND_Unavailable");
        var transfers = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (RestoreFailed || transfers.RestoreFailed || payments.RestoreFailed || Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges ||
            transfers.Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) || payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared))
            throw new ArgumentException("Resolve coordinated, physical or unavailable journals first.");
        var old = Book.Charter(previous);
        var authority = Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority();
        var decision = NpcDecisionAuthority.Read(authority);
        var liege = parent == PlayerIdentity ? authority : SubcharterRepresentative(parent); var subject = SubcharterRepresentative(old.Terms.Subject);
        if (liege != authority || subject != authority || !Book.Entity(old.Terms.Liege).Player && SubcharterRepresentative(old.Terms.Liege) != authority)
            throw new ArgumentException("All affected authorities must already belong to the same current player realm.");
        var descendants = Book.PlayerFollowers().Where(c => c.Terms.Subject == old.Terms.Subject || Book.IsBelow(c.Terms.Subject, old.Terms.Subject)).ToArray();
        foreach (var c in descendants)
            if (SubcharterRepresentative(c.Terms.Subject) != authority) throw new ArgumentException("Align every descendant's actual realm first.");
        var observed = Observe(); long now = Find.TickManager.TicksGame;
        var plan = NpcSubcharters.ReviewAuthorized(Book, previous, parent, created ?? Guid.NewGuid().ToString("N"), decision, now, observed);
        string stamp = JsonSerializer.Serialize(new { plan.Before, plan.After, plan.Tick, Transfers = transfers.Ledger.Export(),
            Authority = authority.GetUniqueLoadID(), Leader = authority.leader.GetUniqueLoadID(), Goodwill = authority.GoodwillWith(Faction.OfPlayer), Observed = observed });
        return new(plan, stamp, descendants.Length - 1, authority.Name, authority.leader.LabelShortCap) { Context = Current.Game };
    }
    public void ApplySubcharter(LiveSubcharterReview reviewed)
    {
        Execute(() => {
            if (reviewed == null || reviewed.Context != Current.Game) throw new ArgumentException("Review this current game first.");
            var fresh = ReviewSubcharter(reviewed.Plan.Previous, reviewed.Plan.Parent, reviewed.Plan.Created);
            if (fresh.Stamp != reviewed.Stamp) throw new ArgumentException("NPC consent, graph or world changed.");
            var replacement = NpcSubcharters.CommitAuthorized(Book, fresh.Plan,
                NpcDecisionAuthority.Read(Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority()), Find.TickManager.TicksGame, Observe());
            Book = replacement; // Only one in-memory swap, after the entire replacement has been validated.
            Messages.Message("NS_Applied".Translate(), MessageTypeDefOf.NeutralEvent);
        });
    }
}

public sealed class NpcSubchartersWindow : Window
{
    private readonly Game context = Current.Game;
    private string previous, parent;
    private LiveSubcharterReview review;
    private Vector2 scroll;
    private float height = 1200;
    public override Vector2 InitialSize => new(850, 760);
    public NpcSubchartersWindow() { forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override void DoWindowContents(Rect rect)
    {
        if (context != Current.Game) { Widgets.Label(rect, "ND_Unavailable".Translate()); return; }
        var ledger = Current.Game.GetComponent<LiveCharterLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, Mathf.Max(20000, height + 1000)));
        list.Label("NS_Warning".Translate());
        list.Label("ND_Limits".Translate());
        if (ledger.RestoreFailed) list.Label("HCP_RestoreFailed".Translate());
        else
        {
            var followers = ledger.Book.PlayerFollowers();
            if (list.ButtonText("NS_Subject".Translate(previous == null ? "HCL_Missing".Translate() : ledger.Label(ledger.Book.Charter(previous).Terms.Subject))))
            {
                var options = followers.Select(c => new FloatMenuOption(ledger.Label(c.Terms.Subject), () => { previous = c.Id; review = null; })).ToList();
                if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
            }
            if (list.ButtonText("NS_Parent".Translate(parent == null ? "HCL_Missing".Translate() : ledger.Label(parent))))
            {
                var options = followers.Select(c => new FloatMenuOption(ledger.Label(c.Terms.Subject), () => { parent = c.Terms.Subject; review = null; })).ToList();
                options.Insert(0, new FloatMenuOption("HCL_Player".Translate(), () => { parent = ledger.PlayerIdentity; review = null; }));
                if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
            }
            if (previous != null && parent != null && list.ButtonText("NS_Review".Translate()))
            {
                try { review = ledger.ReviewSubcharter(previous, parent); }
                catch (NpcDecisionBlocked e) { review = null; Messages.Message(e.Key.Translate(), MessageTypeDefOf.RejectInput); }
                catch (ArgumentException) { review = null; Messages.Message("NS_Rejected".Translate(), MessageTypeDefOf.RejectInput); }
            }
            if (review != null)
            {
                var terms = ledger.Book.Charter(review.Plan.Previous).Terms;
                list.Label("NS_Plan".Translate(ledger.Label(terms.Subject), ledger.Label(terms.Liege), ledger.Label(review.Plan.Parent)));
                list.Label("NS_Descendants".Translate(review.RetainedDescendants));
                list.Label("NS_Representative".Translate(review.Realm, review.Representative));
                list.Label("NS_Terms".Translate(terms.TributeValue.ToString("0.##"), (terms.IntervalTicks / (double)PoliticalWorld.Day).ToString("0.##")));
                if (list.ButtonText("NS_Confirm".Translate())) { ledger.ApplySubcharter(review); review = null; }
            }
            foreach (var c in followers)
            {
                var path = ledger.Book.HierarchyPath(c.Terms.Subject);
                list.Label("NS_Tier".Translate(path.Count, string.Join(" → ", path.Select(ledger.Label))));
                list.Label("NS_Link".Translate(ledger.Label(c.Terms.Liege), ledger.Label(c.Terms.Subject), ledger.PoliticalOwner(c.Terms.Subject)));
                list.Label("NS_NativeFaction".Translate(ledger.CurrentNativeFaction(c.Terms.Subject)));
            }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
