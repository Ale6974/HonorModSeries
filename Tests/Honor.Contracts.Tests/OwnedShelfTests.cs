using System.Text;
using Honor.Contracts.Rules;
internal static class OwnedShelfTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Original owned shelf regression."); }
        void Reject(Action f) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        string root = LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("grid"), CityStockProof.Capture(true, false, -1, []), 0,
            [new("a", "Shelf", "Wood", 1, 40, 0, 0, 2), new("b", "Shelf", "Steel", 1, 100, 1, 0, 4), new("goods", "Steel", null, 50, 100, 0, 0, null)]);
        string storage = LoadedMapPreservation.CaptureStorage(root, [new("a", Hash("settings-a"), [0], ["goods"]), new("b", Hash("settings-b"), [1], [])]);
        string group = LoadedMapPreservation.CaptureGroups(storage, [new("group", Hash("shared-settings"), ["b", "a"])]);
        MapAsset[] owned = [new("a", "source", Hash("a-style")) { Repair = new(100, true) }, new("b", "source", Hash("b-style")) { Repair = new(100, true) }];
        string scope = LoadedMapPreservation.CaptureOwnedStorage(group, owned, true); var map = LoadedMapPreservation.Restore(scope);
        Check(LoadedMapPreservation.Format(scope) == 14 && map.AssetRepairV1 == 1 && map.OwnedStorageV1 == 1);
        Check(map.Storage[0].Contents.SequenceEqual(["goods"]) && map.Groups[0].Members.SequenceEqual(["b", "a"]) && map.Things[0].HitPoints == 40);
        Check(!group.Contains("OwnedAssets") && !group.Contains("OwnedStorageV1"));
        Reject(() => LoadedMapPreservation.CaptureAssets(group, owned));
        Reject(() => LoadedMapPreservation.CaptureRepairAssets(group, owned));
        Reject(() => LoadedMapPreservation.CaptureOwnedStorage(group, [owned[0]], true));
        Reject(() => LoadedMapPreservation.CaptureOwnedStorage(group, owned, false));
        Reject(() => LoadedMapPreservation.CaptureOwnedStorage(scope, owned, true));
        Reject(() => LoadedMapPreservation.CaptureOwnedStorage(root, owned, true));
        Reject(() => LoadedMapPreservation.CaptureOwnedStorage(group, [owned[0], owned[0]], true));
        string independent = LoadedMapPreservation.CaptureOwnedStorage(storage, [owned[0]], true);
        Check(LoadedMapPreservation.Restore(independent).OwnedAssets.Length == 1);
        string calls = MapAssetTransfers.Prepare(scope, "source", "target");
        calls = MapAssetTransfers.Returned(MapAssetTransfers.Begin(calls, "a", "source"), "a", "target");
        Check(MapAssetTransfers.ExpectedOwner(calls, "a") == "target" && MapAssetTransfers.ExpectedOwner(calls, "b") == "source");
        string partial = calls;
        Reject(() => MapAssetTransfers.Pair(partial, scope, "source", "target", true));
        calls = MapAssetTransfers.Returned(MapAssetTransfers.Begin(calls, "b", "source"), "b", "target");
        MapAssetTransfers.Pair(calls, scope, "source", "target", true);
        string changedGroup = LoadedMapPreservation.CaptureGroups(storage, [new("group", Hash("shared-settings"), ["a", "b"])]);
        string changedScope = LoadedMapPreservation.CaptureOwnedStorage(changedGroup, owned, true);
        string completed = calls;
        Reject(() => MapAssetTransfers.Pair(completed, changedScope, "source", "target", true));
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        var observed = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, StockProof: map.Proof, StockFormat: 14) { LoadedMap = scope };
        var physical = new TerritoryTransferLedger(); physical.PrepareLoadedJournaled("shelves", book, "bond", observed, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        string pending = physical.Export(); physical = TerritoryTransferLedger.Restore(pending);
        physical.Confirm("shelves", observed with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(physical.Export()).Transfer("shelves").LoadedMap == scope);
        var cancelled = TerritoryTransferLedger.Restore(pending); cancelled.CancelUnmoved("shelves", observed, 4);
        Check(TerritoryTransferLedger.Restore(cancelled.Export()).Transfer("shelves").Phase == TerritoryTransferPhase.Cancelled);
        return n;
    }
}
