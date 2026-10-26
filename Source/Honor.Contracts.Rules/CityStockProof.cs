using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record CityStockItem(string Id, string Def, string Stuff, int Count, int HitPoints, int? Quality);
public sealed record CityTradeAnimal(string Id, string Def, string Faction, string Health, string Gear, string Status);
public sealed record CityAnimalCargo(string Animal, IReadOnlyList<CityStockItem> Goods, bool UnloadEverything,
    IReadOnlyList<string> NotForSale, IReadOnlyList<string> Unpacked);

/// <summary>Owner-independent ordinary stock and bounded animal inventory evidence; never a stock generator.</summary>
public static class CityStockProof
{
    public static string CapturePackedAnimals(bool visited, bool containerExists, int nextRestockTick,
        IEnumerable<CityStockItem> goods, IEnumerable<CityTradeAnimal> animals, IEnumerable<CityAnimalCargo> cargo)
    {
        RuntimeCompat.NotNull(goods); RuntimeCompat.NotNull(animals); RuntimeCompat.NotNull(cargo);
        var ordinary = goods.Take(10001).ToArray(); var pawns = animals.Take(10001).ToArray(); var loads = cargo.Take(10001).ToArray();
        if (loads.Length is < 1 or > 10000 || ordinary.Length + pawns.Length > 10000 ||
            loads.Any(c => c == null || c.Goods == null || c.Goods.Count is < 1 or > 10000 || !pawns.Any(p => p?.Id == c.Animal)) ||
            loads.Select(c => c.Animal).Distinct(StringComparer.Ordinal).Count() != loads.Length ||
            loads.Sum(c => (long)c.Goods.Count) + ordinary.Length + pawns.Length > 10000)
            throw new ArgumentException("Invalid original animal cargo scope.");
        var carried = loads.SelectMany(c => c.Goods).ToArray();
        string animalProof = CaptureAnimals(visited, containerExists, nextRestockTick, ordinary, pawns);
        // Validate original ordinary item fields, including cross-holder IDs, without minting or moving goods.
        Capture(visited, containerExists, nextRestockTick, ordinary.Concat(carried));
        if (ordinary.Concat(carried).Select(i => i.Id).Intersect(pawns.Select(p => p.Id), StringComparer.Ordinal).Any())
            throw new ArgumentException("Cargo and animal identities overlap.");
        foreach (var load in loads)
        {
            var ids = new HashSet<string>(load.Goods.Select(g => g.Id), StringComparer.Ordinal);
            foreach (var configured in new[] { load.NotForSale, load.Unpacked })
                if (configured == null || configured.Count > load.Goods.Count || configured.Any(id => !ids.Contains(id)) ||
                    configured.Distinct(StringComparer.Ordinal).Count() != configured.Count)
                    throw new ArgumentException("Cargo sale/unpack references must identify the original held goods.");
        }
        var data = new { Version = 3, Animals = animalProof,
            Cargo = loads.OrderBy(c => c.Animal, StringComparer.Ordinal).Select(c => new { c.Animal, Goods = c.Goods.OrderBy(g => g.Id, StringComparer.Ordinal).ToArray(),
                c.UnloadEverything, c.NotForSale, c.Unpacked }).ToArray() };
        return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))));
    }
    public static bool Valid(string proof) => proof is { Length: 64 } && proof.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    public static string Capture(bool visited, bool containerExists, int nextRestockTick, IEnumerable<CityStockItem> goods)
    {
        RuntimeCompat.NotNull(goods);
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
        return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot))));
    }
    public static string CaptureAnimals(bool visited, bool containerExists, int nextRestockTick,
        IEnumerable<CityStockItem> goods, IEnumerable<CityTradeAnimal> animals)
    {
        RuntimeCompat.NotNull(goods); RuntimeCompat.NotNull(animals);
        var ordinary = goods.Take(10001).ToArray(); var pawns = animals.Take(10001).ToArray();
        static bool Key(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 120 && !key.Any(char.IsControl);
        if (!visited || !containerExists || pawns.Length == 0 || ordinary.Length + pawns.Length > 10000 ||
            pawns.Any(p => p == null || !Key(p.Id) || !Key(p.Def) || p.Faction != null && !Key(p.Faction) ||
                !Valid(p.Health) || !Valid(p.Gear) || !Valid(p.Status)) ||
            ordinary.Select(g => g?.Id).Concat(pawns.Select(p => p.Id)).Distinct(StringComparer.Ordinal).Count() != ordinary.Length + pawns.Length)
            throw new ArgumentException("Invalid animal stock evidence.");
        var snapshot = new { Version = 2, Ordinary = Capture(visited, containerExists, nextRestockTick, ordinary),
            Animals = pawns.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray() };
        return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot))));
    }
}
