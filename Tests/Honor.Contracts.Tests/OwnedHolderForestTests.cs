using System.Text;
using Honor.Contracts.Rules;
internal static class OwnedHolderForestTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Original owned holder forest regression."); }
        void Reject(Action f) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        string root = LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("grid"), CityStockProof.Capture(true, false, -1, []), 0,
            [new("outer", "Crate", "Steel", 1, 100, 0, 0, null)]);
        NeutralMapHeld[] held = [new("inner", 0, new("package", "Minified", null, 1, 100, -1, 0, 2)),
            new("outer", 0, new("inner", "Casket", "Steel", 1, 60, -1, 0, null)),
            new("package", 0, new("wall", "Wall", "Wood", 1, 20, -1, 0, 2))];
        NeutralMapContainer[] containers = [new("inner", "casket", Hash("inner-config"), 1), new("outer", "crate", Hash("outer-config"), 1)];
        string forest = LoadedMapPreservation.CaptureContainers(root, held, containers);
        MapAsset[] assets = [new("inner", "source", Hash("inner-style")), new("outer", "source", Hash("outer-style")), new("package", "source", Hash("package-style")), new("wall", "source", Hash("wall-style"))];
        Reject(() => LoadedMapPreservation.CaptureHeldAssets(forest, assets, false, false, true));
        Reject(() => LoadedMapPreservation.CaptureOwnedContainers(forest, assets, false, false));
        string scope = LoadedMapPreservation.CaptureOwnedHolderForest(forest, assets, false, false, true, true);
        var map = LoadedMapPreservation.Restore(scope);
        Check(LoadedMapPreservation.Format(scope) == 20 && map.OwnedHolderForestV1 == 1 && map.Held.SequenceEqual(held));
        Check(!forest.Contains("OwnedHolderForestV1") && LoadedMapPreservation.Format(forest) == 9);
        Reject(() => LoadedMapPreservation.CaptureOwnedHolderForest(forest, assets, false, false, false, true));
        Reject(() => LoadedMapPreservation.CaptureOwnedHolderForest(forest, assets, false, false, true, false));
        Reject(() => LoadedMapPreservation.CaptureOwnedHolderForest(scope, assets, false, false, true, true));
        Reject(() => LoadedMapPreservation.CaptureOwnedHolderForest(forest, [assets[3]], false, false, false, true));
        string Rehash(LoadedMapScope value) {
            var unsigned = value with { Proof = null };
            return System.Text.Json.JsonSerializer.Serialize(unsigned with { Proof = Hash(System.Text.Json.JsonSerializer.Serialize(unsigned)) });
        }
        Reject(() => LoadedMapPreservation.Restore(Rehash(map with { OwnedHolderForestV1 = null })));
        Reject(() => LoadedMapPreservation.Restore(Rehash(map with { OwnedHolderForestV1 = 2 })));
        Reject(() => LoadedMapPreservation.Restore(Rehash(map with { HeldAssetsV1 = null })));
        Reject(() => LoadedMapPreservation.Restore(Rehash(map with { OwnedContainersV1 = null })));
        Reject(() => MapAssetTransfers.Prepare(scope, "foreign", "target"));
        string calls = MapAssetTransfers.Prepare(scope, "source", "target");
        string originalCalls = calls;
        foreach (var asset in assets)
        {
            string unknown = MapAssetTransfers.Begin(calls, asset.Id, "source");
            Reject(() => MapAssetTransfers.ExpectedOwner(unknown, asset.Id));
            Reject(() => MapAssetTransfers.Begin(unknown, asset.Id, "source"));
            calls = MapAssetTransfers.Returned(unknown, asset.Id, "target");
            Check(MapAssetTransfers.ExpectedOwner(calls, asset.Id) == "target");
        }
        MapAssetTransfers.Pair(calls, scope, "source", "target", true);
        Reject(() => MapAssetTransfers.Pair(originalCalls, scope, "source", "target", true));
        string changed = LoadedMapPreservation.CaptureOwnedHolderForest(LoadedMapPreservation.CaptureContainers(root, held,
            [containers[0] with { Configuration = Hash("changed-open-state") }, containers[1]]), assets, false, false, true, true);
        Reject(() => MapAssetTransfers.Pair(calls, changed, "source", "target", true));
        string citizens = LoadedMapPreservation.CaptureResidents(forest, [new("citizen", Hash("clinical"))]);
        string joint = LoadedMapPreservation.CaptureOwnedHolderForest(citizens, assets, false, false, true, true);
        Check(LoadedMapPreservation.Format(joint) == 21 && LoadedMapPreservation.Restore(joint).CivicAssetsV1 == 1);
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        foreach (string current in new[] { scope, joint })
        {
            var currentMap = LoadedMapPreservation.Restore(current);
            var observed = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, HasInhabitants: current == joint,
                StockProof: currentMap.Proof, StockFormat: LoadedMapPreservation.Format(current), ResidentProof: current == joint ? Hash("census") : null) { LoadedMap = current };
            var physical = new TerritoryTransferLedger();
            physical.PrepareLoadedJournaled("forest", book, "bond", observed, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
            string pending = physical.Export(); physical = TerritoryTransferLedger.Restore(pending);
            physical.Confirm("forest", observed with { Owner = "target" }, 4);
            Check(TerritoryTransferLedger.Restore(physical.Export()).Transfer("forest").LoadedMap == current);
            var cancelled = TerritoryTransferLedger.Restore(pending); cancelled.CancelUnmoved("forest", observed, 4);
            Check(TerritoryTransferLedger.Restore(cancelled.Export()).Transfer("forest").Phase == TerritoryTransferPhase.Cancelled);
        }
        return n;
    }
}
