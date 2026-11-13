using System.Text;
using System.Text.Json;
namespace Honor.Contracts.Rules;

/// <summary>New original native work scope. No permission is inherited from a rest snapshot.</summary>
public sealed record ActiveWorkRow(string Pawn, string[] Jobs, string Saved);
public static class ActiveWork
{
    public static string Proof(ActiveWorkRow row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.Pawn) || row.Pawn.Length > 120 || row.Jobs == null || row.Jobs.Length is < 1 or > 33 ||
            row.Jobs.Any(j => string.IsNullOrWhiteSpace(j) || j.Length > 120) || row.Jobs.Distinct().Count() != row.Jobs.Length || row.Saved == null || row.Saved.Length > 256000)
            throw new ArgumentException("Bounded original native current and queued jobs required.");
        using var data = JsonDocument.Parse(row.Saved);
        if (data.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Original native saved work fields required.");
        return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row))));
    }
    public static ActiveWorkRow[] Validate(IEnumerable<ActiveWorkRow> rows, IEnumerable<string> actors)
    {
        var all = rows?.ToArray(); var ids = actors?.ToArray();
        if (all == null || ids == null || all.Length is < 1 or > 128 || all.Any(r => r == null || r.Jobs == null || !ids.Contains(r.Pawn)) ||
            !all.Select(r => r.Pawn).SequenceEqual(all.Select(r => r.Pawn).Distinct().OrderBy(s => s, StringComparer.Ordinal)) ||
            all.SelectMany(r => r.Jobs).Distinct().Count() != all.Sum(r => r.Jobs.Length)) throw new ArgumentException("Unique original work owners and jobs required.");
        foreach (var row in all) _ = Proof(row);
        return all;
    }
    public static string Bind(string original, ActiveWorkRow row)
    {
        if (!CityStockProof.Valid(original)) throw new ArgumentException("Original clinical proof required.");
        return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Profile = 16, Original = original, Work = row == null ? null : Proof(row) }))));
    }
}
