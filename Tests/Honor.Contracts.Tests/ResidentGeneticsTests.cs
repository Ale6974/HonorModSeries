using System.Text.Json;
using Honor.Contracts.Rules;

internal static class ResidentGeneticsTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Resident genes: " + name); }
        void Reject(Action call, string name) { bool rejected = false; try { call(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        var a = new ResidentGene("Gene_1", "DarkHair", false, null, null);
        var b = new ResidentGene("Gene_2", "LightHair", true, "Gene_1", 2);
        var genes = new[] { a, b };
        string Capture(IEnumerable<ResidentGene> rows) => ResidentGenetics.Capture("pawn", "Baseliner", "Original", "Custom", true, rows);
        string proof = Capture(genes), h = new('A', 64), s = new('B', 64), faith = new('C', 64), custody = new('D', 64);
        Check(CityStockProof.Valid(proof) && CityStockProof.Valid(ResidentGenetics.None), "Bound native gene evidence is a valid original-state proof.");
        Check(Capture(JsonSerializer.Deserialize<ResidentGene[]>(JsonSerializer.Serialize(genes))) == proof, "Original identity/classification/override/passions round-trip without generating genes.");
        Check(proof != ResidentGenetics.None && ResidentGenetics.Capture("pawn", null, null, null, false, []) != ResidentGenetics.None, "No tracker, empty tracker and actual genes are distinct.");
        foreach (var changed in new[] { a with { Id = "Gene_3" }, a with { Def = "Other" }, a with { Xenogene = true }, a with { PassionPreAdd = 0 } })
            Check(Capture(new[] { changed, b with { OverriddenBy = changed.Id } }) != proof, "Gene identity, definition, inheritance classification and original passion are bound.");
        Check(Capture([a, b with { OverriddenBy = null }]) != proof, "Suppression changes cannot hide behind an unchanged gene count.");
        Check(Capture(genes.AsEnumerable().Reverse()) != proof, "Native saved gene order is preserved.");
        foreach (var changed in new[] {
            ResidentGenetics.Capture("other", "Baseliner", "Original", "Custom", true, genes),
            ResidentGenetics.Capture("pawn", "Other", "Original", "Custom", true, genes),
            ResidentGenetics.Capture("pawn", "Baseliner", "Changed", "Custom", true, genes),
            ResidentGenetics.Capture("pawn", "Baseliner", "Original", "Other", true, genes),
            ResidentGenetics.Capture("pawn", "Baseliner", "Original", "Custom", false, genes) })
            Check(changed != proof, "Owner, stored xenotype, original name/icon and hybrid status stay bound.");
        Check(ResidentGenetics.Capture("pawn", null, null, null, false, []) != ResidentGenetics.Capture("pawn", "Baseliner", null, null, false, []), "Raw missing xenotype is not replaced by a displayed fallback.");
        Check(ResidentGenetics.Capture("pawn", null, null, null, false, []) != ResidentGenetics.Capture("pawn", null, "", null, false, []), "Null and empty original names are distinct.");
        Reject(() => Capture([a, a]), "Duplicate saved gene IDs are rejected.");
        Reject(() => Capture([a with { OverriddenBy = "foreign" }, b]), "An override must identify an original gene in this same tracker.");
        Reject(() => Capture([a with { OverriddenBy = a.Id }]), "Self-override is not silently repaired.");
        Reject(() => Capture([a with { OverriddenBy = b.Id }, b]), "Cyclic expression is not accepted.");
        Reject(() => Capture([a with { PassionPreAdd = 3 }]), "Unknown saved passion is not normalized.");
        Reject(() => Capture([null]), "Missing native gene rows cannot be discarded.");
        Reject(() => Capture(Enumerable.Range(0, 257).Select(i => a with { Id = "Gene_" + i })), "Bound original tracker size without dropping data.");
        Reject(() => ResidentGenetics.Capture("pawn", null, new string('X', 201), null, false, genes), "Unbounded custom names do not enter the journal.");
        string combined = ResidentMoveProfiles.Compose(h, s, 7, faith, custody, proof);
        Check(typeof(ResidentMoveProfiles).GetMethod("Compose", [typeof(string), typeof(string), typeof(int), typeof(string), typeof(string)]) != null,
            "Existing five-argument compiled API remains available alongside the new genetics overload.");
        Check(CityStockProof.Valid(combined) && combined != ResidentMoveProfiles.Compose(h, s, 6, faith, custody), "New execution profile cannot reuse an older clinical-only proof.");
        Check(combined != ResidentMoveProfiles.Compose(h, s, 7, faith, custody, ResidentGenetics.None), "A missing tracker after native execution is detected.");
        Reject(() => ResidentMoveProfiles.Compose(h, s, 7, faith, custody), "New profile always requires original genetics, even when no tracker exists.");
        for (int profile = 1; profile <= 6; profile++)
            Reject(() => ResidentMoveProfiles.Compose(h, profile == 1 ? null : s, profile, profile >= 3 ? faith : null, profile >= 4 ? custody : null, proof), "Old pending profiles cannot acquire invented gene authority.");
        var set = ResidentMoveSet.Capture([new("pawn", combined, 4)]);
        var source = new ResidentMoveActual("pawn", "source", combined, 4);
        Check(set.ActionFor(source, "source", "target", false) == ResidentMoveAction.ChangeFaction, "Genetic residents use the actual existing native move planner.");
        Check(set.ActionFor(source with { Faction = "target", MedicalCare = 2 }, "source", "target", true) == ResidentMoveAction.RestoreMedicalCare, "Only ordinary medical reset may be restored.");
        Check(ResidentMoveSet.Restore(set.Export()).Proof == set.Proof, "Original genetic proof persists in the same execution snapshot.");
        Reject(() => set.VerifyDestination([source with { Faction = "target", Preserved = ResidentMoveProfiles.Compose(h, s, 7, faith, custody, Capture([a])) }], "source", "target"), "Changed or lost genes block completion and are never reconstructed.");
        return count;
    }
}
