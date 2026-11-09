using System.Text;
using Honor.Contracts.Rules;
internal static class CivicAssetTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Original civic/property regression."); }
        void Reject(Action f) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        string roots = LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("grid"), CityStockProof.Capture(true, false, -1, []), 0,
            [new("shelf", "Shelf", "Wood", 1, 40, 0, 0, 2), new("goods", "Steel", null, 50, 100, 0, 0, null)]);
        string shelf = LoadedMapPreservation.CaptureStorage(roots, [new("shelf", Hash("settings"), [0], ["goods"])]);
        string clinical = ResidentMapped.Bind(Hash("clinical"), new("citizen", "map", 1, 0));
        string bindings = ResidentMapped.Prepare([new("citizen", "map", 1, 0)]);
        var set = ResidentMoveSet.Capture([new("citizen", clinical, 4)]);
        string citizens = LoadedMapPreservation.CaptureResidents(shelf, [new("citizen", clinical)], [new("visitor", Hash("original-visitor"))]);
        MapAsset[] assets = [new("shelf", "source", Hash("style")) { Repair = new(100, true) }];
        string scope = LoadedMapPreservation.CaptureCivicAssets(citizens, assets, true, true); var map = LoadedMapPreservation.Restore(scope);
        Check(LoadedMapPreservation.Format(scope) == 15 && map.CivicAssetsV1 == 1 && map.OwnedStorageV1 == 1 && map.AssetRepairV1 == 1);
        ResidentMapped.Pair(bindings, set, scope);
        Check(LoadedMapPreservation.Format(citizens) == 10 && !citizens.Contains("CivicAssetsV1") && !citizens.Contains("OwnedAssets"));
        Reject(() => LoadedMapPreservation.CaptureAssets(citizens, assets));
        Reject(() => LoadedMapPreservation.CaptureRepairAssets(citizens, assets));
        Reject(() => LoadedMapPreservation.CaptureOwnedStorage(citizens, assets, true));
        Reject(() => LoadedMapPreservation.CaptureCivicAssets(shelf, assets, true, true));
        Reject(() => LoadedMapPreservation.CaptureCivicAssets(scope, assets, true, true));
        Reject(() => LoadedMapPreservation.CaptureCivicAssets(citizens, assets, false, true));
        Reject(() => LoadedMapPreservation.CaptureCivicAssets(citizens, assets, true, false));
        Reject(() => LoadedMapPreservation.Pair(scope, "town", map.Proof, 14));
        string calls = MapAssetTransfers.Prepare(scope, "source", "target");
        string unknown = MapAssetTransfers.Begin(calls, "shelf", "source");
        Reject(() => MapAssetTransfers.ExpectedOwner(unknown, "shelf"));
        calls = MapAssetTransfers.Returned(unknown, "shelf", "target");
        MapAssetTransfers.Pair(calls, scope, "source", "target", true);
        string changedCitizens = LoadedMapPreservation.CaptureResidents(shelf, [new("citizen", Hash("changed"))], [new("visitor", Hash("original-visitor"))]);
        string changed = LoadedMapPreservation.CaptureCivicAssets(changedCitizens, assets, true, true);
        Reject(() => MapAssetTransfers.Pair(calls, changed, "source", "target", true));
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        var actual = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, HasInhabitants: true, StockProof: map.Proof, StockFormat: 15, ResidentProof: set.Proof) { LoadedMap = scope };
        var physical = new TerritoryTransferLedger();
        Reject(() => physical.PrepareLoadedJournaled("missing", book, "bond", actual with { ResidentProof = null }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        Reject(() => physical.PrepareLoadedJournaled("empty", book, "bond", actual with { HasInhabitants = false }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        physical.PrepareLoadedJournaled("combined", book, "bond", actual, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        string pending = physical.Export(); physical = TerritoryTransferLedger.Restore(pending);
        Reject(() => physical.Confirm("combined", actual with { Owner = "target", LoadedMap = changed, StockProof = LoadedMapPreservation.Restore(changed).Proof }, 4));
        Check(physical.Export() == pending);
        physical.Confirm("combined", actual with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(physical.Export()).Transfer("combined").LoadedMap == scope);
        var cancel = TerritoryTransferLedger.Restore(pending); cancel.CancelUnmoved("combined", actual, 4);
        Check(TerritoryTransferLedger.Restore(cancel.Export()).Transfer("combined").Phase == TerritoryTransferPhase.Cancelled);
        return n;
    }
}
