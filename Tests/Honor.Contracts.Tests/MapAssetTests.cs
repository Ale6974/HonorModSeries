using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;

internal static class MapAssetTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Original fixed property regression."); }
        void Reject(Action f) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        string root = LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("grid"), CityStockProof.Capture(true, false, -1, []), 0,
            [new("wall-a", "Wall", "Granite", 1, 100, 0, 0, null), new("wall-b", "Wall", "Granite", 1, 100, 1, 0, null)]);
        MapAsset[] assets = [new("wall-a", "source", Hash("style-a")), new("wall-b", "source", Hash("style-b"))];
        string scope = LoadedMapPreservation.CaptureAssets(root, assets.AsEnumerable().Reverse()); var map = LoadedMapPreservation.Restore(scope);
        Check(LoadedMapPreservation.Format(scope) == 12 && LoadedMapPreservation.Format(root) == 3 && !root.Contains("OwnedAssets"));
        Check(map.OwnedAssets.SequenceEqual(assets));
        Reject(() => LoadedMapPreservation.CaptureAssets(root, []));
        Reject(() => LoadedMapPreservation.CaptureAssets(root, [assets[0], assets[0]]));
        Reject(() => LoadedMapPreservation.CaptureAssets(root, [assets[0] with { Id = "missing" }]));
        Reject(() => LoadedMapPreservation.CaptureAssets(root, [assets[0] with { Configuration = "bad" }]));
        Reject(() => LoadedMapPreservation.CaptureAssets(LoadedMapPreservation.CaptureBystanders(root, [new("visitor", Hash("visitor"))]), assets));
        Reject(() => LoadedMapPreservation.CaptureAssets(scope, assets));
        string journal = MapAssetTransfers.Prepare(scope, "source", "target");
        MapAssetTransfers.Pair(journal, scope, "source", "target");
        Check(MapAssetTransfers.ExpectedOwner(journal, "wall-a") == "source" && !MapAssetTransfers.Started(journal));
        Reject(() => MapAssetTransfers.Prepare(root, "source", "target"));
        Reject(() => MapAssetTransfers.Prepare(scope, "foreign", "target"));
        Reject(() => MapAssetTransfers.Prepare(scope, "source", "source"));
        Reject(() => MapAssetTransfers.Pair(journal, scope, "source", "other"));
        Reject(() => MapAssetTransfers.Pair(journal, scope, "source", "target", true));
        Reject(() => MapAssetTransfers.Begin(journal, "wall-b", "source"));
        Reject(() => MapAssetTransfers.Begin(journal, "wall-a", "target"));
        string started = MapAssetTransfers.Begin(journal, "wall-a", "source");
        Check(MapAssetTransfers.Started(started));
        Reject(() => MapAssetTransfers.ExpectedOwner(started, "wall-a"));
        Reject(() => MapAssetTransfers.Begin(started, "wall-a", "source"));
        Reject(() => MapAssetTransfers.Returned(started, "wall-a", "source"));
        string partial = MapAssetTransfers.Returned(started, "wall-a", "target");
        Check(MapAssetTransfers.ExpectedOwner(partial, "wall-a") == "target" && MapAssetTransfers.ExpectedOwner(partial, "wall-b") == "source");
        Reject(() => MapAssetTransfers.Returned(partial, "wall-a", "target"));
        Reject(() => MapAssetTransfers.Pair(partial, scope, "source", "target", true));
        string done = MapAssetTransfers.Returned(MapAssetTransfers.Begin(partial, "wall-b", "source"), "wall-b", "target");
        MapAssetTransfers.Pair(done, scope, "source", "target", true);
        Check(MapAssetTransfers.Restore(done).Calls.All(c => c.Returned));
        string changed = LoadedMapPreservation.CaptureAssets(root, [assets[0] with { Configuration = Hash("changed") }, assets[1]]);
        Reject(() => MapAssetTransfers.Pair(done, changed, "source", "target", true));
        var state = MapAssetTransfers.Restore(done); state.Calls[0] = state.Calls[0] with { Returned = false };
        Reject(() => MapAssetTransfers.Restore(JsonSerializer.Serialize(state)));
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        var observed = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, StockProof: map.Proof, StockFormat: 12) { LoadedMap = scope };
        var ledger = new TerritoryTransferLedger();
        Reject(() => ledger.Prepare("old", book, "bond", observed, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        string foreign = LoadedMapPreservation.CaptureAssets(root, [assets[0] with { Owner = "foreign" }, assets[1]]);
        Reject(() => ledger.PrepareLoadedJournaled("foreign", book, "bond", observed with { LoadedMap = foreign, StockProof = LoadedMapPreservation.Restore(foreign).Proof }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        ledger.PrepareLoadedJournaled("assets", book, "bond", observed, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        string pending = ledger.Export(); ledger = TerritoryTransferLedger.Restore(pending);
        Reject(() => ledger.Confirm("assets", observed with { Owner = "target", LoadedMap = changed, StockProof = LoadedMapPreservation.Restore(changed).Proof }, 4));
        Check(ledger.Export() == pending);
        ledger.Confirm("assets", observed with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(ledger.Export()).Transfer("assets").LoadedMap == scope);
        var cancelled = TerritoryTransferLedger.Restore(pending); cancelled.CancelUnmoved("assets", observed, 4);
        Check(TerritoryTransferLedger.Restore(cancelled.Export()).Transfer("assets").Phase == TerritoryTransferPhase.Cancelled);
        return n;
    }
}
