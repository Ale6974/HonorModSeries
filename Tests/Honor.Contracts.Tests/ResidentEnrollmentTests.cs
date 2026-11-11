using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;
internal static class ResidentEnrollmentTests
{
    public static int Run()
    {
        int n = 0; void Check(bool b) { n++; if (!b) throw new Exception("Explicit civilian enrollment regression."); }
        void Reject(Action a) { bool rejected = false; try { a(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        var authority = new NpcRealmDecisionEvidence("source", "ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var binding = new ResidentMapBinding("new", "map", 1, 0);
        ResidentMoveEvidence[] selected = [new("new", ResidentMapped.Bind(Hash("new-clinical-area"), binding), 3), new("other", Hash("other-clinical"), null)];
        string book = Hash("original-contracts-debts-signatures");
        string journal = ResidentEnrollment.Prepare("charter", "town", "map", "source", book, authority, ["z", "a"], selected, 10);
        var state = ResidentEnrollment.Restore(journal);
        Check(state.Before.SequenceEqual(["z", "a"]) && state.Residents.SequenceEqual(selected) && !state.Complete && !state.Verified);
        Reject(() => ResidentEnrollment.Prepare("charter", "town", "map", "source", book, authority with { Regime = NpcDecisionRegime.Royal }, [], selected, 10));
        Reject(() => ResidentEnrollment.Prepare("charter", "town", "map", "source", book, authority with { Willing = false }, [], selected, 10));
        Reject(() => ResidentEnrollment.Prepare("charter", "town", "map", "source", book, authority with { Realm = "foreign" }, [], selected, 10));
        Reject(() => ResidentEnrollment.Prepare("charter", "town", "map", "source", book, authority, ["new"], selected, 10));
        Reject(() => ResidentEnrollment.Prepare("charter", "town", "map", "source", book, authority, ["z", "z"], selected, 10));
        Reject(() => ResidentEnrollment.Prepare("charter", "town", "map", "source", book, authority, [], [selected[0], selected[0]], 10));
        Reject(() => ResidentEnrollment.Prepare("charter", "town", "map", "source", book, authority, [], [], 10));
        ResidentEnrollment.Review(journal, ["z", "a"], authority, book);
        Reject(() => ResidentEnrollment.Review(journal, ["a", "z"], authority, book));
        Reject(() => ResidentEnrollment.Review(journal, ["z", "a"], authority with { Representative = "successor" }, book));
        Reject(() => ResidentEnrollment.Review(journal, ["z", "a"], authority, Hash("changed-contracts")));
        Reject(() => ResidentEnrollment.Returned(journal, ["z", "a", "new"]));
        string unknown = ResidentEnrollment.Begin(journal, ["z", "a"], authority, book);
        Reject(() => ResidentEnrollment.Begin(unknown, ["z", "a"], authority, book));
        Reject(() => ResidentEnrollment.Review(unknown, ["z", "a", "new"], authority, book));
        Reject(() => ResidentEnrollment.Returned(unknown, ["z", "a", "other"]));
        Check(ResidentEnrollment.Restore(unknown).Started && !ResidentEnrollment.Restore(unknown).Verified);
        journal = ResidentEnrollment.Returned(unknown, ["z", "a", "new"]);
        Check(ResidentEnrollment.Restore(journal).Added == 1 && !ResidentEnrollment.Restore(journal).Started);
        ResidentEnrollment.Review(journal, ["z", "a", "new"], authority, book);
        Reject(() => ResidentEnrollment.Finish(journal, selected));
        journal = ResidentEnrollment.Returned(ResidentEnrollment.Begin(journal, ["z", "a", "new"], authority, book), ["z", "a", "new", "other"]);
        Check(ResidentEnrollment.Restore(journal).Complete && !ResidentEnrollment.Restore(journal).Verified);
        Reject(() => ResidentEnrollment.Begin(journal, ["z", "a", "new", "other"], authority, book));
        Reject(() => ResidentEnrollment.Finish(journal, [selected[0] with { MedicalCare = 1 }, selected[1]]));
        Reject(() => ResidentEnrollment.Finish(journal, [selected[0], selected[1] with { Preserved = Hash("changed-health") }]));
        ResidentEnrollment.Review(journal, ["z", "a", "new", "other"], authority, book);
        string complete = ResidentEnrollment.Finish(journal, selected);
        Check(ResidentEnrollment.Restore(complete).Verified);
        Reject(() => ResidentEnrollment.Finish(complete, selected));
        Reject(() => ResidentEnrollment.Review(complete, ["z", "a", "new", "other"], authority, book));
        Check(ResidentEnrollment.Restore(complete).Book == book && ResidentEnrollment.Restore(complete).Authority == authority);
        // The new alias feeds the existing mapped clinical/census pipeline, never a substitute for its separate calls/care journal.
        var set = ResidentMoveSet.Capture([selected[0]]);
        string census = ResidentTransferCensus.Prepare("town", "source", "target", 11, set);
        string map = LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("grid"), CityStockProof.Capture(true, false, -1, []), 0, []);
        string mapped = LoadedMapPreservation.CaptureResidents(map, [new("new", selected[0].Preserved)]);
        ResidentMapped.Pair(ResidentMapped.Prepare([binding]), set, mapped);
        Check(ResidentTransferCensus.Restore(census).Residents == set.Export());
        return n;
    }
}
