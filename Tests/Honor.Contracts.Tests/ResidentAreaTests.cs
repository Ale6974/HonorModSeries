using System.Text.Json;
using Honor.Contracts.Rules;

internal static class ResidentAreaTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Original resident area regression."); }
        void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        ResidentAreaEntry[] original = [new("map-a", "area-a"), new("map-b", "area-b")];
        string journal = ResidentAreas.Prepare("resident", Enumerable.Reverse(original));
        string proof = ResidentAreas.Proof(journal);
        Check(ResidentAreas.Restore(journal).Original.SequenceEqual(original));
        Reject(() => ResidentAreas.Prepare("resident", [original[0], original[0]]));
        Reject(() => ResidentAreas.Prepare("resident", [new("map", null)]));
        Reject(() => ResidentAreas.Prepare("resident", Enumerable.Range(0, 129).Select(i => new ResidentAreaEntry("map" + i, "area" + i))));
        Reject(() => ResidentAreas.CheckCurrent(journal, []));
        journal = ResidentAreas.Begin(journal, original);
        Reject(() => ResidentAreas.Begin(journal, original));
        Reject(() => ResidentAreas.CheckCurrent(journal, []));
        Reject(() => ResidentAreas.BeginRestore(journal, original));
        Reject(() => ResidentAreas.Returned(journal, [new("foreign-map", "foreign-area")]));
        journal = ResidentAreas.Returned(journal, []);
        Check(ResidentAreas.Restore(journal).Cleared && ResidentAreas.Proof(journal) == proof);
        Reject(() => ResidentAreas.Returned(journal, []));
        Reject(() => ResidentAreas.CheckCurrent(journal, original)); // No inferred external restoration before our own applying intent.
        Reject(() => ResidentAreas.BeginRestore(journal, [new("map-new", "area-new")]));
        journal = ResidentAreas.BeginRestore(journal, []);
        ResidentAreas.CheckCurrent(journal, [original[0]]);
        journal = ResidentAreas.BeginRestore(JsonSerializer.Serialize(ResidentAreas.Restore(journal)), [original[0]]);
        Reject(() => ResidentAreas.CheckCurrent(journal, [original[1]]));
        Reject(() => ResidentAreas.Complete(journal, [original[0]]));
        journal = ResidentAreas.Complete(journal, original);
        Check(ResidentAreas.Restore(journal).Consumed && ResidentAreas.Proof(journal) == proof);
        Reject(() => ResidentAreas.Complete(journal, original));
        Reject(() => ResidentAreas.BeginRestore(journal, original));
        Reject(() => ResidentAreas.CheckCurrent(journal, [original[0]]));
        Reject(() => ResidentAreas.Restore(JsonSerializer.Serialize(ResidentAreas.Restore(journal) with { Started = false })));
        string unchanged = ResidentAreas.Prepare("same", original);
        unchanged = ResidentAreas.Begin(unchanged, original); unchanged = ResidentAreas.Returned(unchanged, original);
        Check(!ResidentAreas.Restore(unchanged).Cleared);
        unchanged = ResidentAreas.Complete(unchanged, original); Check(ResidentAreas.Restore(unchanged).Consumed);
        string empty = ResidentAreas.Begin(ResidentAreas.Prepare("empty", []), []);
        empty = ResidentAreas.Returned(empty, []); empty = ResidentAreas.Complete(empty, []);
        Check(ResidentAreas.Restore(empty).Consumed && !ResidentAreas.Restore(empty).Cleared);
        string clinical = new('A', 64);
        string bound = ResidentMoveProfiles.BindAreas(clinical, proof);
        Check(bound != clinical && bound != ResidentMoveProfiles.BindAreas(clinical, ResidentAreas.Proof(unchanged)));
        var set = ResidentMoveSet.Capture([new("resident", bound, 4)]);
        string census = ResidentTransferCensus.Prepare("town", "source", "target", 9, set);
        Check(ResidentTransferCensus.Restore(census).Profile == 9);
        var restored = ResidentMoveSet.Restore(set.Export());
        restored.VerifyDestination([new("resident", "target", bound, 4)], "source", "target");
        Reject(() => restored.VerifyDestination([new("resident", "target", clinical, 4)], "source", "target"));
        return n;
    }
}
