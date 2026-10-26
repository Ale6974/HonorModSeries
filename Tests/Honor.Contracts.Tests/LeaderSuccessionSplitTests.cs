using System.Text.Json;
using Honor.Contracts.Rules;

internal static class LeaderSuccessionSplitTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception("Split succession: " + name); }
        void Reject(Action action, string name) { bool failed = false; try { action(); } catch (ArgumentException) { failed = true; } Check(failed, name); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("territory/src", "source", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        book.Offer("c", new("p", "territory/src", 137, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .23m), 0, PoliticalWorld.Day, obs);
        book.Sign("c", true, 0, obs); book.Sign("c", false, 0, obs);
        var transfers = new TerritoryTransferLedger(); string fp = new('A', 64), stock = new('B', 64);
        var plan = new LeaderJoinPlan("c", "leader", "source", "target", "src", "dst", "steward", book.Export(), transfers.Export(), new("leader", fp, 4), stock, stock,
            new[] { "leader", "citizen" }, new[] { "destination-citizen" }, 10, true, SeparateSuccession: true);
        var source = new ResidentMoveActual("leader", "source", fp, 4);
        LeaderJoinAction Review(LeaderJoinExecution j, ResidentMoveActual actor, string ruler, long now = 20, string[] src = null, string[] dst = null)
            => j.Review(actor, ruler, "steward", book.Export(), transfers.Export(), stock, stock, src ?? plan.SourceRoster.ToArray(), dst ?? plan.DestinationRoster.ToArray(), now);
        var j = LeaderJoinExecution.Prepare(plan);
        Check(Review(j, source, "leader") == LeaderJoinAction.ChangeOffice, "new preparation resigns office before any guest/faction callbacks");
        Reject(() => j.Begin(20), "cannot begin affiliation without actual source succession proof");
        var cancel = LeaderJoinExecution.Restore(j.Export()); cancel.Cancel(Review(cancel, source, "leader"), 20);
        Check(cancel.State.Phase == LeaderJoinPhase.Cancelled, "unchanged split preparation can still cancel");
        j.BeginSuccession(20); string officeApplying = j.Export();
        Reject(() => Review(j, source, "leader"), "uncertain unchanged source office is not authority to retry resignation");
        Reject(() => j.BeginSuccession(20), "do not replay a started source succession");
        Reject(() => j.Cancel(LeaderJoinAction.ChangeOffice, 20), "cannot cancel actual office writes as unchanged");
        Reject(() => Review(j, source with { MedicalCare = 2 }, "successor"), "office stage cannot invent an affiliation-induced care reset");
        j.RecordSuccessionReturn("successor", 21);
        Check(Review(j, source, "successor", 21) == LeaderJoinAction.ObserveSuccession, "positive returned source office is separate from actor affiliation");
        Reject(() => Review(j, source, "other-ruler", 21), "never replace the actual returned source successor");
        Reject(() => Review(j, source with { Faction = "target" }, "successor", 21), "external affiliation before own affiliation step is not silently attributed");
        j.ObserveSuccession(Review(j, source, "successor", 21), "successor", 21);
        var resumed = LeaderJoinExecution.Restore(j.Export());
        Check(resumed.State.Phase == LeaderJoinPhase.SuccessionRecorded && resumed.State.Succession.FromNormalReturn && resumed.State.NativeReturn == null &&
            Review(resumed, source, "successor", 21) == LeaderJoinAction.ChangeFaction, "saved source office permits only the not-yet-started affiliation call");
        Reject(() => resumed.RecordSuccessionReturn("successor", 21), "normal office-return receipt cannot be written twice");
        Reject(() => Review(resumed, source, "later-ruler", 21), "recorded succession cannot follow a later unrelated ruler");
        resumed.Begin(22);
        Reject(() => Review(resumed, source, "successor", 22), "uncertain affiliation still cannot replay guest/faction callbacks even with office proof");
        var target = source with { Faction = "target", MedicalCare = 2 };
        resumed.RecordReturn("successor", 2, 23);
        resumed.Observe(Review(resumed, target, "successor", 23), "successor", 2, 23);
        resumed.SealCare(Review(resumed, target, "successor", 23), 23); target = target with { MedicalCare = 4 };
        string[] added = { "destination-citizen", "leader" };
        Check(Review(resumed, target, "successor", 23) == LeaderJoinAction.AddDestination &&
            Review(resumed, target, "successor", 23, dst: added) == LeaderJoinAction.RemoveSource, "split succession retains exact add-before-remove recovery boundaries");
        resumed.Finish(Review(resumed, target, "successor", 24, new[] { "citizen" }, added), 24);
        Check(resumed.State.Phase == LeaderJoinPhase.Completed && LeaderJoinExecution.Restore(resumed.Export()).Export() == resumed.Export() &&
            resumed.State.Succession.FromNormalReturn && resumed.State.FromNormalReturn, "both native return lineages survive completion without duplicate succession");
        var uncertainOffice = LeaderJoinExecution.Restore(officeApplying);
        uncertainOffice.ObserveSuccession(Review(uncertainOffice, source, "successor", 21), "successor", 21);
        Check(!uncertainOffice.State.Succession.FromNormalReturn && uncertainOffice.State.SuccessionReturn == null &&
            Review(uncertainOffice, source, "successor", 21) == LeaderJoinAction.ChangeFaction, "positive current office recovery never fabricates a completed resignation call");
        uncertainOffice.Begin(22);
        uncertainOffice.Observe(Review(uncertainOffice, target, "successor", 23), "successor", 4, 23);
        Check(!uncertainOffice.State.FromNormalReturn && !uncertainOffice.State.Succession.FromNormalReturn, "each uncertain native call keeps its own observation attribution");
        var noSuccessor = LeaderJoinExecution.Restore(officeApplying); noSuccessor.RecordSuccessionReturn(null, 21);
        Reject(() => Review(noSuccessor, source, "later-ruler", 22), "null returned office cannot be filled later");
        var saved = JsonSerializer.Deserialize<LeaderJoinSave>(resumed.Export());
        void Bad(LeaderJoinSave changed, string name) => Reject(() => LeaderJoinExecution.Restore(JsonSerializer.Serialize(changed)), name);
        Bad(saved with { Succession = null }, "split affiliation/completion requires original office receipt");
        Bad(saved with { Plan = saved.Plan with { SeparateSuccession = false } }, "legacy journals cannot adopt a fabricated split lineage");
        Bad(saved with { Succession = saved.Succession with { FromNormalReturn = false } }, "source call attribution cannot contradict its actual receipt");
        Bad(saved with { Succession = saved.Succession with { Successor = "another" } }, "source and affiliation receipts must name the same exact successor");
        Bad(saved with { Succession = saved.Succession with { Tick = 9 } }, "source receipt cannot predate preparation/return");
        Bad(saved with { NativeReturn = saved.NativeReturn with { Tick = 20 } }, "affiliation return cannot predate recorded source succession");
        Check(book.Export() == plan.Book && transfers.Export() == plan.Transfers, "split execution never changes cities, original contracts, stock or debts");
        return count;
    }
}
