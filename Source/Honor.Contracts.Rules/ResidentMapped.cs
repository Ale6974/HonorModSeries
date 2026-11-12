using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record ResidentMapBinding(string Id, string Map, int Cell, int Rotation);
public static class ResidentMapped
{
    private static bool Key(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 120 && !value.Any(char.IsControl);
    public static string Prepare(IEnumerable<ResidentMapBinding> rows)
    {
        if (rows == null) throw new ArgumentException("Original mapped roster required.");
        var original = rows.Take(129).ToArray();
        if (original.Any(r => r == null)) throw new ArgumentException("Missing original mapped resident.");
        string json = JsonSerializer.Serialize(original.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray()); _ = Restore(json); return json;
    }
    public static ResidentMapBinding[] Restore(string json)
    {
        if (json == null || json.Length > 100000) throw new ArgumentException("Original map bindings required.");
        var rows = JsonSerializer.Deserialize<ResidentMapBinding[]>(json);
        if (rows == null || rows.Length is < 1 or > 128 || rows.Any(r => r == null || !Key(r.Id) || r.Rotation is < 0 or > 3 ||
            (r.Map == null ? r.Cell != -1 || r.Rotation != 0 : !Key(r.Map) || r.Cell < 0)) ||
            !rows.Select(r => r.Id).SequenceEqual(rows.Select(r => r.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal))) throw new ArgumentException("Invalid original map bindings.");
        return rows;
    }
    public static string Bind(string originalAreas, ResidentMapBinding binding)
    {
        _ = Restore(Prepare([binding]));
        if (!CityStockProof.Valid(originalAreas)) throw new ArgumentException("Original clinical/area proof required.");
        return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Profile = 11, Previous = originalAreas, Binding = binding }))));
    }
    public static string BindRoyal(string originalAreas, ResidentMapBinding binding)
        => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Profile = 13, Original = Bind(originalAreas, binding) }))));
    public static void Pair(string json, ResidentMoveSet set, string originalMap)
    {
        var rows = Restore(json); var map = LoadedMapPreservation.Restore(originalMap);
        if (map.NativeResidents == null || !rows.Select(r => r.Id).SequenceEqual(set.Residents.Select(r => r.Id))) throw new ArgumentException("Unpaired mapped resident census.");
        var live = rows.Where(r => r.Map != null).ToArray();
        if (live.Any(r => r.Map != map.Map || r.Cell >= map.Width * map.Height) ||
            !live.Select(r => r.Id).SequenceEqual(map.NativeResidents.Select(r => r.Id)) ||
            map.NativeResidents.Any(r => set.Residents.Single(s => s.Id == r.Id).Preserved != r.Preserved)) throw new ArgumentException("Original map and clinical scope differ.");
    }
}
