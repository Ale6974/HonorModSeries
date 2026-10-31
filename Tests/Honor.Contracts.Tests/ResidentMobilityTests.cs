using System.Text.Json;
using Honor.Contracts.Rules;

internal static class ResidentMobilityTests
{
    public static int Run()
    {
        int n = 0;
        void Check(bool ok) { n++; if (!ok) throw new Exception("Clinical resident preservation regression."); }
        void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        string previous = new('A', 64), status = new('B', 64);
        string immobilized = ResidentMoveProfiles.BindMobility(previous, status, true);
        Check(immobilized != previous && immobilized != ResidentMoveProfiles.BindMobility(previous, status, false));
        Check(immobilized != ResidentMoveProfiles.BindMobility(new string('C', 64), status, true));
        Check(immobilized != ResidentMoveProfiles.BindMobility(previous, new string('D', 64), true));
        Reject(() => ResidentMoveProfiles.BindMobility(previous, null, true));
        var set = ResidentMoveSet.Capture([new("patient", immobilized, 4)]);
        var same = new ResidentMoveActual("patient", "source", immobilized, 4);
        Check(set.ActionFor(same, "source", "target", false) == ResidentMoveAction.ChangeFaction);
        Reject(() => set.ActionFor(same with { Preserved = ResidentMoveProfiles.BindMobility(previous, status, false) }, "source", "target", true));
        Reject(() => set.ActionFor(same with { Preserved = ResidentMoveProfiles.BindMobility(new string('C', 64), status, true) }, "source", "target", true));
        string census = ResidentTransferCensus.Prepare("town", "source", "target", 8, set);
        var original = ResidentTransferCensus.Restore(census);
        var calls = new List<string> { JsonSerializer.Serialize(new ResidentAuthorityState("patient")) };
        var care = ResidentCareJournal.Prepare("patient", "source", "target", 4);
        var medical = new List<string> { care.Export() };
        ResidentTransferCensus.Check(census, original.Proof, "town", "source", "target", 8, set, calls, medical, false);
        Reject(() => ResidentTransferCensus.Check(census, original.Proof, "town", "source", "target", 7, set, calls, medical, false));
        Reject(() => ResidentTransferCensus.Prepare("town", "source", "target", 9, set));
        care.Begin("source", 4); medical[0] = care.Export(); calls[0] = ResidentTransferCensus.Begin(calls[0]);
        Reject(() => ResidentTransferCensus.RequireRecoverable(calls, medical));
        calls[0] = ResidentTransferCensus.Returned(calls[0]);
        Reject(() => ResidentTransferCensus.RequireRecoverable(calls, medical));
        care.RecordReturn("target", 1); medical[0] = care.Export();
        ResidentTransferCensus.RequireRecoverable(calls, medical);
        Reject(() => ResidentTransferCensus.Check(census, original.Proof, "town", "source", "target", 8, set, calls, medical, true, true));
        care.Consume("target", 1); medical[0] = care.Export();
        ResidentTransferCensus.Check(census, original.Proof, "town", "source", "target", 8, ResidentMoveSet.Restore(set.Export()), calls, medical, true, true);
        set.VerifyDestination([same with { Faction = "target" }], "source", "target");
        Reject(() => care.Consume("target", 1));
        Check(ResidentTransferCensus.Restore(census).Profile == 8 && ResidentCareJournal.Restore(medical[0]).State.OriginalCare == 4);
        return n;
    }
}
