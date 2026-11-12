using System.Text;
using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record RoyalRightRow(string Pawn, string Data, string[] Realms, string[] Heirs, string[] Abilities);
public sealed record RoyalRightsState(int Version, RoyalRightRow[] Rows);
/// <summary>B05 immutable original saved rights; never permission to change titles or recreate a tracker.</summary>
public static class RoyalRights
{
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static void IDs(string[] ids) { if (ids == null || ids.Length > 1024 || ids.Any(s => !Key(s)) || !ids.SequenceEqual(ids.Distinct().OrderBy(s => s, StringComparer.Ordinal))) throw new ArgumentException("Canonical royal references required."); }
    public static string Capture(IEnumerable<RoyalRightRow> rows)
    { string json = JsonSerializer.Serialize(new RoyalRightsState(2, rows.OrderBy(r => r.Pawn, StringComparer.Ordinal).ToArray())); _ = Restore(json); return json; }
    public static RoyalRightsState Restore(string json)
    {
        if (json == null || json.Length > 2000000) throw new ArgumentException("Bounded original royal rights required.");
        var state = JsonSerializer.Deserialize<RoyalRightsState>(json);
        if (state?.Version != 2 || state.Rows == null || state.Rows.Length > 128 || state.Rows.Any(r => r == null || !Key(r.Pawn) || r.Data == null || r.Data.Length > 200000) ||
            !state.Rows.Select(r => r.Pawn).SequenceEqual(state.Rows.Select(r => r.Pawn).Distinct().OrderBy(s => s, StringComparer.Ordinal))) throw new ArgumentException("Original royal census invalid.");
        foreach (var r in state.Rows) { IDs(r.Realms); IDs(r.Heirs); IDs(r.Abilities); using var data = JsonDocument.Parse(r.Data); if (data.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Saved royal fields required."); }
        var abilities = state.Rows.SelectMany(r => r.Abilities).ToArray();
        if (abilities.Distinct(StringComparer.Ordinal).Count() != abilities.Length) throw new ArgumentException("One original ability cannot belong to multiple resident trackers.");
        if (JsonSerializer.Serialize(state) != json) throw new ArgumentException("Canonical immutable royal document required.");
        return state;
    }
    public static string Proof(string json) { _ = Restore(json); return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(json))); }
    public static string[] Realms(string json) => Restore(json).Rows.SelectMany(r => r.Realms).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray();
    public static void Pair(string json, string proof, IEnumerable<string> people) { if (Proof(json) != proof || !Restore(json).Rows.Select(r => r.Pawn).SequenceEqual(people.OrderBy(s => s, StringComparer.Ordinal))) throw new ArgumentException("Royal rights do not belong to original resident census."); }
    public static void RequireOriginal(string original, string actual) { _ = Restore(original); _ = Restore(actual); if (original != actual) throw new ArgumentException("Royal saved rights changed; never repair/backfill original evidence."); }
}
