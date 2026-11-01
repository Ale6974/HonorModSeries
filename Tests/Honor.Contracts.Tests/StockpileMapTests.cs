using System.Text.Json;
using Honor.Contracts.Rules;

internal static class StockpileMapTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Original stockpile custody regression."); }
        void Reject(Action f) { bool rejected = false; try { f(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        string basic = LoadedMapPreservation.Capture("town", "map", 20, 20, new string('A', 64), CityStockProof.Capture(true, false, -1, []), 0,
            [new("goods", "Steel", null, 50, 100, 1, 0, null)]);
        var pile = new NeutralMapStockpile("zone", new string('B', 64), [0, 1], ["goods"]);
        string original = LoadedMapPreservation.CaptureStockpiles(basic, [pile]);
        var scope = LoadedMapPreservation.Restore(original);
        Check(LoadedMapPreservation.Format(original) == 7 && !basic.Contains("Stockpiles"));
        Check(scope.Stockpiles[0].Contents.SequenceEqual(["goods"]));
        LoadedMapPreservation.Pair(original, "town", scope.Proof, 7);
        Reject(() => LoadedMapPreservation.Pair(original, "town", scope.Proof, 6));
        Reject(() => LoadedMapPreservation.CaptureStockpiles(original, [pile]));
        Reject(() => LoadedMapPreservation.CaptureStockpiles(basic, []));
        Reject(() => LoadedMapPreservation.CaptureStockpiles(basic, [pile with { Cells = [] }]));
        Reject(() => LoadedMapPreservation.CaptureStockpiles(basic, [pile with { Cells = [1, 0] }]));
        Reject(() => LoadedMapPreservation.CaptureStockpiles(basic, [pile with { Cells = [0, 0] }]));
        Reject(() => LoadedMapPreservation.CaptureStockpiles(basic, [pile with { Cells = [0, 400] }]));
        Reject(() => LoadedMapPreservation.CaptureStockpiles(basic, [pile with { Cells = [0] }]));
        Reject(() => LoadedMapPreservation.CaptureStockpiles(basic, [pile with { Contents = ["missing"] }]));
        Reject(() => LoadedMapPreservation.CaptureStockpiles(basic, [pile with { Contents = ["goods", "goods"] }]));
        Reject(() => LoadedMapPreservation.CaptureStockpiles(basic, [pile, pile]));
        Reject(() => LoadedMapPreservation.CaptureStockpiles(basic, [pile, pile with { Id = "zone2" }]));
        string changed = LoadedMapPreservation.CaptureStockpiles(basic, [pile with { Configuration = new string('C', 64) }]);
        Check(LoadedMapPreservation.Restore(changed).Proof != scope.Proof);
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        var observation = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, StockProof: scope.Proof, StockFormat: 7) { LoadedMap = original };
        var ledger = new TerritoryTransferLedger(); string before = ledger.Export();
        Reject(() => ledger.Prepare("old", book, "bond", observation, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        Reject(() => ledger.PrepareLoaded("wrong", book, "bond", observation with { StockFormat = 6 }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        Check(ledger.Export() == before);
        ledger.PrepareLoadedJournaled("zone", book, "bond", observation, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        ledger = TerritoryTransferLedger.Restore(ledger.Export()); string pending = ledger.Export();
        Reject(() => ledger.Confirm("zone", observation with { Owner = "target", LoadedMap = changed, StockProof = LoadedMapPreservation.Restore(changed).Proof }, 4));
        Check(ledger.Export() == pending);
        var save = JsonSerializer.Deserialize<TerritoryTransfersSave>(pending);
        Reject(() => TerritoryTransferLedger.Restore(JsonSerializer.Serialize(save with { Transfers = [save.Transfers[0] with { StockFormat = 6 }] })));
        Check(FollowerTransfers.Review(book, new(), [new("bond", observation)], "target", 3).Moves.SequenceEqual(["bond"]));
        ledger.Confirm("zone", observation with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(ledger.Export()).Transfer("zone").LoadedMap == original);
        var cancelled = TerritoryTransferLedger.Restore(pending); cancelled.CancelUnmoved("zone", observation, 4);
        Check(cancelled.Transfer("zone").Phase == TerritoryTransferPhase.Cancelled);
        string shelfBase = LoadedMapPreservation.Capture("town", "map", 20, 20, new string('A', 64), CityStockProof.Capture(true, false, -1, []), 0,
            [new("shelf", "Shelf", "WoodLog", 1, 100, 2, 0, 2), new("goods", "Steel", null, 50, 100, 1, 0, null)]);
        shelfBase = LoadedMapPreservation.CaptureStorage(shelfBase, [new("shelf", new string('D', 64), [2], [])]);
        shelfBase = LoadedMapPreservation.CaptureGroups(shelfBase, [new("group", new string('E', 64), ["shelf"])]);
        string combined = LoadedMapPreservation.CaptureStockpiles(shelfBase, [pile]);
        Check(LoadedMapPreservation.Restore(combined).Groups[0].Members.SequenceEqual(["shelf"]));
        Reject(() => LoadedMapPreservation.CaptureStockpiles(shelfBase, [pile with { Cells = [0, 1, 2] }]));
        return n;
    }
}
