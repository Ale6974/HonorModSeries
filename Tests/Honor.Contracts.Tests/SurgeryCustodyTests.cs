using Honor.Contracts.Rules;

internal static class SurgeryCustodyTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        void Reject(Action run, string name) { bool rejected = false; try { run(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        string hash = new('A', 64), different = new('B', 64);
        SurgeryOrder[] orders = [new("order-a", "InstallArm", "Human", 10, "Arm", hash), new("order-b", "Anesthetize", "Human", -1, null, different)];
        var expected = SurgeryOrderSet.Capture(orders);
        Check(SurgeryOrderSet.Restore(expected.Export()).Export() == expected.Export(), "ordered surgical snapshot persistence");
        Check(expected.Proof != SurgeryOrderSet.Capture(orders.AsEnumerable().Reverse()).Proof, "priority order changes proof");
        orders[0] = orders[0] with { Recipe = "Changed" };
        Check(expected.Orders[0].Recipe == "InstallArm", "caller list cannot mutate saved medical intent");
        var attached = expected.Orders.ToArray();
        Check(expected.Review(attached, [], false) == SurgeryCustodyAction.Detach, "source originals must detach before native clear");
        Check(expected.Review([], attached, false) == SurgeryCustodyAction.ChangeFaction, "detached originals permit guarded native step");
        Check(expected.Review([], attached, true) == SurgeryCustodyAction.Restore, "target ownership alone does not complete orders");
        Reject(() => expected.VerifyAttached([], attached), "target with detached bills cannot complete");
        expected.VerifyAttached(attached, []); Check(true, "same objects/order restored to the pawn complete custody");
        Check(expected.Review(attached, [], true) == SurgeryCustodyAction.Verify, "repeated recovery does not reissue restored orders");
        foreach (var mutated in new[] { attached[0] with { Id = "other" }, attached[0] with { Recipe = "OtherRecipe" },
            attached[0] with { Body = "OtherBody" }, attached[0] with { PartIndex = 11 }, attached[0] with { PartDef = "Leg" }, attached[0] with { Configuration = different } })
        {
            Reject(() => expected.Review([mutated, attached[1]], [], false), "changed attached order cannot detach");
            Reject(() => expected.Review([], [mutated, attached[1]], true), "changed detached order cannot restore");
        }
        foreach (bool target in new[] { false, true })
        {
            Reject(() => expected.Review(attached, attached, target), "duplicate holders do not prove ownership");
            Reject(() => expected.Review(attached.Take(1), attached.Skip(1), target), "partial mixed ownership is not auto merged");
            Reject(() => expected.Review([], [], target), "missing originals are not regenerated");
            Reject(() => expected.Review(attached.AsEnumerable().Reverse(), [], target), "changed priority is not silently repaired");
        }
        var empty = SurgeryOrderSet.Capture([]);
        Check(empty.Review([], [], false) == SurgeryCustodyAction.ChangeFaction && empty.Review([], [], true) == SurgeryCustodyAction.Verify, "empty orders require no new object");
        for (int length = 1; length <= SurgeryOrderSet.Capacity; length++)
        {
            var rows = Enumerable.Range(0, length).Select(i => new SurgeryOrder("order-" + i, "Recipe", "Human", -1, null, hash)).ToArray();
            var set = SurgeryOrderSet.Capture(rows);
            Check(SurgeryOrderSet.Restore(set.Export()).Proof == set.Proof, "all supported list lengths retain exact evidence");
            Check(set.Review([], rows, true) == SurgeryCustodyAction.Restore, "each list length retains originals until restoration");
        }
        Reject(() => SurgeryOrderSet.Capture(Enumerable.Range(0, 16).Select(i => new SurgeryOrder("o" + i, "R", "B", -1, null, hash))), "surgery capacity bounded");
        Reject(() => SurgeryOrderSet.Capture([attached[0], attached[0]]), "duplicate bill IDs rejected");
        Reject(() => SurgeryOrderSet.Capture([attached[0] with { PartIndex = -2 }]), "invalid anatomy index rejected");
        Reject(() => SurgeryOrderSet.Capture([attached[1] with { PartDef = "Arm" }]), "null part index cannot name a part");
        Reject(() => SurgeryOrderSet.Capture([attached[0] with { PartDef = null }]), "indexed part requires identity");
        Reject(() => SurgeryOrderSet.Capture([attached[0] with { Configuration = "invalid" }]), "invalid surgical configuration proof rejected");
        Reject(() => SurgeryOrderSet.Restore(expected.Export().Replace("\"Version\":1", "\"Version\":8")), "unknown custody schema rejected");
        Check(ResidentMoveProfiles.Compose(hash, null, 1) == hash, "old pending resident proof is byte-identical");
        string combined = ResidentMoveProfiles.Compose(hash, expected.Proof, 2);
        Check(combined != hash && combined != expected.Proof && CityStockProof.Valid(combined), "new profile binds health/social and orders independently");
        Check(combined != ResidentMoveProfiles.Compose(different, expected.Proof, 2), "resident change cannot hide behind stable orders");
        Check(combined != ResidentMoveProfiles.Compose(hash, empty.Proof, 2), "missing orders cannot pass as ordinary residents");
        Reject(() => ResidentMoveProfiles.Compose(hash, expected.Proof, 1), "legacy profile cannot accept new surgical authority");
        Reject(() => ResidentMoveProfiles.Compose(hash, null, 2), "new profile requires surgical evidence even for zero bills");
        Reject(() => ResidentMoveProfiles.Compose(hash, empty.Proof, 3), "unknown resident profile rejected");
        var missingPart = attached[0] with { PartIndex = -1, PartDef = null };
        Check(expected.DecodePartLink(missingPart, true) == attached[0], "proven native decode omission can restore the recorded link");
        Check(expected.DecodePartLink(attached[0], true) == attached[0] && expected.DecodePartLink(attached[1], false) == attached[1], "already decoded and genuinely partless orders unchanged");
        Reject(() => expected.DecodePartLink(missingPart, false), "part removed before save is not a decode omission");
        Reject(() => expected.DecodePartLink(missingPart with { Configuration = different }, true), "decode cannot waive other config changes");
        Reject(() => expected.DecodePartLink(missingPart with { Body = "OtherBody" }, true), "decode cannot change anatomy identity");
        Reject(() => expected.DecodePartLink(attached[0] with { PartIndex = 12 }, true), "nonnull different part is never overwritten");
        Reject(() => expected.DecodePartLink(missingPart with { Id = "unknown" }, true), "unknown surgical object cannot receive a link");
        SurgeryOrderSet.ValidateDistinctOwners([expected, empty]); Check(true, "distinct resident holders validated together");
        Reject(() => SurgeryOrderSet.ValidateDistinctOwners([expected, expected]), "shared medical IDs across residents rejected");
        Reject(() => SurgeryOrderSet.ValidateDistinctOwners(Enumerable.Repeat(empty, 129)), "resident custody owner set bounded");
        return count;
    }
}
