using Honor.Contracts.Rules;

internal static class ResidentCareJournalTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool condition) { count++; if (!condition) throw new Exception("Resident care journal regression."); }
        void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        var journal = ResidentCareJournal.Prepare("pawn", "source", "target", 2);
        Reject(() => journal.RecordReturn("target", 1));
        journal.Begin("source", 2);
        Reject(() => journal.Begin("source", 2));
        journal = ResidentCareJournal.Restore(journal.Export());
        Reject(() => journal.MayRestore("target", 1));
        Check(!journal.MayRestore("target", 2));
        journal.RecordReturn("target", 1);
        journal = ResidentCareJournal.Restore(journal.Export());
        Reject(() => journal.MayRestore("target", 3));
        Check(journal.MayRestore("target", 1));
        journal.Consume("target", 1);
        journal = ResidentCareJournal.Restore(journal.Export());
        Reject(() => journal.MayRestore("target", 1));
        Check(!journal.MayRestore("target", 2));
        var unchanged = ResidentCareJournal.Prepare("second", "source", "target", 2);
        unchanged.Begin("source", 2); unchanged.RecordReturn("target", 2);
        unchanged.SealUnchanged("target", 2);
        unchanged = ResidentCareJournal.Restore(unchanged.Export());
        Reject(() => unchanged.MayRestore("target", 1));
        Reject(() => unchanged.SealUnchanged("target", 2));
        Reject(() => ResidentCareJournal.Prepare("pawn", "same", "same", 2));
        return count;
    }
}
