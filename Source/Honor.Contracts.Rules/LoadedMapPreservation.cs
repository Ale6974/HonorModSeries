using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record NeutralMapThing(string Id, string Def, string Stuff, int Count, int HitPoints, int Cell, int Rotation, int? Quality);
public sealed record NeutralMapHeld(string Holder, int Slot, NeutralMapThing Item);
public sealed record NeutralMapStorage(string Id, string Configuration, int[] Cells, string[] Contents);
public sealed record LoadedMapScope(string Town, string Map, int Width, int Height, string Grid, string Stock, int StockFormat, NeutralMapThing[] Things, string Proof)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapHeld[] Held { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapStorage[] Storage { get; init; }
}

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
    public static string CapturePacked(string town, string map, int width, int height, string grid, string stock, int stockFormat,
        IEnumerable<NeutralMapThing> things, IEnumerable<NeutralMapHeld> held)
    {
        var original = Restore(Capture(town, map, width, height, grid, stock, stockFormat, things));
        if (held == null) throw new ArgumentException("Original packed-map custody required.");
        var rows = held.Take(10001).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original packed item.");
        var s = original with { Held = rows.OrderBy(r => r.Holder, StringComparer.Ordinal).ThenBy(r => r.Slot).ToArray(), Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    private static bool Item(NeutralMapThing t, int cells, bool held) => t != null && Key(t.Id) && Key(t.Def) && (t.Stuff == null || Key(t.Stuff)) &&
        t.Count > 0 && t.HitPoints >= 0 && (held ? t.Cell == -1 : t.Cell >= 0 && t.Cell < cells) && t.Rotation is >= 0 and <= 3 && (t.Quality == null || t.Quality is >= 0 and <= 6);
    public static string CaptureStorage(string original, IEnumerable<NeutralMapStorage> storage)
    {
        var before = Restore(original);
        if (before.Storage != null || storage == null) throw new ArgumentException("Fresh original shelf scope required.");
        var rows = storage.Take(1001).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original shelf.");
        var s = before with { Storage = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static LoadedMapScope Restore(string json)
    {
        if (json == null || json.Length > 3000000) throw new ArgumentException("Bounded original loaded-map scope required.");
        var s = JsonSerializer.Deserialize<LoadedMapScope>(json);
        if (s == null || !Key(s.Town) || !Key(s.Map) || s.Width is < 1 or > 500 || s.Height is < 1 or > 500 || !CityStockProof.Valid(s.Grid) || !CityStockProof.Valid(s.Stock) ||
            s.StockFormat is < 0 or > 2 || s.Things == null || s.Things.Length > 10000 || s.Things.Any(t => !Item(t, s.Width * s.Height, false)) ||
            s.Things.Select(t => t.Id).Distinct().Count() != s.Things.Length || !s.Things.Select(t => t.Id).SequenceEqual(s.Things.Select(t => t.Id).OrderBy(x => x, StringComparer.Ordinal)) || s.Proof != Hash(s))
            throw new ArgumentException("Invalid original neutral-map custody.");
        if (s.Held != null)
        {
            var roots = new HashSet<string>(s.Things.Select(t => t.Id), StringComparer.Ordinal);
            if (s.Held.Length == 0 || s.Held.Length + roots.Count > 10000 || s.Held.Any(r => r == null || !Key(r.Holder) || r.Slot != 0 || !Item(r.Item, 0, true)) ||
                s.Held.Select(r => r.Holder).Distinct().Count() != s.Held.Length || s.Held.Select(r => r.Item.Id).Distinct().Count() != s.Held.Length ||
                s.Held.Any(r => roots.Contains(r.Item.Id)) || !s.Held.Select(r => r.Holder).SequenceEqual(s.Held.Select(r => r.Holder).OrderBy(x => x, StringComparer.Ordinal)))
                throw new ArgumentException("Invalid original packed-map tree.");
            var parents = s.Held.ToDictionary(r => r.Item.Id, r => r.Holder, StringComparer.Ordinal);
            foreach (var row in s.Held)
            {
                string at = row.Item.Id; int depth = 0;
                while (!roots.Contains(at))
                {
                    if (++depth > 8 || !parents.TryGetValue(at, out string parent)) throw new ArgumentException("Missing, cyclic or excessive original packed-map ancestry.");
                    at = parent;
                }
            }
        }
        if (s.Storage != null)
        {
            if (s.Storage.Length is < 1 or > 1000 || s.Storage.Any(r => r == null || !Key(r.Id) || !CityStockProof.Valid(r.Configuration) || r.Cells == null || r.Cells.Length is < 1 or > 4096 ||
                r.Cells.Any(c => c < 0 || c >= s.Width * s.Height) || !r.Cells.SequenceEqual(r.Cells.Distinct().OrderBy(c => c)) || r.Contents == null || r.Contents.Length > 10000 ||
                r.Contents.Any(id => !Key(id)) || !r.Contents.SequenceEqual(r.Contents.Distinct().OrderBy(id => id, StringComparer.Ordinal))) ||
                !s.Storage.Select(r => r.Id).SequenceEqual(s.Storage.Select(r => r.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Invalid original shelf scope.");
            var occupied = new HashSet<int>(); var stored = new HashSet<string>(StringComparer.Ordinal);
            foreach (var shelf in s.Storage)
            {
                var root = s.Things.SingleOrDefault(t => t.Id == shelf.Id);
                if (root == null || !shelf.Cells.Contains(root.Cell) || shelf.Cells.Any(c => !occupied.Add(c))) throw new ArgumentException("Missing or overlapping original shelf.");
                foreach (string id in shelf.Contents)
                {
                    var item = s.Things.SingleOrDefault(t => t.Id == id);
                    if (item == null || item.Id == shelf.Id || s.Storage.Any(r => r.Id == id) || !shelf.Cells.Contains(item.Cell) || !stored.Add(id)) throw new ArgumentException("Stored item is missing, duplicated or outside its original shelf.");
                }
            }
        }
        return s;
    }
    public static void Pair(string json, string town, string proof)
    {
        var s = Restore(json); if (s.Town != town || s.Proof != proof) throw new ArgumentException("Original loaded map differs from transfer scope.");
    }
    public static int Format(string json)
    {
        var s = Restore(json); return s.Storage != null ? 5 : s.Held == null ? 3 : 4;
    }
    public static void Pair(string json, string town, string proof, int format)
    {
        Pair(json, town, proof); if (Format(json) != format) throw new ArgumentException("Original loaded-map custody format changed.");
    }
}
