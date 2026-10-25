using Honor.Contracts.Rules;

internal static class CharterOfferTests
{
    public static int Run()
    {
        int count = 0; long day = PoliticalWorld.Day;
        void Check(bool v, string name) { count++; if (!v) { Console.Error.WriteLine("FAIL CharterOffer: " + name); throw new Exception(name); } }
        void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        var defaults = new CharterOfferDraft();
        Check(defaults.Bind("npc", "player") == new RecordedCharterTerms("npc", "player", 100, day * 5, day, TributeTransport.SubjectDelivery, .1m),
            "default signed schema and economics remain unchanged");
        Check(defaults.RequiredValue == 90 && defaults.Assess(true, 0).DailyValue == 18, "preview matches actual discounted tribute assessment");
        Check(defaults.Assess(true, 0).Accepted && defaults.Assess(false, 50).Accepted, "legacy NPC consent thresholds retained at default terms");
        Check(defaults.Assess(true, -1).Decision == CharterOfferDecision.GoodwillTooLow && defaults.Assess(false, 49).Decision == CharterOfferDecision.GoodwillTooLow,
            "role-specific goodwill boundary");
        Check((defaults with { TributeValue = 99 }).Assess(true, 100).Decision == CharterOfferDecision.ValueTooLow, "NPC liege does not accept inadequate value solely for high goodwill");
        Check((defaults with { TributeValue = 101 }).Assess(false, 100).Decision == CharterOfferDecision.ValueTooHigh, "NPC subject can reject excessive net burden");
        Check((defaults with { GraceDays = 6 }).Assess(true, 100).Decision == CharterOfferDecision.GraceTooLong, "NPC liege rejects grace longer than billing interval");
        Check((defaults with { GraceDays = 30 }).Assess(false, 50).Accepted, "extended grace favors NPC subject without rewriting bill schedules");
        var custom = new CharterOfferDraft(200, 10, 2, 10);
        Check(custom.RequiredValue == 180 && custom.Assess(true, 0).Accepted && custom.Assess(false, 50).Accepted, "equivalent daily value can use a different fixed schedule");
        Check((defaults with { AllowancePercent = 0 }).RequiredValue == 100 && (defaults with { AllowancePercent = 50 }).RequiredValue == 50,
            "transport allowance bounded and visible");
        foreach (var bad in new[] { defaults with { TributeValue = 0 }, defaults with { TributeValue = 10001 }, defaults with { IntervalDays = 0 },
            defaults with { IntervalDays = 61 }, defaults with { GraceDays = -1 }, defaults with { GraceDays = 31 }, defaults with { AllowancePercent = -1 }, defaults with { AllowancePercent = 51 } })
            Reject(() => bad.Bind("npc", "player"), "out-of-range draft never reaches the signed book");
        Reject(() => defaults.Assess(true, 101), "invalid goodwill input cannot fabricate acceptance");
        Reject(() => defaults.Bind("player", "player"), "self contract rejected");
        Reject(() => defaults.Bind(null, "player"), "missing party rejected");
        CharterBook Seed()
        {
            var b = new CharterBook(); b.Register(new("player", "native-player", true), 0); b.Register(new("npc", "native-npc", false), 0); return b;
        }
        TerritoryObservation[] observations = [new("player", "native-player", true, true, true), new("npc", "native-npc", true, false, true)];
        var original = Seed(); string unchanged = original.Export();
        var result = CharterOffers.Propose(original, "custom", "player", "npc", true, custom, 0, day, observations);
        Check(original.Export() == unchanged, "proposal and NPC response do not mutate original book");
        var offer = result.Book.Charter("custom");
        Check(result.Assessment.Accepted && offer.Phase == RecordedCharterPhase.Offered && offer.NpcSignedTick == day && offer.PlayerSignedTick == null,
            "NPC acceptance remains separate from player consent");
        Check(result.Book.Root("player") == "player", "accepted proposal alone cannot change allegiance");
        var ledger = new TributeLedger();
        Reject(() => ledger.Enable(result.Book, "custom", day), "unsigned player cannot activate physical billing");
        result.Book.Sign("custom", true, day * 2, observations);
        Check(result.Book.Charter("custom").Phase == RecordedCharterPhase.Active && result.Book.Root("player") == "npc", "explicit player consent activates exact reviewed terms");
        ledger.Enable(result.Book, "custom", day * 10);
        var bill = ledger.Bill("custom", 1);
        Check(bill.Principal == 200 && bill.RequiredValue == 180 && bill.DueTick == day * 20 && bill.GraceUntil == day * 22,
            "new terms reach actual payment schedule with no pre-enable backbilling");
        Check(ledger.Bills("custom", day * 10).All(b => b.DueTick >= day * 20), "old development periods never become debt");
        Reject(() => ledger.Prepare("short", "custom", 1, [new("goods", 19, 9)], day * 20), "short delivery cannot settle configured value");
        ledger.Prepare("receipt", "custom", 1, [new("goods", 20, 9)], day * 20);
        Reject(() => ledger.Commit("receipt", [new("goods", 19, 9)], day * 20), "actual delivery proof still requires exact goods/count/quote");
        ledger.Commit("receipt", [new("goods", 20, 9)], day * 20);
        Check(ledger.IsPaid("custom", 1), "configured obligation settles only after verified delivery receipt");
        var edited = custom with { TributeValue = 50 };
        Check(edited.TributeValue == 50 && result.Book.Charter("custom").Terms.TributeValue == 200 && ledger.Account("custom").Terms.TributeValue == 200,
            "later editing cannot rewrite signed terms or debt history");
        Check(CharterBook.Restore(result.Book.Export()).Charter("custom").Terms == offer.Terms && TributeLedger.Restore(ledger.Export()).Bill("custom", 1) == bill,
            "book and billing roundtrip retain configured terms");
        result.Book.ConsentToRelease("custom", true, day * 21, observations); result.Book.ConsentToRelease("custom", false, day * 21, observations);
        ledger.SynchronizeClosure(result.Book, "custom", day * 21);
        Check(ledger.IsPaid("custom", 1) && ledger.Account("custom").ClosedTick == day * 21, "release retains exact historical settlement");
        Reject(() => ledger.Bill("custom", 2), "agreed release stops future custom periods");
        var declined = CharterOffers.Propose(original, "declined", "player", "npc", false, custom with { TributeValue = 1000 }, 50, day, observations);
        Check(declined.Book.Charter("declined").Phase == RecordedCharterPhase.Declined && declined.Book.Charter("declined").NpcSignedTick == null,
            "unacceptable terms record rejection without fabricated NPC consent");
        Check(original.Export() == unchanged, "separate rejected proposal also leaves input untouched");
        var stale = observations.Select(o => o.Id == "npc" ? o with { NativeOwner = "conqueror" } : o).ToArray();
        Reject(() => CharterOffers.Propose(original, "stale", "player", "npc", true, defaults, 0, day, stale), "changed ownership cannot authorize a fresh signature");
        Reject(() => CharterOffers.Propose(original, "scope", "npc", "player", true, defaults, 0, day, observations), "role inversion does not impersonate player or NPC");
        Check(original.Export() == unchanged, "failed staged proposals leave no partial book mutations");
        var active = Seed(); var activeResult = CharterOffers.Propose(active, "first", "player", "npc", false, defaults, 50, 0, observations);
        activeResult.Book.Sign("first", true, 0, observations); string activeBefore = activeResult.Book.Export();
        Reject(() => CharterOffers.Propose(activeResult.Book, "second", "player", "npc", false, defaults, 50, day, observations), "new proposal cannot overwrite an active charter");
        Check(activeResult.Book.Export() == activeBefore, "active contract/history survives rejected replacement attempt");
        return count;
    }
}
