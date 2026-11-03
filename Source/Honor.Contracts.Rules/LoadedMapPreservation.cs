using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record NeutralMapThing(string Id, string Def, string Stuff, int Count, int HitPoints, int Cell, int Rotation, int? Quality);
public sealed record NeutralMapHeld(string Holder, int Slot, NeutralMapThing Item);
public sealed record NeutralMapStorage(string Id, string Configuration, int[] Cells, string[] Contents);
public sealed record NeutralMapStorageGroup(string Id, string Configuration, string[] Members);
public sealed record NeutralMapStockpile(string Id, string Configuration, int[] Cells, string[] Contents);
public sealed record NativeMapResident(string Id, string Preserved);
public sealed record NeutralMapContainer(string Id, string Kind, string Configuration, int Count);
public sealed record NeutralMapGrowing(string Id, string Configuration, int[] Cells);
public sealed record LoadedMapScope(string Town, string Map, int Width, int Height, string Grid, string Stock, int StockFormat, NeutralMapThing[] Things, string Proof)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapHeld[] Held { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapStorage[] Storage { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapStorageGroup[] Groups { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapStockpile[] Stockpiles { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapGrowing[] Growing { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapContainer[] Containers { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NativeMapResident[] NativeResidents { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NativeMapResident[] Visitors { get; init; }
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
    public static string CaptureGroups(string original, IEnumerable<NeutralMapStorageGroup> groups)
    {
        var before = Restore(original);
        if (before.Groups != null || before.Storage == null || groups == null) throw new ArgumentException("Fresh original grouped shelf scope required.");
        var rows = groups.Take(1001).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original storage group.");
        var s = before with { Groups = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureStockpiles(string original, IEnumerable<NeutralMapStockpile> stockpiles)
    {
        var before = Restore(original);
        if (before.Stockpiles != null || stockpiles == null) throw new ArgumentException("Fresh original stockpile scope required.");
        var rows = stockpiles.Take(1001).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original stockpile.");
        var s = before with { Stockpiles = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureGrowing(string original, IEnumerable<NeutralMapGrowing> growing)
    {
        var before = Restore(original);
        if (before.Growing != null || growing == null) throw new ArgumentException("Fresh original growing scope required.");
        var rows = growing.Take(1001).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original growing zone.");
        var s = before with { Growing = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureContainers(string original, IEnumerable<NeutralMapHeld> held, IEnumerable<NeutralMapContainer> containers)
    {
        var before = Restore(original);
        if (before.Held != null || before.Containers != null || held == null || containers == null) throw new ArgumentException("Fresh original container forest required.");
        var children = held.Take(10001).ToArray(); var holders = containers.Take(1001).ToArray();
        if (children.Any(r => r == null) || holders.Any(r => r == null)) throw new ArgumentException("Missing original container evidence.");
        var s = before with { Held = children.Length == 0 ? null : children.OrderBy(r => r.Holder, StringComparer.Ordinal).ThenBy(r => r.Slot).ToArray(),
            Containers = holders.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureResidents(string original, IEnumerable<NativeMapResident> residents, IEnumerable<NativeMapResident> visitors = null)
    {
        var before = Restore(original);
        if (before.NativeResidents != null || residents == null) throw new ArgumentException("Fresh original mapped residents required.");
        var rows = residents.Take(129).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original map resident.");
        var guests = visitors?.Take(129).ToArray();
        if (guests?.Any(r => r == null) == true) throw new ArgumentException("Missing original visitor.");
        var s = before with { NativeResidents = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), Visitors = guests == null || guests.Length == 0 ? null : guests.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), Proof = null };
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
            if (s.Held.Length == 0 || s.Held.Length + roots.Count > 10000 || s.Held.Any(r => r == null || !Key(r.Holder) || (s.Containers == null ? r.Slot != 0 : r.Slot is < 0 or > 9999) || !Item(r.Item, 0, true)) ||
                (s.Containers == null ? s.Held.Select(r => r.Holder).Distinct().Count() != s.Held.Length : s.Held.Select(r => (r.Holder, r.Slot)).Distinct().Count() != s.Held.Length) || s.Held.Select(r => r.Item.Id).Distinct().Count() != s.Held.Length ||
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
        if (s.Containers != null)
        {
            var allIds = new HashSet<string>(s.Things.Select(t => t.Id).Concat((s.Held ?? []).Select(r => r.Item.Id)), StringComparer.Ordinal);
            if (s.Containers.Length is < 1 or > 1000 || s.Containers.Any(c => c == null || !Key(c.Id) || !allIds.Contains(c.Id) || c.Kind is not ("casket" or "crate") ||
                !CityStockProof.Valid(c.Configuration) || c.Count is < 0 or > 10000) ||
                !s.Containers.Select(c => c.Id).SequenceEqual(s.Containers.Select(c => c.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal))) throw new ArgumentException("Invalid original container scope.");
            foreach (var c in s.Containers)
                if (!(s.Held ?? []).Where(r => r.Holder == c.Id).Select(r => r.Slot).SequenceEqual(Enumerable.Range(0, c.Count))) throw new ArgumentException("Missing or reordered original container slots.");
            foreach (var group in (s.Held ?? []).GroupBy(r => r.Holder))
                if (!s.Containers.Any(c => c.Id == group.Key) && (group.Count() != 1 || group.First().Slot != 0)) throw new ArgumentException("Legacy package acquired extra contents.");
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
        if (s.Groups != null)
        {
            if (s.Storage == null || s.Groups.Length is < 1 or > 1000 ||
                s.Groups.Any(g => g == null || !Key(g.Id) || !CityStockProof.Valid(g.Configuration) || g.Members == null || g.Members.Length is < 1 or > 1000 ||
                    g.Members.Any(id => !Key(id) || !s.Storage.Any(r => r.Id == id)) || g.Members.Distinct().Count() != g.Members.Length) ||
                !s.Groups.Select(g => g.Id).SequenceEqual(s.Groups.Select(g => g.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal)) ||
                s.Groups.SelectMany(g => g.Members).Distinct().Count() != s.Groups.Sum(g => g.Members.Length))
                throw new ArgumentException("Missing, foreign or duplicated original storage group membership.");
        }
        if (s.Stockpiles != null)
        {
            if (s.Stockpiles.Length is < 1 or > 1000 || s.Stockpiles.Any(r => r == null || !Key(r.Id) || !CityStockProof.Valid(r.Configuration) || r.Cells == null || r.Cells.Length is < 1 or > 10000 ||
                r.Cells.Any(c => c < 0 || c >= s.Width * s.Height) || !r.Cells.SequenceEqual(r.Cells.Distinct().OrderBy(c => c)) || r.Contents == null || r.Contents.Length > 10000 ||
                r.Contents.Any(id => !Key(id)) || !r.Contents.SequenceEqual(r.Contents.Distinct().OrderBy(id => id, StringComparer.Ordinal))) ||
                s.Stockpiles.Sum(r => r.Cells.Length) > 100000 || !s.Stockpiles.Select(r => r.Id).SequenceEqual(s.Stockpiles.Select(r => r.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Invalid original stockpile scope.");
            var cells = new HashSet<int>((s.Storage ?? []).SelectMany(r => r.Cells));
            var goods = new HashSet<string>((s.Storage ?? []).SelectMany(r => r.Contents), StringComparer.Ordinal);
            foreach (var pile in s.Stockpiles)
            {
                if (pile.Cells.Any(c => !cells.Add(c))) throw new ArgumentException("Original stockpile overlaps storage custody.");
                foreach (string id in pile.Contents)
                {
                    var item = s.Things.SingleOrDefault(t => t.Id == id);
                    if (item == null || !pile.Cells.Contains(item.Cell) || !goods.Add(id)) throw new ArgumentException("Missing, outside or duplicated original stockpile goods.");
                }
            }
        }
        if (s.Growing != null)
        {
            if (s.Growing.Length is < 1 or > 1000 || s.Growing.Any(r => r == null || !Key(r.Id) || !CityStockProof.Valid(r.Configuration) || r.Cells == null || r.Cells.Length is < 1 or > 10000 ||
                r.Cells.Any(c => c < 0 || c >= s.Width * s.Height) || !r.Cells.SequenceEqual(r.Cells.Distinct().OrderBy(c => c))) ||
                s.Growing.Sum(r => r.Cells.Length) + (s.Stockpiles ?? []).Sum(r => r.Cells.Length) > 100000 ||
                !s.Growing.Select(r => r.Id).SequenceEqual(s.Growing.Select(r => r.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal)) ||
                s.Growing.Any(r => (s.Stockpiles ?? []).Any(p => p.Id == r.Id))) throw new ArgumentException("Invalid original growing scope.");
            var occupied = new HashSet<int>((s.Storage ?? []).SelectMany(r => r.Cells).Concat((s.Stockpiles ?? []).SelectMany(r => r.Cells)));
            if (s.Growing.SelectMany(r => r.Cells).Any(c => !occupied.Add(c))) throw new ArgumentException("Original growing zone overlaps storage or another zone.");
        }
        if (s.NativeResidents != null)
        {
            var objectIds = new HashSet<string>(s.Things.Select(t => t.Id).Concat((s.Held ?? []).Select(r => r.Item.Id)), StringComparer.Ordinal);
            if (s.NativeResidents.Length is < 1 or > 128 || objectIds.Count + s.NativeResidents.Length > 10000 ||
                s.NativeResidents.Any(r => r == null || !Key(r.Id) || !CityStockProof.Valid(r.Preserved) || objectIds.Contains(r.Id)) ||
                !s.NativeResidents.Select(r => r.Id).SequenceEqual(s.NativeResidents.Select(r => r.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Invalid original map resident scope.");
        }
        if (s.Visitors != null)
        {
            var used = new HashSet<string>(s.Things.Select(t => t.Id).Concat((s.Held ?? []).Select(r => r.Item.Id)).Concat((s.NativeResidents ?? []).Select(r => r.Id)), StringComparer.Ordinal);
            if (s.NativeResidents == null || s.Visitors.Length is < 1 or > 128 || used.Count + s.Visitors.Length > 10000 ||
                s.Visitors.Any(r => r == null || !Key(r.Id) || !CityStockProof.Valid(r.Preserved) || used.Contains(r.Id)) ||
                !s.Visitors.Select(r => r.Id).SequenceEqual(s.Visitors.Select(r => r.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal))) throw new ArgumentException("Invalid original bystander scope.");
        }
        return s;
    }
    public static void Pair(string json, string town, string proof)
    {
        var s = Restore(json); if (s.Town != town || s.Proof != proof) throw new ArgumentException("Original loaded map differs from transfer scope.");
    }
    public static int Format(string json)
    {
        var s = Restore(json); return s.NativeResidents != null ? 10 : s.Containers != null ? 9 : s.Growing != null ? 8 : s.Stockpiles != null ? 7 : s.Groups != null ? 6 : s.Storage != null ? 5 : s.Held == null ? 3 : 4;
    }
    public static void Pair(string json, string town, string proof, int format)
    {
        Pair(json, town, proof); if (Format(json) != format) throw new ArgumentException("Original loaded-map custody format changed.");
    }
}
