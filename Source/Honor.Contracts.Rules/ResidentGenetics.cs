using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record ResidentGene(string Id, string Def, bool Xenogene, string OverriddenBy, int? PassionPreAdd);

/// <summary>Original gene identity/expression links only; never creates, removes or repairs a gene.</summary>
public static class ResidentGenetics
{
    private static string Hash(object data) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))));
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    public static string None => Hash(new { Version = 1, Tracker = false });
    public static string Capture(string pawn, string xenotype, string name, string icon, bool hybrid, IEnumerable<ResidentGene> genes)
    {
        RuntimeCompat.NotNull(genes); var rows = genes.Take(257).ToArray();
        if (!Key(pawn) || xenotype != null && !Key(xenotype) || icon != null && !Key(icon) ||
            name != null && (name.Length > 200 || name.Any(char.IsControl)) || rows.Length > 256 ||
            rows.Any(g => g == null || !Key(g.Id) || !Key(g.Def) || g.OverriddenBy != null && !Key(g.OverriddenBy) || g.PassionPreAdd is < 0 or > 2) ||
            rows.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count() != rows.Length)
            throw new ArgumentException("Unsupported original gene evidence.");
        var index = rows.ToDictionary(g => g.Id, StringComparer.Ordinal);
        foreach (var g in rows)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { g.Id }; var next = g.OverriddenBy;
            while (next != null)
            {
                if (!seen.Add(next) || !index.TryGetValue(next, out var overriding)) throw new ArgumentException("Foreign or cyclic gene override.");
                next = overriding.OverriddenBy;
            }
        }
        return Hash(new { Version = 1, Tracker = true, Pawn = pawn, Xenotype = xenotype, Name = name, Icon = icon, Hybrid = hybrid, Genes = rows });
    }
}
