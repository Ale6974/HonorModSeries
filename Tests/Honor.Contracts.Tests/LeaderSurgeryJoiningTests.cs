using Honor.Contracts.Rules;

internal static class LeaderSurgeryJoiningTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Leader surgery: " + name); }
        void Reject(Action run, string name) { bool bad = false; try { run(); } catch (ArgumentException) { bad = true; } Check(bad, name); }
        string baseline = new('A', 64), stock = new('B', 64);
        var orders = SurgeryOrderSet.Capture([new("Bill_1", "InstallArm", "Human", 10, "Arm", baseline), new("Bill_2", "Anesthetize", "Human", -1, null, stock)]);
        string Fingerprint(SurgeryOrderSet s) => ResidentMoveProfiles.Compose(baseline, s.Proof, 7, baseline, baseline, ResidentGenetics.None);
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("territory/src", "source", false), 0);
        var observed = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        book.Offer("c", new("p", "territory/src", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, observed);
        book.Sign("c", true, 0, observed); book.Sign("c", false, 0, observed);
        var transfers = new TerritoryTransferLedger();
        var plan = new LeaderJoinPlan("c", "leader", "source", "target", "src", "dst", "steward", book.Export(), transfers.Export(),
            new("leader", Fingerprint(orders), 4), stock, stock, ["leader", "citizen"], ["other"], 10, true, SeparateSuccession: true)
            { ResidentProfile = 7, SurgeryOrders = orders.Export() };
        var j = LeaderJoinExecution.Prepare(plan); var actor = new ResidentMoveActual("leader", "source", plan.Actor.Preserved, 4);
        LeaderJoinAction Review(LeaderJoinExecution x, ResidentMoveActual a, string chief, string[] src = null, string[] dst = null) =>
            x.Review(a, chief, "steward", book.Export(), transfers.Export(), stock, stock, src ?? plan.SourceRoster.ToArray(), dst ?? plan.DestinationRoster.ToArray(), 20);
        Check(LeaderJoinExecution.Restore(j.Export()).State.Plan.SurgeryOrders == orders.Export(), "exact ordered medical intent persists with the personal agreement");
        Reject(() => LeaderJoinExecution.Prepare(plan with { ResidentProfile = 0 }), "surgery cannot be backfilled onto an old clinical profile");
        Reject(() => LeaderJoinExecution.Prepare(plan with { SeparateSuccession = false }), "combined legacy calls cannot acquire surgical custody");
        Reject(() => LeaderJoinExecution.Prepare(plan with { SurgeryOrders = orders.Export() + " " }), "noncanonical snapshots rejected");
        Reject(() => LeaderJoinExecution.Prepare(plan with { SurgeryOrders = "{}" }), "malformed original orders rejected");
        Check(Review(j, actor, "leader") == LeaderJoinAction.ChangeOffice, "supported original orders do not imply resignation already occurred");
        j.BeginSuccession(20); j.RecordSuccessionReturn("successor", 20);
        j.ObserveSuccession(Review(j, actor, "successor"), "successor", 20);
        var attached = orders.Orders.ToArray();
        Check(orders.Review(attached, [], false) == SurgeryCustodyAction.Detach, "source originals must leave the native stack before affiliation");
        j = LeaderJoinExecution.Restore(j.Export()); orders = SurgeryOrderSet.Restore(j.State.Plan.SurgeryOrders);
        Check(Review(j, actor, "successor") == LeaderJoinAction.ChangeFaction && orders.Review([], attached, false) == SurgeryCustodyAction.ChangeFaction,
            "interruption after detachment retains separate succession and original custody without repeating resignation");
        Reject(() => j.Cancel(Review(j, actor, "successor"), 20), "post-succession custody is forward recovery rather than a fictitious unchanged cancellation");
        j.Begin(20); j = LeaderJoinExecution.Restore(j.Export());
        Reject(() => Review(j, actor, "successor"), "uncertain source-side affiliation never replays SetFaction even when bills are preserved");
        var target = actor with { Faction = "target", MedicalCare = 2 };
        Reject(() => Review(j, target, "successor"), "uncertain affiliation cannot invent permission to repair a changed medical-care setting");
        target = target with { MedicalCare = 4 };
        j.Observe(Review(j, target, "successor"), "successor", 4, 20);
        Check(!j.State.FromNormalReturn && j.State.NativeReturn == null && j.State.Succession.FromNormalReturn,
            "positive target observation does not fabricate a normal affiliation return");
        Check(orders.Review([], attached, true) == SurgeryCustodyAction.Restore, "observed target still requires original bill reattachment");
        Reject(() => orders.VerifyAttached([], attached), "detached originals cannot be discarded at completion");
        var altered = attached.ToArray(); altered[0] = altered[0] with { PartIndex = 11 };
        Reject(() => orders.Review([], altered, true), "changed anatomy cannot be repaired to complete joining");
        Reject(() => Review(j, target with { Preserved = Fingerprint(SurgeryOrderSet.Capture(altered)) }, "successor"), "changed medical configuration invalidates the actor fingerprint");
        orders.VerifyAttached(attached, []);
        Check(orders.Review(attached, [], true) == SurgeryCustodyAction.Verify, "recovery of already attached bills does not duplicate orders");
        j.SealCare(Review(j, target, "successor"), 20); target = target with { MedicalCare = 4 };
        j.Finish(Review(j, target, "successor", ["citizen"], ["other", "leader"]), 20);
        var done = LeaderJoinExecution.Restore(j.Export());
        Check(done.State.Phase == LeaderJoinPhase.Completed && done.State.Plan.SurgeryOrders == orders.Export() && done.State.Plan.Actor.Preserved == plan.Actor.Preserved,
            "separate office/affiliation/care/roster completion retains ordered medical evidence without old contract changes");
        var cancelled = LeaderJoinExecution.Prepare(plan);
        cancelled.Cancel(Review(cancelled, actor, "leader"), 20);
        Check(cancelled.State.Phase == LeaderJoinPhase.Cancelled && orders.Review(attached, [], true) == SurgeryCustodyAction.Verify,
            "unchanged prepared cancellation keeps all original orders on the source actor");
        return count;
    }
}
