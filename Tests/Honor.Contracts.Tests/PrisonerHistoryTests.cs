using System.Text.Json;
using Honor.Contracts.Rules;

internal static class PrisonerHistoryTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool v, string name) { count++; if (!v) { Console.Error.WriteLine("FAIL PrisonerHistory: " + name); throw new Exception(name); } }
        void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        var before = new RecruitmentInteraction("Warden", .75f, 1.5f, .5f, 1f);
        string proof = PrisonerHistory.Interaction(before);
        Check(CityStockProof.Valid(proof), "native interaction metadata yields bounded preservation evidence");
        Check(PrisonerHistory.Interaction(JsonSerializer.Deserialize<RecruitmentInteraction>(JsonSerializer.Serialize(before))) == proof,
            "record metadata survives JSON without changing live recruitment");
        foreach (var changed in new[] { before with { Initiator = "Other" }, before with { ResistanceReduction = .5f }, before with { Negotiation = 2f },
            before with { Mood = .75f }, before with { Opinion = .5f } })
            Check(PrisonerHistory.Interaction(changed) != proof, "every native saved interaction field is bound");
        Check(PrisonerHistory.Interaction(before with { Initiator = null }) != PrisonerHistory.Interaction(before with { Initiator = "" }), "null and empty initiator history stay distinct");
        Check(CityStockProof.Valid(PrisonerHistory.Interaction(before with { ResistanceReduction = -.5f, Opinion = -1f })), "finite negative native history is preserved rather than clamped");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Reject(() => PrisonerHistory.Interaction(before with { ResistanceReduction = invalid }), "invalid recorded reduction blocks");
            Reject(() => PrisonerHistory.Interaction(before with { Negotiation = invalid }), "invalid recorded negotiation blocks");
            Reject(() => PrisonerHistory.Interaction(before with { Mood = invalid }), "invalid recorded mood blocks");
            Reject(() => PrisonerHistory.Interaction(before with { Opinion = invalid }), "invalid recorded opinion blocks");
        }
        Reject(() => PrisonerHistory.Interaction(null), "missing record is not a fabricated zero record");
        Reject(() => PrisonerHistory.Interaction(before with { Initiator = "bad\nname" }), "invalid names are not silently normalized");
        Reject(() => PrisonerHistory.Interaction(before with { Initiator = new string('x', 4097) }), "record text bounded");
        string target = PrisonerHistory.ConversionTarget("target-faith", true, true);
        Check(CityStockProof.Valid(target) && target != PrisonerHistory.ConversionTarget("other-faith", true, true), "exact target identity bound without conversion");
        Reject(() => PrisonerHistory.ConversionTarget("target-faith", false, true), "missing or ambiguous registered target cannot be invented");
        Reject(() => PrisonerHistory.ConversionTarget("target-faith", true, false), "custom target type does not gain native authority");
        foreach (string invalid in new[] { null, "", "bad\nid", new string('i', 251) }) Reject(() => PrisonerHistory.ConversionTarget(invalid, true, true), "invalid conversion identity rejected");
        string a = new('A', 64), b = new('B', 64), c = new('C', 64), d = new('D', 64);
        Check(ResidentMoveProfiles.Compose(a, b, 5, c, d) == "79843BE76460932EB456435E378210F818EE4D325AE8127B651783DF18C48228", "legacy profile-5 digest remains byte-identical");
        string current = ResidentMoveProfiles.Compose(a, b, 6, c, proof);
        Check(CityStockProof.Valid(current) && current != ResidentMoveProfiles.Compose(a, b, 5, c, proof), "extended eligibility uses a separate city execution profile");
        foreach (string changed in new[] { target, PrisonerHistory.Interaction(before with { ResistanceReduction = .25f }) })
            Check(current != ResidentMoveProfiles.Compose(a, b, 6, c, changed), "changed guest history cannot hide inside unchanged clinical evidence");
        var set = ResidentMoveSet.Capture([new("prisoner", current, 2)]);
        var actual = new ResidentMoveActual("prisoner", "destination", current, 2);
        set.VerifyDestination([actual], "source", "destination");
        Check(true, "unchanged history can complete an existing native custody move");
        Reject(() => set.VerifyDestination([actual with { Preserved = ResidentMoveProfiles.Compose(a, b, 6, c, target) }], "source", "destination"),
            "later history or target changes block completion instead of being restored");
        Reject(() => ResidentMoveProfiles.Compose(a, b, 6, c), "new profile needs explicit custody evidence even when guest records are absent");
        Reject(() => ResidentMoveProfiles.Compose(a, b, 7, c, d), "unknown profile rejected");
        return count;
    }
}
