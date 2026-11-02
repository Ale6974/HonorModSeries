using System.Text.Json;
using Honor.Contracts.Rules;

internal static class ContainerMapTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Container forest regression."); }
        void Reject(Action f) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad); }
        var root = new NeutralMapThing("crate", "AncientCrate", null, 1, 100, 0, 0, null);
        var a = new NeutralMapThing("a", "Steel", null, 50, 100, -1, 0, null);
        var b = a with { Id = "b", Def = "Silver", Count = 100 };
        string basic = LoadedMapPreservation.Capture("town", "map", 20, 20, new string('A', 64), CityStockProof.Capture(true, false, -1, []), 0, [root]);
        var holder = new NeutralMapContainer("crate", "crate", new string('B', 64), 2);
        NeutralMapHeld[] children = [new("crate", 0, a), new("crate", 1, b)];
        string original = LoadedMapPreservation.CaptureContainers(basic, children, [holder]);
        var scope = LoadedMapPreservation.Restore(original);
        Check(LoadedMapPreservation.Format(original) == 9 && !basic.Contains("Containers"));
        Check(scope.Held.Select(r => r.Item.Id).SequenceEqual(["a", "b"]));
        Reject(() => LoadedMapPreservation.CapturePacked("town", "map", 20, 20, new string('A', 64), CityStockProof.Capture(true, false, -1, []), 0, [root], children));
        Reject(() => LoadedMapPreservation.CaptureContainers(original, children, [holder]));
        Reject(() => LoadedMapPreservation.CaptureContainers(basic, children, []));
        Reject(() => LoadedMapPreservation.CaptureContainers(basic, children.Take(1), [holder]));
        Reject(() => LoadedMapPreservation.CaptureContainers(basic, children, [holder with { Count = 1 }]));
        Reject(() => LoadedMapPreservation.CaptureContainers(basic, [children[0], children[0]], [holder]));
        Reject(() => LoadedMapPreservation.CaptureContainers(basic, [children[0], new("crate", 2, b)], [holder]));
        Reject(() => LoadedMapPreservation.CaptureContainers(basic, children, [holder with { Id = "missing" }]));
        Reject(() => LoadedMapPreservation.CaptureContainers(basic, children, [holder with { Kind = "custom" }]));
        Reject(() => LoadedMapPreservation.CaptureContainers(basic, children, [holder, holder]));
        string empty = LoadedMapPreservation.CaptureContainers(basic, [], [holder with { Count = 0 }]);
        Check(LoadedMapPreservation.Restore(empty).Held == null && LoadedMapPreservation.Format(empty) == 9);
        var package = a with { Id = "package", Def = "MinifiedThing", Count = 1 };
        string nested = LoadedMapPreservation.CaptureContainers(basic, [new("crate", 0, package), new("package", 0, b)], [holder with { Count = 1 }]);
        Check(LoadedMapPreservation.Restore(nested).Held.Length == 2);
        Reject(() => LoadedMapPreservation.CaptureContainers(basic, [new("crate", 0, package), new("package", 0, a), new("package", 1, b)], [holder with { Count = 1 }]));
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        var observation = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, StockProof: scope.Proof, StockFormat: 9) { LoadedMap = original };
        var ledger = new TerritoryTransferLedger(); ledger.PrepareLoadedJournaled("crate", book, "bond", observation, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        ledger = TerritoryTransferLedger.Restore(ledger.Export()); string pending = ledger.Export();
        string reordered = LoadedMapPreservation.CaptureContainers(basic, [new("crate", 0, b), new("crate", 1, a)], [holder]);
        Reject(() => ledger.Confirm("crate", observation with { Owner = "target", LoadedMap = reordered, StockProof = LoadedMapPreservation.Restore(reordered).Proof }, 4));
        Check(ledger.Export() == pending);
        var tampered = JsonSerializer.Deserialize<TerritoryTransfersSave>(pending);
        Reject(() => TerritoryTransferLedger.Restore(JsonSerializer.Serialize(tampered with { Transfers = [tampered.Transfers[0] with { StockFormat = 8 }] })));
        ledger.Confirm("crate", observation with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(ledger.Export()).Transfer("crate").LoadedMap == original);
        var cancelled = TerritoryTransferLedger.Restore(pending); cancelled.CancelUnmoved("crate", observation, 4);
        Check(cancelled.Transfer("crate").Phase == TerritoryTransferPhase.Cancelled);
        return n;
    }
}
