using Honor.Contracts.Rules;

internal static class ResidentFaithTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        void Reject(Action run, string name) { bool rejected = false; try { run(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        ResidentFaithPrecept[] rows = [new("p-a", "Charity", "RimWorld.Precept"), new("p-b", "MoralGuide", "RimWorld.Precept_RoleSingle")];
        string Proof(IEnumerable<ResidentFaithPrecept> values, float certainty = .75f, string id = "faith") => ResidentFaith.Capture(id, certainty, values, ["older"], false);
        string proof = Proof(rows);
        Check(proof == Proof(rows.AsEnumerable().Reverse()), "precept list order is not conversion evidence");
        Check(proof != Proof(rows, .5f) && proof != Proof(rows, id: "another"), "certainty and original faith identity bound");
        Check(proof != ResidentFaith.None && CityStockProof.Valid(ResidentFaith.None), "no faith is a distinct recorded state");
        foreach (var change in new[] { rows[0] with { Id = "other" }, rows[0] with { Def = "Other" }, rows[0] with { Type = "Other" } })
            Check(proof != Proof([change, rows[1]]), "doctrine or precept identity changes detected");
        foreach (var unsupported in new[] { rows[0] with { AssignedRole = true }, rows[0] with { KnownCallback = false }, rows[0] with { PatchedCallback = true } })
            Reject(() => Proof([unsupported, rows[1]]), "role holders/unverified callbacks cannot acquire ordinary resident authority");
        Reject(() => ResidentFaith.Capture("faith", .75f, rows, [], true), "age-reversal obligation not silently reset");
        foreach (float invalid in new[] { -.1f, 1.1f, float.NaN, float.PositiveInfinity }) Reject(() => Proof(rows, invalid), "invalid certainty rejected without clamping or conversion");
        Reject(() => Proof([rows[0], rows[0]]), "duplicate precepts rejected");
        Reject(() => ResidentFaith.Capture("faith", .5f, rows, [null], false), "missing previous-faith reference is not dropped");
        Check(ResidentFaith.Capture("faith", .5f, rows, ["a", "b"], false) != ResidentFaith.Capture("faith", .5f, rows, ["b", "a"], false), "previous conversion history order preserved");
        Reject(() => Proof(Enumerable.Range(0, 257).Select(i => new ResidentFaithPrecept("p" + i, "D", "T"))), "bounded ideology metadata");
        string legacy = new('A', 64), surgery = new('B', 64);
        string old = ResidentMoveProfiles.Compose(legacy, surgery, 2);
        string combined = ResidentMoveProfiles.Compose(legacy, surgery, 3, proof);
        Check(CityStockProof.Valid(combined) && combined != old, "profile 3 separately binds ordinary faith");
        Check(combined != ResidentMoveProfiles.Compose(legacy, surgery, 3, ResidentFaith.None), "faith cannot be introduced/removed unnoticed");
        Check(combined != ResidentMoveProfiles.Compose(surgery, legacy, 3, proof), "faith proof cannot mask clinical or surgery changes");
        Check(ResidentMoveProfiles.Compose(legacy, null, 1) == legacy && ResidentMoveProfiles.Compose(legacy, surgery, 2) == old, "old pending profiles remain byte-identical");
        Reject(() => ResidentMoveProfiles.Compose(legacy, surgery, 2, proof), "profile 2 cannot be upgraded in place");
        Reject(() => ResidentMoveProfiles.Compose(legacy, surgery, 3), "new profile requires explicit faith evidence");
        Reject(() => ResidentMoveProfiles.Compose(legacy, surgery, 4, proof), "custody profile requires explicit custody evidence");
        return count;
    }
}
