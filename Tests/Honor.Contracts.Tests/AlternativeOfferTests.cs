using System.Text.Json;
using Honor.Contracts.Rules;

internal static class AlternativeOfferTests
{
    public static int Run()
    {
        int count = 0; long day = PoliticalWorld.Day;
        void Check(bool v, string name) { count++; if (!v) { Console.Error.WriteLine("FAIL AlternativeOffer: " + name); throw new Exception(name); } }
        void Reject(CharterBook book, Action action, string name)
        {
            string before = book.Export(); bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected && book.Export() == before, name + ": no mutation");
        }
        var book = new CharterBook(); book.Register(new("player", "native-player", true), 0);
        foreach (string id in new[] { "old", "one", "two", "three", "follower" }) book.Register(new(id, "native-" + id, false), 0);
        var observations = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string liege, string subject = "player") => new(liege, subject, 100, day * 5, day, TributeTransport.SubjectDelivery, .1m);
        book.Offer("old-bond", Terms("old"), 0, day * 3, observations); book.Sign("old-bond", false, 0, observations); book.Sign("old-bond", true, 0, observations);
        book.Offer("child", Terms("player", "follower"), 0, day * 3, observations); book.Sign("child", false, 0, observations); book.Sign("child", true, 0, observations);
        var child = book.Charter("child"); long switchAt = day * 5;
        var money = new TributeLedger(); money.Enable(book, "old-bond", 0); var oldBill = money.Bill("old-bond", 1);
        Check(!book.Export().Contains("ConditionalOffer"), "legacy false field omitted to retain previous JSON/digests");
        Reject(book, () => book.Offer("legacy-second", Terms("one"), 0, day * 3, observations), "ordinary offer API remains strict");
        book.OfferAlternative("candidate-one", Terms("one"), switchAt, day * 3, observations); book.Sign("candidate-one", false, switchAt, observations);
        Check(book.Root("player") == "old" && book.Root("follower") == "old", "candidate and NPC consent do not change existing allegiance or descendants");
        Check(book.Charter("candidate-one").ConditionalOffer && book.Charter("candidate-one").PlayerSignedTick == null, "conditional consent scope explicit");
        book.OfferAlternative("candidate-two", Terms("two"), switchAt, day * 3, observations); book.Sign("candidate-two", false, switchAt, observations);
        Check(book.Charters.Count(c => c.Phase == RecordedCharterPhase.Offered && c.Terms.Subject == "player") == 2, "multiple distinct candidates can coexist without two masters");
        Reject(book, () => book.OfferAlternative("duplicate", Terms("one"), switchAt, day * 3, observations), "same NPC cannot spam duplicate candidate");
        Reject(book, () => book.Sign("candidate-one", true, switchAt + 1, observations), "old active liege blocks final player signature");
        book.ConsentToRelease("old-bond", true, switchAt + 1, observations);
        Reject(book, () => book.Sign("candidate-one", true, switchAt + 1, observations), "unilateral release request cannot authorize new allegiance");
        Check(book.Charter("old-bond").Phase == RecordedCharterPhase.Active && money.Bill("old-bond", 1) == oldBill, "old obligations persist during negotiation");
        book.ConsentToRelease("old-bond", false, switchAt + 2, observations); money.SynchronizeClosure(book, "old-bond", switchAt + 2);
        book.Sign("candidate-one", true, switchAt + 2, observations);
        Check(book.Root("player") == "one" && book.Charters.Count(c => c.Phase == RecordedCharterPhase.Active && c.Terms.Subject == "player") == 1,
            "lawful release plus explicit final consent permits exactly one new liege");
        Check(book.Charter("child") == child && book.Root("follower") == "one", "internal descendant terms/history retained under new recorded root");
        Check(money.Account("old-bond").ClosedTick == switchAt + 2 && money.Account("old-bond").Terms == Terms("old"), "old payment account retained rather than reset for new candidate");
        Check(money.Bill("old-bond", 1) == oldBill && !money.IsPaid("old-bond", 1), "already owed unpaid tribute survives the new liege agreement");
        Reject(book, () => book.Sign("candidate-two", true, switchAt + 2, observations), "another accepted candidate cannot displace active new liege");
        Check(CharterBook.Restore(book.Export()).Export() == book.Export(), "conditional and historical records roundtrip together");
        book.OfferAlternative("unsigned", Terms("three"), switchAt + 2, day * 3, observations);
        Reject(book, () => book.Sign("unsigned", true, switchAt + 2, observations), "player cannot create an NPC signature or sign first on a candidate");
        Reject(book, () => book.OfferAlternative("cycle", Terms("follower"), switchAt + 2, day * 3, observations), "hypothetical replacement still checks descendant cycles");
        Reject(book, () => book.OfferAlternative("wrong-scope", Terms("player", "three"), switchAt + 2, day * 3, observations), "conditional mode cannot create NPC subject contracts");
        var bad = JsonSerializer.Deserialize<CharterBookSave>(book.Export());
        bool rejectedSave = false;
        try { CharterBook.Restore(JsonSerializer.Serialize(bad with { Charters = bad.Charters.Select(c => c.Id == "child" ? c with { ConditionalOffer = true } : c).ToArray() })); }
        catch (ArgumentException) { rejectedSave = true; }
        Check(rejectedSave, "saved conditional flag cannot be used to bypass NPC-only scope");
        var bounded = new CharterBook(); bounded.Register(new("p", "native-player", true), 0);
        for (int i = 0; i < 9; i++) bounded.Register(new("n" + i, "f" + i, false), 0);
        var fresh = bounded.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        for (int i = 0; i < 8; i++) bounded.OfferAlternative("o" + i, Terms("n" + i, "p"), 0, day, fresh);
        Reject(bounded, () => bounded.OfferAlternative("o8", Terms("n8", "p"), 0, day, fresh), "maximum eight live candidates");
        bounded.Decline("o0", 0); bounded.OfferAlternative("o8", Terms("n8", "p"), 0, day, fresh);
        Check(bounded.Charters.Count(c => bounded.Phase(c, 0) == RecordedCharterPhase.Offered) == 8, "declining frees candidate capacity without deleting history");
        Check(CharterBook.Restore(bounded.Export()).Export() == bounded.Export(), "bounded parallel candidates survive persistence");
        var stale = fresh.Select(o => o.Id == "n8" ? o with { NativeOwner = "foreign" } : o).ToArray();
        Reject(bounded, () => bounded.Sign("o8", false, 0, stale), "fresh ownership required for actual candidate response");
        return count;
    }
}
