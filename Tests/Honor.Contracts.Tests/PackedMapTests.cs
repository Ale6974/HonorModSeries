using System.Text.Json;
using Honor.Contracts.Rules;

internal static class PackedMapTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Original packed-map custody regression."); }
        void Reject(Action f) { bool rejected = false; try { f(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        string stock = CityStockProof.Capture(true, false, -1, []), grid = new('A', 64);
        var root = new NeutralMapThing("pack", "MinifiedThing", null, 1, 100, 10, 0, null);
        var inner = new NeutralMapThing("table", "Table", "WoodLog", 1, 120, -1, 0, 3);
        string legacy = LoadedMapPreservation.Capture("town", "map", 20, 20, grid, stock, 0, [root]);
        Check(!legacy.Contains("Held") && LoadedMapPreservation.Format(legacy) == 3);
        string packed = LoadedMapPreservation.CapturePacked("town", "map", 20, 20, grid, stock, 0, [root], [new("pack", 0, inner)]);
        var scope = LoadedMapPreservation.Restore(packed);
        Check(LoadedMapPreservation.Format(packed) == 4 && scope.Held[0].Item == inner && scope.Proof != LoadedMapPreservation.Restore(legacy).Proof);
        LoadedMapPreservation.Pair(packed, "town", scope.Proof, 4);
        Reject(() => LoadedMapPreservation.Pair(packed, "town", scope.Proof, 3));
        Reject(() => LoadedMapPreservation.CapturePacked("town", "map", 20, 20, grid, stock, 0, [root], []));
        Reject(() => LoadedMapPreservation.CapturePacked("town", "map", 20, 20, grid, stock, 0, [root], [new("missing", 0, inner)]));
        Reject(() => LoadedMapPreservation.CapturePacked("town", "map", 20, 20, grid, stock, 0, [root], [new("pack", 1, inner)]));
        Reject(() => LoadedMapPreservation.CapturePacked("town", "map", 20, 20, grid, stock, 0, [root], [new("pack", 0, inner with { Cell = 0 })]));
        Reject(() => LoadedMapPreservation.CapturePacked("town", "map", 20, 20, grid, stock, 0, [root], [new("pack", 0, inner), new("pack", 0, inner with { Id = "other" })]));
        Reject(() => LoadedMapPreservation.CapturePacked("town", "map", 20, 20, grid, stock, 0, [root], [new("pack", 0, inner with { Id = "pack" })]));
        var middle = inner with { Id = "pack-inner", Def = "MinifiedThing", Quality = null };
        string nested = LoadedMapPreservation.CapturePacked("town", "map", 20, 20, grid, stock, 0, [root], [new("pack-inner", 0, inner), new("pack", 0, middle)]);
        Check(LoadedMapPreservation.Restore(nested).Held.Length == 2);
        Reject(() => LoadedMapPreservation.CapturePacked("town", "map", 20, 20, grid, stock, 0, [root], [new("pack-inner", 0, inner), new("table", 0, middle)]));
        var deep = Enumerable.Range(0, 9).Select(i => new NeutralMapHeld(i == 0 ? "pack" : "inner" + (i - 1), 0, inner with { Id = "inner" + i })).ToArray();
        Reject(() => LoadedMapPreservation.CapturePacked("town", "map", 20, 20, grid, stock, 0, [root], deep));
        string changed = LoadedMapPreservation.CapturePacked("town", "map", 20, 20, grid, stock, 0, [root], [new("pack", 0, inner with { HitPoints = 119 })]);
        Check(LoadedMapPreservation.Restore(changed).Proof != scope.Proof);
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        var observation = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, StockProof: scope.Proof, StockFormat: 4) { LoadedMap = packed };
        var ledger = new TerritoryTransferLedger(); var before = ledger.Export();
        Reject(() => ledger.Prepare("legacy", book, "bond", observation, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        Reject(() => ledger.PrepareLoaded("old-format", book, "bond", observation with { StockFormat = 3 }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        Check(ledger.Export() == before);
        var plan = FollowerTransfers.Review(book, ledger, [new("bond", observation)], "target", 3); Check(plan.Moves.SequenceEqual(["bond"]));
        ledger.PrepareLoadedJournaled("packed", book, "bond", observation, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        ledger = TerritoryTransferLedger.Restore(ledger.Export()); var pending = ledger.Export();
        Reject(() => ledger.Confirm("packed", observation with { Owner = "target", LoadedMap = changed, StockProof = LoadedMapPreservation.Restore(changed).Proof }, 4));
        Check(ledger.Export() == pending);
        var save = JsonSerializer.Deserialize<TerritoryTransfersSave>(pending);
        Reject(() => TerritoryTransferLedger.Restore(JsonSerializer.Serialize(save with { Transfers = [save.Transfers[0] with { StockFormat = 3 }] })));
        ledger.Confirm("packed", observation with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(ledger.Export()).Transfer("packed").StockFormat == 4 && ledger.Transfer("packed").LoadedMap == packed);
        var cancelled = TerritoryTransferLedger.Restore(pending); cancelled.CancelUnmoved("packed", observation, 4);
        Check(cancelled.Transfer("packed").Phase == TerritoryTransferPhase.Cancelled && cancelled.Transfer("packed").LoadedMap == packed);
        return n;
    }
}
