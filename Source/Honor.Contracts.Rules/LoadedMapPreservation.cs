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
public sealed record NativeWorktable(string Id, string Configuration, string[] Bills);
public sealed record NativeWorkFrame(string Id, string Configuration, string[] Resources)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string StorageV7 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string TerrainV8 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkTerrainLayers TerrainLayersV9 { get; init; }
}
public sealed record LoadedMapScope(string Town, string Map, int Width, int Height, string Grid, string Stock, int StockFormat, NeutralMapThing[] Things, string Proof)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public ActiveWorkRow[] ActiveWorkV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NativeWorktable[] WorktablesV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NativeWorkFrame[] WorkFramesV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapHeld[] Held { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapStorage[] Storage { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapStorageGroup[] Groups { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapStockpile[] Stockpiles { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapGrowing[] Growing { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapContainer[] Containers { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NativeMapResident[] NativeResidents { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? RoyalMappedV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public GroundRestRow[] GroundRestV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? GivenGroundRestV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NativeMapResident[] Visitors { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? ReadOnlyBystandersV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public MapAsset[] OwnedAssets { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? AssetRepairV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? OwnedStorageV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? CivicAssetsV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? OwnedContainersV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? HeldAssetsV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? OwnedHolderForestV1 { get; init; }
}

/// <summary>Original map custody. Actor affiliation requires separate clinical authority; bystanders grant no actor or property writes.</summary>
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
        if (before.NativeResidents != null || before.Visitors != null || before.ReadOnlyBystandersV1 != null || residents == null) throw new ArgumentException("Fresh original mapped residents required.");
        var rows = residents.Take(129).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original map resident.");
        var guests = visitors?.Take(129).ToArray();
        if (guests?.Any(r => r == null) == true) throw new ArgumentException("Missing original visitor.");
        var s = before with { NativeResidents = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), Visitors = guests == null || guests.Length == 0 ? null : guests.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureBystanders(string original, IEnumerable<NativeMapResident> visitors)
    {
        var before = Restore(original);
        if (before.NativeResidents != null || before.Visitors != null || before.ReadOnlyBystandersV1 != null || visitors == null) throw new ArgumentException("Fresh original read-only bystander scope required.");
        var rows = visitors.Take(129).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original bystander.");
        var s = before with { ReadOnlyBystandersV1 = 1, Visitors = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureAssets(string original, IEnumerable<MapAsset> assets)
    {
        var before = Restore(original);
        if (before.OwnedAssets != null || before.NativeResidents != null || before.Visitors != null || assets == null) throw new ArgumentException("Fresh unoccupied original property scope required.");
        var rows = assets.Take(1001).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original property.");
        var s = before with { OwnedAssets = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureRepairAssets(string original, IEnumerable<MapAsset> assets)
    {
        var before = Restore(original);
        if (before.OwnedAssets != null || before.AssetRepairV1 != null || before.NativeResidents != null || before.Visitors != null || assets == null) throw new ArgumentException("Fresh original damaged property scope required.");
        var rows = assets.Take(1001).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original property.");
        var s = before with { OwnedAssets = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), AssetRepairV1 = 1, Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureOwnedStorage(string original, IEnumerable<MapAsset> assets, bool repair)
    {
        var before = Restore(original);
        if (before.OwnedAssets != null || before.OwnedStorageV1 != null || before.NativeResidents != null || before.Visitors != null || assets == null) throw new ArgumentException("Fresh original faction storage required.");
        var rows = assets.Take(1001).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original storage property.");
        var s = before with { OwnedAssets = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), OwnedStorageV1 = 1, AssetRepairV1 = repair ? 1 : null, Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureCivicAssets(string original, IEnumerable<MapAsset> assets, bool repair, bool storage)
    {
        var before = Restore(original);
        if (before.NativeResidents == null || before.OwnedAssets != null || before.CivicAssetsV1 != null || assets == null) throw new ArgumentException("Fresh original civic and property scope required.");
        var rows = assets.Take(1001).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original civic property.");
        var s = before with { OwnedAssets = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), CivicAssetsV1 = 1, AssetRepairV1 = repair ? 1 : null, OwnedStorageV1 = storage ? 1 : null, Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureOwnedContainers(string original, IEnumerable<MapAsset> assets, bool repair, bool storage)
    {
        var before = Restore(original);
        if (before.OwnedAssets != null || before.OwnedContainersV1 != null || before.ReadOnlyBystandersV1 != null || assets == null) throw new ArgumentException("Fresh original container property required.");
        var rows = assets.Take(1001).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original container property.");
        var s = before with { OwnedAssets = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), OwnedContainersV1 = 1,
            CivicAssetsV1 = before.NativeResidents == null ? null : 1, AssetRepairV1 = repair ? 1 : null, OwnedStorageV1 = storage ? 1 : null, Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureHeldAssets(string original, IEnumerable<MapAsset> assets, bool repair, bool storage, bool containers)
    {
        var before = Restore(original);
        if (before.OwnedAssets != null || before.HeldAssetsV1 != null || before.ReadOnlyBystandersV1 != null || assets == null) throw new ArgumentException("Fresh original held property required.");
        var rows = assets.Take(1001).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original held property.");
        var s = before with { OwnedAssets = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), HeldAssetsV1 = 1,
            OwnedContainersV1 = containers ? 1 : null, CivicAssetsV1 = before.NativeResidents == null ? null : 1,
            AssetRepairV1 = repair ? 1 : null, OwnedStorageV1 = storage ? 1 : null, Proof = null };
        s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureOwnedHolderForest(string original, IEnumerable<MapAsset> assets, bool repair, bool storage, bool containers, bool held)
    {
        var before = Restore(original);
        if (before.OwnedAssets != null || before.OwnedHolderForestV1 != null || before.ReadOnlyBystandersV1 != null || assets == null) throw new ArgumentException("Fresh original owned holder forest required.");
        var rows = assets.Take(1001).ToArray();
        if (rows.Any(r => r == null)) throw new ArgumentException("Missing original holder property.");
        var s = before with { OwnedAssets = rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), OwnedHolderForestV1 = 1,
            HeldAssetsV1 = held ? 1 : null, OwnedContainersV1 = containers ? 1 : null, CivicAssetsV1 = before.NativeResidents == null ? null : 1,
            AssetRepairV1 = repair ? 1 : null, OwnedStorageV1 = storage ? 1 : null, Proof = null };
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
        if (s.GivenGroundRestV1 != null && (s.GivenGroundRestV1 != 1 || s.GroundRestV1?.Any(r => r.GiverV1 != null) != true) || s.GivenGroundRestV1 == null && s.GroundRestV1?.Any(r => r.GiverV1 != null) == true) throw new ArgumentException("Original given-work scope missing or expanded.");
        if (s.ActiveWorkV1 != null) { if (s.NativeResidents == null || s.GroundRestV1 != null || s.RoyalMappedV1 != null) throw new ArgumentException("Separate original active work requires mapped civilians."); ActiveWork.Validate(s.ActiveWorkV1, s.NativeResidents.Select(r => r.Id)); }
        if (s.WorktablesV1 != null && (s.ActiveWorkV1 == null || s.WorktablesV1.Length is < 1 or > 1000 || s.WorktablesV1.Any(t => t == null || !Key(t.Id) || !CityStockProof.Valid(t.Configuration) ||
            !s.Things.Any(r => r.Id == t.Id) || s.OwnedAssets?.Any(r => r.Id == t.Id) != true || t.Bills == null || t.Bills.Length > 15 || t.Bills.Any(b => !Key(b))) ||
            !s.WorktablesV1.Select(t => t.Id).SequenceEqual(s.WorktablesV1.Select(t => t.Id).Distinct().OrderBy(n => n, StringComparer.Ordinal)) ||
            s.WorktablesV1.SelectMany(t => t.Bills).Distinct().Count() != s.WorktablesV1.Sum(t => t.Bills.Length))) throw new ArgumentException("Original distinct owned worktables/bills required.");
        if (s.WorkFramesV1 != null)
        {
            if (s.ActiveWorkV1 == null || s.WorkFramesV1.Length is < 1 or > 1000 || s.WorkFramesV1.Any(f => f == null || !Key(f.Id) || !CityStockProof.Valid(f.Configuration) ||
                !s.Things.Any(r => r.Id == f.Id) || s.OwnedAssets?.Any(r => r.Id == f.Id) != true || s.WorktablesV1?.Any(t => t.Id == f.Id) == true ||
                f.Resources == null || f.Resources.Length > 10000 || f.Resources.Any(id => !Key(id)) || f.StorageV7!=null&&!CityStockProof.Valid(f.StorageV7) || f.TerrainV8!=null&&(!Key(f.TerrainV8)||f.StorageV7!=null)) ||
                !s.WorkFramesV1.Select(f => f.Id).SequenceEqual(s.WorkFramesV1.Select(f => f.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal))) throw new ArgumentException("Original owned construction frames required.");
            var occupied = new HashSet<string>(s.Things.Select(t => t.Id).Concat((s.Held ?? []).Select(r => r.Item.Id)).Concat((s.NativeResidents ?? []).Select(r => r.Id)).Concat((s.Visitors ?? []).Select(r => r.Id)), StringComparer.Ordinal);
            foreach(var frame in s.WorkFramesV1.Where(frame=>frame.TerrainLayersV9!=null)){if(frame.TerrainV8==null)throw new ArgumentException("Layered frame scope requires its original terrain definition.");WorkTerrainLayerRules.Check(frame.TerrainLayersV9);}
            foreach (var resource in s.WorkFramesV1.SelectMany(f => f.Resources)) if (!occupied.Add(resource) || occupied.Count > 10000) throw new ArgumentException("Duplicated/oversized original construction resource scope.");
        }
        if (s.GroundRestV1 != null) { if (s.NativeResidents == null || s.RoyalMappedV1 != null) throw new ArgumentException("Rest scope requires separate original mapped civilians."); GroundRest.Validate(s.GroundRestV1, s.NativeResidents.Select(r => r.Id)); }
        if (s.RoyalMappedV1 != null && (s.RoyalMappedV1 != 1 || s.NativeResidents == null || s.ReadOnlyBystandersV1 != null)) throw new ArgumentException("Incomplete original royal map scope.");
        if (s.NativeResidents != null)
        {
            var objectIds = new HashSet<string>(s.Things.Select(t => t.Id).Concat((s.Held ?? []).Select(r => r.Item.Id)), StringComparer.Ordinal);
            if (s.NativeResidents.Length is < 1 or > 128 || objectIds.Count + s.NativeResidents.Length > 10000 ||
                s.NativeResidents.Any(r => r == null || !Key(r.Id) || !CityStockProof.Valid(r.Preserved) || objectIds.Contains(r.Id)) ||
                !s.NativeResidents.Select(r => r.Id).SequenceEqual(s.NativeResidents.Select(r => r.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Invalid original map resident scope.");
        }
        if (s.ReadOnlyBystandersV1 != null && (s.ReadOnlyBystandersV1 != 1 || s.NativeResidents != null || s.Visitors == null)) throw new ArgumentException("Unknown or expanded read-only bystander scope.");
        if (s.Visitors != null)
        {
            var used = new HashSet<string>(s.Things.Select(t => t.Id).Concat((s.Held ?? []).Select(r => r.Item.Id)).Concat((s.NativeResidents ?? []).Select(r => r.Id)), StringComparer.Ordinal);
            if (s.NativeResidents == null && s.ReadOnlyBystandersV1 != 1 || s.Visitors.Length is < 1 or > 128 || used.Count + s.Visitors.Length > 10000 ||
                s.Visitors.Any(r => r == null || !Key(r.Id) || !CityStockProof.Valid(r.Preserved) || used.Contains(r.Id)) ||
                !s.Visitors.Select(r => r.Id).SequenceEqual(s.Visitors.Select(r => r.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal))) throw new ArgumentException("Invalid original bystander scope.");
        }
        if (s.OwnedAssets != null && ((s.NativeResidents != null || s.Visitors != null) && s.CivicAssetsV1 != 1 || s.ReadOnlyBystandersV1 != null || s.OwnedAssets.Length is < 1 or > 1000 ||
            s.OwnedAssets.Any(a => a == null || !Key(a.Id) || !Key(a.Owner) || !CityStockProof.Valid(a.Configuration) || !s.Things.Any(t => t.Id == a.Id) && (s.HeldAssetsV1 != 1 || !(s.Held ?? []).Any(r => r.Item.Id == a.Id)) ||
                s.OwnedStorageV1 != 1 && (s.Storage ?? []).Any(x => x.Id == a.Id) || s.OwnedContainersV1 != 1 && (s.Containers ?? []).Any(x => x.Id == a.Id)) ||
            !s.OwnedAssets.Select(a => a.Id).SequenceEqual(s.OwnedAssets.Select(a => a.Id).Distinct().OrderBy(x => x, StringComparer.Ordinal))))
            throw new ArgumentException("Invalid or overlapping original fixed property scope.");
        if (s.OwnedHolderForestV1 != null && (s.OwnedHolderForestV1 != 1 || s.OwnedAssets == null ||
            !s.OwnedAssets.Any(a => (s.Held ?? []).Any(r => r.Holder == a.Id) || (s.Containers ?? []).Any(c => c.Id == a.Id)))) throw new ArgumentException("Unknown or incomplete owned holder forest.");
        if (s.OwnedHolderForestV1 != 1 && s.OwnedAssets?.Any(a => (s.Held ?? []).Any(r => r.Holder == a.Id) && !(s.Containers ?? []).Any(c => c.Id == a.Id)) == true) throw new ArgumentException("Owned packages require their own original holder scope.");
        if (s.HeldAssetsV1 != null && (s.HeldAssetsV1 != 1 || s.OwnedAssets == null || s.Held == null || s.OwnedHolderForestV1 != 1 && s.OwnedAssets.Any(a => s.Held.Any(r => r.Item.Id == a.Id) && (s.Containers ?? []).Any(c => c.Id == a.Id)) ||
            !s.OwnedAssets.Any(a => s.Held.Any(r => r.Item.Id == a.Id)))) throw new ArgumentException("Unknown or incomplete original held property scope.");
        if (s.OwnedContainersV1 != null && (s.OwnedContainersV1 != 1 || s.OwnedAssets == null || s.Containers == null ||
            !s.OwnedAssets.Any(a => s.Containers.Any(c => c.Id == a.Id)))) throw new ArgumentException("Unknown or incomplete original owned container scope.");
        if (s.CivicAssetsV1 != null && (s.CivicAssetsV1 != 1 || s.NativeResidents == null || s.OwnedAssets == null || s.ReadOnlyBystandersV1 != null)) throw new ArgumentException("Unknown or incomplete civic/property scope.");
        if (s.OwnedStorageV1 != null)
        {
            if (s.OwnedStorageV1 != 1 || s.OwnedAssets == null || s.Storage == null || !s.OwnedAssets.Any(a => s.Storage.Any(r => r.Id == a.Id))) throw new ArgumentException("Unknown original owned storage scope.");
            var owned = new HashSet<string>(s.OwnedAssets.Select(a => a.Id), StringComparer.Ordinal);
            if ((s.Groups ?? []).Any(g => g.Members.Any(owned.Contains) && !g.Members.All(owned.Contains))) throw new ArgumentException("A shared owned group requires every original member's property authority.");
        }
        if (s.AssetRepairV1 != null)
        {
            if (s.AssetRepairV1 != 1 || s.OwnedAssets == null || !s.OwnedAssets.Any(a => a.Repair != null)) throw new ArgumentException("Unknown original repair scope.");
            foreach (var a in s.OwnedAssets.Where(a => a.Repair != null))
            {
                var item = s.Things.Concat((s.Held ?? []).Select(r => r.Item)).Single(t => t.Id == a.Id);
                if (a.Repair.Maximum <= 0 || item.HitPoints <= 0 || item.HitPoints > a.Repair.Maximum) throw new ArgumentException("Invalid original asset health.");
            }
        }
        else if (s.OwnedAssets?.Any(a => a.Repair != null) == true) throw new ArgumentException("Repair evidence cannot backfill an old property scope.");
        return s;
    }
    public static void Pair(string json, string town, string proof)
    {
        var s = Restore(json); if (s.Town != town || s.Proof != proof) throw new ArgumentException("Original loaded map differs from transfer scope.");
    }
    public static string CaptureActiveWork(string original, IEnumerable<ActiveWorkRow> rows, IEnumerable<NativeWorktable> tables = null, IEnumerable<NativeWorkFrame> frames = null)
    {
        var s = Restore(original);
        if (s.ActiveWorkV1 != null || s.GroundRestV1 != null || s.RoyalMappedV1 != null || s.NativeResidents == null) throw new ArgumentException("Fresh separate original active-work map required.");
        s = s with { ActiveWorkV1 = ActiveWork.Validate(rows, s.NativeResidents.Select(r => r.Id)), WorktablesV1 = tables?.OrderBy(t => t.Id, StringComparer.Ordinal).ToArray(), WorkFramesV1 = frames?.OrderBy(f => f.Id, StringComparer.Ordinal).ToArray(), Proof = null }; s = s with { Proof = Hash(s) };
        string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureGroundRest(string original, IEnumerable<GroundRestRow> rows, bool given = false)
    {
        var s = Restore(original);
        if (s.ActiveWorkV1 != null || s.GroundRestV1 != null || s.RoyalMappedV1 != null || s.NativeResidents == null) throw new ArgumentException("Fresh original ground-rest map required.");
        s = s with { GroundRestV1 = GroundRest.Validate(rows, s.NativeResidents.Select(r => r.Id)), GivenGroundRestV1 = given ? 1 : null, Proof = null }; s = s with { Proof = Hash(s) };
        string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static string CaptureRoyalMapped(string original)
    {
        var s = Restore(original);

        if (s.ActiveWorkV1 != null || s.RoyalMappedV1 != null || s.GroundRestV1 != null || s.NativeResidents == null) throw new ArgumentException("Fresh original mapped royal scope required.");
        s = s with { RoyalMappedV1 = 1, Proof = null }; s = s with { Proof = Hash(s) };
        string json = JsonSerializer.Serialize(s); _ = Restore(json); return json;
    }
    public static int Format(string json)
    {
        var s = Restore(json); return s.ActiveWorkV1 != null ? 25 : s.GivenGroundRestV1 == 1 ? 24 : s.GroundRestV1 != null ? 23 : s.RoyalMappedV1 == 1 ? 22 : s.OwnedHolderForestV1 == 1 ? (s.CivicAssetsV1 == 1 ? 21 : 20) : s.HeldAssetsV1 == 1 ? (s.CivicAssetsV1 == 1 ? 19 : 18) : s.OwnedContainersV1 == 1 ? (s.CivicAssetsV1 == 1 ? 17 : 16) : s.CivicAssetsV1 == 1 ? 15 : s.OwnedStorageV1 == 1 ? 14 : s.AssetRepairV1 == 1 ? 13 : s.OwnedAssets != null ? 12 : s.ReadOnlyBystandersV1 == 1 ? 11 : s.NativeResidents != null ? 10 : s.Containers != null ? 9 : s.Growing != null ? 8 : s.Stockpiles != null ? 7 : s.Groups != null ? 6 : s.Storage != null ? 5 : s.Held == null ? 3 : 4;
    }
    public static void Pair(string json, string town, string proof, int format)
    {
        Pair(json, town, proof); if (Format(json) != format) throw new ArgumentException("Original loaded-map custody format changed.");
    }
}
