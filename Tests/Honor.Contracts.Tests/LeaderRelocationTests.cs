using Honor.Contracts.Rules;

internal static class LeaderRelocationTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool v, string name) { count++; if (!v) { Console.Error.WriteLine("FAIL LeaderRelocation: " + name); throw new Exception(name); } }
        void Reject(Action a, string name) { bool rejected = false; try { a(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        string a = new('A', 64), b = new('B', 64), c = new('C', 64);
        var actor = new ResidentMoveActual("leader", "native", a, 2);
        string[] source = ["leader", "resident"], destination = ["other"];
        LeaderRelocationPlan Make(string[] s, string[] d, ResidentMoveActual p = null) => LeaderRelocationPlan.Capture("charter", "leader", "native", "source", "destination", "book",
            p ?? actor, b, 0, c, 1, s, d, 10);
        var plan = Make(source, destination);
        LeaderRelocationAction Review(string[] s, string[] d, ResidentMoveActual p = null, string leader = "leader", string owner = "native", string book = "book", string stock = null, int format = 0) =>
            plan.Review(p ?? actor, leader, owner, "native", book, stock ?? b, format, c, 1, s, d);
        Check(Review(source, destination) == LeaderRelocationAction.AddDestination, "fresh separate retention agreement permits only residency copy");
        Check(Review(source, ["leader", "other"]) == LeaderRelocationAction.RemoveSource, "partial double-reference state recovers forward by removing only source residency");
        Check(Review(["resident"], ["other", "leader"]) == LeaderRelocationAction.Verify, "completed leader keeps faction and office");
        Check(Review(source.AsEnumerable().Reverse().ToArray(), destination) == LeaderRelocationAction.AddDestination, "roster identity set is canonical");
        source[0] = "replacement";
        Check(Review(["leader", "resident"], destination) == LeaderRelocationAction.AddDestination, "input array cannot rewrite saved agreement");
        source = ["leader", "resident"];
        Check(LeaderRelocationPlan.Restore(plan.Export()).Export() == plan.Export(), "pending agreement roundtrip retains exact scope");
        Reject(() => Review(["resident"], destination), "missing from both towns is not permission to recreate or reinsert leader");
        Reject(() => Review(source, ["other", "leader", "leader"]), "duplicate destination references rejected");
        Reject(() => Review(["leader", "leader", "resident"], destination), "duplicate source rejected");
        Reject(() => Review(["leader"], destination), "unrelated source resident loss cannot be repaired");
        Reject(() => Review(source, []), "unrelated destination resident loss blocks");
        Reject(() => Review(source, ["other", "unexpected"]), "new destination resident invalidates agreement");
        Reject(() => Review(source, destination, leader: "successor"), "unrelated succession cannot be overwritten");
        Reject(() => Review(source, destination, owner: "foreign"), "foreign city ownership invalidates retention");
        Reject(() => Review(source, destination, book: "changed"), "charter terms/debts/history are bound without mutation");
        Reject(() => Review(source, destination, stock: c), "stock is not generated or repaired to pass relocation");
        Reject(() => Review(source, destination, format: 1), "stock profiles remain distinct");
        foreach (var change in new[] { actor with { Id = "clone" }, actor with { Faction = "new-faction" }, actor with { Preserved = c }, actor with { MedicalCare = 3 }, actor with { MedicalCare = null } })
            Reject(() => Review(source, destination, change), "identity/faction/clinical/care changes block without healing or recruitment");
        Reject(() => Make(source, ["leader"]), "existing destination leader is not a fresh one-holder plan");
        Reject(() => Make(source, ["resident"]), "other cross-town overlapping references rejected");
        Reject(() => Make(["resident"], destination), "leader must actually be in source");
        Reject(() => Make(source, destination, actor with { Faction = "foreign" }), "capture cannot replace native affiliation");
        Reject(() => Make(Enumerable.Range(0, 129).Select(i => i == 0 ? "leader" : "p" + i).ToArray(), destination), "bounded source roster");
        Reject(() => Make(source, Enumerable.Range(0, 128).Select(i => "d" + i).ToArray()), "destination reserves room before adding original");
        Reject(() => LeaderRelocationPlan.Restore(plan.Export().Replace(plan.Proof, a)), "altered agreement digest rejected");
        Reject(() => LeaderRelocationPlan.Restore(plan.Export().Replace("\"Version\":1", "\"Version\":2")), "unknown persisted plan rejected");
        var solo = Make(["leader"], []);
        Check(solo.Review(actor, "leader", "native", "native", "book", b, 0, c, 1, [], ["leader"]) == LeaderRelocationAction.Verify,
            "last saved resident can leave while both cities and native faction remain intact");
        Reject(() => plan.Review(actor, "leader", "native", "other-owner", "book", b, 0, c, 1, source, destination), "destination custody cannot be foreign");
        Reject(() => plan.Review(actor, "leader", "native", "native", "book", b, 0, b, 1, source, destination), "destination stock changes block independently");
        return count;
    }
}
