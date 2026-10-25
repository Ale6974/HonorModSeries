using System.Text.Json;
using Honor.Contracts.Rules;

internal static class PrisonerPreservationTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        string hash = new('A', 64), other = new('B', 64);
        var before = new PrisonerEvidence("captive", "original", hash, 17.5f, 3f);
        var source = new PrisonerActual(before.Id, before.OriginalFaction, "old-host", hash, before.Resistance, before.Will);
        var reset = new PrisonerNativeReset("new-host", 21f, 5f);
        bool Review(PrisonerActual actual, PrisonerNativeReset observed = null) => PrisonerPreservation.NeedsRestore(before, actual, "old-host", "new-host", observed);
        string proof = PrisonerPreservation.Proof(before);
        Check(CityStockProof.Valid(proof) && proof != PrisonerPreservation.None, "custody proof distinct from ordinary resident");
        Check(!Review(source), "fresh host change preserves original progress");
        var target = source with { Host = "new-host" };
        Check(!Review(target), "complete custody change needs no progress repair");
        var native = target with { Resistance = reset.Resistance, Will = reset.Will };
        Check(Review(native, reset), "only observed native reset at target grants restoration");
        Reject(() => Review(native), "interrupted native call without observed reset remains blocked");
        Reject(() => Review(source with { Resistance = reset.Resistance, Will = reset.Will }, reset), "unmoved progress is never rewritten");
        Reject(() => Review(native, reset with { Host = "third-host" }), "reset cannot authorize an unrelated host");
        Reject(() => Review(native with { Resistance = 20.5f }, reset), "later resistance progress blocks instead of being erased");
        Reject(() => Review(native with { Will = 4.5f }, reset), "later will progress blocks instead of being erased");
        Reject(() => Review(target with { Resistance = reset.Resistance }, reset), "partial unexpected progress is not the observed pair");
        Reject(() => Review(target with { Host = "third-host" }, reset), "foreign custody cannot acquire recovery authority");
        Reject(() => Review(target with { OriginalFaction = "new-host" }, reset), "conversion to target faction is not prisoner preservation");
        Reject(() => Review(target with { Prisoner = false }, reset), "release is not custody transfer");
        Reject(() => Review(target with { Slave = true }, reset), "enslavement is not custody transfer");
        Reject(() => Review(target with { Id = "replacement" }, reset), "replacement pawn rejected");
        Reject(() => Review(target with { State = other }, reset), "guest settings or work priorities changed");
        foreach (float invalid in new[] { -2f, float.NaN, float.PositiveInfinity })
        {
            Reject(() => PrisonerPreservation.Proof(before with { Resistance = invalid }), "invalid source resistance");
            Reject(() => Review(native with { Will = invalid }, reset), "invalid actual will");
            Reject(() => Review(native, reset with { Resistance = invalid }), "invalid reset evidence");
        }
        Reject(() => Review(native, reset with { Resistance = -1f }), "unset values cannot be fabricated native reset evidence");
        foreach (var change in new[] { before with { OriginalFaction = "another" }, before with { State = other },
            before with { Resistance = 16.5f }, before with { Will = 2f }, before with { Id = "another" } })
            Check(PrisonerPreservation.Proof(change) != proof, "all preserved prisoner evidence bound to execution proof");
        Reject(() => PrisonerPreservation.NeedsRestore(before, target, "old-host", "original", reset), "no move into captive's original faction");
        Reject(() => PrisonerPreservation.NeedsRestore(before, target, "original", "new-host", reset), "invalid own-faction source prison");
        Reject(() => PrisonerPreservation.NeedsRestore(before, target, "old-host", "old-host", null), "source and target must differ");
        var saved = JsonSerializer.Deserialize<PrisonerEvidence>(JsonSerializer.Serialize(before));
        var savedReset = JsonSerializer.Deserialize<PrisonerNativeReset>(JsonSerializer.Serialize(reset));
        Check(PrisonerPreservation.Proof(saved) == proof && Review(native, savedReset), "pending original and observed reset survive JSON roundtrip");
        // Consuming a reset after successful restoration prevents a later coincidental value from gaining repair permission.
        Check(!Review(target, savedReset), "already restored values are idempotent");
        Reject(() => Review(native, null), "consumed reset cannot be reused for later changes");
        string ordinary = ResidentMoveProfiles.Compose(hash, other, 4, hash, PrisonerPreservation.None);
        Check(ResidentMoveProfiles.Compose(hash, other, 3, new string('C', 64)) == "9D3C2E7BC62013DB573FDA18473CADB3BD2846298937CC73612EDB910F854FE3",
            "persisted profile-3 digest remains byte-identical after custody support");
        string captive = ResidentMoveProfiles.Compose(hash, other, 4, hash, proof);
        Check(captive != ordinary && CityStockProof.Valid(captive), "profile 4 distinguishes custody from ordinary affiliation");
        Reject(() => ResidentMoveProfiles.Compose(hash, other, 3, hash, proof), "old profile cannot silently gain custody support");
        Reject(() => ResidentMoveProfiles.Compose(hash, other, 4, hash), "custody evidence mandatory in new profile");
        Reject(() => ResidentMoveProfiles.Compose(hash, other, 7, hash, proof), "unknown preservation version rejected");
        // Mixed town recovery: the captive changes host while an ordinary resident changes faction.
        var town = ResidentMoveSet.Capture([new("ordinary", hash, 2), new("captive", captive, 1)]);
        ResidentMoveActual[] partial = [new("ordinary", "old-host", hash, 2), new("captive", "new-host", captive, 0)];
        Check(town.Review(partial, "old-host", "new-host", true).Count == 2 && Review(native, savedReset), "partial mixed town retains both execution paths");
        Reject(() => town.VerifyDestination(partial, "old-host", "new-host"), "partial residents and medical settings cannot complete city");
        ResidentMoveActual[] completed = [new("ordinary", "new-host", hash, 2), new("captive", "new-host", captive, 1)];
        town.VerifyDestination(completed, "old-host", "new-host");
        Check(!Review(target), "completion requires actual original progress and target host");
        return count;
    }
}
