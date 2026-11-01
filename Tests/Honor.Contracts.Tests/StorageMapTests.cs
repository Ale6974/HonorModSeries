using System.Text.Json;
using Honor.Contracts.Rules;

internal static class StorageMapTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Original shelf custody regression."); }
        void Reject(Action f) { bool rejected = false; try { f(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        var shelf = new NeutralMapThing("shelf", "Shelf", "WoodLog", 1, 100, 0, 0, 2);
        var goods = new NeutralMapThing("steel", "Steel", null, 50, 100, 1, 0, null);
        string basic = LoadedMapPreservation.Capture("town", "map", 20, 20, new string('A', 64), CityStockProof.Capture(true, false, -1, []), 0, [shelf, goods]);
        var settings = new NeutralMapStorage("shelf", new string('B', 64), [0, 1], ["steel"]);
        string original = LoadedMapPreservation.CaptureStorage(basic, [settings]);
        var scope = LoadedMapPreservation.Restore(original);
        Check(LoadedMapPreservation.Format(original) == 5 && !basic.Contains("Storage"));
        Check(scope.Storage[0].Configuration == settings.Configuration && scope.Storage[0].Contents.SequenceEqual(["steel"]));
        LoadedMapPreservation.Pair(original, "town", scope.Proof, 5);
        Reject(() => LoadedMapPreservation.Pair(original, "town", scope.Proof, 3));
        Reject(() => LoadedMapPreservation.CaptureStorage(basic, []));
        Reject(() => LoadedMapPreservation.CaptureStorage(original, [settings]));
        Reject(() => LoadedMapPreservation.CaptureStorage(basic, [settings with { Id = "missing" }]));
        Reject(() => LoadedMapPreservation.CaptureStorage(basic, [settings with { Cells = [1] }]));
        Reject(() => LoadedMapPreservation.CaptureStorage(basic, [settings with { Cells = [0, 400] }]));
        Reject(() => LoadedMapPreservation.CaptureStorage(basic, [settings with { Cells = [0, 0] }]));
        Reject(() => LoadedMapPreservation.CaptureStorage(basic, [settings with { Cells = [1, 0] }]));
        Reject(() => LoadedMapPreservation.CaptureStorage(basic, [settings with { Contents = ["shelf"] }]));
        Reject(() => LoadedMapPreservation.CaptureStorage(basic, [settings with { Contents = ["steel", "steel"] }]));
        Reject(() => LoadedMapPreservation.CaptureStorage(basic, [settings with { Contents = ["missing"] }]));
        Reject(() => LoadedMapPreservation.CaptureStorage(basic, [settings with { Cells = [0] }]));
        string changed = LoadedMapPreservation.CaptureStorage(basic, [settings with { Configuration = new string('C', 64) }]);
        Check(LoadedMapPreservation.Restore(changed).Proof != scope.Proof);
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        var observation = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, StockProof: scope.Proof, StockFormat: 5) { LoadedMap = original };
        var ledger = new TerritoryTransferLedger(); string before = ledger.Export();
        Reject(() => ledger.Prepare("legacy", book, "bond", observation, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        Reject(() => ledger.PrepareLoaded("wrong", book, "bond", observation with { StockFormat = 4 }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        Check(ledger.Export() == before);
        Check(FollowerTransfers.Review(book, ledger, [new("bond", observation)], "target", 3).Moves.SequenceEqual(["bond"]));
        ledger.PrepareLoadedJournaled("shelf", book, "bond", observation, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        ledger = TerritoryTransferLedger.Restore(ledger.Export()); string pending = ledger.Export();
        Reject(() => ledger.Confirm("shelf", observation with { Owner = "target", LoadedMap = changed, StockProof = LoadedMapPreservation.Restore(changed).Proof }, 4));
        Check(ledger.Export() == pending);
        var save = JsonSerializer.Deserialize<TerritoryTransfersSave>(pending);
        Reject(() => TerritoryTransferLedger.Restore(JsonSerializer.Serialize(save with { Transfers = [save.Transfers[0] with { StockFormat = 3 }] })));
        ledger.Confirm("shelf", observation with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(ledger.Export()).Transfer("shelf").LoadedMap == original);
        var cancelled = TerritoryTransferLedger.Restore(pending); cancelled.CancelUnmoved("shelf", observation, 4);
        Check(cancelled.Transfer("shelf").Phase == TerritoryTransferPhase.Cancelled);
        var group = new NeutralMapStorageGroup("group", new string('D', 64), ["shelf"]);
        string grouped = LoadedMapPreservation.CaptureGroups(original, [group]);
        var groupedScope = LoadedMapPreservation.Restore(grouped);
        Check(LoadedMapPreservation.Format(grouped) == 6 && !original.Contains("Groups"));
        LoadedMapPreservation.Pair(grouped, "town", groupedScope.Proof, 6);
        Reject(() => LoadedMapPreservation.Pair(grouped, "town", groupedScope.Proof, 5));
        Reject(() => LoadedMapPreservation.CaptureGroups(basic, [group]));
        Reject(() => LoadedMapPreservation.CaptureGroups(grouped, [group]));
        Reject(() => LoadedMapPreservation.CaptureGroups(original, []));
        Reject(() => LoadedMapPreservation.CaptureGroups(original, [group with { Members = [] }]));
        Reject(() => LoadedMapPreservation.CaptureGroups(original, [group with { Members = ["missing"] }]));
        Reject(() => LoadedMapPreservation.CaptureGroups(original, [group with { Members = ["steel"] }]));
        Reject(() => LoadedMapPreservation.CaptureGroups(original, [group with { Members = ["shelf", "shelf"] }]));
        Reject(() => LoadedMapPreservation.CaptureGroups(original, [group, group]));
        Reject(() => LoadedMapPreservation.CaptureGroups(original, [group, group with { Id = "group2" }]));
        string twoShelves = LoadedMapPreservation.Capture("town", "map", 20, 20, new string('A', 64), CityStockProof.Capture(true, false, -1, []), 0,
            [shelf, shelf with { Id = "shelf2", Cell = 2 }]);
        twoShelves = LoadedMapPreservation.CaptureStorage(twoShelves, [settings with { Cells = [0], Contents = [] }, settings with { Id = "shelf2", Cells = [2], Contents = [] }]);
        string orderedGroup = LoadedMapPreservation.CaptureGroups(twoShelves, [group with { Members = ["shelf2", "shelf"] }]);
        string reorderedGroup = LoadedMapPreservation.CaptureGroups(twoShelves, [group with { Members = ["shelf", "shelf2"] }]);
        Check(LoadedMapPreservation.Restore(orderedGroup).Groups[0].Members.SequenceEqual(["shelf2", "shelf"]));
        Check(LoadedMapPreservation.Restore(orderedGroup).Proof != LoadedMapPreservation.Restore(reorderedGroup).Proof);
        var groupObservation = observation with { LoadedMap = grouped, StockFormat = 6, StockProof = groupedScope.Proof };
        var groupLedger = new TerritoryTransferLedger();
        groupLedger.PrepareLoadedJournaled("group", book, "bond", groupObservation, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        groupLedger = TerritoryTransferLedger.Restore(groupLedger.Export()); string groupPending = groupLedger.Export();
        string changedGroup = LoadedMapPreservation.CaptureGroups(original, [group with { Configuration = new string('E', 64) }]);
        Reject(() => groupLedger.Confirm("group", groupObservation with { Owner = "target", LoadedMap = changedGroup, StockProof = LoadedMapPreservation.Restore(changedGroup).Proof }, 4));
        Check(groupLedger.Export() == groupPending);
        Reject(() => groupLedger.Confirm("group", observation with { Owner = "target" }, 4));
        Check(groupLedger.Export() == groupPending);
        Check(FollowerTransfers.Review(book, new(), [new("bond", groupObservation)], "target", 3).Moves.SequenceEqual(["bond"]));
        groupLedger.Confirm("group", groupObservation with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(groupLedger.Export()).Transfer("group").LoadedMap == grouped);
        var groupCancelled = TerritoryTransferLedger.Restore(groupPending); groupCancelled.CancelUnmoved("group", groupObservation, 4);
        Check(groupCancelled.Transfer("group").Phase == TerritoryTransferPhase.Cancelled);
        var forged = JsonSerializer.Deserialize<TerritoryTransfersSave>(groupPending);
        Reject(() => TerritoryTransferLedger.Restore(JsonSerializer.Serialize(forged with { Transfers = [forged.Transfers[0] with { StockFormat = 5 }] })));
        return n;
    }
}
