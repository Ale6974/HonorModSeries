using System.Text.Json;
using Honor.Contracts.Rules;

internal static class CharterProtectionTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Protection aid: " + name); }
        void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("n", "realm", false), 0); book.Register(new("town", "realm", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string liege, string subject) => new(liege, subject, 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m);
        book.Offer("c", Terms("n", "p"), 0, PoliticalWorld.Day, obs); book.Sign("c", true, 0, obs); book.Sign("c", false, 0, obs);
        book.Offer("follower", Terms("p", "town"), 0, PoliticalWorld.Day, obs); book.Sign("follower", true, 0, obs); book.Sign("follower", false, 0, obs);
        string original = book.Export(); var oldFollower = book.Charter("follower"); var empty = new CharterProtection();
        var decision = new NpcRealmDecisionEvidence("realm", "ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var plan = new CharterAidPlan("aid", "c", "realm", "ruler", "map", ["enemy"], ["colonist", "enemy"], 10, new string('A', 64), new string('B', 64), decision);
        Reject(() => empty.Begin(book, plan, decision, true, 10), "a signed charter alone is not a protection agreement");
        Reject(() => empty.Agree(book, "c", decision, 74, true, 1), "separate current ordinary NPC agreement obeys explicit goodwill75 policy");
        Reject(() => empty.Agree(book, "c", decision, 100, false, 1), "reciprocal actual alliance is required");
        Reject(() => empty.Agree(book, "c", decision with { Regime = NpcDecisionRegime.Royal }, 100, true, 1), "ruler is not invented collective noble consent");
        Reject(() => empty.Agree(book, "c", decision with { IndependentAuthority = false }, 100, true, 1), "provider authority cannot be bypassed");
        Reject(() => empty.Agree(book, "follower", decision, 100, true, 1), "NPC subject compact is not this player defense path");
        var agreed = empty.Agree(book, "c", decision, 75, true, 1); agreed.ValidateBook(book);
        Check(empty.Compacts.Count == 0 && agreed.Compacts.Single().Original == book.Charter("c") && book.Export() == original,
            "separate agreement is saved on a copy without rewriting original economic terms, signatures or followers");
        var pending = agreed.Begin(book, plan, decision, true, 10);
        var refusal = agreed.Refuse(book, plan, decision, true, "Honor_Vile", 1, 120, 10);
        Check(!refusal.Pending && refusal.Requests.Single().Outcome == CharterAidOutcome.IntentionallyRefused &&
            refusal.Requests.Single().ReturnedTick == null && refusal.Requests.Single().Generation == null && refusal.Requests.Single().Arrivals == null,
            "fresh deliberate ordinary NPC response has no fabricated dispatch, army, arrival or native return");
        var refusalRow = refusal.Requests.Single();
        Check(CharterProtection.Restore(refusal.Export()).Export() == refusal.Export() && refusalRow.Refusal.Decision == decision &&
            refusalRow.Refusal.Tick == plan.Tick && refusalRow.Refusal.NativePoints == 120 && refusal.Compacts.Single().Original == book.Charter("c"),
            "original decision/capability policy binds the exact signed obligation and defense request through restore");
        Reject(() => agreed.Refuse(book, plan, decision, true, "Honor_Honorable", 1, 120, 10), "unimplemented personality policy cannot create an intentional refusal");
        Reject(() => agreed.Refuse(book, plan, decision, true, "Honor_Vile", 2, 120, 10), "unknown decision policy cannot grant rights");
        Reject(() => agreed.Refuse(book, plan, decision, true, "Honor_Vile", 1, 0, 10), "absence of positive capability cannot be refusal");
        Reject(() => agreed.Refuse(book, plan, decision, true, "Honor_Vile", 1, 100001, 10), "unsupported capability range cannot be refusal");
        Reject(() => agreed.Refuse(book, plan, decision with { Representative = "later-ruler" }, true, "Honor_Vile", 1, 120, 10), "changed current decision cannot be substituted for requested representative");
        Reject(() => pending.Refuse(book, plan, decision, true, "Honor_Vile", 1, 120, 10), "an uncertain begun native dispatch can never be relabeled as deliberate refusal");
        Reject(() => refusal.Returned("aid", true, ["invented"], 11), "refusal has no native dispatch to return later");
        Reject(() => refusal.Refuse(book, plan, decision, true, "Honor_Vile", 1, 120, 10), "one original NPC response is immutable and cannot be duplicated");
        Reject(() => refusal.Begin(book, plan with { Id = "spam", Tick = 11 }, decision, true, 11), "daily source quota includes refused requests");
        var refusalSave = JsonSerializer.Deserialize<CharterProtectionSave>(refusal.Export());
        foreach (var altered in new[] {
            refusalRow with { Refusal = null }, refusalRow with { ReturnedTick = 10 }, refusalRow with { Arrivals = [] },
            refusalRow with { Refusal = refusalRow.Refusal with { PlanProof = new('F', 64) } },
            refusalRow with { Refusal = refusalRow.Refusal with { Tick = 11 } },
            refusalRow with { Refusal = refusalRow.Refusal with { NativePoints = 0 } },
            refusalRow with { Outcome = CharterAidOutcome.ReturnedFalse, ReturnedTick = 10, Arrivals = [] },
            refusalRow with { Refusal = refusalRow.Refusal with { Decision = decision with { Representative = "other" } } } })
            Reject(() => CharterProtection.Restore(JsonSerializer.Serialize(refusalSave with { Requests = [altered] })), "changed refusal pairing/clock/decision/call attribution rejected");
        refusal.ValidateBook(book);
        Check(book.Export() == original && book.Charter("follower") == oldFollower && agreed.Requests.Count == 0,
            "NPC refusal alone never changes allegiance, old signatures, economic terms, debts or descendant charters");
        Check(pending.Pending && pending.Requests.Single().Outcome == CharterAidOutcome.Applying && pending.Requests.Single().ReturnedTick == null,
            "save Applying before native dispatch; no success or refusal is inferred");
        var pendingRestored = CharterProtection.Restore(pending.Export()); pendingRestored.ValidateBook(book);
        Reject(() => pendingRestored.Begin(book, plan with { Id = "replay", Tick = PoliticalWorld.Day + 10 }, decision, true, PoliticalWorld.Day + 10), "unknown applying native call never auto-replays after restore");
        Reject(() => pending.Returned("aid", true, ["colonist"], 11), "existing pawn is never fresh reinforcement evidence");
        Reject(() => pending.Returned("aid", true, ["new", "new"], 11), "duplicate arrival identity rejected");
        var success = pending.Returned("aid", true, ["new-a", "new-b"], 11);
        Check(!success.Pending && success.Requests.Single().Outcome == CharterAidOutcome.ArrivalsObserved && success.Requests.Single().Arrivals.Length == 2,
            "only normal native return plus positive new arrivals records reinforcement arrival, never battle victory");
        Reject(() => success.Returned("aid", true, ["new-c"], 12), "original native receipt cannot be rewritten");
        Reject(() => success.Begin(book, plan with { Id = "spam", Tick = 12 }, decision, true, 12), "source-wide one-day cooldown applies after native return");
        var renewedBook = CharterBook.Restore(original); renewedBook.ConsentToRelease("c", true, 20, obs); renewedBook.ConsentToRelease("c", false, 20, obs);
        renewedBook.Offer("renewed", Terms("n", "p"), 21, PoliticalWorld.Day, obs); renewedBook.Sign("renewed", true, 21, obs); renewedBook.Sign("renewed", false, 21, obs);
        var renewedAgreement = success.Agree(renewedBook, "renewed", decision, 75, true, 22);
        Reject(() => renewedAgreement.Begin(renewedBook, plan with { Id = "renewed-spam", Charter = "renewed", Tick = 23 }, decision, true, 23),
            "ending and renewing a charter never resets the same actual liege's daily request allowance");
        Check(renewedAgreement.Requests.Single().Plan.Charter == "c" && renewedAgreement.Compacts.Count == 2 && renewedBook.Charter("c").Terms == book.Charter("c").Terms,
            "new explicit agreement preserves former duty, old debt basis and original native call history");
        var failed = pending.Returned("aid", false, ["new-a"], 11);
        Check(failed.Requests.Single().Outcome == CharterAidOutcome.ReturnedFalse && failed.Requests.Single().Arrivals.Length == 1,
            "failed native result retains positive observations without declaring success, intentional refusal or dishonor");
        var unverified = pending.Returned("aid", true, [], 11);
        Check(unverified.Requests.Single().Outcome == CharterAidOutcome.ReturnedUnverified, "native true with no positive arrival stays unverified");
        var successor = decision with { Representative = "successor" };
        var future = plan with { Id = "future", Tick = PoliticalWorld.Day + 10, Representative = "successor", Decision = successor };
        var later = success.Begin(book, future, successor, true, future.Tick).Returned("future", true, ["later"], future.Tick + 1);
        Check(later.Compacts.Single().Representative == "ruler" && later.Requests.Last().Plan.Representative == "successor",
            "current representative request does not rewrite historical faction agreement");
        Check(CharterProtection.Restore(later.Export()).Export() == later.Export(), "both real receipts and original supplementary signatures persist exactly");
        Reject(() => agreed.Begin(book, plan with { Representative = "stale" }, decision, true, 10), "stale current representative rejected");
        Reject(() => agreed.Begin(book, plan with { Threats = ["unknown"] }, decision, true, 10), "threat must be in the exact reviewed map roster");
        Reject(() => agreed.Begin(book, plan with { BeforePawns = ["enemy", "enemy"] }, decision, true, 10), "duplicate original identities rejected");
        Reject(() => agreed.Begin(book, plan with { BookProof = "fake" }, decision, true, 10), "book evidence digest required");
        Reject(() => agreed.Begin(book, plan, decision, false, 10), "changed current alliance blocks request");
        Reject(() => agreed.Begin(book, plan, decision, true, 11), "stale request time rejected");
        plan.Threats[0] = "mutated";
        var exposed = pending.Requests.Single(); exposed.Plan.BeforePawns[0] = "mutated";
        Check(pending.Requests.Single().Plan.Threats.Single() == "enemy" && pending.Requests.Single().Plan.BeforePawns[0] == "colonist",
            "caller and UI arrays cannot mutate saved original intent");
        var closed = CharterBook.Restore(original); closed.ConsentToRelease("c", true, PoliticalWorld.Day * 2, obs); closed.ConsentToRelease("c", false, PoliticalWorld.Day * 2, obs);
        later.ValidateBook(closed);
        Reject(() => agreed.Begin(closed, future, successor, true, future.Tick), "an ended contract cannot dispatch new aid but retains historical agreement and calls");
        var endedBeforeRequest = CharterBook.Restore(original); endedBeforeRequest.ConsentToRelease("c", true, 5, obs); endedBeforeRequest.ConsentToRelease("c", false, 5, obs);
        Reject(() => success.ValidateBook(endedBeforeRequest), "stored dispatch cannot be backdated across the actual charter ending");
        var savedBook = JsonSerializer.Deserialize<CharterBookSave>(original);
        var changedBook = CharterBook.Restore(JsonSerializer.Serialize(savedBook with { Charters = savedBook.Charters.Select(c => c.Id == "c" ? c with { Terms = c.Terms with { TributeValue = 200 } } : c).ToArray() }));
        Reject(() => agreed.ValidateBook(changedBook), "original terms cannot be changed underneath supplementary agreement");
        var saved = JsonSerializer.Deserialize<CharterProtectionSave>(success.Export());
        Reject(() => CharterProtection.Restore(JsonSerializer.Serialize(saved with { Requests = [saved.Requests[0], saved.Requests[0]] })), "duplicate stored native calls rejected");
        Reject(() => CharterProtection.Restore(JsonSerializer.Serialize(saved with { Compacts = [] })), "native receipt cannot outlive its actual agreement record");
        Reject(() => CharterProtection.Restore(JsonSerializer.Serialize(saved with { Requests = [saved.Requests[0] with { Arrivals = [] }] })), "positive arrival outcome needs original identities");
        Check(book.Export() == original && book.Charter("follower") == oldFollower && !empty.Pending && empty.Requests.Count == 0,
            "original book, debt basis and descendant charters remain unchanged through all decisions and failed attempts");
        return count;
    }
}
