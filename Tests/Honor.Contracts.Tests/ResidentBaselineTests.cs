using System.Text.Json;
using Honor.Contracts.Rules;

internal static class ResidentBaselineTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool condition, string name) { count++; if (!condition) throw new Exception(name); }
        void Reject(Action action, string name)
        {
            bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, name);
        }
        string a = new('A', 64), b = new('B', 64);
        ResidentEvidence first = new("pawn1", "source", a, a, a), second = new("pawn2", null, a, a, a);
        var input = new[] { second, first };
        var baseline = ResidentBaseline.Capture("city", "source", 10, input);
        string original = baseline.Export();
        Check(baseline.Residents.Select(r => r.Id).SequenceEqual(new[] { "pawn1", "pawn2" }), "deterministic resident order");
        input[0] = first;
        Check(baseline.Export() == original, "caller array mutation cannot rewrite saved evidence");
        bool immutable = false;
        try { ((IList<ResidentEvidence>)baseline.Residents)[0] = second; } catch (NotSupportedException) { immutable = true; }
        Check(immutable, "resident evidence collection immutable");
        Check(ResidentBaseline.Restore(original).Export() == original, "baseline JSON roundtrip");
        var same = baseline.Compare("city", "source", 11, [second, first]);
        Check(!same.CityOwnerChanged && same.ObservedTick == 11 && same.Residents.All(r => r.Difference == ResidentDifference.None), "reordered identical residents are unchanged");
        Check(baseline.Compare("city", "new-owner", 11, [first, second]).CityOwnerChanged, "city owner changes are reported independently of resident state");
        foreach (var pair in new[] {
            (first with { Faction = "target" }, ResidentDifference.Faction),
            (first with { Faction = null }, ResidentDifference.Faction),
            (first with { Health = b }, ResidentDifference.Health),
            (first with { Gear = b }, ResidentDifference.Gear),
            (first with { Status = b }, ResidentDifference.Status) })
        {
            var compared = baseline.Compare("city", "source", 12, [pair.Item1, second]);
            Check(compared.Residents.Single(r => r.Id == "pawn1").Difference == pair.Item2, "each tracked state category changes independently");
            Check(baseline.Export() == original, "comparison never mutates the baseline");
        }
        var changed = baseline.Compare("city", "target", 12, [first with { Faction = "target", Health = b, Gear = b, Status = b }, second with { Id = "pawn3" }]);
        Check(changed.Residents.Single(r => r.Id == "pawn1").Difference == (ResidentDifference.Faction | ResidentDifference.Health | ResidentDifference.Gear | ResidentDifference.Status), "combined differences remain distinct flags");
        Check(changed.Residents.Single(r => r.Id == "pawn2").Difference == ResidentDifference.Removed && changed.Residents.Single(r => r.Id == "pawn3").Difference == ResidentDifference.Added,
            "replacing a resident cannot masquerade as an unchanged body or gear fingerprint");
        Check(baseline.Compare("city", "source", 12, []).Residents.All(r => r.Difference == ResidentDifference.Removed), "empty current roster reports all missing references");
        Check(ResidentBaseline.Capture("city", "source", 0, []).Compare("city", "source", 0, [first]).Residents.Single().Difference == ResidentDifference.Added, "empty baseline detects newly referenced residents");
        Reject(() => baseline.Compare("other-city", "source", 12, [first]), "different city cannot consume baseline");
        Reject(() => baseline.Compare("city", "source", 9, [first]), "comparison cannot precede baseline clock");
        Reject(() => baseline.Compare("city", "source", (long)int.MaxValue + 1, [first]), "comparison clock bounded");
        foreach (var bad in new[] { first with { Id = "\n" }, first with { Faction = "" }, first with { Health = "bad" }, first with { Gear = null }, first with { Status = b.ToLowerInvariant() } })
            Reject(() => ResidentBaseline.Capture("city", "source", 10, [bad]), "malformed identity or proof rejected");
        Reject(() => ResidentBaseline.Capture("city", "source", 10, [first, first]), "duplicate resident identity rejected");
        Reject(() => baseline.Compare("city", "source", 11, [first, first]), "duplicate current roster is not silently deduplicated");
        Reject(() => ResidentBaseline.Capture("city", "source", -1, []), "negative clock rejected");
        Reject(() => ResidentBaseline.Capture("city", "source", 10, null), "missing roster rejected");
        Reject(() => ResidentBaseline.Capture("city", "source", 10, [null]), "missing resident rejected");
        Reject(() => ResidentBaseline.Capture("city", "source", 10, Enumerable.Range(0, 1001).Select(i => first with { Id = "pawn" + i })), "oversized resident manifest rejected");
        var saved = JsonSerializer.Deserialize<ResidentBaselineSave>(original);
        foreach (var bad in new[] { saved with { Version = 2 }, saved with { Residents = null }, saved with { City = "" }, saved with { Tick = -1 },
            saved with { Residents = [first with { Health = "bad" }] }, saved with { Residents = [first, first] } })
            Reject(() => ResidentBaseline.Restore(JsonSerializer.Serialize(bad)), "invalid saved schema or evidence rejected");
        Check(baseline.Export() == original, "failed comparisons and restores preserve existing evidence");
        return count;
    }
}
