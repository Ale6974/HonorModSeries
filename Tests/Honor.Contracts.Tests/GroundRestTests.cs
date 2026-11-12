using System.Text.Json;
using Honor.Contracts.Rules;
internal static class GroundRestTests
{
    public static int Run()
    {
        int n = 0; void Check(bool b) { n++; if (!b) throw new Exception("Original ground-rest custody regression."); }
        void Reject(Action f) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad); }
        string hash = new('A', 64);
        var cell = new GroundRestCell("resting", "Job_37", "map", 2);
        GroundRest.ValidateCells([cell], [cell]); GroundRest.ValidateCells([], [cell]);
        foreach (var wrong in new[] { cell with { Pawn = "foreign" }, cell with { Job = "different-job" }, cell with { Map = "other-map" }, cell with { Cell = 3 }, cell with { MaxPawns = 2 }, cell with { Stack = 1 } }) Reject(() => GroundRest.ValidateCells([wrong], [cell]));
        Reject(() => GroundRest.ValidateCells([cell, cell], [cell]));
        var second = cell with { Pawn = "other", Job = "Job_38" };
        Reject(() => GroundRest.ValidateCells([cell, second], [cell, second]));
        Reject(() => GroundRest.ValidateCells([cell], []));
        var work = new GroundRestRow("resting", "Job_37", "{\"Driver\":{\"curToilIndex\":1,\"ticksLeftThisToil\":920,\"asleep\":true},\"Posture\":1}");
        Check(GroundRest.Bind(hash, work) != GroundRest.Bind(hash, null));
        Check(GroundRest.Proof(work) != GroundRest.Proof(work with { Job = "Job_38" }));
        Check(GroundRest.Proof(work) != GroundRest.Proof(work with { Saved = work.Saved.Replace("920", "919") }));
        Reject(() => GroundRest.Validate([work, work], ["resting"]));
        Reject(() => GroundRest.Validate([work, work with { Pawn = "other" }], ["resting", "other"]));
        Reject(() => GroundRest.Validate([work], ["other"]));
        Reject(() => GroundRest.Validate([], ["resting"]));
        string bare = LoadedMapPreservation.Capture("town", "map", 20, 20, hash, CityStockProof.Capture(true, false, -1, []), 0, []);
        ResidentMapBinding[] positions = [new("idle", "map", 1, 0), new("resting", "map", 2, 0)];
        var set = ResidentMoveSet.Capture(positions.Select(p => new ResidentMoveEvidence(p.Id, GroundRest.Bind(ResidentMapped.Bind(hash, p), p.Id == "resting" ? work : null), 3)));
        string mapped = LoadedMapPreservation.CaptureResidents(bare, set.Residents.Select(r => new NativeMapResident(r.Id, r.Preserved)));
        string original = LoadedMapPreservation.CaptureGroundRest(mapped, [work]);
        Check(LoadedMapPreservation.Format(original) == 23 && !mapped.Contains("GroundRestV1"));
        Reject(() => LoadedMapPreservation.CaptureGroundRest(original, [work]));
        Reject(() => LoadedMapPreservation.CaptureRoyalMapped(original));
        Reject(() => LoadedMapPreservation.CaptureGroundRest(LoadedMapPreservation.CaptureRoyalMapped(mapped), [work]));
        Reject(() => LoadedMapPreservation.Pair(original, "town", LoadedMapPreservation.Restore(original).Proof, 10));
        ResidentMapped.Pair(ResidentMapped.Prepare(positions), set, original);
        string census = ResidentTransferCensus.Prepare("town", "source", "target", 14, set);
        var c = ResidentTransferCensus.Restore(census);
        var calls = set.Residents.Select(r => JsonSerializer.Serialize(new ResidentAuthorityState(r.Id))).ToArray();
        var care = set.Residents.Select(r => ResidentCareJournal.Prepare(r.Id, "source", "target", 3).Export()).ToArray();
        var book = new CharterBook(); book.Register(new("player", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("player", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("charter", new("player", "town", 90, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("charter", true, 1, obs); book.Sign("charter", false, 2, obs);
        var actual = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, HasInhabitants: true, StockProof: LoadedMapPreservation.Restore(original).Proof, StockFormat: 23, ResidentProof: set.Proof) { LoadedMap = original };
        var physical = new TerritoryTransferLedger();
        var t = physical.PrepareLoadedJournaled("ongoing", book, "charter", actual, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        string bookBefore = book.Export();
        for (int i = 0; i < calls.Length; i++)
        {
            calls[i] = ResidentTransferCensus.Begin(calls[i]); var medical = ResidentCareJournal.Restore(care[i]); medical.Begin("source", 3); care[i] = medical.Export();
            Reject(() => ResidentTransferCensus.RequireRecoverable(calls, care));
            string uncertain = calls[i]; Reject(() => ResidentTransferCensus.Begin(uncertain));
            calls[i] = ResidentTransferCensus.Returned(calls[i]); medical.RecordReturn("target", 1); medical.Consume("target", 1); care[i] = medical.Export();
            physical = TerritoryTransferLedger.Restore(physical.Export());
            original = JsonSerializer.Serialize(LoadedMapPreservation.Restore(original));
            Check(LoadedMapPreservation.Restore(original).GroundRestV1[0] == work);
            ResidentTransferCensus.Check(census, c.Proof, "town", "source", "target", 14, set, calls, care, true);
            if (i == 0) Reject(() => ResidentTransferCensus.Check(census, c.Proof, "town", "source", "target", 14, set, calls, care, true, true));
        }
        ResidentTransferCensus.Check(census, c.Proof, "town", "source", "target", 14, set, calls, care, true, true);
        Reject(() => ResidentTransferCensus.Check(census, c.Proof, "town", "source", "target", 11, set, calls, care, true, true));
        physical.Confirm(t.Id, actual with { Owner = "target" }, 4);
        Check(physical.Transfer(t.Id).Phase == TerritoryTransferPhase.Applied && book.Export() == bookBefore && LoadedMapPreservation.Restore(physical.Transfer(t.Id).LoadedMap).GroundRestV1[0] == work);
        return n;
    }
}
