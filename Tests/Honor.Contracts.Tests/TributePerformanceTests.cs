using System.Text.Json;
using Honor.Contracts.Rules;

internal static class TributePerformanceTests
{
    public static int Run()
    {
        int count = 0; long day = PoliticalWorld.Day;
        void Check(bool v, string name) { count++; if (!v) throw new Exception("Performance: " + name); }
        void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        var book = new CharterBook(); book.Register(new("p", "native-p", true), 0);
        book.Register(new("a", "native-a", false), 0); book.Register(new("b", "native-b", false), 0);
        var observations = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        book = CharterOffers.Propose(book, "old", "p", "a", true, new(), 10, 0, observations).Book;
        var payments = new TributeLedger();
        book = CharterOffers.Confirm(book, "old", 10, 0, observations, payments).Book;
        var unknown = TributePerformanceReview.ForCharter(book, payments, "old", 0);
        Check(unknown.Accounts == 0 && unknown.Due == 0, "unenabled development records imply no obligations or performance");
        string originalBook = book.Export(); payments.Enable(book, "old", 0);
        var next = TributePerformanceReview.ForPair(book, payments, "a", "p", day * 4);
        Check(next.Accounts == 1 && next.Due == 0 && next.OutstandingValue == 0, "future bills are not failures or debts due now");
        next = TributePerformanceReview.ForPair(book, payments, "a", "p", day * 5);
        Check(next.Due == 1 && next.WithinGrace == 1 && next.Overdue == 0 && next.OutstandingValue == 90, "actual discounted bill and grace boundary");
        payments.Prepare("first", "old", 1, new[] { new TributeLine("stack1", 1, 150) }, day * 5);
        long preparedRevision = payments.Revision;
        next = TributePerformanceReview.ForCharter(book, payments, "old", day * 7);
        Check(next.Reserved == 1 && next.Overdue == 0, "uncertain physical reservation is distinct from unpaid overdue");
        Reject(() => CharterOffers.Propose(book, "during-escrow", "p", "b", true, new(), 10, day * 7, observations, payments), "prepared physical delivery freezes proposals even to another party");
        payments.Commit("first", new[] { new TributeLine("stack1", 1, 150) }, day * 7);
        Check(payments.Revision > preparedRevision, "same-operation commit invalidates read caches");
        next = TributePerformanceReview.ForCharter(book, payments, "old", day * 7);
        Check(next.PaidLate == 1 && next.FulfilledValue == 90 && next.OutstandingValue == 0, "late verified delivery fulfills only agreed value, not overpayment");
        payments.Prepare("second", "old", 2, new[] { new TributeLine("stack2", 1, 90) }, day * 10);
        payments.Commit("second", new[] { new TributeLine("stack2", 1, 90) }, day * 11);
        next = TributePerformanceReview.ForCharter(book, payments, "old", day * 11);
        Check(next.PaidWithinGrace == 1 && next.PaidLate == 1 && next.Due == 2, "inclusive last grace tick counts as within grace");
        payments.Prepare("third", "old", 3, new[] { new TributeLine("stack3", 1, 90) }, day * 15);
        next = TributePerformanceReview.ForCharter(book, payments, "old", day * 17);
        Check(next.Reserved == 1 && next.Overdue == 0 && next.Due == 3, "reserved period stays separate after grace expires");
        payments.CancelAfterVerifiedReturn("third", true, day * 17);
        next = TributePerformanceReview.ForCharter(book, payments, "old", day * 17);
        Check(next.Overdue == 1 && next.Reserved == 0, "verified full return reopens the same actual obligation");
        book.ConsentToRelease("old", true, day * 20, observations); book.ConsentToRelease("old", false, day * 20, observations);
        string beforeReview = payments.Export(), releasedBook = book.Export();
        next = TributePerformanceReview.ForPair(book, payments, "a", "p", day * 100);
        Check(next.Due == 4 && next.Overdue == 2 && next.OutstandingValue == 180 && next.FulfilledValue == 180,
            "released unsynchronized account has no later obligations but keeps two old debts");
        Check(payments.Export() == beforeReview && book.Export() == releasedBook, "read-only performance does not synchronize or rewrite either journal");
        payments.SynchronizeClosure(book, "old", day * 100);
        Check(TributePerformanceReview.ForPair(book, payments, "a", "p", day * 100) == next, "explicit closure and read-only closure agree");
        var blocked = CharterOffers.Propose(book, "return-a", "p", "a", true, new(), 100, day * 100, observations, payments);
        Check(blocked.Assessment.Decision == CharterOfferDecision.PriorTributeOverdue && blocked.Book.Charter("return-a").Phase == RecordedCharterPhase.Declined,
            "same counterparty can refuse renewal while previous actual tribute is overdue");
        Check(book.Export() == releasedBook, "declined proposal is staged without altering original history");
        Check(TributePerformanceReview.ForPair(book, payments, "b", "p", day * 100).Accounts == 0, "unrelated counterparty cannot directly use another contract's performance");
        var offerB = CharterOffers.Propose(book, "offer-b", "p", "b", true, new(), 10, day * 100, observations, payments).Book;
        string beforeFinal = offerB.Export();
        var refused = CharterOffers.Confirm(offerB, "offer-b", -1, day * 100 + 1, observations, payments);
        Check(refused.Assessment.Decision == CharterOfferDecision.GoodwillTooLow && refused.Book.Export() == beforeFinal,
            "changed NPC position blocks final signing without rewriting first signature or closing the offer");
        var confirmed = CharterOffers.Confirm(offerB, "offer-b", 10, day * 100 + 1, observations, payments);
        Check(confirmed.Book.Charter("offer-b").Phase == RecordedCharterPhase.Active && offerB.Export() == beforeFinal, "fresh accepted response signs on a copy");
        Check(payments.Account("old").ClosedTick == day * 20 && !payments.IsPaid("old", 3), "new liege does not forgive earlier debt");
        Check(TributeLedger.Restore(payments.Export()).Export() == payments.Export() && !payments.Export().Contains("Revision"), "cache revision is not part of legacy payment JSON");
        Check(TributePerformanceReview.ForPair(CharterBook.Restore(book.Export()), TributeLedger.Restore(payments.Export()), "a", "p", day * 100) == next,
            "performance reconstructed exactly from persisted authoritative evidence");
        Reject(() => TributePerformanceReview.ForPair(book, payments, "a", "p", -1), "negative review time");
        Reject(() => TributePerformanceReview.ForPair(book, payments, "p", "a", day * 100), "party scope cannot be reversed");
        Reject(() => TributePerformanceReview.ForCharter(book, payments, "missing", day * 100), "unknown charter cannot be an empty success");
        Reject(() => CharterOffers.Confirm(offerB, "offer-b", 10, day * 99, observations, payments), "backdated final consent");
        Reject(() => CharterOffers.Confirm(offerB, "offer-b", 10, day * 104, observations, payments), "expired offer cannot acquire new approval");
        var altered = JsonSerializer.Deserialize<TributeLedgerSave>(payments.Export());
        var changed = TributeLedger.Restore(JsonSerializer.Serialize(altered with { Accounts = altered.Accounts.Select(a => a with { Terms = a.Terms with { TributeValue = 99 } }).ToArray() }));
        Reject(() => TributePerformanceReview.ForPair(book, changed, "a", "p", day * 100), "mismatching source terms never become performance evidence");
        // A previously acceptable proposal must be rechecked when an old bill passes grace during its response window.
        var early = CharterBook.Restore(originalBook); var earlyPayments = new TributeLedger(); earlyPayments.Enable(early, "old", 0);
        early.ConsentToRelease("old", true, day * 5, observations); early.ConsentToRelease("old", false, day * 5, observations);
        early = CharterOffers.Propose(early, "renew", "p", "a", true, new(), 50, day * 5, observations, earlyPayments).Book;
        Check(early.Charter("renew").NpcSignedTick.HasValue, "within-grace old bill does not invent a refusal");
        string pendingSnapshot = early.Export();
        var nowOverdue = CharterOffers.Confirm(early, "renew", 50, day * 7, observations, earlyPayments);
        Check(nowOverdue.Assessment.Decision == CharterOfferDecision.PriorTributeOverdue && nowOverdue.Book.Export() == pendingSnapshot,
            "old signature is not irrevocable approval after debt becomes overdue");
        earlyPayments.Prepare("settle", "old", 1, new[] { new TributeLine("real-goods", 1, 90) }, day * 7);
        Reject(() => CharterOffers.Confirm(early, "renew", 50, day * 7, observations, earlyPayments), "final signing cannot run through an uncertain physical handoff");
        earlyPayments.Commit("settle", new[] { new TributeLine("real-goods", 1, 90) }, day * 7);
        var settled = CharterOffers.Confirm(early, "renew", 50, day * 7, observations, earlyPayments);
        Check(settled.Assessment.Accepted && settled.Book.Charter("renew").Phase == RecordedCharterPhase.Active, "verified settlement restores opportunity before offer expiry");
        Check(TributePerformanceReview.ForPair(settled.Book, earlyPayments, "a", "p", day * 7).PaidLate == 1, "renewal retains late-performance history");
        earlyPayments.Enable(settled.Book, "renew", day * 7);
        var combined = TributePerformanceReview.ForPair(settled.Book, earlyPayments, "a", "p", day * 12);
        Check(combined.Accounts == 2 && combined.Due == 2 && combined.PaidLate == 1 && combined.WithinGrace == 1,
            "same-party aggregate includes earlier and current accounts without resetting outcomes");
        Check(TributePerformanceReview.ForCharter(settled.Book, earlyPayments, "renew", day * 12).PaidLate == 0,
            "per-contract view is separate from same-party cumulative history");
        Reject(() => CharterOffers.Assess(settled.Book, earlyPayments, "p", "a", true, new(), -1, day), "poor goodwill cannot waive source-clock validation");
        var longBook = CharterBook.Restore(originalBook); var longPayments = new TributeLedger(); longPayments.Enable(longBook, "old", 0);
        long far = day * 5 * (TributeLedger.MaxPeriods + 1);
        var limited = TributePerformanceReview.ForPair(longBook, longPayments, "a", "p", far);
        Check(!limited.Complete && limited.Due == TributeLedger.MaxPeriods, "limited billing horizon is explicit rather than a false complete performance record");
        Check(CharterOffers.Assess(longBook, longPayments, "p", "a", true, new(), 100, far).Decision == CharterOfferDecision.HistoryIncomplete,
            "unknown remainder of an old history cannot grant new approval");
        return count;
    }
}
