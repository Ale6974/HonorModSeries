using System.Text.Json;
using Honor.Contracts.Rules;

internal static class CityStockProofTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        void Reject(Action action, string name)
        {
            bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, name);
        }
        CityStockItem[] goods = [new("Thing_1", "Steel", null, 50, 100, null), new("Thing_2", "Longsword", "Steel", 1, 90, 4)];
        string Proof(IEnumerable<CityStockItem> items) => CityStockProof.Capture(true, true, 1800000, items);
        string proof = Proof(goods);
        Check(CityStockProof.Valid(proof), "stock digest has bounded canonical form");
        Check(Proof(goods.Reverse()) == proof, "stock order does not invalidate identical goods");
        Check(Proof(goods.Select(i => i with { })) == proof, "stable identity survives snapshot recreation");
        foreach (var changed in new[] { goods[0] with { Id = "Thing_3" }, goods[0] with { Def = "Silver" },
            goods[0] with { Stuff = "WoodLog" }, goods[0] with { Count = 49 }, goods[0] with { HitPoints = 99 }, goods[0] with { Quality = 1 } })
            Check(Proof([changed, goods[1]]) != proof, "identity, kind, material, quantity, condition and quality changes invalidate preservation");
        Check(Proof(goods[..1]) != proof && Proof([]) != proof, "missing goods cannot confirm conservation");
        Check(CityStockProof.Capture(true, true, 1800001, goods) != proof, "restock schedule change invalidates review");
        string empty = CityStockProof.Capture(false, false, -1, []);
        string visitedEmpty = CityStockProof.Capture(true, false, -1, []);
        Check(empty != visitedEmpty && visitedEmpty != CityStockProof.Capture(true, true, -1, []), "unvisited, expired stock and empty live container differ");
        Reject(() => Proof([goods[0], goods[0]]), "duplicate identity rejected");
        foreach (var invalid in new[] { goods[0] with { Count = 0 }, goods[0] with { HitPoints = -1 }, goods[0] with { Quality = 7 }, goods[0] with { Id = "\n" }, goods[0] with { Def = null } })
            Reject(() => Proof([invalid]), "invalid observed item rejected");
        Reject(() => CityStockProof.Capture(true, false, -1, goods), "absent container cannot hold goods");
        Reject(() => CityStockProof.Capture(false, true, -1, goods), "generated stock cannot be called untouched");
        Reject(() => CityStockProof.Capture(true, false, 4, []), "absent container has no active restock schedule");
        Reject(() => Proof(Enumerable.Range(0, 10001).Select(i => goods[0] with { Id = "id" + i })), "bounded snapshot size");
        foreach (string bad in new[] { "", "x", new string('G', 64), proof.ToLowerInvariant() }) Check(!CityStockProof.Valid(bad), "malformed digest rejected");

        var book = new CharterBook(); book.Register(new("player", "native-player", true), 0); book.Register(new("town", "old", false), 0);
        TerritoryObservation[] identities = [new("player", "native-player", true, true, true), new("town", "old", true, false, true)];
        book.Offer("bond", new("player", "town", 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m), 0, PoliticalWorld.Day, identities);
        book.Sign("bond", true, 1, identities); book.Sign("bond", false, 2, identities);
        var ledger = new TerritoryTransferLedger();
        var source = new TerritoryTransferObservation("town", "old", 2, Visited: true, StockProof: proof);
        void Prepare(TerritoryTransferObservation observed) => ledger.Prepare("move", book, "bond", observed, "liege", "liege", null, TerritoryTransferPurpose.ActiveCharter, 3);
        foreach (var invalid in new[] { source with { HasMap = true }, source with { HasInhabitants = true }, source with { StockProof = null }, source with { StockProof = "bad" } })
        {
            string before = ledger.Export(); Reject(() => Prepare(invalid), "stock evidence never bypasses map/resident guards");
            Check(ledger.Export() == before, "failed preparation leaves no intent");
        }
        Prepare(source);
        Check(ledger.Pending("town") && ledger.Transfer("move").StockProof == proof, "visited city records conservation requirement before mutation");
        var restored = TerritoryTransferLedger.Restore(ledger.Export());
        Check(restored.Export() == ledger.Export(), "prepared stock requirement survives rules save roundtrip");
        foreach (var changed in new[] { source with { StockProof = Proof([goods[0] with { Count = 49 }, goods[1]]) },
            source with { StockProof = null }, source with { HasInhabitants = true }, source with { HasMap = true } })
        {
            string before = restored.Export();
            Reject(() => restored.Confirm("move", changed with { Owner = "liege" }, 4), "changed or unobservable resources cannot confirm target ownership");
            Reject(() => restored.CancelUnmoved("move", changed, 4), "unchanged owner alone cannot cancel changed resources");
            Check(restored.Export() == before && restored.Pending("town"), "mismatched resource recovery retains unresolved intent");
        }
        restored.Confirm("move", source with { Owner = "liege" }, 4);
        Check(restored.VerifiedOwner("town", "old", "liege"), "visited ordinary-stock town can complete with matching owner and stock proof");
        Check(TerritoryTransferLedger.Restore(restored.Export()).Export() == restored.Export(), "completed proof persists");
        ledger.CancelUnmoved("move", source, 4);
        Check(!ledger.Pending("town") && ledger.Transfer("move").Phase == TerritoryTransferPhase.Cancelled, "unchanged stock and source can cancel prepared intent");
        var expired = new TerritoryTransferLedger();
        expired.Prepare("expired", book, "bond", source with { StockProof = visitedEmpty }, "liege", "liege", null, TerritoryTransferPurpose.ActiveCharter, 3);
        expired.Confirm("expired", source with { Owner = "liege", StockProof = visitedEmpty }, 4);
        Check(expired.Transfers[0].Phase == TerritoryTransferPhase.Applied, "visited city with already expired stock need not generate new goods");

        var plan = FollowerTransfers.Review(book, new(), [new("bond", source)], "liege", 3);
        var changedPlan = FollowerTransfers.Review(book, new(), [new("bond", source with { StockProof = visitedEmpty })], "liege", 3);
        Check(plan.Moves.Count == 1 && plan.Stamp != changedPlan.Stamp, "coordinated review includes existing stock and invalidates stale confirmation");
        var legacy = new TerritoryTransferLedger();
        legacy.Prepare("legacy", book, "bond", source with { Visited = false, StockProof = null }, "liege", "liege", null, TerritoryTransferPurpose.ActiveCharter, 3);
        string oldJson = legacy.Export();
        Check(!oldJson.Contains("StockProof") && TerritoryTransferLedger.Restore(oldJson).Export() == oldJson, "legacy v1 JSON bytes and political cancellation digest remain stable");
        Reject(() => legacy.Confirm("legacy", source with { Owner = "liege" }, 4), "old untouched-only intent cannot be upgraded using new visited proof");
        var saved = JsonSerializer.Deserialize<TerritoryTransfersSave>(restored.Export());
        Reject(() => TerritoryTransferLedger.Restore(JsonSerializer.Serialize(saved with { Transfers = [saved.Transfers[0] with { StockProof = "bad" }] })), "malformed saved stock digest fails closed");
        return count;
    }
}
