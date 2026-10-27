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
        // A pending prisoner may have both original surgery custody and a separately observed host reset.
        string proof = new('A', 64);
        var surgery = SurgeryOrderSet.Capture([new SurgeryOrder("bill", "InstallArm", "Human", 10, "Arm", proof)]);
        var original = surgery.Orders.ToArray();
        var prisoner = new PrisonerEvidence("captive", "original-faction", proof, 17.5f, 3f);
        var target = new PrisonerActual("captive", "original-faction", "target", proof, 21f, 5f);
        var reset = new PrisonerNativeReset("target", 21f, 5f);
        var pending = ResidentCareJournal.Prepare("captive", "source", "target", 2);
        Check(surgery.Review(original, [], false) == SurgeryCustodyAction.Detach);
        pending.Begin("source", 2);
        pending = ResidentCareJournal.Restore(pending.Export());
        surgery = SurgeryOrderSet.Restore(surgery.Export());
        Reject(() => pending.MayRestore("target", 1));
        Reject(() => PrisonerPreservation.NeedsRestore(prisoner, target, "source", "target", null));
        Check(surgery.Review([], original, true) == SurgeryCustodyAction.Restore);
        // A separate operation records the return in its original call scope; never upgrade the interrupted one.
        var returned = ResidentCareJournal.Prepare("captive", "source", "target", 2);
        returned.Begin("source", 2); returned.RecordReturn("target", 1);
        Check(PrisonerPreservation.NeedsRestore(prisoner, target, "source", "target", reset));
        returned.Consume("target", 1);
        returned = ResidentCareJournal.Restore(returned.Export());
        Reject(() => returned.MayRestore("target", 1));
        surgery.VerifyAttached(original, []);
        Check(!returned.MayRestore("target", 2));
        return count;
    }
}
