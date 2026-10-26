using System.Text.Json;
using Honor.Contracts.Rules;

internal static class LeaderJoiningTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Leader joining: " + name); }
        void Reject(Action action, string name) { bool failed = false; try { action(); } catch (ArgumentException) { failed = true; } Check(failed, name); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("territory/src", "source", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        book.Offer("c", new("p", "territory/src", 137, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .23m), 0, PoliticalWorld.Day, obs);
        book.Sign("c", true, 0, obs); book.Sign("c", false, 0, obs);
        var transfers = new TerritoryTransferLedger(); string fp = new('A', 64), stock = new('B', 64);
        var plan = new LeaderJoinPlan("c", "old-leader", "source", "target", "src", "dst", "steward", book.Export(), transfers.Export(),
            new("old-leader", fp, 4), stock, stock, new[] { "old-leader", "citizen" }, new[] { "destination-citizen" }, 10, true);
        var actor = new ResidentMoveActual(plan.Leader, plan.SourceFaction, fp, 4);
        LeaderJoinAction Review(LeaderJoinExecution j, ResidentMoveActual current, string ruler, string[] source = null, string[] destination = null, long now = 20)
            => j.Review(current, ruler, "steward", book.Export(), transfers.Export(), stock, stock, source ?? new[] { "old-leader", "citizen" }, destination ?? new[] { "destination-citizen" }, now);
        Reject(() => LeaderJoinExecution.Prepare(plan with { PersonalConsent = false }), "a city/merger signature cannot become personal joining consent");
        Reject(() => LeaderJoinExecution.Prepare(plan with { TargetFaction = "player" }), "native player control is never the foreign leader destination");
        Reject(() => LeaderJoinExecution.Prepare(plan with { DestinationRoster = new[] { "old-leader" } }), "no initial duplicate residency");
        var j = LeaderJoinExecution.Prepare(plan);
        string legacy = JsonSerializer.Serialize(new { Version = 1, Plan = new { plan.Charter, plan.Leader, plan.SourceFaction, plan.TargetFaction, plan.SourceCity, plan.DestinationCity,
            plan.TargetLeader, plan.Book, plan.Transfers, plan.Actor, plan.SourceStock, plan.DestinationStock, plan.SourceRoster, plan.DestinationRoster, plan.Tick, plan.PersonalConsent },
            Phase = LeaderJoinPhase.Prepared, NativeReturn = (LeaderJoinReturn)null, Successor = (string)null, ObservedCare = (int?)null, ObservedTick = (long?)null,
            FromNormalReturn = false, FinishedTick = (long?)null, CareCheckTick = (long?)null });
        Check(j.Export() == legacy && LeaderJoinExecution.Restore(legacy).Export() == legacy && (int)LeaderJoinPhase.Cancelled == 4,
            "legacy prepared bytes, default omitted split fields and phase values remain unchanged");
        Check(Review(j, actor, "old-leader") == LeaderJoinAction.ChangeFaction && book.Export() == plan.Book && transfers.Export() == plan.Transfers, "preparation is not actual affiliation, succession or city ownership");
        Reject(() => j.Review(actor, "old-leader", "other-steward", book.Export(), transfers.Export(), stock, stock, plan.SourceRoster, plan.DestinationRoster, 20),
            "changed destination authority cannot inherit the personal agreement");
        var changedBook = CharterBook.Restore(book.Export()); changedBook.Register(new("third-party", "other", false), 1);
        Reject(() => j.Review(actor, "old-leader", "steward", changedBook.Export(), transfers.Export(), stock, stock, plan.SourceRoster, plan.DestinationRoster, 20),
            "new third-party book rights require a fresh review rather than silent preservation claims");
        Reject(() => j.Review(actor, "old-leader", "steward", book.Export(), transfers.Export(), new string('C', 64), stock, plan.SourceRoster, plan.DestinationRoster, 20),
            "changed source goods are not reconstructed or accepted as old stock");
        var cancel = LeaderJoinExecution.Restore(j.Export()); cancel.Cancel(Review(cancel, actor, "old-leader"), 20);
        Check(cancel.State.Phase == LeaderJoinPhase.Cancelled, "only wholly unchanged prepared joining may cancel");
        j.Begin(20); string applying = j.Export();
        Reject(() => Review(j, actor, "old-leader"), "uncertain pre-affiliation native return cannot replay SetFaction");
        Reject(() => Review(j, actor, "successor"), "succession alone with actor still source cannot be called joining complete");
        Reject(() => j.Cancel(LeaderJoinAction.ChangeFaction, 20), "applying cannot falsely cancel as unchanged");
        var arrived = actor with { Faction = "target", MedicalCare = 2 };
        Reject(() => Review(j, arrived, "successor"), "unobserved care reset cannot be repaired after an uncertain call");
        j.RecordReturn("successor", 2, 20);
        Check(Review(j, arrived, "successor") == LeaderJoinAction.Observe, "normal native return with positive destination and actual successor can be observed");
        Reject(() => j.RecordReturn("other", 2, 20), "native-return receipt cannot overwrite original successor");
        Reject(() => Review(j, arrived, "old-leader"), "old source office cannot remain on foreign actor");
        Reject(() => Review(j, arrived with { Preserved = new string('C', 64) }, "successor"), "health, gear, identity, relations or needs changes are never repaired");
        Reject(() => Review(j, arrived, "different-successor"), "current office must match exact normally observed source successor");
        j.Observe(Review(j, arrived, "successor"), "successor", 2, 20);
        Check(j.State.FromNormalReturn && Review(j, arrived, "successor") == LeaderJoinAction.RestoreCare, "only recorded actual reset permits restoring original care");
        j.SealCare(LeaderJoinAction.RestoreCare, 20);
        Reject(() => Review(j, arrived, "successor"), "consumed care permission does not replay a failed/uncertain field write");
        arrived = arrived with { MedicalCare = 4 };
        Check(Review(j, arrived, "successor") == LeaderJoinAction.AddDestination, "verified original care precedes residency changes");
        var added = new[] { "destination-citizen", "old-leader" };
        Check(Review(j, arrived, "successor", destination: added) == LeaderJoinAction.RemoveSource, "double-reference safe boundary recovers forward without pawn recreation");
        var reloaded = LeaderJoinExecution.Restore(j.Export());
        Check(Review(reloaded, arrived, "successor", destination: added) == LeaderJoinAction.RemoveSource, "observed successor and consumed care permission survive reload");
        Reject(() => Review(reloaded, arrived with { MedicalCare = 2 }, "successor", destination: added), "later user/natural care change matching the old reset is not overwritten");
        Reject(() => Review(j, arrived, "successor", source: new[] { "citizen" }), "missing from both towns is not inferred safe arrival");
        var verified = Review(j, arrived, "successor", new[] { "citizen" }, added);
        Check(verified == LeaderJoinAction.Verify, "exact destination and original source minus actor verify actual saved association");
        j.Finish(verified, 21);
        Check(j.State.Phase == LeaderJoinPhase.Completed && LeaderJoinExecution.Restore(j.Export()).Export() == j.Export(), "completed joining and native succession evidence survive save");
        Check(book.Export() == plan.Book && transfers.Export() == plan.Transfers, "personal joining does not release charters, forgive debt or merge/transfer cities");
        var uncertain = LeaderJoinExecution.Restore(applying); var currentTarget = actor with { Faction = "target" };
        uncertain.Observe(Review(uncertain, currentTarget, "successor"), "successor", 4, 20);
        Check(!uncertain.State.FromNormalReturn && uncertain.State.NativeReturn == null, "positive current outcome recovery does not fabricate a completed native call");
        uncertain.SealCare(LeaderJoinAction.AddDestination, 20);
        uncertain.Finish(Review(uncertain, currentTarget, "successor", new[] { "citizen" }, added), 20);
        var failedSuccession = LeaderJoinExecution.Restore(applying); failedSuccession.RecordReturn(null, 4, 20);
        Reject(() => Review(failedSuccession, currentTarget, "later-leader"), "missing native successor is retained, not silently filled with a later unrelated ruler");
        var save = JsonSerializer.Deserialize<LeaderJoinSave>(j.Export());
        Reject(() => LeaderJoinExecution.Restore(JsonSerializer.Serialize(save with { Successor = "old-leader" })), "old actor cannot be their own source successor");
        Reject(() => LeaderJoinExecution.Restore(JsonSerializer.Serialize(save with { CareCheckTick = null })), "completion must retain consumed original-care proof");
        Reject(() => LeaderJoinExecution.Restore(JsonSerializer.Serialize(save with { FinishedTick = 19 })), "completion cannot precede native observation");
        Reject(() => LeaderJoinExecution.Restore(JsonSerializer.Serialize(save with { FromNormalReturn = false })), "normal-return attribution cannot contradict its immutable receipt");
        return count;
    }
}
