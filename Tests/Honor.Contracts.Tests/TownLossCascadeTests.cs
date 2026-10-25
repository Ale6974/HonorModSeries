using System.Text.Json;
using Honor.Contracts.Rules;

internal static class TownLossCascadeTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception("Town loss cascade: " + name); }
        void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        var authority = new NpcCharterAuthority("current", "current");
        (CharterBook Book, TerritoryObservation[] Obs) Seed(bool higher = false)
        {
            var book = new CharterBook(); book.Register(new("p", "native-player", true), 0);
            foreach (string id in new[] { "upper", "a", "b", "g", "extra", "higher" }) book.Register(new(id, "realm", false), 0);
            var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
            RecordedCharterTerms Terms(string parent, string child) => new(parent, child, 137, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .23m);
            if (higher)
            {
                book.Offer("top", Terms("p", "higher"), 0, PoliticalWorld.Day, obs); book.Sign("top", true, 0, obs); book.Sign("top", false, 0, obs);
                book.OfferNpc("root", Terms("higher", "upper"), authority, 0, PoliticalWorld.Day, obs);
                book.SignNpc("root", "higher", "current", 0, obs); book.SignNpc("root", "upper", "current", 0, obs);
            }
            else { book.Offer("root", Terms("p", "upper"), 0, PoliticalWorld.Day, obs); book.Sign("root", true, 0, obs); book.Sign("root", false, 0, obs); }
            foreach (var row in (higher ? new[] { (Id: "a", Parent: "upper"), (Id: "b", Parent: "upper") } :
                new[] { (Id: "a", Parent: "upper"), (Id: "b", Parent: "upper"), (Id: "g", Parent: "a") }))
            { book.OfferNpc(row.Id, Terms(row.Parent, row.Id), authority, 0, PoliticalWorld.Day, obs); book.SignNpc(row.Id, row.Parent, "current", 0, obs); book.SignNpc(row.Id, row.Id, "current", 0, obs); }
            return (book, obs);
        }
        (CharterBook Book, TownSuccessionArchive Archive) Lose(CharterBook book, TownSuccessionArchive archive, string id, long tick, bool hostile = false)
        {
            var c = book.Charter(id); var entity = book.Entity(c.Terms.Subject); string scope = archive.ObservationScope(book, id, "realm");
            var proof = new TownLossProof(id, entity.Id, entity.NativeOwner, "realm", id, tick, tick, book.Export(),
                Kind: hostile ? TownLossKind.HostileNativeOwnershipChange : TownLossKind.Destroyed, CapturingOwner: hostile ? "captor" : null,
                ScopeRoot: scope, ScopeOrdinal: scope == null ? null : archive.NextScopeOrdinal(scope));
            var after = archive.CloseForObservation(proof); return (after, archive.Observe(proof, after));
        }
        var seed = Seed(); var root = Lose(seed.Book, new(), "root", 100);
        Check(!root.Archive.Export().Contains("ScopeRoot") && !root.Archive.Export().Contains("ScopeOrdinal"), "new default fields retain prior archive bytes");
        Check(root.Archive.ObservationScope(root.Book, "a", "realm") == "root" && root.Archive.NextScopeOrdinal("root") == 1, "only original unchanged descendants receive nested observation scope");
        Reject(() => root.Archive.ObservationScope(root.Book, "a", "other"), "a foreign owner cannot borrow original realm loss authority");
        var both = Lose(root.Book, root.Archive, "a", 200);
        Check(both.Archive.Record("a").ScopeRoot == "root" && both.Archive.Record("a").ScopeOrdinal == 1 &&
            both.Book.Charter("a").Phase == RecordedCharterPhase.TerritoryLost && both.Book.Charter("g").Phase == RecordedCharterPhase.Active,
            "real nested upper loss closes its own incoming bond and retains the grandchild");
        Reject(() => root.Book.CloseDestroyedTown("a", "a", "realm", 200, 200, true, true), "old public close API does not waive orphan upper authority");
        var obs = seed.Obs.Select(o => o.Id is "upper" or "a" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var plan = both.Archive.Review("root", both.Book, "cascade", authority, 300, obs);
        Check(plan.Replacements.Count == 2 && plan.Replacements.ContainsKey("b") && plan.Replacements.ContainsKey("g") &&
            plan.Descendants.SequenceEqual(new[] { "b", "g" }) && plan.ScopeRoots.SequenceEqual(new[] { "root", "a" }), "review includes surviving frontier across a broken active chain");
        var completed = both.Archive.Commit(both.Book, plan, 300, obs);
        Check(completed.Book.Charter("a").Phase == RecordedCharterPhase.TerritoryLost && completed.Book.Charter("root").Phase == RecordedCharterPhase.TerritoryLost,
            "no revival, fake release or replacement of lost incoming contracts");
        Check(completed.Book.Charter(plan.Replacements["g"]).Terms == seed.Book.Charter("g").Terms with { Liege = "p" } &&
            completed.Book.Charter("g").Replacement.SkippedRoots.SequenceEqual(new[] { "root" }), "grandchild bypasses only the recorded ordered lost ancestors");
        Check(completed.Book.Root("g") == "p" && completed.Book.Root("b") == "p" && completed.Book.Charter("g").Terms == seed.Book.Charter("g").Terms &&
            completed.Book.Charter("g").NpcAuthority == seed.Book.Charter("g").NpcAuthority, "old terms and historical actors remain intact while survivors reconnect");
        Check(!completed.Archive.HasUnresolved && completed.Archive.Record("root").Resolution.Replacements.Count == 1 &&
            completed.Archive.Record("a").Resolution.Replacements.Count == 1 && completed.Archive.Record("a").Resolution.ScopeCompletion == "root",
            "one atomic commit resolves every scope record with its own direct replacements");
        var restored = TownSuccessionArchive.Restore(completed.Archive.Export()); restored.ValidateBook(CharterBook.Restore(completed.Book.Export()));
        Check(restored.Export() == completed.Archive.Export(), "scope, skipped chain and all receipts round-trip");
        var laterBook = CharterBook.Restore(completed.Book.Export());
        laterBook.CloseDestroyedLeaf(plan.Replacements["g"], "g", "realm", 400, 400, true); restored.ValidateBook(laterBook);
        Check(laterBook.Charter(plan.Replacements["g"]).Phase == RecordedCharterPhase.TerritoryLost, "later loss of a successor does not rewrite prior cascade receipts");
        Reject(() => both.Archive.Review("a", both.Book, "partial", authority, 300, obs), "a nested loss cannot be resolved outside the whole scope");
        Reject(() => both.Archive.Commit(both.Book, plan with { ScopeRoots = new[] { "root" } }, 300, obs), "caller cannot drop a reviewed loss record");
        Reject(() => both.Archive.Commit(both.Book, plan with { After = both.Book.Export() }, 300, obs), "altered after book");
        Reject(() => both.Archive.Review("root", both.Book, "stale", authority, 300, obs.Select(o => o.Id == "g" ? o with { NativeOwner = "foreign" } : o)), "hidden-by-broken-chain survivor still needs exact ownership");
        Reject(() => both.Archive.Review("root", both.Book, "missing", authority, 300, obs.Where(o => o.Id != "g")), "missing surviving grandchild cannot disappear from the review");
        string untouchedBook = both.Book.Export(), untouchedArchive = both.Archive.Export();
        Reject(() => both.Archive.Commit(both.Book, plan, 301, obs), "stale confirmation time");
        Check(both.Book.Export() == untouchedBook && both.Archive.Export() == untouchedArchive, "failed confirmations leave all original state unchanged");
        var leaf = Lose(root.Book, root.Archive, "b", 200);
        Check(leaf.Archive.Record("b").ScopeRoot == "root", "nested leaf loss also gets an actual scoped record");
        var leafObs = seed.Obs.Select(o => o.Id is "upper" or "b" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var leafPlan = leaf.Archive.Review("root", leaf.Book, "leaf", authority, 300, leafObs);
        var leafDone = leaf.Archive.Commit(leaf.Book, leafPlan, 300, leafObs);
        Check(leafDone.Book.Charter("g") == seed.Book.Charter("g") && leafDone.Book.Root("g") == "p" &&
            leafDone.Archive.Record("b").Resolution.Replacements.Count == 0, "lost leaf resolves without fabricated replacements or rewriting living internal bonds");
        var deepLeaf = Lose(root.Book, root.Archive, "g", 200);
        var deepObs = seed.Obs.Select(o => o.Id is "upper" or "g" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var deepDone = deepLeaf.Archive.Commit(deepLeaf.Book, deepLeaf.Archive.Review("root", deepLeaf.Book, "deep", authority, 300, deepObs), 300, deepObs);
        Check(deepDone.Archive.Record("g").Resolution.Parent == "a" && deepDone.Book.Root("a") == "p", "nearest surviving internal parent is retained instead of skipping living authority");
        var highSeed = Seed(true); var high = Lose(highSeed.Book, new(), "root", 100); high = Lose(high.Book, high.Archive, "a", 200);
        var highObs = highSeed.Obs.Select(o => o.Id is "upper" or "a" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var highPlan = high.Archive.Review("root", high.Book, "higher", authority, 300, highObs);
        var highDone = high.Archive.Commit(high.Book, highPlan, 300, highObs);
        Check(highDone.Book.Charter(highPlan.Replacements["b"]).Terms.Liege == "higher" && highDone.Book.Charter(highPlan.Replacements["b"]).NpcAuthority == authority &&
            highDone.Book.Charter("top") == highSeed.Book.Charter("top"), "surviving NPC higher liege receives fresh NPC consent without rewriting its own incoming contract");
        var all = Lose(both.Book, both.Archive, "g", 300); all = Lose(all.Book, all.Archive, "b", 400);
        var noTownObs = seed.Obs.Select(o => o.Id is "upper" or "a" or "b" or "g" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var empty = all.Archive.Review("root", all.Book, "empty", authority, 500, noTownObs);
        var emptyDone = all.Archive.Commit(all.Book, empty, 500, noTownObs);
        Check(empty.Replacements.Count == 0 && empty.Descendants.Length == 0 && !emptyDone.Archive.HasUnresolved &&
            emptyDone.Book.Charters.All(c => c.Phase == RecordedCharterPhase.TerritoryLost), "all actually lost descendants resolve without minting contracts or leaving an eternal freeze");
        var hostile = Lose(root.Book, root.Archive, "a", 200, true);
        var hostileObs = seed.Obs.Select(o => o.Id == "upper" ? o with { Exists = false, Eligible = false } : o.Id == "a" ? o with { NativeOwner = "captor", Eligible = false } : o).ToArray();
        var hostileDone = hostile.Archive.Commit(hostile.Book, hostile.Archive.Review("root", hostile.Book, "mixed", authority, 300, hostileObs), 300, hostileObs);
        Check(hostileDone.Archive.Record("a").CapturingOwner == "captor" && hostileDone.Archive.Record("root").Kind == TownLossKind.Destroyed &&
            hostileDone.Book.Root("g") == "p", "destruction and hostile native losses retain distinct proofs in the same reviewed scope");
        var reverseSeed = Seed(); var reverse = Lose(reverseSeed.Book, new(), "a", 100);
        Reject(() => reverse.Archive.ObservationScope(reverse.Book, "root", "realm"), "ancestor-after-descendant loss does not invent scope merger authority");
        var malformed = new TownLossProof("a", "a", "realm", "realm", "a", 200, 200, root.Book.Export(), ScopeRoot: "root", ScopeOrdinal: 2);
        Reject(() => root.Archive.Observe(malformed, root.Archive.CloseForObservation(malformed)), "missing sequence member");
        Reject(() => root.Archive.CloseForObservation(malformed with { ScopeOrdinal = 1, ScopeRoot = "a" }), "self scope");
        Reject(() => root.Archive.CloseForObservation(malformed with { ScopeOrdinal = 1, OwnerAtLoss = "foreign" }), "wrong actual realm");
        Reject(() => root.Archive.CloseForObservation(malformed with { ScopeOrdinal = 1, Root = "b", Territory = "g" }), "wrong native territory binding");
        var saved = JsonSerializer.Deserialize<TownLossSave>(completed.Archive.Export());
        void Bad(TownLossSave value, string name) => Reject(() => { var a = TownSuccessionArchive.Restore(JsonSerializer.Serialize(value)); a.ValidateBook(completed.Book); }, name);
        Bad(saved with { Records = saved.Records.Where(r => r.Root != "a").ToArray() }, "no missing native intermediate loss proof");
        Bad(saved with { Records = saved.Records.Select(r => r.Root == "a" ? r with { Resolution = null } : r).ToArray() }, "no partial archived completion");
        Bad(saved with { Records = saved.Records.Select(r => r.Root == "a" ? r with { ScopeOrdinal = 2 } : r).ToArray() }, "sequence altered after save");
        Bad(saved with { Records = saved.Records.Select(r => r.Root == "a" ? r with { ScopeRoot = "b" } : r).ToArray() }, "scope substituted after save");
        var bookSave = JsonSerializer.Deserialize<CharterBookSave>(completed.Book.Export());
        Reject(() => CharterBook.Restore(JsonSerializer.Serialize(bookSave with { Charters = bookSave.Charters.Select(c => c.Id == "g" ? c with { Replacement = c.Replacement with { SkippedRoots = null } } : c).ToArray() })), "skipped ancestor chain is required by the saved successor's actual parent");
        Reject(() => CharterBook.Restore(JsonSerializer.Serialize(bookSave with { Charters = bookSave.Charters.Select(c => c.Id == "g" ? c with { Replacement = c.Replacement with { SkippedRoots = new[] { "a", "root" } } } : c).ToArray() })), "cannot repeat the immediate lost root in skipped ancestors");
        var full = CharterBook.Restore(both.Book.Export());
        for (int i = full.Charters.Count; i < CharterBook.MaxCharters - 1; i++)
        {
            full.Offer("capacity-" + i, new("p", "extra", 1, PoliticalWorld.Day, PoliticalWorld.Day, TributeTransport.SubjectDelivery, 0), 250, PoliticalWorld.Day, obs);
            full.Decline("capacity-" + i, 250);
        }
        string fullBefore = full.Export();
        Reject(() => both.Archive.Review("root", full, "full", authority, 300, obs), "failure in a later loss group cannot partially reconnect the earlier group");
        Check(full.Export() == fullBefore && both.Archive.Export() == untouchedArchive, "whole-scope capacity failure retains both book and archive");
        return count;
    }
}
