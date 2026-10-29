using System.Text.Json;
using Honor.Contracts.Rules;

internal static class TownLossMergeTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool v, string name) { count++; if (!v) throw new Exception("Town loss merge: " + name); }
        void Reject(Action action, string name) { bool fail = false; try { action(); } catch (ArgumentException) { fail = true; } Check(fail, name); }
        var authority = new NpcCharterAuthority("current", "current");
        (CharterBook Book, TerritoryObservation[] Obs) Seed()
        {
            var book = new CharterBook(); book.Register(new("p", "player", true), 0);
            foreach (string id in new[] { "upper", "left", "right", "g", "h", "extra" }) book.Register(new(id, "realm", false), 0);
            var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
            RecordedCharterTerms Terms(string a, string b) => new(a, b, 137, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .23m);
            book.Offer("upper", Terms("p", "upper"), 0, PoliticalWorld.Day, obs); book.Sign("upper", true, 0, obs); book.Sign("upper", false, 0, obs);
            foreach (var row in new[] { (Id: "z-first", Parent: "upper", Child: "left"), (Id: "a-second", Parent: "upper", Child: "right"),
                (Id: "g", Parent: "left", Child: "g"), (Id: "h", Parent: "right", Child: "h") })
            { book.OfferNpc(row.Id, Terms(row.Parent, row.Child), authority, 0, PoliticalWorld.Day, obs); book.SignNpc(row.Id, row.Parent, "current", 0, obs); book.SignNpc(row.Id, row.Child, "current", 0, obs); }
            return (book, obs);
        }
        (CharterBook Book, TownSuccessionArchive Archive) Lose(CharterBook book, TownSuccessionArchive archive, string id, long tick, bool hostile = false)
        {
            var c = book.Charter(id); var entity = book.Entity(c.Terms.Subject); var prior = archive.ObservationMerges(book, id, "realm");
            string scope = prior == null ? archive.ObservationScope(book, id, "realm") : null;
            var proof = new TownLossProof(id, entity.Id, entity.NativeOwner, "realm", id, tick, tick, book.Export(),
                Kind: hostile ? TownLossKind.HostileNativeOwnershipChange : TownLossKind.Destroyed, CapturingOwner: hostile ? "captor" : null,
                ScopeRoot: scope, ScopeOrdinal: scope == null ? null : archive.NextScopeOrdinal(scope), PriorScopes: prior);
            var after = archive.CloseForObservation(proof); return (after, archive.Observe(proof, after));
        }
        var seed = Seed(); var first = Lose(seed.Book, new(), "z-first", 100); string originalProof = JsonSerializer.Serialize(first.Archive.Record("z-first"));
        Check(!first.Archive.Export().Contains("PriorScopes"), "old default-null merge field omitted");
        Reject(() => first.Archive.ObservationScope(first.Book, "upper", "realm"), "old observation API still cannot waive higher-loss guard");
        Check(first.Archive.ObservationMerges(first.Book, "upper", "realm").SequenceEqual(new[] { "z-first" }), "dedicated reverse path retains original scope ID");
        Reject(() => first.Archive.ObservationMerges(first.Book, "upper", "foreign"), "foreign realm cannot inherit prior observation authority");
        var higher = Lose(first.Book, first.Archive, "upper", 200);
        Check(higher.Archive.Record("upper").PriorScopes.SequenceEqual(new[] { "z-first" }) && JsonSerializer.Serialize(higher.Archive.Record("z-first")) == originalProof,
            "new higher event adopts the old unresolved scope without rewriting its Before, ID, actor or kind");
        Check(higher.Archive.IsReviewRoot("upper") && !higher.Archive.IsReviewRoot("z-first") && higher.Archive.NextScopeOrdinal("upper") == 1,
            "one current review head with a separate local append sequence");
        Reject(() => higher.Archive.ScopeLosses("z-first"), "old scope cannot be separately resolved after adoption");
        Reject(() => higher.Archive.NextScopeOrdinal("z-first"), "no new nested observations may append to a superseded scope");
        var obs = seed.Obs.Select(o => o.Id is "upper" or "left" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var plan = higher.Archive.Review("upper", higher.Book, "merge", authority, 300, obs);
        Check(plan.Descendants.SequenceEqual(new[] { "a-second", "g", "h" }) && plan.Replacements.Count == 2 && plan.Replacements.ContainsKey("g"),
            "original graph recovers deeper survivors hidden by earlier loss");
        var done = higher.Archive.Commit(higher.Book, plan, 300, obs);
        Check(done.Book.Root("g") == "p" && done.Book.Root("h") == "p" && done.Book.Charter("h") == seed.Book.Charter("h"),
            "all surviving frontier contracts reconnect while living internal parent is retained");
        Check(done.Book.Charter("g").Replacement.SkippedRoots.SequenceEqual(new[] { "upper" }) && done.Book.Charter("z-first").Phase == RecordedCharterPhase.TerritoryLost,
            "only recorded higher loss is skipped, never revived");
        Check(done.Archive.Record("z-first").Before == first.Archive.Record("z-first").Before && done.Archive.Record("z-first").Resolution.ScopeCompletion == "upper" &&
            !done.Archive.HasUnresolved, "old loss proof remains immutable and gets a separately attributed joint receipt");
        var reload = TownSuccessionArchive.Restore(done.Archive.Export()); reload.ValidateBook(CharterBook.Restore(done.Book.Export()));
        Check(reload.Export() == done.Archive.Export(), "merge references, original proofs and receipts survive reload");
        // Both independent lower scopes at the same tick: the earlier actual snapshot wins, never alphabetical ID order.
        var siblings = Lose(first.Book, first.Archive, "a-second", 100);
        var priorScopes = siblings.Archive.ObservationMerges(siblings.Book, "upper", "realm");
        Check(priorScopes.SequenceEqual(new[] { "z-first", "a-second" }), "same-tick common origin follows actual graph snapshots");
        var merged = Lose(siblings.Book, siblings.Archive, "upper", 200, true);
        var mixedObs = seed.Obs.Select(o => o.Id is "left" or "right" ? o with { Exists = false, Eligible = false } : o.Id == "upper" ? o with { NativeOwner = "captor", Eligible = false } : o).ToArray();
        var mergedPlan = merged.Archive.Review("upper", merged.Book, "siblings", authority, 300, mixedObs);
        var mergedDone = merged.Archive.Commit(merged.Book, mergedPlan, 300, mixedObs);
        Check(mergedDone.Book.Root("g") == "p" && mergedDone.Book.Root("h") == "p" && mergedDone.Archive.ScopeLosses("upper").Length == 3 &&
            mergedDone.Archive.Record("upper").Kind == TownLossKind.HostileNativeOwnershipChange, "multiple prior scopes and mixed native loss kinds resolve together");
        var appended = Lose(higher.Book, higher.Archive, "g", 250);
        Check(appended.Archive.Record("g").ScopeRoot == "upper" && appended.Archive.Record("g").ScopeOrdinal == 1, "later descendant appends to the new head, preserving old local ordinals");
        var appendObs = obs.Select(o => o.Id == "g" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var appendDone = appended.Archive.Commit(appended.Book, appended.Archive.Review("upper", appended.Book, "append", authority, 300, appendObs), 300, appendObs);
        Check(!appendDone.Archive.HasUnresolved && appendDone.Archive.Record("z-first").Resolution.Replacements.Count == 0 && appendDone.Book.Root("h") == "p",
            "later genuine loss removes only its own survivor, not living sibling descendants");
        var earlierNested = Lose(first.Book, first.Archive, "g", 150); string oldNested = JsonSerializer.Serialize(earlierNested.Archive.Record("g"));
        var adoptedNested = Lose(earlierNested.Book, earlierNested.Archive, "upper", 200);
        var nestedObs = obs.Select(o => o.Id == "g" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var nestedDone = adoptedNested.Archive.Commit(adoptedNested.Book,
            adoptedNested.Archive.Review("upper", adoptedNested.Book, "nested-prior", authority, 300, nestedObs), 300, nestedObs);
        Check(JsonSerializer.Serialize(adoptedNested.Archive.Record("g")) == oldNested && nestedDone.Archive.Record("g").ScopeRoot == "z-first" &&
            nestedDone.Archive.Record("g").ScopeOrdinal == 1 && nestedDone.Archive.Record("g").Resolution.ScopeCompletion == "upper",
            "adopted scope retains its own earlier nested sequence and proof unchanged");
        Check(nestedDone.Book.Root("h") == "p" && !nestedDone.Archive.HasUnresolved, "nested prior records participate in one verified completion");
        var beforeHigherObs = seed.Obs.Select(o => o.Id == "left" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var lowerDone = first.Archive.Commit(first.Book, first.Archive.Review("z-first", first.Book, "prior-done", authority, 150, beforeHigherObs), 150, beforeHigherObs);
        Check(lowerDone.Archive.ObservationMerges(lowerDone.Book, "upper", "realm") == null, "completed lower scope is never selected for adoption");
        var afterCompleted = Lose(lowerDone.Book, lowerDone.Archive, "upper", 200);
        var finalAfterCompleted = afterCompleted.Archive.Commit(afterCompleted.Book,
            afterCompleted.Archive.Review("upper", afterCompleted.Book, "new-higher", authority, 300, obs), 300, obs);
        Check(finalAfterCompleted.Archive.Record("z-first").Resolution == lowerDone.Archive.Record("z-first").Resolution ||
            JsonSerializer.Serialize(finalAfterCompleted.Archive.Record("z-first")) == JsonSerializer.Serialize(lowerDone.Archive.Record("z-first")),
            "later ordinary higher loss preserves an already completed receipt and its original successor history");
        var proof = new TownLossProof("upper", "upper", "realm", "realm", "upper", 200, 200, siblings.Book.Export(), PriorScopes: priorScopes);
        string beforeBook = siblings.Book.Export(), beforeArchive = siblings.Archive.Export();
        Reject(() => siblings.Archive.CloseForObservation(proof with { PriorScopes = new[] { "a-second", "z-first" } }), "a later snapshot cannot masquerade as pre-loss origin");
        Reject(() => siblings.Archive.CloseForObservation(proof with { PriorScopes = new[] { "z-first" } }), "cannot omit another already lost descendant scope");
        Reject(() => siblings.Archive.CloseForObservation(proof with { PriorScopes = new[] { "z-first", "z-first" } }), "duplicate prior scope");
        Reject(() => siblings.Archive.CloseForObservation(proof with { PriorScopes = new[] { "upper" } }), "self adoption");
        Reject(() => siblings.Archive.CloseForObservation(proof with { ScopeRoot = "z-first", ScopeOrdinal = 1 }), "cannot combine nested append and reverse merge authority");
        Check(siblings.Book.Export() == beforeBook && siblings.Archive.Export() == beforeArchive, "failed merge preflights leave both book and prior archive unchanged");
        Reject(() => merged.Archive.Commit(merged.Book, mergedPlan with { ScopeRoots = new[] { "upper", "z-first" } }, 300, mixedObs), "confirmation cannot drop one prior scope");
        Reject(() => merged.Archive.Review("upper", merged.Book, "wrong-owner", authority, 300, mixedObs.Select(o => o.Id == "h" ? o with { NativeOwner = "foreign" } : o)), "deeper sibling owner is still required");
        var saved = JsonSerializer.Deserialize<TownLossSave>(mergedDone.Archive.Export());
        void Bad(TownLossSave value, string name) => Reject(() => { var a = TownSuccessionArchive.Restore(JsonSerializer.Serialize(value)); a.ValidateBook(mergedDone.Book); }, name);
        Bad(saved with { Records = saved.Records.Where(r => r.Root != "z-first").ToArray() }, "missing adopted original proof");
        Bad(saved with { Records = saved.Records.Select(r => r.Root == "upper" ? r with { PriorScopes = null } : r).ToArray() }, "cannot erase merge attribution");
        Bad(saved with { Records = saved.Records.Select(r => r.Root == "z-first" ? r with { PriorScopes = new[] { "upper" } } : r).ToArray() }, "cyclic adoption rejected before recursive source traversal");
        Bad(saved with { Records = saved.Records.Select(r => r.Root == "z-first" ? r with { Resolution = null } : r).ToArray() }, "no partial merged completion");
        Bad(saved with { Records = saved.Records.Select(r => r.Root == "a-second" ? r with { ScopeRoot = "z-first", ScopeOrdinal = 1 } : r).ToArray() }, "independent scope cannot be retroactively rewritten as nested");
        var completedPriorProof = new TownLossProof("upper", "upper", "realm", "realm", "upper", 400, 400, done.Book.Export(), PriorScopes: new[] { "z-first" });
        Reject(() => done.Archive.Observe(completedPriorProof, done.Book), "completed original receipts cannot be adopted and overwritten later");
        var capturedLower = Lose(seed.Book, new(), "z-first", 100, true);
        var capturedMerged = Lose(capturedLower.Book, capturedLower.Archive, "upper", 200, true);
        var oldLowerLoss = capturedLower.Archive.Record("z-first");
        var oldLowerReturn = new TownOwnershipReturn("z-first", "left", "captor", "realm", TownOwnershipReturns.LossDigest(oldLowerLoss), 150, 151);
        var beforeMergeReturns = new TownOwnershipReturns().Observe(oldLowerReturn, capturedLower.Archive, capturedLower.Book);
        beforeMergeReturns.ValidatePair(capturedMerged.Archive, capturedMerged.Book);
        var obsoleteObs = seed.Obs.Select(o => o.Id == "upper" ? o with { NativeOwner = "captor", Eligible = false } : o).ToArray();
        var obsoletePlan = capturedMerged.Archive.Review("upper", capturedMerged.Book, "old-lower-fact", new("p", "current"), 302, obsoleteObs);
        var obsoleteDecision = new NpcRealmDecisionEvidence("realm", "current", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var obsoleteReview = new HistoricalTownReturnReview(new(obsoletePlan, capturedMerged.Archive.Export(), obsoleteObs, "p", "player", "player", obsoleteDecision, null),
            beforeMergeReturns.Export(), null, null, new Dictionary<string, string> { ["upper"] = "captor", ["z-first"] = "realm" },
            new Dictionary<string, string> { ["z-first"] = TownOwnershipReturns.ReceiptDigest(oldLowerReturn) });
        var obsoleteDone = new HistoricalTownReturnReconciliations().Execute(obsoleteReview, beforeMergeReturns, capturedMerged.Archive, capturedMerged.Book);
        HistoricalTownReturnReconciliations.Restore(obsoleteDone.History.Export()).ValidatePair(TownSuccessionArchive.Restore(obsoleteDone.Archive.Export()), CharterBook.Restore(obsoleteDone.Book.Export()), beforeMergeReturns);
        Check(obsoleteDone.Book.Root("g") == "p" && obsoleteDone.Archive.Record("upper").Resolution.HistoricalReturnProof != null, "pre-merge lower return retains its original fact while new whole-scope consent reconnects survivors");
        Reject(() => beforeMergeReturns.RequireActive("z-first", capturedMerged.Archive, capturedMerged.Book, "left", "realm", 302), "old absorbed-head permission remains unavailable without separate reconciliation");
        var returnArchive = new TownOwnershipReturns();
        var returnProofs = new Dictionary<string, string>();
        foreach (string id in new[] { "upper", "z-first" })
        {
            var loss = capturedMerged.Archive.Record(id);
            var r = new TownOwnershipReturn(id, loss.Territory, "captor", "realm", TownOwnershipReturns.LossDigest(loss), 300, 301)
                { PeerScope = "upper", ScopeSnapshot = capturedMerged.Archive.Export() };
            returnArchive = returnArchive.Observe(r, capturedMerged.Archive, capturedMerged.Book);
            returnProofs.Add(id, returnArchive.RequireActive(id, capturedMerged.Archive, capturedMerged.Book, loss.Territory, "realm", 302));
        }
        var returnedPlan = capturedMerged.Archive.Review("upper", capturedMerged.Book, "returned-merged", authority, 302, seed.Obs);
        var returnedMerged = returnArchive.CommitSuccessionGroup(capturedMerged.Archive, capturedMerged.Book, returnedPlan, 302, seed.Obs,
            returnProofs, new Dictionary<string, string> { ["upper"] = "realm", ["z-first"] = "realm" }, null, null);
        Check(returnedMerged.Archive.Records.All(p => p.Resolution.OwnershipReturnGroup == "upper" && p.Resolution.OwnershipReturnProof != null), "merged head and absorbed prior head pair independent returns with exact containing scope");
        Check(returnedMerged.Book.Charter("upper").Phase == RecordedCharterPhase.TerritoryLost && returnedMerged.Book.Charter("z-first").Phase == RecordedCharterPhase.TerritoryLost && returnedMerged.Book.Root("g") == "p", "merged returns preserve ended rights and reconnect original surviving grandchildren");
        TownOwnershipReturns.Restore(returnArchive.Export()).ValidatePair(TownSuccessionArchive.Restore(returnedMerged.Archive.Export()), CharterBook.Restore(returnedMerged.Book.Export()));
        Reject(() => new TownOwnershipReturns().ValidatePair(returnedMerged.Archive, returnedMerged.Book), "merged returned scope needs all original positive receipts");
        Reject(() => returnArchive.CommitSuccessionGroup(capturedMerged.Archive, capturedMerged.Book, returnedPlan, 302, seed.Obs,
            new Dictionary<string, string> { ["upper"] = returnProofs["upper"] }, new Dictionary<string, string> { ["upper"] = "realm", ["z-first"] = "realm" }, null, null), "no returned absorbed town is silently omitted");
        return count;
    }
}
