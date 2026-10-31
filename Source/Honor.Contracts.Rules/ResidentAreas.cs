using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record ResidentAreaEntry(string Map, string Area);
public sealed record ResidentAreaState(string Id, ResidentAreaEntry[] Original, bool Started = false, bool Returned = false,
    bool Cleared = false, bool Applying = false, bool Consumed = false);

/// <summary>One observed native area reset, never permission to overwrite unrelated later restrictions.</summary>
public static class ResidentAreas
{
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    public static ResidentAreaState Restore(string json)
    {
        if (json == null || json.Length > 100000) throw new ArgumentException("Bounded original area journal required.");
        var s = JsonSerializer.Deserialize<ResidentAreaState>(json);
        if (s == null || !Key(s.Id) || s.Original == null || s.Original.Length > 128 || s.Original.Any(e => e == null || !Key(e.Map) || !Key(e.Area)) ||
            s.Original.Select(e => e.Map).Distinct().Count() != s.Original.Length || !s.Original.Select(e => e.Map).SequenceEqual(s.Original.Select(e => e.Map).OrderBy(x => x, StringComparer.Ordinal)) ||
            s.Returned && !s.Started || s.Cleared && (!s.Returned || s.Original.Length == 0) || s.Applying && (!s.Returned || !s.Cleared) || s.Consumed && !s.Returned || s.Consumed && s.Cleared && !s.Applying)
            throw new ArgumentException("Invalid original area journal.");
        return s;
    }
    public static string Prepare(string id, IEnumerable<ResidentAreaEntry> original)
    {
        var json = JsonSerializer.Serialize(new ResidentAreaState(id, original.OrderBy(e => e.Map, StringComparer.Ordinal).ToArray()));
        _ = Restore(json); return json;
    }
    public static string Proof(string json)
    {
        var s = Restore(json);
        return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { s.Id, s.Original }))));
    }
    private static bool Same(IEnumerable<ResidentAreaEntry> a, IEnumerable<ResidentAreaEntry> b) => a.SequenceEqual(b);
    public static void CheckCurrent(string json, IReadOnlyList<ResidentAreaEntry> actual)
    {
        if (actual == null) throw new ArgumentException("Current area evidence required.");
        var s = Restore(json);
        bool ok = s.Consumed || !s.Returned || !s.Cleared ? Same(s.Original, actual) : s.Applying
            ? actual.Count <= s.Original.Length && Same(s.Original.Take(actual.Count), actual) : actual.Count == 0;
        if (!ok) throw new ArgumentException("Original area restriction changed; never overwrite it.");
    }
    public static string Begin(string json, IReadOnlyList<ResidentAreaEntry> actual)
    {
        var s = Restore(json); CheckCurrent(json, actual);
        if (s.Started) throw new ArgumentException("Never replay an original area-reset call.");
        return JsonSerializer.Serialize(s with { Started = true });
    }
    public static string Returned(string json, IReadOnlyList<ResidentAreaEntry> actual)
    {
        var s = Restore(json);
        if (!s.Started || s.Returned || actual.Count != 0 && !Same(s.Original, actual)) throw new ArgumentException("Exact original normal-return area output required.");
        return JsonSerializer.Serialize(s with { Returned = true, Cleared = s.Original.Length > 0 && actual.Count == 0 });
    }
    public static string BeginRestore(string json, IReadOnlyList<ResidentAreaEntry> actual)
    {
        var s = Restore(json); CheckCurrent(json, actual);
        if (!s.Returned || !s.Cleared || s.Consumed) throw new ArgumentException("No observed original area reset to restore.");
        return JsonSerializer.Serialize(s with { Applying = true });
    }
    public static string Complete(string json, IReadOnlyList<ResidentAreaEntry> actual)
    {
        var s = Restore(json); CheckCurrent(json, actual);
        if (!s.Returned || s.Consumed || !Same(s.Original, actual)) throw new ArgumentException("Exact original areas must be restored before completion.");
        return JsonSerializer.Serialize(s with { Consumed = true });
    }
}
