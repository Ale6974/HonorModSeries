using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record CityStockItem(string Id, string Def, string Stuff, int Count, int HitPoints, int? Quality);

/// <summary>Owner-independent evidence for ordinary, non-nested goods; never a stock generator.</summary>
public static class CityStockProof
{
    public static bool Valid(string proof) => proof is { Length: 64 } && proof.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    public static string Capture(bool visited, bool containerExists, int nextRestockTick, IEnumerable<CityStockItem> goods)
    {
        ArgumentNullException.ThrowIfNull(goods);
        var items = goods.Take(10001).ToArray();
        static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
        if (items.Length > 10000 || nextRestockTick < -1 || !containerExists && items.Length > 0 ||
            !visited && (containerExists || nextRestockTick != -1) || !containerExists && nextRestockTick != -1 ||
            items.Any(i => i == null || !Key(i.Id) || !Key(i.Def) || i.Stuff != null && !Key(i.Stuff) ||
                i.Count <= 0 || i.HitPoints < 0 || i.Quality is < 0 or > 6) ||
            items.Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() != items.Length)
            throw new ArgumentException("Invalid ordinary city stock evidence.");
        var snapshot = new { Version = 1, Visited = visited, ContainerExists = containerExists, NextRestockTick = nextRestockTick,
            Goods = items.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray() };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot))));
    }
}
