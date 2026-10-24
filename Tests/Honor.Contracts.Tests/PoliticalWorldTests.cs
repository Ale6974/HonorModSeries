using Honor.Contracts.Rules;

internal static class PoliticalWorldTests
{
    public static int Run()
    {
        int count = 0;
        string State(PoliticalWorld w) => System.Text.Json.JsonSerializer.Serialize(new {
            w.Now, w.NextTurn, w.Domains, w.Contracts, w.Assessments, w.Attacks });
        void Check(bool value, string message) { count++; if (!value) throw new Exception(message); }
        void Reject(PoliticalWorld w, WorldCommand command, string message)
        {
            string before = w.Export(), stateBefore = State(w); bool rejected = false;
            try { w.Apply(command); } catch (ArgumentException) { rejected = true; }
            Check(rejected && w.Export() == before && State(w) == stateBefore, message + ": rejected command leaves state and journal intact");
        }
        long day = PoliticalWorld.Day;
        PoliticalWorld Seed(long turn = PoliticalWorld.Day)
        {
            var w = new PoliticalWorld(turn);
            foreach (var id in new[] { "player", "liege", "town", "enemy", "child", "grandchild", "other" })
                w.Apply(new(WorldAction.AddDomain, 0, id, id == "player" ? "native-player" : "native-" + id,
                    id is "town" or "other" ? "union" : null, Count: id == "town" ? 10 : 100, Flag: id == "player"));
            return w;
        }
        void Bind(PoliticalWorld w, string id, string subject, string liege, bool collection = false) =>
            w.Apply(new(WorldAction.Bind, w.Now, id, subject, liege, Value: 100, Interval: day * 5,
                Grace: day, Rate: .1m, Flag: collection));
        var hierarchy = Seed();
        Bind(hierarchy, "c1", "town", "liege"); Bind(hierarchy, "c2", "child", "town");
        Bind(hierarchy, "c3", "grandchild", "child");
        Check(hierarchy.Depth("grandchild") == 4 && hierarchy.DiplomaticGroup("grandchild") == "liege", "four levels share top diplomacy");
        Reject(hierarchy, new(WorldAction.Bind, 0, "fifth", "other", "grandchild", Value: 100, Interval: day), "fifth level");
        Reject(hierarchy, new(WorldAction.Bind, 0, "cycle", "liege", "grandchild", Value: 100, Interval: day), "cycle");
        hierarchy.Apply(new(WorldAction.Release, 0, Actor: "town", Target: "liege", Flag: true));
        Check(hierarchy.Domain("child").Parent == "town" && hierarchy.DiplomaticGroup("grandchild") == "free-city-union", "release carries subtree to union");
        Bind(hierarchy, "c4", "town", "player");
        Check(hierarchy.Domain("player").NativeFactionId == "native-player" && hierarchy.Domain("player").Player,
            "political affiliation cannot replace native player ownership");
        Reject(hierarchy, new(WorldAction.Bind, 0, "too-deep", "player", "other", Value: 100, Interval: day), "moving subtree depth");
        Reject(hierarchy, new(WorldAction.Release, 0, Actor: "town", Target: "player"), "release requires agreement");

        var tax = Seed(); Bind(tax, "delivery", "town", "liege"); Bind(tax, "collection", "other", "liege", true);
        Check(tax.Assessments.Single(b => b.Contract == "delivery").DeliveryValue == 90 &&
            tax.Assessments.Single(b => b.Contract == "collection").DeliveryValue == 110, "transport compensation is explicit both directions");
        Reject(tax, new(WorldAction.DeliverTribute, 0, "delivery@300000", "town", Evidence: "receipt", Value: 90), "no advance fulfillment");
        tax.Apply(new(WorldAction.Advance, day * 5));
        var bill = tax.Assessments.Single(b => b.Id == "delivery@300000");
        Check(bill.State(tax.Now) == ArrearsState.Grace && bill.Principal == 100, "due creates grace with fixed principal");
        Reject(tax, new(WorldAction.DeliverTribute, tax.Now, bill.Id, "town", Evidence: "short", Value: 89), "actual price value insufficient");
        tax.Apply(new(WorldAction.DeliverTribute, tax.Now, bill.Id, "town", Evidence: "receipt", Value: 1000));
        Check(bill.Paid && tax.Assessments.Count(b => b.Paid) == 1, "excess cannot pay another bill");
        Reject(tax, new(WorldAction.DeliverTribute, tax.Now, "collection@300000", "other", Evidence: "receipt", Value: 110), "global receipt reuse");
        tax.Apply(new(WorldAction.Advance, day * 6));
        Check(!tax.Assessments.Single(b => b.Id == "collection@300000").OverdueRecorded, "last grace tick inclusive");
        tax.Apply(new(WorldAction.Advance, day * 6 + 1));
        Check(tax.Assessments.Single(b => b.Id == "collection@300000").OverdueRecorded && tax.Contracts.All(c => c.Active), "overdue does not auto terminate or start war");

        var defended = Seed(); Bind(defended, "bond", "town", "player");
        defended.Apply(new(WorldAction.OpenAttack, 0, "raid", "enemy", "town", Count: 50, Interval: day * 2));
        defended.Apply(new(WorldAction.SendRelief, 0, "raid", "player", Count: 50));
        Check(defended.Domain("player").Available == 50 && defended.Domain("enemy").Available == 50, "deployments reserve troops");
        Reject(defended, new(WorldAction.SendRelief, 0, "raid", "player", Count: 50), "same cohort cannot deploy twice");
        Reject(defended, new(WorldAction.OpenAttack, 0, "player-falls", "enemy", "player", Count: 10, Interval: day), "player cannot lose colony to abstract simulation");
        defended.Apply(new(WorldAction.Advance, day * 2));
        Check(defended.Attacks.Single().State == AttackState.AwaitingRelief, "response deadline inclusive");
        defended.Apply(new(WorldAction.Advance, day * 2 + 1));
        Check(defended.Attacks.Single().State == AttackState.Relieved && defended.Domain("player").Committed == 0 &&
            defended.Domain("player").Troops == 95 && !defended.Contracts.Single().ProtectionBreached, "relief resolves losses and protection once");
        decimal reward = defended.Domain("player").Honor;
        defended.Apply(new(WorldAction.Advance, day * 3));
        Check(defended.Domain("player").Honor == reward, "no repeated relief rewards");

        var abandoned = Seed(); Bind(abandoned, "bond", "town", "liege"); Bind(abandoned, "child-bond", "child", "town");
        Reject(abandoned, new(WorldAction.DemandCure, 0, Actor: "town", Interval: day), "honor or dissatisfaction alone cannot justify exit");
        abandoned.Apply(new(WorldAction.OpenAttack, 0, "siege", "enemy", "town", Count: 50, Interval: day));
        abandoned.Apply(new(WorldAction.Advance, day + 1));
        Check(abandoned.Attacks.Single().State == AttackState.Captured && abandoned.Contracts.Single(c => c.Id == "bond").ProtectionBreached,
            "unanswered model protection duty supplies breach cause, capture does not fabricate new bond");
        Check(abandoned.Domain("town").Parent == "liege", "capture outcome alone cannot overwrite existing contract");
        abandoned.Apply(new(WorldAction.DemandCure, abandoned.Now, Actor: "town", Interval: day * 2));
        var remedied = PoliticalWorld.Restore(abandoned.Export());
        Reject(remedied, new(WorldAction.CureProtection, remedied.Now, Actor: "town", Target: "liege", Evidence: "remedy"), "unilateral cure cannot erase subject complaint");
        remedied.Apply(new(WorldAction.CureProtection, remedied.Now, Actor: "town", Target: "liege", Evidence: "remedy", Flag: true));
        Check(!remedied.Contracts.Single(c => c.Id == "bond").ProtectionBreached &&
            remedied.Contracts.Single(c => c.Id == "bond").CureUntil == null, "accepted remedy resets pending exit cause, not history");
        Reject(remedied, new(WorldAction.Secede, remedied.Now, Actor: "town"), "remedied breach cannot justify exit");
        Reject(abandoned, new(WorldAction.Secede, abandoned.Now, Actor: "town"), "notice guarantees response period");
        abandoned.Apply(new(WorldAction.Advance, day * 3 + 1));
        Reject(abandoned, new(WorldAction.Secede, abandoned.Now, Actor: "town"), "cure final tick inclusive");
        abandoned.Apply(new(WorldAction.Advance, day * 3 + 2));
        abandoned.Apply(new(WorldAction.Secede, abandoned.Now, Actor: "town"));
        Check(abandoned.Domain("town").InUnion && abandoned.Domain("child").Parent == "town" &&
            abandoned.Contracts.Single(c => c.Id == "child-bond").Active, "lawful exit preserves child bonds");

        foreach (long turn in new[] { day / 4, day, day * 3, day * 10 })
        {
            var sparse = Seed(turn); var daily = Seed(turn);
            Bind(sparse, "bond", "town", "liege"); Bind(daily, "bond", "town", "liege");
            sparse.Apply(new(WorldAction.OpenAttack, 0, "raid", "enemy", "town", Count: 50, Interval: day * 2));
            daily.Apply(new(WorldAction.OpenAttack, 0, "raid", "enemy", "town", Count: 50, Interval: day * 2));
            sparse.Apply(new(WorldAction.Advance, day * 20));
            for (int i = 1; i <= 20; i++) daily.Apply(new(WorldAction.Advance, day * i));
            Check(sparse.Domain("town").Prosperity == daily.Domain("town").Prosperity &&
                sparse.Contracts.Single().Trust == daily.Contracts.Single().Trust && sparse.Assessments.Count == daily.Assessments.Count,
                "sparse advancement is chronological at each turn interval");
            var restored = PoliticalWorld.Restore(sparse.Export());
            Check(restored.Export() == sparse.Export() && restored.Now == sparse.Now &&
                restored.Contracts.Single().ProtectionBreached == sparse.Contracts.Single().ProtectionBreached &&
                restored.Attacks.Single().State == sparse.Attacks.Single().State, "journal restores deterministic state");
        }
        Check(PoliticalWorld.Restore(tax.Export()).Assessments.Count(b => b.Paid) == 1, "receipt and fulfillment survive serialization");
        var reload = PoliticalWorld.Restore(tax.Export());
        Reject(reload, new(WorldAction.DeliverTribute, reload.Now, "collection@300000", "other", Evidence: "receipt", Value: 110), "replay restores consumed receipt protection");
        Reject(reload, new(WorldAction.Advance, 0), "time reversal");
        bool schemaRejected = false;
        try { PoliticalWorld.Restore("{\"Version\":99,\"TurnTicks\":60000,\"Commands\":[]}"); }
        catch (ArgumentException) { schemaRejected = true; }
        Check(schemaRejected, "future schema is rejected instead of wiped");
        var manyTicks = Seed(); Bind(manyTicks, "bond", "town", "liege");
        for (int i = 1; i <= 500; i++) manyTicks.Apply(new(WorldAction.Advance, i * 250));
        var tickSave = System.Text.Json.JsonSerializer.Deserialize<PoliticalWorldSave>(manyTicks.Export());
        Check(tickSave.Commands.Length == 9 && State(PoliticalWorld.Restore(manyTicks.Export())) == State(manyTicks),
            "per-tick advancement compacts to one deterministic replay span");

        // Exercise mixed valid/invalid interleavings, rather than only mirroring each method's happy path.
        var random = new Random(71498); var mixed = Seed();
        for (int i = 0; i < 300; i++)
        {
            string before = State(mixed), journal = mixed.Export();
            string subject = random.Next(2) == 0 ? "town" : "child";
            WorldCommand command = random.Next(6) switch {
                0 => new(WorldAction.Advance, mixed.Now + random.Next(1, 10000)),
                1 => new(WorldAction.Bind, mixed.Now, "mixed-" + i, subject, random.Next(2) == 0 ? "liege" : "player",
                    Value: 100, Interval: day * 5, Grace: day),
                2 => new(WorldAction.OpenAttack, mixed.Now, "raid-" + i, "enemy", subject, Count: 25, Interval: day),
                3 => new(WorldAction.Release, mixed.Now, Actor: subject, Target: "liege", Flag: true),
                4 => new(WorldAction.SendRelief, mixed.Now, mixed.Attacks.LastOrDefault()?.Id, "player", Count: 25),
                _ => new(WorldAction.Secede, mixed.Now, Actor: subject)
            };
            try { mixed.Apply(command); }
            catch (ArgumentException) { Check(State(mixed) == before && mixed.Export() == journal, "invalid mixed event is atomic"); }
            Check(mixed.Domains.All(d => d.Troops >= 0 && d.Committed >= 0 && d.Available >= 0 && mixed.Depth(d.Id) <= 4),
                "mixed events conserve deployed troops and acyclic bounded hierarchy");
            if (i % 25 == 0) Check(State(PoliticalWorld.Restore(mixed.Export())) == State(mixed), "mixed replay reproduces all public state");
        }
        return count;
    }
}
