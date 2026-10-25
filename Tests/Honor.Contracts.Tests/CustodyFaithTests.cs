using Honor.Contracts.Rules;

internal static class CustodyFaithTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) { Console.Error.WriteLine("FAIL CustodyFaith: " + name); throw new Exception(name); } }
        void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        ResidentFaithPrecept[] precepts = [new("p-1", "Charity", "RimWorld.Precept"), new("p-2", "Leader", "RimWorld.Precept_RoleSingle")];
        string Faith(IEnumerable<ResidentFaithPrecept> rows, bool patchedDispatch = false) => ResidentFaith.Capture("original-faith", .8f, rows, ["previous"], false, patchedDispatch);
        var faith = Faith(precepts);
        // Existing serializers use 0.800000012 on Framework and 0.8 on .NET 10. Do not rewrite saved proofs to normalize them.
#if NETFRAMEWORK
        const string legacyFaith = "12BC69145C274041E1D20C24F3F170BB1D880429CF87E6A11B02DB6F57FCB90F";
#else
        const string legacyFaith = "9EB7C278F236E9B26C2C51EDE4959099E7C45799559FA6FB3589058DE8F3A63A";
#endif
        Check(faith == legacyFaith, "legacy faith digest unchanged by notification safety flags: " + faith);
        Check(faith == Faith(precepts.AsEnumerable().Reverse()), "guest callback validation keeps canonical faith ordering");
        foreach (var unsupported in new[] { precepts[0] with { KnownGuestCallback = false }, precepts[0] with { PatchedGuestCallback = true },
            precepts[0] with { AssignedRole = true }, precepts[0] with { KnownCallback = false }, precepts[0] with { PatchedCallback = true } })
            Reject(() => Faith([unsupported, precepts[1]]), "unsafe notifications/roles cannot grant prisoner faith preservation");
        Reject(() => Faith(precepts, true), "patched ideology dispatcher blocked even with individually safe precepts");
        Reject(() => Faith([], true), "empty doctrine cannot waive a patched dispatcher");
        string a = new('A', 64), b = new('B', 64), c = new('C', 64), d = new('D', 64);
        Check(ResidentMoveProfiles.Compose(a, b, 4, c, d) == "757EB57AE62E3B520B4481F498957B0418023D62A1C478D9A7371F0C729FAB12",
            "legacy profile-4 digest remains byte-identical");
        var custody = new PrisonerEvidence("prisoner", "original-faction", d, 15f, 4f);
        string custodyProof = PrisonerPreservation.Proof(custody);
        string old = ResidentMoveProfiles.Compose(a, b, 4, ResidentFaith.None, custodyProof);
        string current = ResidentMoveProfiles.Compose(a, b, 5, faith, custodyProof);
        Check(current != old && CityStockProof.Valid(current), "new execution profile explicitly binds prisoner faith and custody");
        Check(current != ResidentMoveProfiles.Compose(a, b, 4, faith, custodyProof), "new eligibility is not silently applied to legacy pending records");
        Check(current != ResidentMoveProfiles.Compose(a, b, 5, ResidentFaith.None, custodyProof), "prisoner faith cannot disappear without detection");
        Check(current != ResidentMoveProfiles.Compose(a, b, 5, faith, PrisonerPreservation.None), "ordinary resident proof cannot replace prisoner custody");
        foreach (string proof in new[] { null, "", "bad" })
        {
            Reject(() => ResidentMoveProfiles.Compose(a, b, 5, proof, custodyProof), "missing or invalid prisoner faith evidence");
            Reject(() => ResidentMoveProfiles.Compose(a, b, 5, faith, proof), "missing or invalid prisoner custody evidence");
        }
        var set = ResidentMoveSet.Capture([new("prisoner", current, 2)]);
        var actual = new ResidentMoveActual("prisoner", "source-host", current, 2);
        Check(set.ActionFor(actual, "source-host", "target-host", false) == ResidentMoveAction.ChangeFaction, "faith-bearing captive participates in coordinated authority plan");
        var native = new PrisonerActual("prisoner", custody.OriginalFaction, "target-host", custody.State, 20f, 6f);
        var reset = new PrisonerNativeReset("target-host", 20f, 6f);
        Check(PrisonerPreservation.NeedsRestore(custody, native, "source-host", "target-host", reset), "faith support does not waive observed reset requirement");
        string changedFaith = ResidentFaith.Capture("other-faith", .8f, precepts, ["previous"], false);
        foreach (string changed in new[] { changedFaith, ResidentFaith.Capture("original-faith", .7f, precepts, ["previous"], false),
            ResidentFaith.Capture("original-faith", .8f, precepts, ["different-history"], false) })
        {
            var changedActual = actual with { Faction = "target-host", Preserved = ResidentMoveProfiles.Compose(a, b, 5, changed, custodyProof) };
            Reject(() => set.Review([changedActual], "source-host", "target-host", true), "later faith/certainty/history changes are not repaired as native custody resets");
        }
        var complete = actual with { Faction = "target-host" };
        set.VerifyDestination([complete], "source-host", "target-host");
        Check(!PrisonerPreservation.NeedsRestore(custody, native with { Resistance = custody.Resistance, Will = custody.Will }, "source-host", "target-host", null),
            "completed prisoner retains original affiliation and progress without ongoing repair permission");
        var legacy = ResidentMoveSet.Capture([new("prisoner", old, 2)]);
        var restored = ResidentMoveSet.Restore(legacy.Export());
        Check(restored.Proof == legacy.Proof, "legacy pending proof survives roundtrip without upgrade");
        Reject(() => restored.VerifyDestination([complete], "source-host", "target-host"), "new profile cannot confirm a legacy pending intent");
        return count;
    }
}
