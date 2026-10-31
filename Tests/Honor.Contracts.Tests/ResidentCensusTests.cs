using System.Text.Json;
using Honor.Contracts.Rules;

internal static class ResidentCensusTests
{
    public static int Run()
    {
        int n = 0; void Check(bool b) { n++; if (!b) throw new Exception("Resident census regression."); }
        void Reject(Action f) { bool rejected = false; try { f(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        var set = ResidentMoveSet.Capture([new("a", new string('A', 64), 0), new("b", new string('B', 64), 4), new("c", new string('C', 64), null)]);
        var locations = set.Residents.Select(r => new ResidentLocation("town", r.Id)).ToArray();
        ResidentTransferCensus.RequireUniqueLocations("town", set.Residents.Select(r => r.Id), locations);
        Reject(() => ResidentTransferCensus.RequireUniqueLocations("town", set.Residents.Select(r => r.Id), locations.Append(new("other", "b"))));
        Reject(() => ResidentTransferCensus.RequireUniqueLocations("town", set.Residents.Select(r => r.Id), locations.Take(2)));
        Reject(() => ResidentTransferCensus.RequireUniqueLocations("town", set.Residents.Select(r => r.Id), locations.Append(locations[0])));
        string census = ResidentTransferCensus.Prepare("town", "source", "target", 7, set); var original = ResidentTransferCensus.Restore(census);
        var calls = set.Residents.Select(r => JsonSerializer.Serialize(new ResidentAuthorityState(r.Id))).ToList();
        var care = set.Residents.Select(r => ResidentCareJournal.Prepare(r.Id, "source", "target", r.MedicalCare).Export()).ToList();
        void Validate(bool started, bool complete = false) => ResidentTransferCensus.Check(census, original.Proof, "town", "source", "target", 7, set, calls, care, started, complete);
        Validate(false); Check(original.Residents == set.Export());
        Reject(() => ResidentTransferCensus.Check(census, original.Proof, "other", "source", "target", 7, set, calls, care, false));
        Reject(() => ResidentTransferCensus.Check(census, original.Proof, "town", "source", "other", 7, set, calls, care, false));
        Reject(() => ResidentTransferCensus.Check(census, original.Proof, "town", "source", "target", 6, set, calls, care, false));
        for (int i = 0; i < 3; i++)
        {
            var medical = ResidentCareJournal.Restore(care[i]); medical.Begin("source", set.Residents[i].MedicalCare); care[i] = medical.Export();
            calls[i] = ResidentTransferCensus.Begin(calls[i]); Validate(true);
            Reject(() => ResidentTransferCensus.RequireRecoverable(calls, care));
            Reject(() => ResidentTransferCensus.Begin(calls[i]));
            calls[i] = ResidentTransferCensus.Returned(calls[i]);
            Validate(true); Check(ResidentTransferCensus.Call(calls[i]).Returned && !medical.State.Returned);
            Reject(() => ResidentTransferCensus.RequireRecoverable(calls, care));
            int? returned = i == 2 ? null : 1;
            medical.RecordReturn("target", returned); care[i] = medical.Export();
            ResidentTransferCensus.RequireRecoverable(calls, care);
            if (i != 2) { Check(medical.MayRestore("target", returned)); medical.Consume("target", returned); }
            else medical.SealUnchanged("target", null);
            care[i] = medical.Export(); Validate(true);
            if (i < 2) Reject(() => Validate(true, true));
            Reject(() => ResidentTransferCensus.Returned(calls[i]));
        }
        Validate(true, true); Check(calls.All(c => ResidentTransferCensus.Call(c).Returned));
        ResidentTransferCensus.Check(JsonSerializer.Serialize(original), original.Proof, "town", "source", "target", 7, ResidentMoveSet.Restore(set.Export()),
            calls.Select(s => JsonSerializer.Serialize(ResidentTransferCensus.Call(s))).ToList(), care.Select(s => ResidentCareJournal.Restore(s).Export()).ToList(), true, true);
        Reject(() => ResidentTransferCensus.Check(census, original.Proof, "town", "source", "target", 7, set, calls.Take(2).ToList(), care, true));
        Reject(() => ResidentTransferCensus.Check(census, original.Proof, "town", "source", "target", 7, set, calls.AsEnumerable().Reverse().ToList(), care, true));
        Reject(() => ResidentTransferCensus.Restore(JsonSerializer.Serialize(original with { Target = "changed" })));
        Reject(() => ResidentTransferCensus.Call(JsonSerializer.Serialize(new ResidentAuthorityState("a", false, true))));
        return n;
    }
}
