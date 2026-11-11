using System.Text;
using Honor.Contracts.Rules;
internal static class DeferredTownTransferTests
{
    public static int Run()
    {
        int n = 0; void Check(bool b) { n++; if (!b) throw new Exception("Deferred town execution regression."); }
        void Reject(Action a) { bool bad = false; try { a(); } catch (ArgumentException) { bad = true; } Check(bad); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        var authority = new NpcRealmDecisionEvidence("source", "ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        DeferredTownPerson[] people = [new("old", "Job_42", Hash("original-progress-observation")), new("unlisted", "Job_43", Hash("other-original-observation"))];
        string book = Hash("original-book");
        string pending = DeferredTownTransfer.Prepare("intent", "bond", "town", "map", "source", "target", book, authority, ["old"], ["historical"], people, 20);
        var s = DeferredTownTransfer.Restore(pending);
        Check(s.Phase == DeferredTownPhase.Waiting && s.People.SequenceEqual(people) && s.Before.SequenceEqual(["old"]) && s.PreviousTransfers.SequenceEqual(["historical"]));
        DeferredTownTransfer.Review(pending, book, authority, ["unlisted", "old"]);
        Reject(() => DeferredTownTransfer.Review(pending, Hash("changed-contract"), authority, ["old", "unlisted"]));
        Reject(() => DeferredTownTransfer.Review(pending, book, authority with { Representative = "new-ruler" }, ["old", "unlisted"]));
        Reject(() => DeferredTownTransfer.Review(pending, book, authority, ["old"]));
        Reject(() => DeferredTownTransfer.Review(pending, book, authority, ["old", "unlisted", "new-arrival"]));
        Reject(() => DeferredTownTransfer.Prepare("intent", "bond", "town", "map", "source", "target", book, authority with { Regime = NpcDecisionRegime.Union }, [], [], people, 20));
        Reject(() => DeferredTownTransfer.Prepare("intent", "bond", "town", "map", "source", "source", book, authority, [], [], people, 20));
        Reject(() => DeferredTownTransfer.Prepare("intent", "bond", "town", "map", "source", "target", book, authority, ["missing"], [], people, 20));
        Reject(() => DeferredTownTransfer.Prepare("intent", "bond", "town", "map", "source", "target", book, authority, [], ["dup", "dup"], people, 20));
        string cancelled = DeferredTownTransfer.Cancel(pending); Check(DeferredTownTransfer.Restore(cancelled).Phase == DeferredTownPhase.Cancelled);
        Reject(() => DeferredTownTransfer.Begin(cancelled, book, authority, ["old", "unlisted"]));
        string stopped = DeferredTownTransfer.Stop(pending); Check(DeferredTownTransfer.Restore(stopped).Phase == DeferredTownPhase.Stopped);
        Reject(() => DeferredTownTransfer.Begin(stopped, book, authority, ["old", "unlisted"]));
        string started = DeferredTownTransfer.Begin(pending, book, authority, ["old", "unlisted"]);
        Check(DeferredTownTransfer.Restore(started).Phase == DeferredTownPhase.Started);
        Reject(() => DeferredTownTransfer.Begin(started, book, authority, ["old", "unlisted"]));
        Reject(() => DeferredTownTransfer.Cancel(started));
        Check(DeferredTownTransfer.Restore(DeferredTownTransfer.Stop(started)).Phase == DeferredTownPhase.Stopped);
        var receipt = new TerritoryTransfer("actual", "town", "source", "source", "target", "bond", TerritoryTransferPurpose.ActiveCharter, 21, TerritoryTransferPhase.Applied, 22);
        Reject(() => DeferredTownTransfer.Applied(pending, receipt));
        Reject(() => DeferredTownTransfer.Applied(started, receipt with { Phase = TerritoryTransferPhase.Prepared }));
        Reject(() => DeferredTownTransfer.Applied(started, receipt with { Id = "historical" }));
        Reject(() => DeferredTownTransfer.Applied(started, receipt with { FromOwner = "captor" }));
        Reject(() => DeferredTownTransfer.Applied(started, receipt with { TargetOwner = "other" }));
        Reject(() => DeferredTownTransfer.Applied(started, receipt with { Charter = "other-bond" }));
        Reject(() => DeferredTownTransfer.Applied(started, receipt with { Territory = "other-town" }));
        Reject(() => DeferredTownTransfer.Applied(started, receipt with { PreparedTick = 19 }));
        Reject(() => DeferredTownTransfer.Applied(started, receipt with { ResolvedTick = null }));
        string applied = DeferredTownTransfer.Applied(started, receipt);
        Check(DeferredTownTransfer.Restore(applied).Phase == DeferredTownPhase.Applied && DeferredTownTransfer.Restore(applied).Transfer == receipt.Id);
        Reject(() => DeferredTownTransfer.Applied(applied, receipt));
        Check(DeferredTownTransfer.Restore(applied).People.SequenceEqual(people));
        return n;
    }
}
