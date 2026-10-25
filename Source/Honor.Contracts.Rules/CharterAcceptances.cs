using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record CharterAcceptance(string Charter, string NpcFaction, string Representative, string FactionLabel,
    string RepresentativeLabel, long SignedTick, long FirstNpcConsentTick, string TermsProof);
public sealed record CharterAcceptanceSave(int Version, CharterAcceptance[] Records);

/// <summary>Historical final-consent audit. Never authorizes transfers, breaches, succession or future consent.</summary>
public sealed class CharterAcceptances
{
    private readonly Dictionary<string, CharterAcceptance> records = new(StringComparer.Ordinal);
    public IReadOnlyCollection<CharterAcceptance> Records => records.Values.ToArray();
    public CharterAcceptance Find(string id) => records.TryGetValue(id ?? "", out var record) ? record : null;
    private static bool Key(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 200 && !value.Any(char.IsControl);
    private static string Proof(RecordedCharterTerms terms) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(terms))));
    private static void Check(CharterAcceptance record)
    {
        if (record == null || !Key(record.Charter) || record.Charter.Length > 120 || !Key(record.NpcFaction) || !Key(record.Representative) ||
            !Key(record.FactionLabel) || !Key(record.RepresentativeLabel) || record.SignedTick < 0 || record.SignedTick > int.MaxValue ||
            record.FirstNpcConsentTick < 0 || record.FirstNpcConsentTick > record.SignedTick || !CityStockProof.Valid(record.TermsProof))
            throw new ArgumentException("Invalid historical final-consent record.");
    }
    public CharterAcceptances Record(CharterBook before, CharterBook after, string id, string faction, string representative,
        string factionLabel, string representativeLabel, long now)
    {
        RuntimeCompat.NotNull(before); RuntimeCompat.NotNull(after);
        var old = before.Charter(id); var actual = after.Charter(id);
        if (old.NpcAuthority != null || !old.NpcSignedTick.HasValue || old.PlayerSignedTick.HasValue ||
            before.Phase(old, now) != RecordedCharterPhase.Offered || actual.Phase != RecordedCharterPhase.Active || actual.PlayerSignedTick != now ||
            records.ContainsKey(id) || records.Count >= CharterBook.MaxCharters)
            throw new ArgumentException("An actual final player-signing transition is required.");
        var expected = CharterBook.Restore(before.Export());
        expected.Sign(id, true, now, before.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)));
        if (expected.Export() != after.Export()) throw new ArgumentException("Final consent cannot hide unrelated book edits.");
        var record = new CharterAcceptance(id, faction, representative, factionLabel, representativeLabel, now, old.NpcSignedTick.Value, Proof(old.Terms));
        Check(record);
        var copy = Restore(Export()); copy.records.Add(id, record); copy.ValidateBook(after); return copy;
    }
    public void ValidateBook(CharterBook book)
    {
        RuntimeCompat.NotNull(book);
        foreach (var record in records.Values)
        {
            var c = book.Charter(record.Charter);
            if (c.NpcAuthority != null || c.Phase is not (RecordedCharterPhase.Active or RecordedCharterPhase.Released or RecordedCharterPhase.TerritoryLost) ||
                c.PlayerSignedTick != record.SignedTick || c.NpcSignedTick != record.FirstNpcConsentTick || record.SignedTick > book.LastTick || Proof(c.Terms) != record.TermsProof)
                throw new ArgumentException("Final-consent audit differs from charter history.");
        }
    }
    public string Export() => JsonSerializer.Serialize(new CharterAcceptanceSave(1, records.Values.ToArray()));
    public static CharterAcceptances Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 12000000) throw new ArgumentException("Invalid final-consent archive size.");
        var save = JsonSerializer.Deserialize<CharterAcceptanceSave>(json);
        if (save == null || save.Version != 1 || save.Records == null || save.Records.Length > CharterBook.MaxCharters)
            throw new ArgumentException("Unsupported final-consent archive.");
        var result = new CharterAcceptances();
        foreach (var record in save.Records)
        {
            Check(record);
            if (result.records.ContainsKey(record.Charter)) throw new ArgumentException("Duplicate final-consent record.");
            result.records.Add(record.Charter, record);
        }
        return result;
    }
}
