using Honor.Contracts.Rules;

internal static class FollowerTransfersTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        var book = new CharterBook(); book.Register(new("player", "native-player", true), 0);
        foreach (var id in new[] { "a", "b", "c" }) book.Register(new(id, id == "c" ? "other" : "source", false), 0);
        TerritoryObservation[] Observations() => book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        foreach (var id in new[] { "a", "b", "c" })
        {
            book.Offer("bond-" + id, new("player", id, 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m), 0, PoliticalWorld.Day, Observations());
            book.Sign("bond-" + id, true, 0, Observations()); book.Sign("bond-" + id, false, 0, Observations());
        }
        var ledger = new TerritoryTransferLedger();
        FollowerCandidate[] Candidates() => [new("bond-a", new("a", "source", 3)), new("bond-b", new("b", "source", 3)), new("bond-c", new("c", "other", 2))];
        void Reject(FollowerCandidate[] candidates, string name, string target = "administration", string administration = null)
        {
            string before = ledger.Export(), beforeBook = book.Export(); bool rejected = false;
            try { FollowerTransfers.Review(book, ledger, candidates, target, 10, administration); } catch (ArgumentException) { rejected = true; }
            Check(rejected && ledger.Export() == before && book.Export() == beforeBook, name + ": all preflight; no partial execution");
        }
        var before = ledger.Export(); var plan = FollowerTransfers.Review(book, ledger, Candidates(), "administration", 10, "administration");
        Check(plan.Entries.Count == 3 && plan.Moves.SequenceEqual(new[] { "bond-a", "bond-b", "bond-c" }), "all active NPC subjects selected deterministically");
        Check(ledger.Export() == before && ledger.Transfers.Count == 0, "successful preflight creates no real execution proof");
        Check(FollowerTransfers.Review(book, ledger, Candidates().Reverse(), "administration", 10, "administration").Stamp == plan.Stamp, "enumeration order cannot change review");
        Check(FollowerTransfers.Review(CharterBook.Restore(book.Export()), TerritoryTransferLedger.Restore(ledger.Export()), Candidates(), "administration", 10, "administration").Stamp == plan.Stamp, "same persisted inputs yield same plan");
        Reject(Candidates()[..2], "cannot omit one vassal");
        Reject([Candidates()[0], Candidates()[0], Candidates()[2]], "duplicate and missing subjects");
        Reject([.. Candidates(), new("extra", new("extra", "source", 3))], "unrelated contract cannot enter group");
        Reject([null], "missing candidate");
        Reject(Candidates(), "native player destination forbidden", "native-player");
        Reject(Candidates(), "missing destination", "");
        var candidate = Candidates()[1]; var observation = candidate.Observation;
        foreach (var invalid in new[] { observation with { Territory = "a" }, observation with { Owner = "conqueror" },
            observation with { Exists = false }, observation with { Player = true }, observation with { HasMap = true }, observation with { Visited = true },
            observation with { Eligible = false }, observation with { TargetEligible = false }, observation with { TargetPlayer = true },
            observation with { PendingDelivery = true }, observation with { SourceSettlements = 1 } })
        {
            var candidates = Candidates(); candidates[1] = candidate with { Observation = invalid };
            Reject(candidates, "one unsafe follower blocks entire batch");
        }
        var depleted = Candidates(); depleted[0] = depleted[0] with { Observation = depleted[0].Observation with { SourceSettlements = 2 } };
        depleted[1] = depleted[1] with { Observation = depleted[1].Observation with { SourceSettlements = 2 } };
        Reject(depleted, "two individually eligible cities cannot together empty a real source faction");
        Reject(depleted, "unrelated administrative identity cannot waive source's last-base protection", administration: "wrong");
        Check(FollowerTransfers.Review(book, ledger, depleted, "new-liege", 10, "source").Moves.Count == 3, "verified owned administration may empty while external source still keeps its base");
        var changedCounts = Candidates(); changedCounts[0] = changedCounts[0] with { Observation = changedCounts[0].Observation with { SourceSettlements = 4 } };
        Reject(changedCounts, "inconsistent counts from same source rejected");
        var changedWorld = Candidates().Select(c => c.Observation.Owner == "source" ? c with { Observation = c.Observation with { SourceSettlements = 4 } } : c).ToArray();
        Check(FollowerTransfers.Review(book, ledger, changedWorld, "administration", 10, "administration").Stamp != plan.Stamp, "world-count change invalidates reviewed snapshot");
        var readonlyList = (IList<FollowerCandidate>)plan.Entries; bool immutable = false;
        try { readonlyList[0] = Candidates()[2]; } catch (NotSupportedException) { immutable = true; }
        Check(immutable, "review entry collection immutable");

        ledger.Prepare("real-a", book, "bond-a", Candidates()[0].Observation, "administration", "administration", null, TerritoryTransferPurpose.ActiveCharter, 10, "administration");
        Reject(Candidates(), "pending physical ownership execution blocks a second group plan");
        ledger.Confirm("real-a", Candidates()[0].Observation with { Owner = "administration" }, 10);
        var partial = Candidates(); partial[0] = new("bond-a", new("a", "administration", 1, HasMap: true, Visited: true));
        partial[1] = partial[1] with { Observation = partial[1].Observation with { SourceSettlements = 2 } };
        var rest = FollowerTransfers.Review(book, ledger, partial, "administration", 10, "administration");
        Check(rest.Moves.SequenceEqual(new[] { "bond-b", "bond-c" }), "re-review after partial completion does not move completed or visited-at-destination city again");
        Check(rest.Stamp != plan.Stamp && ledger.Transfers.Count == 1, "partial progress is durable per-city, not fictitious batch completion");
        foreach (string id in rest.Moves)
        {
            var current = partial.Single(c => c.Charter == id);
            ledger.Prepare("real-" + id, book, id, current.Observation, "administration", "administration", null, TerritoryTransferPurpose.ActiveCharter, 10, "administration");
            ledger.Confirm("real-" + id, current.Observation with { Owner = "administration" }, 10);
        }
        var arrived = Candidates().Select(c => c with { Observation = c.Observation with { Owner = "administration", SourceSettlements = 3, HasMap = true, Visited = true } }).ToArray();
        Check(FollowerTransfers.Review(book, ledger, arrived, "administration", 10, "administration").Moves.Count == 0, "completed batch creates no duplicate transfers");
        Reject(arrived, "visited followers cannot leave administration for a new liege", "new-liege", "administration");
        var untouched = arrived.Select(c => c with { Observation = c.Observation with { HasMap = false, Visited = false } }).ToArray();
        Check(FollowerTransfers.Review(book, ledger, untouched, "new-liege", 10, "administration").Moves.Count == 3, "all administration towns can follow player into another polity");
        Reject(untouched, "ordinary external faction cannot be emptied by batch", "new-liege");
        foreach (var move in FollowerTransfers.Review(book, ledger, untouched, "new-liege", 10, "administration").Moves)
        {
            var c = untouched.Single(c => c.Charter == move); int remaining = 3 - ledger.Transfers.Count(t => t.TargetOwner == "new-liege");
            ledger.Prepare("depart-" + move, book, move, c.Observation with { SourceSettlements = remaining }, "new-liege", "new-liege", null, TerritoryTransferPurpose.ActiveCharter, 10, "administration");
            ledger.Confirm("depart-" + move, c.Observation with { Owner = "new-liege" }, 10);
        }
        Check(ledger.Transfers.Count == 6 && ledger.Transfers.All(t => t.Phase == TerritoryTransferPhase.Applied), "proxy may retain zero towns with all actual owner proofs recorded");
        Check(TerritoryTransferLedger.Restore(ledger.Export()).Export() == ledger.Export(), "administration departure history remains version-one compatible");

        var diplomacy = SharedDiplomacyPlan.Build("administration", "native-player", [new("native-player", 100, SharedRelationKind.Ally),
            new("administration", 100, SharedRelationKind.Ally), new("other", -80, SharedRelationKind.Hostile)]);
        Check(diplomacy.Count == 1 && diplomacy[0].Other == "other" && diplomacy[0].BaseGoodwill == -80, "reverse mirror copies player external values to NPC proxy, excluding internal pair");
        return count;
    }
}
