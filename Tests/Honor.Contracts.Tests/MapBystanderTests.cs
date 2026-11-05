using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;

internal static class MapBystanderTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Original read-only map bystander regression."); }
        void Reject(Action f) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        string Rewrite(LoadedMapScope s) { s = s with { Proof = null }; return JsonSerializer.Serialize(s with { Proof = Hash(JsonSerializer.Serialize(s)) }); }
        string roots = LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("grid"), CityStockProof.Capture(true, false, -1, []), 0,
            [new("crate", "Crate", null, 1, 100, 0, 0, null)]);
        string packed = LoadedMapPreservation.CaptureContainers(roots, [new("crate", 0, new("goods", "Steel", null, 50, 100, -1, 0, null))],
            [new("crate", "crate", Hash("configuration"), 1)]);
        var a = new NativeMapResident("visitor-a", Hash("original-a")); var b = new NativeMapResident("visitor-b", Hash("original-b"));
        string scope = LoadedMapPreservation.CaptureBystanders(packed, [b, a]);
        var original = LoadedMapPreservation.Restore(scope);
        Check(original.NativeResidents == null && original.ReadOnlyBystandersV1 == 1 && original.Visitors.Select(x => x.Id).SequenceEqual([a.Id, b.Id]));
        Check(LoadedMapPreservation.Format(scope) == 11 && LoadedMapPreservation.Format(packed) == 9);
        Check(!packed.Contains("ReadOnlyBystandersV1") && !packed.Contains("Visitors"));
        LoadedMapPreservation.Pair(scope, "town", original.Proof, 11);
        Reject(() => LoadedMapPreservation.Pair(scope, "town", original.Proof, 10));
        Reject(() => LoadedMapPreservation.CaptureBystanders(scope, [a]));
        Reject(() => LoadedMapPreservation.CaptureResidents(scope, [a]));
        Reject(() => LoadedMapPreservation.CaptureBystanders(packed, []));
        Reject(() => LoadedMapPreservation.CaptureBystanders(packed, [a, a]));
        Reject(() => LoadedMapPreservation.CaptureBystanders(packed, [a with { Id = "goods" }]));
        Reject(() => LoadedMapPreservation.CaptureBystanders(packed, [a with { Preserved = "bad" }]));
        Reject(() => LoadedMapPreservation.CaptureBystanders(packed, Enumerable.Range(0, 129).Select(i => a with { Id = "v" + i })));
        Reject(() => LoadedMapPreservation.Restore(Rewrite(original with { ReadOnlyBystandersV1 = null })));
        Reject(() => LoadedMapPreservation.Restore(Rewrite(original with { ReadOnlyBystandersV1 = 2 })));
        Reject(() => LoadedMapPreservation.Restore(Rewrite(original with { Visitors = null })));
        Reject(() => LoadedMapPreservation.Restore(Rewrite(original with { NativeResidents = [new("citizen", Hash("citizen"))] })));
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        var observed = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, StockProof: original.Proof, StockFormat: 11) { LoadedMap = scope };
        var physical = new TerritoryTransferLedger();
        Reject(() => physical.Prepare("old", book, "bond", observed, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        Reject(() => physical.PrepareLoadedJournaled("wrong", book, "bond", observed with { StockFormat = 10 }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        physical.PrepareLoadedJournaled("visitors", book, "bond", observed, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        string pending = physical.Export(); physical = TerritoryTransferLedger.Restore(pending);
        Check(physical.Transfer("visitors").ResidentProof == null && physical.Transfer("visitors").LoadedMap == scope);
        string changed = LoadedMapPreservation.CaptureBystanders(packed, [a with { Preserved = Hash("changed") }, b]);
        Reject(() => physical.Confirm("visitors", observed with { Owner = "target", LoadedMap = changed, StockProof = LoadedMapPreservation.Restore(changed).Proof }, 4));
        Check(physical.Export() == pending);
        physical.Confirm("visitors", observed with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(physical.Export()).Transfer("visitors").LoadedMap == scope);
        var cancel = TerritoryTransferLedger.Restore(pending); cancel.CancelUnmoved("visitors", observed, 4);
        Check(TerritoryTransferLedger.Restore(cancel.Export()).Transfer("visitors").Phase == TerritoryTransferPhase.Cancelled);
        // A read-only bystander proof is never a positive city call return.
        Reject(() => TerritoryCallGuard.RequireNewCall(true, true, false));
        Reject(() => TerritoryCallGuard.RequireStartedForTarget(true, false, false));
        Reject(() => TerritoryCallGuard.RequireVerifiedUnmoved(true, true, false));
        return n;
    }
}
