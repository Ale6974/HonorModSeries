using System.Text.Json;
using Honor.Contracts.Rules;

internal static class UnassignedLeaderJoinTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string text) { count++; if (!ok) throw new Exception("Unassigned joining: " + text); }
        void Reject(Action call, string text) { bool rejected = false; try { call(); } catch (ArgumentException) { rejected = true; } Check(rejected, text); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("territory/src", "source", false), 0);
        var observations = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        book.Offer("c", new("p", "territory/src", 137, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .23m), 0, PoliticalWorld.Day, observations);
        book.Sign("c", true, 0, observations); book.Sign("c", false, 0, observations);
        var transfers = new TerritoryTransferLedger(); string proof = new('A', 64), stock = new('B', 64);
        foreach (var source in new[] { Array.Empty<string>(), new[] { "source-citizen" } })
        {
            var plan = new LeaderJoinPlan("c", "leader", "source", "target", "src", "dst", "steward", book.Export(), transfers.Export(),
                new("leader", proof, 4), stock, stock, source, new[] { "destination-citizen" }, 10, true, SeparateSuccession: true, UnassignedSource: true);
            var actor = new ResidentMoveActual("leader", "source", proof, 4);
            LeaderJoinAction Review(LeaderJoinExecution j, ResidentMoveActual actual, string ruler, string[] src = null, string[] dst = null)
                => j.Review(actual, ruler, "steward", book.Export(), transfers.Export(), stock, stock, src ?? source, dst ?? plan.DestinationRoster.ToArray(), 20);
            Reject(() => LeaderJoinExecution.Prepare(plan with { UnassignedSource = false }), "Old assigned mode cannot claim an absent actor.");
            Reject(() => LeaderJoinExecution.Prepare(plan with { SeparateSuccession = false }), "Unassigned joining requires separately recorded native source succession.");
            Reject(() => LeaderJoinExecution.Prepare(plan with { SourceRoster = source.Append("leader").ToArray() }), "A real source resident cannot be relabelled unassigned.");
            Reject(() => LeaderJoinExecution.Prepare(plan with { DestinationRoster = new[] { "leader" } }), "Do not duplicate an already assigned destination actor.");
            var j = LeaderJoinExecution.Prepare(plan);
            Check(Review(j, actor, "leader") == LeaderJoinAction.ChangeOffice, "No source residence is created before native office change.");
            var cancel = LeaderJoinExecution.Restore(j.Export()); cancel.Cancel(Review(cancel, actor, "leader"), 20);
            Check(cancel.State.Phase == LeaderJoinPhase.Cancelled, "Unchanged preparation cancels without inventing a source association.");
            j.BeginSuccession(20); j.RecordSuccessionReturn("successor", 20);
            j.ObserveSuccession(Review(j, actor, "successor"), "successor", 20);
            j = LeaderJoinExecution.Restore(j.Export());
            Check(j.State.Plan.UnassignedSource && Review(j, actor, "successor") == LeaderJoinAction.ChangeFaction, "Reload retains absence evidence and only starts the not-yet-started affiliation.");
            j.Begin(20);
            Reject(() => Review(j, actor, "successor"), "Uncertain source-side affiliation never replays a native call.");
            var arrived = actor with { Faction = "target", MedicalCare = 2 }; j.RecordReturn("successor", 2, 20);
            j.Observe(Review(j, arrived, "successor"), "successor", 2, 20);
            Check(Review(j, arrived, "successor") == LeaderJoinAction.RestoreCare, "Recorded reset is restored even without original city residence.");
            j.SealCare(LeaderJoinAction.RestoreCare, 20); arrived = arrived with { MedicalCare = 4 };
            Check(Review(j, arrived, "successor") == LeaderJoinAction.AddDestination, "The same world actor is first added to the reviewed destination.");
            var added = plan.DestinationRoster.Append("leader").ToArray();
            Check(Review(j, arrived, "successor", dst: added) == LeaderJoinAction.Verify, "There is no fictitious source removal step.");
            j = LeaderJoinExecution.Restore(j.Export());
            Check(Review(j, arrived, "successor", dst: added) == LeaderJoinAction.Verify, "Actual destination addition recovers forward after reload.");
            Reject(() => Review(j, arrived, "successor", source.Append("leader").ToArray(), added), "Later source assignment blocks rather than deleting it.");
            Reject(() => Review(j, arrived, "successor", dst: added.Append("leader").ToArray()), "Duplicate destination association is never normalized.");
            Reject(() => Review(j, arrived, "successor", source.Append("new-citizen").ToArray(), added), "Unrelated source roster changes require a new review.");
            Reject(() => Review(j, arrived with { MedicalCare = 2 }, "successor", dst: added), "Consumed care reset permission is never replayed.");
            var saved = JsonSerializer.Deserialize<LeaderJoinSave>(j.Export());
            Reject(() => LeaderJoinExecution.Restore(JsonSerializer.Serialize(saved with { Plan = saved.Plan with { UnassignedSource = false } })), "A stored unassigned outcome cannot turn into invented source-resident evidence.");
            j.Finish(Review(j, arrived, "successor", dst: added), 20);
            Check(j.State.Phase == LeaderJoinPhase.Completed && LeaderJoinExecution.Restore(j.Export()).Export() == j.Export(), "Complete personal native outcome round-trips without a removed source resident.");
            Check(book.Export() == plan.Book && transfers.Export() == plan.Transfers, "Source town ownership, contracts, tribute and debt journals remain exact.");
        }
        return count;
    }
}
