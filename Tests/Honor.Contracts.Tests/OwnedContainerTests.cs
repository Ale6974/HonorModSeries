using System.Text;
using Honor.Contracts.Rules;
internal static class OwnedContainerTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Original owned container regression."); }
        void Reject(Action f) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        string root = LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("grid"), CityStockProof.Capture(true, false, -1, []), 0,
            [new("a", "Crate", "Steel", 1, 40, 0, 0, 2), new("b", "Casket", "Steel", 1, 100, 1, 0, null)]);
        NeutralMapHeld[] held = [new("a", 0, new("pack", "Minified", null, 1, 100, -1, 0, null)), new("a", 1, new("goods", "Steel", null, 50, 100, -1, 0, null)), new("pack", 0, new("inner", "Meal", null, 3, 100, -1, 0, null))];
        NeutralMapContainer[] containers = [new("a", "crate", Hash("original-hack-glow-known-signal"), 2), new("b", "casket", Hash("empty"), 0)];
        string forest = LoadedMapPreservation.CaptureContainers(root, held, containers);
        MapAsset[] owned = [new("a", "source", Hash("a-style")) { Repair = new(100, true) }, new("b", "source", Hash("b-style")) { Repair = new(100, true) }];
        string scope = LoadedMapPreservation.CaptureOwnedContainers(forest, owned, true, false); var map = LoadedMapPreservation.Restore(scope);
        Check(LoadedMapPreservation.Format(scope) == 16 && map.OwnedContainersV1 == 1 && map.AssetRepairV1 == 1);
        Check(map.Held.SequenceEqual(held) && map.Containers.SequenceEqual(containers) && map.Things[0].HitPoints == 40);
        Check(!forest.Contains("OwnedContainersV1") && LoadedMapPreservation.Format(forest) == 9);
        Reject(() => LoadedMapPreservation.CaptureAssets(forest, owned));
        Reject(() => LoadedMapPreservation.CaptureRepairAssets(forest, owned));
        Reject(() => LoadedMapPreservation.CaptureOwnedStorage(forest, owned, true));
        Reject(() => LoadedMapPreservation.CaptureOwnedContainers(root, owned, true, false));
        Reject(() => LoadedMapPreservation.CaptureOwnedContainers(forest, owned, false, false));
        Reject(() => LoadedMapPreservation.CaptureOwnedContainers(scope, owned, true, false));
        Reject(() => LoadedMapPreservation.CaptureOwnedContainers(forest, [owned[0], owned[0]], true, false));
        Reject(() => LoadedMapPreservation.CaptureOwnedContainers(forest, [new("goods", "source", Hash("no-held-property-authority"))], false, false));
        Reject(() => LoadedMapPreservation.Pair(scope, "town", map.Proof, 9));
        string calls = MapAssetTransfers.Prepare(scope, "source", "target");
        string unknown = MapAssetTransfers.Begin(calls, "a", "source");
        Reject(() => MapAssetTransfers.ExpectedOwner(unknown, "a"));
        Reject(() => MapAssetTransfers.Begin(unknown, "a", "source"));
        calls = MapAssetTransfers.Returned(unknown, "a", "target");
        Check(MapAssetTransfers.ExpectedOwner(calls, "a") == "target" && MapAssetTransfers.ExpectedOwner(calls, "b") == "source");
        string partial = calls; Reject(() => MapAssetTransfers.Pair(partial, scope, "source", "target", true));
        calls = MapAssetTransfers.Returned(MapAssetTransfers.Begin(calls, "b", "source"), "b", "target");
        MapAssetTransfers.Pair(calls, scope, "source", "target", true);
        string changed = LoadedMapPreservation.CaptureOwnedContainers(LoadedMapPreservation.CaptureContainers(root,
            [held[1] with { Slot = 0 }, held[0] with { Slot = 1 }, held[2]], containers), owned, true, false);
        Reject(() => MapAssetTransfers.Pair(calls, changed, "source", "target", true));
        string citizens = LoadedMapPreservation.CaptureResidents(forest, [new("citizen", Hash("clinical"))]);
        Reject(() => LoadedMapPreservation.CaptureCivicAssets(citizens, owned, true, false));
        string joint = LoadedMapPreservation.CaptureOwnedContainers(citizens, owned, true, false);
        Check(LoadedMapPreservation.Format(joint) == 17 && LoadedMapPreservation.Restore(joint).CivicAssetsV1 == 1);
        string Rehash(LoadedMapScope value) {
            var unsigned = value with { Proof = null };
            string hash = Hash(System.Text.Json.JsonSerializer.Serialize(unsigned));
            return System.Text.Json.JsonSerializer.Serialize(unsigned with { Proof = hash });
        }
        Reject(() => LoadedMapPreservation.Restore(Rehash(map with { OwnedContainersV1 = null })));
        Reject(() => LoadedMapPreservation.Restore(Rehash(map with { OwnedContainersV1 = 2 })));
        Reject(() => LoadedMapPreservation.Restore(Rehash(map with { OwnedAssets = null })));
        Reject(() => LoadedMapPreservation.Restore(Rehash(map with { Containers = null })));
        Reject(() => LoadedMapPreservation.Restore(Rehash(map with { Held = map.Held.Skip(1).ToArray() })));
        Reject(() => LoadedMapPreservation.Restore(Rehash(map with { OwnedAssets = [new("inner", "source", Hash("held-rights"))] })));
        string changedConfig = LoadedMapPreservation.CaptureOwnedContainers(LoadedMapPreservation.CaptureContainers(root, held,
            [containers[0] with { Configuration = Hash("hacked-later") }, containers[1]]), owned, true, false);
        Reject(() => MapAssetTransfers.Pair(calls, changedConfig, "source", "target", true));
        Check(MapAssetTransfers.Restore(unknown).Calls[0].Started && !MapAssetTransfers.Restore(unknown).Calls[0].Returned);
        MapAsset[] packedAssets = [new("a", "source", Hash("a-style")) { Repair = new(100, true) }, new("inner", "source", Hash("inner-style")) { Repair = new(100, true) }];
        string packedOwned = LoadedMapPreservation.CaptureHeldAssets(forest, packedAssets, true, false, true);
        Check(LoadedMapPreservation.Format(packedOwned) == 18 && LoadedMapPreservation.Restore(packedOwned).HeldAssetsV1 == 1);
        string packedCalls = MapAssetTransfers.Prepare(packedOwned, "source", "target");
        packedCalls = MapAssetTransfers.Returned(MapAssetTransfers.Begin(packedCalls, "a", "source"), "a", "target");
        Check(MapAssetTransfers.ExpectedOwner(packedCalls, "inner") == "source");
        packedCalls = MapAssetTransfers.Returned(MapAssetTransfers.Begin(packedCalls, "inner", "source"), "inner", "target");
        MapAssetTransfers.Pair(packedCalls, packedOwned, "source", "target", true);
        string heldOnly = LoadedMapPreservation.CaptureHeldAssets(forest, [packedAssets[1]], true, false, false);
        Check(LoadedMapPreservation.Restore(heldOnly).OwnedContainersV1 == null && LoadedMapPreservation.Format(heldOnly) == 18);
        Reject(() => LoadedMapPreservation.CaptureOwnedContainers(forest, packedAssets, true, false));
        Reject(() => LoadedMapPreservation.CaptureHeldAssets(forest, owned, true, false, true));
        Reject(() => LoadedMapPreservation.CaptureHeldAssets(packedOwned, packedAssets, true, false, true));
        Reject(() => LoadedMapPreservation.Restore(Rehash(LoadedMapPreservation.Restore(packedOwned) with { HeldAssetsV1 = null })));
        Reject(() => LoadedMapPreservation.Restore(Rehash(LoadedMapPreservation.Restore(packedOwned) with { HeldAssetsV1 = 2 })));
        string heldJoint = LoadedMapPreservation.CaptureHeldAssets(citizens, packedAssets, true, false, true);
        Check(LoadedMapPreservation.Format(heldJoint) == 19 && LoadedMapPreservation.Restore(heldJoint).CivicAssetsV1 == 1);
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        var observed = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, StockProof: map.Proof, StockFormat: 16) { LoadedMap = scope };
        var physical = new TerritoryTransferLedger(); physical.PrepareLoadedJournaled("containers", book, "bond", observed, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        string pending = physical.Export(); physical = TerritoryTransferLedger.Restore(pending);
        physical.Confirm("containers", observed with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(physical.Export()).Transfer("containers").LoadedMap == scope);
        var cancelled = TerritoryTransferLedger.Restore(pending); cancelled.CancelUnmoved("containers", observed, 4);
        Check(TerritoryTransferLedger.Restore(cancelled.Export()).Transfer("containers").Phase == TerritoryTransferPhase.Cancelled);
        var jointMap = LoadedMapPreservation.Restore(joint);
        var jointObservation = observed with { LoadedMap = joint, StockFormat = 17, StockProof = jointMap.Proof, HasInhabitants = true, ResidentProof = Hash("original-census") };
        var jointPhysical = new TerritoryTransferLedger();
        Reject(() => jointPhysical.PrepareLoadedJournaled("joint", book, "bond", jointObservation with { ResidentProof = null }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        Reject(() => jointPhysical.PrepareLoadedJournaled("joint", book, "bond", jointObservation with { HasInhabitants = false }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        jointPhysical.PrepareLoadedJournaled("joint", book, "bond", jointObservation, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        jointPhysical = TerritoryTransferLedger.Restore(jointPhysical.Export());
        jointPhysical.Confirm("joint", jointObservation with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(jointPhysical.Export()).Transfer("joint").LoadedMap == joint);
        var packedPhysical = new TerritoryTransferLedger();
        var packedObservation = observed with { LoadedMap = packedOwned, StockFormat = 18, StockProof = LoadedMapPreservation.Restore(packedOwned).Proof };
        packedPhysical.PrepareLoadedJournaled("packed", book, "bond", packedObservation, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        string packedPending = packedPhysical.Export(); packedPhysical = TerritoryTransferLedger.Restore(packedPending);
        packedPhysical.Confirm("packed", packedObservation with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(packedPhysical.Export()).Transfer("packed").LoadedMap == packedOwned);
        var packedCancel = TerritoryTransferLedger.Restore(packedPending); packedCancel.CancelUnmoved("packed", packedObservation, 4);
        Check(TerritoryTransferLedger.Restore(packedCancel.Export()).Transfer("packed").Phase == TerritoryTransferPhase.Cancelled);
        return n;
    }
}
