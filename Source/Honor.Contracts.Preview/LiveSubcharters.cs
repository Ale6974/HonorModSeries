using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed record LiveSubcharterReview(NpcSubcharterPlan Plan, string Stamp, int RetainedDescendants, string Realm, string Representative)
{
    [System.Text.Json.Serialization.JsonIgnore] public Game Context { get; init; }
    public RetainedRealmConsent[] RetainedRealms { get; init; }
    public RetainedRealmConsent[] Parties { get; init; }
}

public sealed partial class LiveCharterLedger
{
    private string reparentSnapshot;
    private NpcReparentArchive reparentHistory = new();
    private readonly Dictionary<string, NpcReparentPlayerPerformance> reparentPerformanceCache = [];
    private void ExposeReparentHistory()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed && reparentHistory.Records.Count > 0) reparentSnapshot = reparentHistory.Export();
        Scribe_Values.Look(ref reparentSnapshot, "honorNpcReparentHistoryV1");
    }
    private void ValidateReparentHistory()
    {
        if (reparentSnapshot != null) reparentHistory = NpcReparentArchive.Restore(reparentSnapshot);
        reparentHistory.ValidateBook(Book);
    }
    internal NpcReparentPlayerPerformance ReparentPlayerPerformance(string created)
    {
        if (!reparentPerformanceCache.TryGetValue(created, out var result))
        { result = reparentHistory.PlayerPerformanceAt(created); reparentPerformanceCache[created] = result; }
        return result;
    }
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
        if (Current.ProgramState != ProgramState.Playing || Current.Game != context) throw new NpcDecisionBlocked("ND_Unavailable");
        var transfers = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        var protection = Current.Game.GetComponent<CharterProtectionLedger>();
        if (RestoreFailed || transfers.RestoreFailed || payments.RestoreFailed || protection.RestoreFailed || Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges ||
            transfers.Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) || payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared))
            throw new ArgumentException("Resolve coordinated, physical or unavailable journals first.");
        var old = Book.Charter(previous);
        reparentHistory.ValidateBook(Book);
        reparentHistory.ValidatePeers(transfers.Ledger, payments.Ledger);
        var authority = Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority();
        var decision = NpcDecisionAuthority.Read(authority);
        var liege = parent == PlayerIdentity ? authority : SubcharterRepresentative(parent);
        if (liege != authority) throw new ArgumentException("New parent must belong to the current player realm.");
        var partyDecisions = new List<RetainedRealmConsent>();
        var observedRealms = new HashSet<Faction> { authority };
        foreach (var party in new[] { old.Terms.Subject, old.Terms.Liege, parent }.Distinct(StringComparer.Ordinal).Where(p => !Book.Entity(p).Player))
        {
            var actual = SubcharterRepresentative(party);
            observedRealms.Add(actual);
            if (actual.HostileTo(authority) || authority.HostileTo(actual) || !transfers.Ledger.VerifiedOwner(party, Book.Entity(party).NativeOwner, actual.GetUniqueLoadID()))
                throw new ArgumentException("Incoming parties need original ownership provenance and current reciprocal peace.");
            partyDecisions.Add(new(party, actual.GetUniqueLoadID(), NpcDecisionAuthority.Read(actual), true));
        }
        var parties = partyDecisions.Any(p => p.ActualOwner != authority.GetUniqueLoadID()) ? partyDecisions.ToArray() : null;
        var descendants = Book.PlayerFollowers().Where(c => c.Terms.Subject == old.Terms.Subject || Book.IsBelow(c.Terms.Subject, old.Terms.Subject)).ToArray();
        var retained = new List<RetainedRealmConsent>();
        foreach (var c in descendants)
        {
            var actual = SubcharterRepresentative(c.Terms.Subject);
            observedRealms.Add(actual);
            if (c.Terms.Subject == old.Terms.Subject) continue; // Exact incoming consent is paired above, never counted as a retained child.
            if (actual == authority) continue;
            if (actual.HostileTo(authority) || authority.HostileTo(actual) ||
                !transfers.Ledger.VerifiedOwner(c.Terms.Subject, Book.Entity(c.Terms.Subject).NativeOwner, actual.GetUniqueLoadID()))
                throw new ArgumentException("Original completed transfer and reciprocal peace required for a retained foreign descendant.");
            retained.Add(new(c.Terms.Subject, actual.GetUniqueLoadID(), NpcDecisionAuthority.Read(actual), true));
        }
        if (observedRealms.Any(a => observedRealms.Any(b => a != b && (a.HostileTo(b) || b.HostileTo(a)))))
            throw new ArgumentException("Every affected current realm must be reciprocally peaceful; wartime reparenting needs separate authority.");
        var observed = Observe(); long now = Find.TickManager.TicksGame;
        created ??= Guid.NewGuid().ToString("N");
        var plan = parties == null ? NpcSubcharters.ReviewAuthorized(Book, previous, parent, created, decision, now, observed)
            : NpcSubcharters.ReviewWithParties(Book, previous, parent, created, decision, parties, now, observed);
        string stamp = JsonSerializer.Serialize(new { plan.Before, plan.After, plan.Tick, Transfers = transfers.Ledger.Export(), Payments = payments.Ledger.Export(),
            FinalConsents = finalConsents.Export(), Amendments = amendments.Export(), PlayerRenewals = playerRenewals.Export(),
            TownLosses = townLosses.Export(), OwnershipReturns = ownershipReturns.Export(), Protection = protection.Ledger.Export(), ReparentHistory = reparentHistory.Export(),
            Authority = authority.GetUniqueLoadID(), Leader = authority.leader.GetUniqueLoadID(), Goodwill = authority.GoodwillWith(Faction.OfPlayer), Observed = observed,
            RetainedRealms = retained.Count == 0 ? null : retained.ToArray(), Parties = parties });
        // Validate complete provenance/third-party terms and archive capacity before offering this confirmation.
        var witness = new NpcReparentWitness(plan.Previous, plan.Parent, plan.Created, plan.Before, now, decision, observed,
            transfers.Ledger.Export(), payments.Ledger.Export()) { RetainedRealms = retained.Count == 0 ? null : retained.ToArray(), Parties = parties };
        reparentHistory.Append(witness, Book);
        if (Current.Game != context || Current.ProgramState != ProgramState.Playing) throw new ArgumentException("Game changed during current realm review.");
        return new(plan, stamp, descendants.Length - 1, authority.Name, authority.leader.LabelShortCap) { Context = Current.Game, RetainedRealms = witness.RetainedRealms, Parties = parties };
    }
    public void ApplySubcharter(LiveSubcharterReview reviewed)
    {
        Execute(() => {
            if (reviewed == null || reviewed.Context != Current.Game) throw new ArgumentException("Review this current game first.");
            var fresh = ReviewSubcharter(reviewed.Plan.Previous, reviewed.Plan.Parent, reviewed.Plan.Created);
            if (fresh.Stamp != reviewed.Stamp) throw new ArgumentException("NPC consent, graph or world changed.");
            var currentDecision = NpcDecisionAuthority.Read(Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority());
            var replacement = fresh.Parties == null ? NpcSubcharters.CommitAuthorized(Book, fresh.Plan,
                currentDecision, Find.TickManager.TicksGame, Observe()) : NpcSubcharters.CommitWithParties(Book, fresh.Plan,
                currentDecision, fresh.Parties, Find.TickManager.TicksGame, Observe());
            var witness = new NpcReparentWitness(fresh.Plan.Previous, fresh.Plan.Parent, fresh.Plan.Created, fresh.Plan.Before,
                fresh.Plan.Tick, fresh.Plan.Decision, Observe(), Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger.Export(),
                Current.Game.GetComponent<TributePaymentLedger>().Ledger.Export()) { RetainedRealms = fresh.RetainedRealms?.ToArray(), Parties = fresh.Parties?.ToArray() };
            var recorded = reparentHistory.Append(witness, Book);
            recorded.Archive.ValidatePeers(Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger,
                Current.Game.GetComponent<TributePaymentLedger>().Ledger);
            if (replacement.Export() != fresh.Plan.After) throw new ArgumentException("Current reparent result changed.");
            replacement = recorded.Book;
            // Retained descendants and third-party obligations must remain paired with their original records.
            finalConsents.ValidateBook(replacement); amendments.ValidateBook(replacement); playerRenewals.ValidatePair(amendments, replacement);
            townLosses.ValidateBook(replacement); ownershipReturns.ValidatePair(townLosses, replacement);
            context.GetComponent<CharterProtectionLedger>().Ledger.ValidateBook(replacement);
            if (ReviewSubcharter(fresh.Plan.Previous, fresh.Plan.Parent, fresh.Plan.Created).Stamp != fresh.Stamp)
                throw new ArgumentException("Current party decisions or retained scope changed before atomic assignment.");
            if (Current.Game != context || Current.ProgramState != ProgramState.Playing)
                throw new ArgumentException("Current game changed before committing reparent history.");
            Book = replacement; reparentHistory = recorded.Archive; // Both staged models change without native callbacks.
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
            if (previous != null && ledger.Book.Charter(previous).NpcAuthority != null && list.ButtonText("NT_Request".Translate()))
                ledger.RequestNpcTermination(previous);
            if (previous != null && ledger.Book.Charter(previous).NpcAuthority != null && list.ButtonText("NT_Queue".Translate()))
                ledger.QueueNpcTermination(previous);
            ledger.DrawNpcTerminations(list);
            if (review != null)
            {
                var terms = ledger.Book.Charter(review.Plan.Previous).Terms;
                list.Label("NS_Plan".Translate(ledger.Label(terms.Subject), ledger.Label(terms.Liege), ledger.Label(review.Plan.Parent)));
                list.Label("NS_Descendants".Translate(review.RetainedDescendants));
                list.Label("NS_Representative".Translate(review.Realm, review.Representative));
                if (review.RetainedRealms != null)
                    foreach (var retained in review.RetainedRealms)
                        list.Label("NS_RetainedRealm".Translate(ledger.Label(retained.Party), retained.ActualOwner, retained.Decision.Representative));
                if (review.Parties != null)
                    foreach (var party in review.Parties)
                        list.Label("NS_IncomingParty".Translate(ledger.Label(party.Party), party.ActualOwner, party.Decision.Representative));
                list.Label("NS_Terms".Translate(terms.TributeValue.ToString("0.##"), (terms.IntervalTicks / (double)PoliticalWorld.Day).ToString("0.##")));
                if (list.ButtonText("NS_Confirm".Translate())) { ledger.ApplySubcharter(review); review = null; }
            }
            foreach (var c in followers)
            {
                var path = ledger.Book.HierarchyPath(c.Terms.Subject);
                list.Label("NS_Tier".Translate(path.Count, string.Join(" → ", path.Select(ledger.Label))));
                list.Label("NS_Link".Translate(ledger.Label(c.Terms.Liege), ledger.Label(c.Terms.Subject), ledger.PoliticalOwner(c.Terms.Subject)));
                list.Label("NS_NativeFaction".Translate(ledger.CurrentNativeFaction(c.Terms.Subject)));
                if (c.Reparent != null)
                {
                    list.Label("NS_PreviousLiege".Translate(ledger.Label(ledger.Book.Charter(c.Reparent.Previous).Terms.Liege)));
                    var past = ledger.ReparentPlayerPerformance(c.Id);
                    if (past != null)
                    {
                        list.Label("NS_PastPerformance".Translate(ledger.Label(past.Liege), (past.Tick / (double)PoliticalWorld.Day).ToString("0.##")));
                        LiveCharterLedger.DrawPerformance(list, past.Performance);
                    }
                }
            }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
