using System.Text.Json;
using Honor.Contracts.Rules;

internal static class CharterAcceptanceTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string reason) { count++; if (!value) throw new Exception("Acceptance audit: " + reason); }
        void Reject(Action action, string reason)
        { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, reason); }
        var before = new CharterBook(); before.Register(new("p", "native-player", true), 0); before.Register(new("n", "native-npc", false), 0);
        var observations = before.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        before = CharterOffers.Propose(before, "offer", "p", "n", true, new(), 10, 0, observations).Book;
        var after = CharterOffers.Confirm(before, "offer", 10, 10, observations, new()).Book;
        var audit = new CharterAcceptances(); string original = audit.Export();
        Reject(() => audit.Record(before, before, "offer", "native-npc", "leader", "Faction", "Name", 10), "a pending proposal is not final consent");
        var final = audit.Record(before, after, "offer", "native-npc", "new-successor", "세력", "후임 대표", 10);
        var record = final.Find("offer");
        Check(record.Representative == "new-successor" && record.FirstNpcConsentTick == 0 && record.SignedTick == 10,
            "current final representative is distinct from the original response time");
        Check(record.RepresentativeLabel == "후임 대표" && record.FactionLabel == "세력", "historical labels are retained independently of later renaming");
        Check(audit.Export() == original && audit.Find("offer") == null, "staged audit does not mutate its source");
        final.ValidateBook(after); Check(final.Records.Count == 1, "actual final book validates");
        Check(CharterAcceptances.Restore(final.Export()).Export() == final.Export(), "audit save roundtrip");
        Reject(() => final.Record(before, after, "offer", "native-npc", "duplicate", "Faction", "Name", 10), "duplicate final history cannot overwrite original");
        Reject(() => audit.Record(before, after, "offer", "native-npc", "leader", "Faction", "Name", 11), "claimed final timestamp must match actual signature");
        Reject(() => audit.Record(before, after, "offer", "native-npc", "bad\nkey", "Faction", "Name", 10), "malformed representative identity");
        var edited = CharterBook.Restore(after.Export()); edited.Register(new("unrelated", "unrelated-native", false), 10);
        Reject(() => audit.Record(before, edited, "offer", "native-npc", "leader", "Faction", "Name", 10), "final audit cannot hide unrelated registrations");
        edited = CharterBook.Restore(after.Export()); edited.ConsentToRelease("offer", true, 10, observations);
        Reject(() => audit.Record(before, edited, "offer", "native-npc", "leader", "Faction", "Name", 10), "unrelated release mutation cannot be hidden in signing");
        var released = CharterBook.Restore(after.Export()); released.ConsentToRelease("offer", true, 11, observations); released.ConsentToRelease("offer", false, 11, observations);
        final.ValidateBook(released); Check(final.Find("offer") == record, "lawful release does not erase historical final representative");
        new CharterAcceptances().ValidateBook(after); Check(true, "legacy signed contracts are not assigned invented historical representatives");
        var saved = JsonSerializer.Deserialize<CharterAcceptanceSave>(final.Export());
        void Bad(CharterAcceptanceSave value, string reason)
        { Reject(() => { var model = CharterAcceptances.Restore(JsonSerializer.Serialize(value)); model.ValidateBook(after); }, reason); }
        Bad(saved with { Version = 99 }, "unknown audit version");
        Bad(saved with { Records = null }, "missing records");
        Bad(saved with { Records = new[] { record, record } }, "duplicate saved history");
        Bad(saved with { Records = new[] { record with { SignedTick = 11 } } }, "saved final time cannot differ from book");
        Bad(saved with { Records = new[] { record with { FirstNpcConsentTick = 1 } } }, "saved first NPC time must match unchanged original signature");
        Bad(saved with { Records = new[] { record with { TermsProof = new string('A', 64) } } }, "changed terms proof");
        Bad(saved with { Records = new[] { record with { TermsProof = "invalid" } } }, "malformed proof");
        Bad(saved with { Records = new[] { record with { Charter = "missing" } } }, "unknown charter reference");
        Bad(saved with { Records = new[] { record with { NpcFaction = "" } } }, "missing faction identity");
        Check(CharterAcceptances.Restore(final.Export()).Find("offer").Representative == "new-successor", "historical IDs do not require a currently living pawn reference");
        return count;
    }
}
