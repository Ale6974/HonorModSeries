using System.Text.Json;
using Honor.Contracts.Rules;

internal static class ResidentMappedTests
{
    public static int Run()
    {
        int n = 0; void Check(bool value) { n++; if (!value) throw new Exception("Mapped civic resident regression."); }
        void Reject(Action f) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad); }
        string original = new('A', 64);
        var actor = new ResidentMapBinding("actor", "map", 0, 0);
        var world = new ResidentMapBinding("world", null, -1, 0);
        string bound = ResidentMapped.Bind(original, actor);
        string worldBound = ResidentMapped.Bind(original, world);
        Check(bound != original && bound != worldBound);
        Check(bound != ResidentMapped.Bind(original, actor with { Cell = 1 }));
        Check(bound != ResidentMapped.Bind(original, actor with { Map = "other" }));
        string bindings = ResidentMapped.Prepare([world, actor]);
        Check(ResidentMapped.Restore(bindings).Select(r => r.Id).SequenceEqual(["actor", "world"]));
        Reject(() => ResidentMapped.Prepare([actor, actor]));
        Reject(() => ResidentMapped.Prepare([world with { Cell = 0 }]));
        Reject(() => ResidentMapped.Prepare([actor with { Rotation = 4 }]));
        Reject(() => ResidentMapped.Bind("bad", actor));
        var set = ResidentMoveSet.Capture([new("actor", bound, 4), new("world", worldBound, 2)]);
        string mapBase = LoadedMapPreservation.Capture("town", "map", 20, 20, new string('B', 64), CityStockProof.Capture(true, false, -1, []), 0,
            [new("steel", "Steel", null, 50, 100, 1, 0, null)]);
        var live = new NativeMapResident("actor", bound); var visitor = new NativeMapResident("visitor", new string('C', 64));
        string map = LoadedMapPreservation.CaptureResidents(mapBase, [live], [visitor]);
        Check(LoadedMapPreservation.Format(map) == 10 && !mapBase.Contains("NativeResidents") && !mapBase.Contains("Visitors"));
        ResidentMapped.Pair(bindings, set, map);
        Reject(() => ResidentMapped.Pair(bindings, set, mapBase));
        Reject(() => ResidentMapped.Pair(ResidentMapped.Prepare([actor with { Map = "other" }, world]), set, map));
        Reject(() => ResidentMapped.Pair(ResidentMapped.Prepare([actor with { Cell = 400 }, world]), set, map));
        Reject(() => LoadedMapPreservation.CaptureResidents(mapBase, [], [visitor]));
        Reject(() => LoadedMapPreservation.CaptureResidents(mapBase, [live], [live]));
        Reject(() => LoadedMapPreservation.CaptureResidents(mapBase, [live], [visitor, visitor]));
        Reject(() => LoadedMapPreservation.CaptureResidents(mapBase, [live with { Id = "steel" }]));
        string census = ResidentTransferCensus.Prepare("town", "source", "target", 11, set);
        var originalCensus = ResidentTransferCensus.Restore(census);
        var calls = set.Residents.Select(r => JsonSerializer.Serialize(new ResidentAuthorityState(r.Id))).ToList();
        var care = set.Residents.Select(r => ResidentCareJournal.Prepare(r.Id, "source", "target", r.MedicalCare).Export()).ToList();
        for (int i = 0; i < set.Residents.Count; i++)
        {
            var medical = ResidentCareJournal.Restore(care[i]); medical.Begin("source", set.Residents[i].MedicalCare); care[i] = medical.Export();
            calls[i] = ResidentTransferCensus.Begin(calls[i]);
            Reject(() => ResidentTransferCensus.RequireRecoverable(calls, care));
            calls[i] = ResidentTransferCensus.Returned(calls[i]);
            Reject(() => ResidentTransferCensus.RequireRecoverable(calls, care));
            medical.RecordReturn("target", 1); medical.Consume("target", 1); care[i] = medical.Export();
            ResidentTransferCensus.Check(census, originalCensus.Proof, "town", "source", "target", 11, set, calls, care, true);
            if (i == 0) Reject(() => ResidentTransferCensus.Check(census, originalCensus.Proof, "town", "source", "target", 11, set, calls, care, true, true));
        }
        ResidentTransferCensus.Check(census, originalCensus.Proof, "town", "source", "target", 11, set, calls, care, true, true);
        Reject(() => ResidentTransferCensus.Check(census, originalCensus.Proof, "town", "source", "target", 10, set, calls, care, true, true));
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        var observed = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, HasInhabitants: true, StockProof: LoadedMapPreservation.Restore(map).Proof, StockFormat: 10, ResidentProof: set.Proof) { LoadedMap = map };
        var physical = new TerritoryTransferLedger();
        Reject(() => physical.PrepareLoadedJournaled("bad", book, "bond", observed with { ResidentProof = null }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        physical.PrepareLoadedJournaled("mapped", book, "bond", observed, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        physical = TerritoryTransferLedger.Restore(physical.Export()); string pending = physical.Export();
        string changed = LoadedMapPreservation.CaptureResidents(mapBase, [live], [visitor with { Preserved = new string('D', 64) }]);
        Reject(() => physical.Confirm("mapped", observed with { Owner = "target", LoadedMap = changed, StockProof = LoadedMapPreservation.Restore(changed).Proof }, 4));
        Check(physical.Export() == pending);
        physical.Confirm("mapped", observed with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(physical.Export()).Transfer("mapped").LoadedMap == map);
        var cancelled = TerritoryTransferLedger.Restore(pending); cancelled.CancelUnmoved("mapped", observed, 4);
        Check(cancelled.Transfer("mapped").Phase == TerritoryTransferPhase.Cancelled);
        return n;
    }
}
