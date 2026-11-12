using System.Text;
using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record GroundRestRow(string Pawn, string Job, string Saved);
public sealed record GroundRestCell(string Pawn, string Job, string Map, int Cell, int MaxPawns = 1, int Stack = -1);
/// <summary>Same native ongoing job, never a copied/restarted driver or permission to rebuild reservations.</summary>
public static class GroundRest
{
    public static void ValidateCells(IEnumerable<GroundRestCell> reservations, IEnumerable<GroundRestCell> owners)
    {
        var rows = reservations?.ToArray(); var actual = owners?.ToArray();
        if (rows == null || actual == null || rows.Length > 128 || actual.Length > 128 || rows.Any(r => r == null || string.IsNullOrWhiteSpace(r.Pawn) || string.IsNullOrWhiteSpace(r.Job) || string.IsNullOrWhiteSpace(r.Map) || r.Cell < 0 || r.MaxPawns != 1 || r.Stack != -1 || actual.Count(a => a == r) != 1) || rows.Select(r => r.Pawn).Distinct().Count() != rows.Length || rows.Select(r => (r.Map, r.Cell)).Distinct().Count() != rows.Length) throw new ArgumentException("Original claimant, job and exclusive current ground cell must all match.");
    }
    public static string Proof(GroundRestRow r)
    {
        if (r == null || string.IsNullOrWhiteSpace(r.Pawn) || r.Pawn.Length > 120 || string.IsNullOrWhiteSpace(r.Job) || r.Job.Length > 120 || r.Saved == null || r.Saved.Length > 64000) throw new ArgumentException("Bounded original ground-rest state required.");
        using var data = JsonDocument.Parse(r.Saved);
        if (data.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Original native job fields required.");
        return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r))));
    }
    public static string Bind(string original, GroundRestRow r)
    {
        if (!CityStockProof.Valid(original)) throw new ArgumentException("Original clinical/map proof required.");
        return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Profile = 14, Previous = original, Work = r == null ? null : Proof(r) }))));
    }
    public static GroundRestRow[] Validate(IEnumerable<GroundRestRow> rows, IEnumerable<string> actors)
    {
        var all = rows?.ToArray(); var ids = actors?.ToArray();
        if (all == null || ids == null || all.Length is < 1 or > 128 || all.Any(r => r == null || !ids.Contains(r.Pawn)) || !all.Select(r => r.Pawn).SequenceEqual(all.Select(r => r.Pawn).Distinct().OrderBy(s => s, StringComparer.Ordinal)) || all.Select(r => r.Job).Distinct().Count() != all.Length) throw new ArgumentException("Exact original ongoing-work owners required.");
        foreach (var r in all) _ = Proof(r); return all;
    }
}
