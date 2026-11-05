using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record MapAsset(string Id, string Owner, string Configuration);
public sealed record MapAssetCall(string Id, bool Started = false, bool Returned = false);
public sealed record MapAssetState(string Town, string Map, string OriginalProof, string Source, string Target, MapAssetCall[] Calls, string Proof);

/// <summary>Original faction-owned fixed property and independent call facts. Unknown calls never replay.</summary>
public static class MapAssetTransfers
{
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static string Hash(MapAssetState s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(s with { Proof = null }))));
    private static string Write(MapAssetState s) { s = s with { Proof = Hash(s) }; string json = JsonSerializer.Serialize(s); _ = Restore(json); return json; }
    public static MapAssetState Restore(string json)
    {
        if (json == null || json.Length > 300000) throw new ArgumentException("Bounded original asset calls required.");
        var s = JsonSerializer.Deserialize<MapAssetState>(json);
        if (s == null || !Key(s.Town) || !Key(s.Map) || !Key(s.Source) || !Key(s.Target) || s.Source == s.Target || !CityStockProof.Valid(s.OriginalProof) || s.Proof != Hash(s) ||
            s.Calls == null || s.Calls.Length is < 1 or > 1000 || s.Calls.Any(c => c == null || !Key(c.Id) || c.Returned && !c.Started) ||
            !s.Calls.Select(c => c.Id).SequenceEqual(s.Calls.Select(c => c.Id).Distinct().OrderBy(x => x, StringComparer.Ordinal))) throw new ArgumentException("Invalid original asset calls.");
        bool unfinished = false;
        foreach (var c in s.Calls) { if (unfinished && c.Started) throw new ArgumentException("Asset calls must retain the original ordered prefix."); if (!c.Returned) unfinished = true; }
        return s;
    }
    public static string Prepare(string mapScope, string source, string target)
    {
        var map = LoadedMapPreservation.Restore(mapScope);
        if (map.OwnedAssets == null || map.OwnedAssets.Any(a => a.Owner != source)) throw new ArgumentException("Original faction property is required; no private or foreign rights are inferred.");
        return Write(new(map.Town, map.Map, map.Proof, source, target, map.OwnedAssets.Select(a => new MapAssetCall(a.Id)).ToArray(), null));
    }
    public static void Pair(string json, string mapScope, string source, string target, bool complete = false)
    {
        var s = Restore(json); var map = LoadedMapPreservation.Restore(mapScope);
        if (s.Town != map.Town || s.Map != map.Map || s.OriginalProof != map.Proof || s.Source != source || s.Target != target || map.OwnedAssets == null ||
            map.OwnedAssets.Any(a => a.Owner != source) || !s.Calls.Select(c => c.Id).SequenceEqual(map.OwnedAssets.Select(a => a.Id)) || complete && s.Calls.Any(c => !c.Returned))
            throw new ArgumentException("Asset calls do not pair with their original map and authority.");
    }
    public static string ExpectedOwner(string json, string id)
    {
        var s = Restore(json); var c = s.Calls.SingleOrDefault(c => c.Id == id) ?? throw new ArgumentException("Unreviewed original asset.");
        if (c.Started && !c.Returned) throw new ArgumentException("Unknown asset call cannot infer success from current ownership.");
        return c.Returned ? s.Target : s.Source;
    }
    public static string Begin(string json, string id, string actualOwner)
    {
        var s = Restore(json); int i = Array.FindIndex(s.Calls, c => c.Id == id);
        if (i < 0 || s.Calls[i].Started || s.Calls.Take(i).Any(c => !c.Returned) || actualOwner != s.Source) throw new ArgumentException("Never replay or rebase an original asset call.");
        var calls = s.Calls.ToArray(); calls[i] = calls[i] with { Started = true }; return Write(s with { Calls = calls });
    }
    public static string Returned(string json, string id, string actualOwner)
    {
        var s = Restore(json); int i = Array.FindIndex(s.Calls, c => c.Id == id);
        if (i < 0 || !s.Calls[i].Started || s.Calls[i].Returned || actualOwner != s.Target) throw new ArgumentException("Positive original normal return and target required.");
        var calls = s.Calls.ToArray(); calls[i] = calls[i] with { Returned = true }; return Write(s with { Calls = calls });
    }
    public static bool Started(string json) => Restore(json).Calls.Any(c => c.Started);
}
