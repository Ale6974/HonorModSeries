using Honor.Contracts.Rules;
using RimWorld;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed partial class LiveCharterLedger
{
    private CharterBook performanceBook;
    private TributeLedger performancePayments;
    private long performanceRevision = -1, performanceSlice = -1, performanceBookTick = -1;
    private readonly Dictionary<(string, string), TributePerformance> performanceCache = [];
    internal TributePerformance Performance(string id, bool pair)
    {
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (RestoreFailed || payments.RestoreFailed) throw new ArgumentException("Performance evidence unavailable.");
        long now = Find.TickManager.TicksGame;
        if (performanceBook != Book || performancePayments != payments.Ledger || performanceRevision != payments.Ledger.Revision ||
            performanceSlice != now / 250 || performanceBookTick != Book.LastTick)
        {
            performanceCache.Clear(); performanceBook = Book; performancePayments = payments.Ledger;
            performanceRevision = payments.Ledger.Revision; performanceSlice = now / 250; performanceBookTick = Book.LastTick;
        }
        var c = Book.Charter(id); var key = (pair ? c.Terms.Liege : id, pair ? c.Terms.Subject : "");
        if (!performanceCache.TryGetValue(key, out var result))
        {
            result = pair ? TributePerformanceReview.ForPair(Book, payments.Ledger, c.Terms.Liege, c.Terms.Subject, now)
                : TributePerformanceReview.ForCharter(Book, payments.Ledger, id, now);
            performanceCache.Add(key, result);
        }
        return result;
    }
    internal CharterOfferAssessment AssessOffer(string id, bool exact = true)
    {
        var c = Book.Charter(id); var f = CounterpartyFaction(id);
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (RestoreFailed || payments.RestoreFailed || f == null || f.def == null || f.IsPlayer || f.Hidden || f.temporary || f.defeated || !f.def.humanlikeFaction || !f.HasGoodwill ||
            f.leader == null || f.leader.Dead || f.leader.Faction != f || !Find.FactionManager.AllFactionsListForReading.Contains(f))
            throw new ArgumentException("No current NPC authority or payment evidence.");
        bool subject = Book.Entity(c.Terms.Subject).Player;
        if (!exact)
        {
            var assessment = CharterOffers.Draft(c.Terms).Assess(subject, f.GoodwillWith(Faction.OfPlayer));
            return subject ? CharterOffers.AssessHistory(assessment, Performance(id, true)) : assessment;
        }
        return CharterOffers.Assess(Book, payments.Ledger, subject ? c.Terms.Subject : c.Terms.Liege,
            subject ? c.Terms.Liege : c.Terms.Subject, subject, CharterOffers.Draft(c.Terms), f.GoodwillWith(Faction.OfPlayer), Find.TickManager.TicksGame);
    }
    internal static void DrawPerformance(Listing_Standard list, TributePerformance result)
    {
        if (result.Accounts == 0) { list.Label("PH_Untracked".Translate()); return; }
        list.Label("PH_Summary".Translate(result.Due, result.PaidWithinGrace, result.PaidLate, result.WithinGrace, result.Overdue, result.Reserved));
        list.Label("PH_Values".Translate(result.FulfilledValue.ToString("0.##"), result.OutstandingValue.ToString("0.##")));
        if (!result.Complete) list.Label("PH_Incomplete".Translate());
    }
}
