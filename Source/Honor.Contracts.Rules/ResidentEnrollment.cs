using System.Text;
using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record ResidentEnrollmentState(string Charter, string Town, string Map, string Source, string Book,
    NpcRealmDecisionEvidence Authority, string[] Before, ResidentMoveEvidence[] Residents, int Added, bool Started, bool Complete, bool Verified, long Tick, string Proof);
/// <summary>New explicit roster enrollment, never retroactive authority for an old migration.</summary>
public static class ResidentEnrollment
{
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static string Hash(ResidentEnrollmentState s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(s with { Proof = null }))));
    private static string Write(ResidentEnrollmentState s) { s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json; }
    public static ResidentEnrollmentState Restore(string json)
    {
        if (json == null || json.Length > 100000) throw new ArgumentException("Bounded original enrollment required.");
        var s = JsonSerializer.Deserialize<ResidentEnrollmentState>(json);
        if (s == null || !Key(s.Charter) || !Key(s.Town) || !Key(s.Map) || !Key(s.Source) || !CityStockProof.Valid(s.Book) || s.Tick < 0 || s.Proof != Hash(s) ||
            s.Authority?.Realm != s.Source || s.Before == null || s.Before.Length > 128 || s.Before.Any(id => !Key(id)) || s.Before.Distinct().Count() != s.Before.Length ||
            s.Residents == null || s.Residents.Length is < 1 or > 128 || s.Before.Length + s.Residents.Length > 128 || s.Residents.Any(r => r == null || !Key(r.Id) ||
                !CityStockProof.Valid(r.Preserved) || r.MedicalCare is < 0 or > 4 || s.Before.Contains(r.Id)) ||
            !s.Residents.Select(r => r.Id).SequenceEqual(s.Residents.Select(r => r.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal)) ||
            s.Added < 0 || s.Added > s.Residents.Length || s.Started && s.Added == s.Residents.Length || s.Complete != (s.Added == s.Residents.Length) || s.Verified && (!s.Complete || s.Started))
            throw new ArgumentException("Invalid original enrollment journal.");
        _ = NpcRealmDecision.Consent(s.Authority); return s;
    }
    public static string Prepare(string charter, string town, string map, string source, string book, NpcRealmDecisionEvidence authority,
        IEnumerable<string> before, IEnumerable<ResidentMoveEvidence> residents, long tick)
    {
        if (before == null || residents == null) throw new ArgumentException("Original roster and selected residents required.");
        return Write(new(charter, town, map, source, book, authority, before.Take(129).ToArray(), residents.Take(129).OrderBy(r => r?.Id, StringComparer.Ordinal).ToArray(), 0, false, false, false, tick, null));
    }
    public static void Review(string json, IEnumerable<string> actual, NpcRealmDecisionEvidence authority, string book)
    {
        var s = Restore(json);
        if (s.Verified || s.Started || actual == null || s.Authority != authority || s.Book != book ||
            !actual.SequenceEqual(s.Before.Concat(s.Residents.Take(s.Added).Select(r => r.Id)))) throw new ArgumentException("Original enrollment prefix or current authority changed; unknown writes never replay.");
        _ = NpcRealmDecision.Consent(authority);
    }
    public static string Begin(string json, IEnumerable<string> actual, NpcRealmDecisionEvidence authority, string book)
    { Review(json, actual, authority, book); var s = Restore(json); if (s.Complete) throw new ArgumentException("All appends already returned."); return Write(s with { Started = true }); }
    public static string Finish(string json, IEnumerable<ResidentMoveEvidence> actual)
    {
        var s = Restore(json);
        if (!s.Complete || s.Verified || s.Started || actual == null || !actual.SequenceEqual(s.Residents)) throw new ArgumentException("All positive appends and unchanged original clinical evidence required.");
        return Write(s with { Verified = true });
    }
    public static string Returned(string json, IEnumerable<string> actual)
    {
        var s = Restore(json);
        if (!s.Started || s.Complete || actual == null || !actual.SequenceEqual(s.Before.Concat(s.Residents.Take(s.Added + 1).Select(r => r.Id))))
            throw new ArgumentException("Positive original append return required.");
        return Write(s with { Added = s.Added + 1, Started = false, Complete = s.Added + 1 == s.Residents.Length });
    }
}
