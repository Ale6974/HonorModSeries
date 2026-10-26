using Honor.Contracts.Rules;

internal static class CharterReleaseTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Charter release: " + name); }
        void Reject(Action action, string name) { bool bad = false; try { action(); } catch (ArgumentException) { bad = true; } Check(bad, name); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("n", "realm", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        var terms = new RecordedCharterTerms("n", "p", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0);
        book.Offer("old", terms, 0, PoliticalWorld.Day, obs); book.Sign("old", true, 0, obs); book.Sign("old", false, 0, obs);
        var paid = new TributeLedger(); paid.Enable(book, "old", 0); var transfers = new TerritoryTransferLedger();
        var decision = new NpcRealmDecisionEvidence("realm", "chief", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        string original = book.Export(), money = paid.Export(), physical = transfers.Export(); long tick = PoliticalWorld.Day * 2;
        CharterReleaseReview Review(NpcRealmDecisionEvidence d = null) => CharterReleases.Review(book, paid, transfers, "old", d ?? decision, tick, obs);
        var plan = Review(); var after = CharterBook.Restore(plan.After); var closed = TributeLedger.Restore(plan.PaymentsAfter);
        Check(book.Export() == original && paid.Export() == money && transfers.Export() == physical, "review cannot leave a partial signature or close actual billing");
        Check(after.Charter("old").Phase == RecordedCharterPhase.Released && after.Charter("old").Terms == terms &&
            after.Charter("old").PlayerSignedTick == 0 && after.Charter("old").NpcSignedTick == 0, "actual mutual release preserves original economics and signatures");
        Check(closed.Account("old").ClosedTick == tick && closed.Bills("old", tick).Count() == 2, "all existing unpaid obligations survive staged closure");
        Reject(() => closed.Bill("old", 3), "no future tribute can start beyond agreed closure");
        Check(plan == Review(), "same current review has stable equality for final confirmation");
        Check(plan != Review(decision with { Representative = "successor" }), "legitimate successor requires fresh player confirmation");
        foreach (var bad in new[] { decision with { Regime = NpcDecisionRegime.Union }, decision with { Regime = NpcDecisionRegime.Royal },
            decision with { DecisionCapable = false }, decision with { UniqueOffice = false }, decision with { IndependentAuthority = false },
            decision with { Willing = false }, decision with { Registered = false }, decision with { Realm = "foreign" } })
            Reject(() => Review(bad), "unsupported or changed authority cannot sign a release");
        var half = CharterBook.Restore(original); half.ConsentToRelease("old", true, 1, obs);
        var completed = CharterReleases.Review(half, paid, transfers, "old", decision, tick, obs);
        Check(CharterBook.Restore(completed.After).Charter("old").PlayerReleaseTick == 1, "legacy pending player request keeps its original timestamp");
        Reject(() => CharterReleases.Review(after, closed, transfers, "old", decision, tick, obs), "completed release is never signed twice");
        Reject(() => CharterReleases.Review(book, paid, transfers, "old", decision, tick, obs.Select(o => o.Id == "n" ? o with { Exists = false } : o)), "missing actual counterparty prevents release");
        var owing = TributeLedger.Restore(money); owing.Prepare("delivery", "old", 1, [new("silver", 100, 1)], PoliticalWorld.Day);
        Reject(() => CharterReleases.Review(book, owing, transfers, "old", decision, tick, obs), "reserved actual delivery prevents any release signature");
        var mismatched = TributeLedger.Restore(money.Replace("\"TributeValue\":100", "\"TributeValue\":101"));
        string mismatchBefore = mismatched.Export();
        Reject(() => CharterReleases.Review(book, mismatched, transfers, "old", decision, tick, obs), "invalid billing terms fail before actual release signatures are written");
        Check(book.Export() == original && mismatched.Export() == mismatchBefore, "closure failure preserves both original models without half-commit");
        var family = new CharterBook(); family.Register(new("p", "player", true), 0); family.Register(new("a", "realm", false), 0); family.Register(new("b", "realm", false), 0);
        var seen = family.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        family.Offer("upper", terms with { Liege = "p", Subject = "a" }, 0, PoliticalWorld.Day, seen);
        family.Sign("upper", true, 0, seen); family.Sign("upper", false, 0, seen);
        var leaf = CharterReleases.Review(family, new(), new(), "upper", decision, tick, seen);
        Check(CharterBook.Restore(leaf.After).Charter("upper").Phase == RecordedCharterPhase.Released, "ordinary player-liege NPC leaf has the same reviewed release path");
        family.OfferNpc("lower", terms with { Liege = "a", Subject = "b" }, new("historic", "historic"), 0, PoliticalWorld.Day, seen);
        family.SignNpc("lower", "a", "historic", 0, seen); family.SignNpc("lower", "b", "historic", 0, seen);
        Reject(() => CharterReleases.Review(family, new(), new(), "upper", decision, tick, seen), "NPC subtree needs its separate bilateral exit and migration coordinator");
        Reject(() => CharterReleases.Review(family, new(), new(), "lower", decision, tick, seen), "player confirmation cannot replace two current NPC decisions");
        Check(book.Export() == original && paid.Export() == money, "all rejected reviews preserve original ledgers");
        return count;
    }
}
