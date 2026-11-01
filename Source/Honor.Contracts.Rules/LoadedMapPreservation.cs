using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record NeutralMapThing(string Id, string Def, string Stuff, int Count, int HitPoints, int Cell, int Rotation, int? Quality);
public sealed record LoadedMapScope(string Town, string Map, int Width, int Height, string Grid, string Stock, int StockFormat, NeutralMapThing[] Things, string Proof);

/// <summary>Original neutral, pawn-free map custody. No actor, property or map-write authority.</summary>
public static class LoadedMapPreservation
{
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static string Hash(LoadedMapScope s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(s with { Proof = null }))));
    public static string Capture(string town, string map, int width, int height, string grid, string stock, int stockFormat, IEnumerable<NeutralMapThing> things)
    {
        var s = new LoadedMapScope(town, map, width, height, grid, stock, stockFormat, things.OrderBy(t => t.Id, StringComparer.Ordinal).ToArray(), null);
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static LoadedMapScope Restore(string json)
    {
        if (json == null || json.Length > 3000000) throw new ArgumentException("Bounded original loaded-map scope required.");
        var s = JsonSerializer.Deserialize<LoadedMapScope>(json);
        if (s == null || !Key(s.Town) || !Key(s.Map) || s.Width is < 1 or > 500 || s.Height is < 1 or > 500 || !CityStockProof.Valid(s.Grid) || !CityStockProof.Valid(s.Stock) ||
            s.StockFormat is < 0 or > 2 || s.Things == null || s.Things.Length > 10000 || s.Things.Any(t => t == null || !Key(t.Id) || !Key(t.Def) || t.Stuff != null && !Key(t.Stuff) ||
                t.Count < 1 || t.HitPoints < 0 || t.Cell < 0 || t.Cell >= s.Width * s.Height || t.Rotation is < 0 or > 3 || t.Quality is < 0 or > 6) ||
            s.Things.Select(t => t.Id).Distinct().Count() != s.Things.Length || !s.Things.Select(t => t.Id).SequenceEqual(s.Things.Select(t => t.Id).OrderBy(x => x, StringComparer.Ordinal)) || s.Proof != Hash(s))
            throw new ArgumentException("Invalid original neutral-map custody.");
        return s;
    }
    public static void Pair(string json, string town, string proof)
    {
        var s = Restore(json); if (s.Town != town || s.Proof != proof) throw new ArgumentException("Original loaded map differs from transfer scope.");
    }
}
