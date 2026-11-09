using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;

internal static class MapRepairAssetTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Original damaged asset regression."); }
        void Reject(Action f) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        var repair = new MapAssetRepair(100, true);
        MapAssetRepairs.Verify("source", 41, repair, [new("source", 1, true), new("target", 0, false)]);
        MapAssetRepairs.Verify("target", 41, repair, [new("source", 0, false), new("target", 1, true)]);
        MapAssetRepairs.Verify("source", 100, repair, []);
        MapAssetRepairs.Verify("source", 41, repair with { Repairable = false }, []);
        Reject(() => MapAssetRepairs.Verify("source", 41, repair, []));
        Reject(() => MapAssetRepairs.Verify("source", 41, repair, [new("source", 0, true)]));
        Reject(() => MapAssetRepairs.Verify("source", 41, repair, [new("source", 2, true)]));
        Reject(() => MapAssetRepairs.Verify("source", 41, repair, [new("source", 1, true), new("target", 1, true)]));
        Reject(() => MapAssetRepairs.Verify("target", 41, repair, [new("source", 1, true)]));
        Reject(() => MapAssetRepairs.Verify("source", 100, repair, [new("source", 1, true)]));
        Reject(() => MapAssetRepairs.Verify("source", 41, repair with { Repairable = false }, [new("source", 1, true)]));
        Reject(() => MapAssetRepairs.Verify("source", 0, repair, []));
        Reject(() => MapAssetRepairs.Verify("source", 101, repair, []));
        Reject(() => MapAssetRepairs.Verify("source", 41, repair, [new("source", 1, true), new("source", 0, false)]));
        string Root(int hp) => LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("grid"), CityStockProof.Capture(true, false, -1, []), 0,
            [new("wall", "Wall", "Granite", 1, hp, 0, 0, null)]);
        var asset = new MapAsset("wall", "source", Hash("original-paint-style")) { Repair = repair };
        string scope = LoadedMapPreservation.CaptureRepairAssets(Root(41), [asset]); var map = LoadedMapPreservation.Restore(scope);
        Check(LoadedMapPreservation.Format(scope) == 13 && map.AssetRepairV1 == 1 && map.Things.Single().HitPoints == 41 && map.OwnedAssets.Single().Repair == repair);
        string old = LoadedMapPreservation.CaptureAssets(Root(100), [asset with { Repair = null }]);
        Check(LoadedMapPreservation.Format(old) == 12 && !old.Contains("AssetRepairV1") && !old.Contains("Repair"));
        Reject(() => LoadedMapPreservation.CaptureAssets(Root(41), [asset]));
        Reject(() => LoadedMapPreservation.CaptureRepairAssets(old, [asset]));
        Reject(() => LoadedMapPreservation.CaptureRepairAssets(Root(41), [asset with { Repair = null }]));
        Reject(() => LoadedMapPreservation.CaptureRepairAssets(Root(101), [asset]));
        Reject(() => LoadedMapPreservation.CaptureRepairAssets(Root(41), [asset with { Repair = repair with { Maximum = 0 } }]));
        Reject(() => LoadedMapPreservation.Pair(scope, "town", map.Proof, 12));
        string journal = MapAssetTransfers.Prepare(scope, "source", "target");
        string unknown = MapAssetTransfers.Begin(journal, "wall", "source");
        Reject(() => MapAssetTransfers.ExpectedOwner(unknown, "wall"));
        string returned = MapAssetTransfers.Returned(unknown, "wall", "target");
        MapAssetTransfers.Pair(returned, scope, "source", "target", true);
        string changed = LoadedMapPreservation.CaptureRepairAssets(Root(42), [asset]);
        Reject(() => MapAssetTransfers.Pair(returned, changed, "source", "target", true));
        Reject(() => MapAssetTransfers.Pair(returned, old, "source", "target", true));
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        var observed = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, StockProof: map.Proof, StockFormat: 13) { LoadedMap = scope };
        var ledger = new TerritoryTransferLedger();
        ledger.PrepareLoadedJournaled("damaged", book, "bond", observed, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        string pending = ledger.Export(); ledger = TerritoryTransferLedger.Restore(pending);
        Reject(() => ledger.Confirm("damaged", observed with { Owner = "target", LoadedMap = changed, StockProof = LoadedMapPreservation.Restore(changed).Proof }, 4));
        Check(ledger.Export() == pending);
        ledger.Confirm("damaged", observed with { Owner = "target" }, 4);
        Check(LoadedMapPreservation.Restore(TerritoryTransferLedger.Restore(ledger.Export()).Transfer("damaged").LoadedMap).Things.Single().HitPoints == 41);
        var cancel = TerritoryTransferLedger.Restore(pending); cancel.CancelUnmoved("damaged", observed, 4);
        Check(TerritoryTransferLedger.Restore(cancel.Export()).Transfer("damaged").Phase == TerritoryTransferPhase.Cancelled);
        return n;
    }
}
