using Honor.Contracts.Rules;

internal static class LeaderCargoRelocationTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Leader cargo relocation: " + name); }
        void Reject(Action a, string name) { bool bad = false; try { a(); } catch (ArgumentException) { bad = true; } Check(bad, name); }
        string evidence = new('A', 64);
        string Cargo(string animal, string item, int amount = 40, bool unload = false, bool sale = false) => CityStockProof.CapturePackedAnimals(true, true, 100,
            [], [new(animal, "PackAnimal", "native", evidence, evidence, evidence)],
            [new(animal, [new(item, "Silver", null, amount, 10, null)], unload, sale ? [] : [item], [])]);
        string src = Cargo("source-animal", "source-silver"), dst = Cargo("destination-animal", "destination-silver");
        var actor = new ResidentMoveActual("leader", "native", evidence, 2);
        var plan = LeaderRelocationPlan.CaptureProfiled("charter", "leader", "native", "source", "destination", "book", actor,
            src, 2, dst, 2, ["leader", "resident"], ["other"], 10, 7);
        LeaderRelocationAction Review(string[] s, string[] d, string stock = null, int format = 2) => plan.Review(actor, "leader", "native", "native", "book",
            stock ?? src, format, dst, 2, s, d);
        Check(Review(["leader", "resident"], ["other"]) == LeaderRelocationAction.AddDestination, "supported original animal cargo in both towns permits only same-faction leader residency");
        Check(Review(["leader", "resident"], ["other", "leader"]) == LeaderRelocationAction.RemoveSource, "exact double-reference state recovers forward without moving stock or animals");
        Check(Review(["resident"], ["other", "leader"]) == LeaderRelocationAction.Verify, "finished relocation retains the same leader/faction and all original cargo proofs");
        Check(LeaderRelocationPlan.Restore(plan.Export()).Export() == plan.Export(), "both explicit stock2 profiles survive persistence with the exact original agreement");
        Reject(() => Review(["leader", "resident"], ["other"], Cargo("source-animal", "source-silver", 39)), "lost or changed cargo is never regenerated to finish relocation");
        Reject(() => Review(["leader", "resident"], ["other"], Cargo("source-animal", "source-silver", unload: true)), "original unload flag remains binding");
        Reject(() => Review(["leader", "resident"], ["other"], Cargo("source-animal", "source-silver", sale: true)), "original same-inventory sale references remain binding");
        Reject(() => Review(["leader", "resident"], ["other"], format: 1), "stock profiles cannot be relabeled during confirmation or recovery");
        Reject(() => LeaderRelocationPlan.Restore(plan.Export().Replace("\"SourceFormat\":2", "\"SourceFormat\":1")), "changing saved stock profile invalidates the original agreement proof");
        Reject(() => LeaderRelocationPlan.CaptureProfiled("charter", "leader", "native", "source", "destination", "book", actor,
            src, 3, dst, 2, ["leader"], [], 10, 7), "unknown complex stock formats remain blocked");
        Check(plan.EffectiveResidentProfile == 7 && actor.Faction == "native" && src == Cargo("source-animal", "source-silver"), "actor genetics profile and native faction remain separate from original animal/item identity evidence");
        return count;
    }
}
