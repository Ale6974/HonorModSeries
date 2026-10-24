using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record ResidentEvidence(string Id, string Faction, string Health, string Gear, string Status);
[Flags]
public enum ResidentDifference { None = 0, Added = 1, Removed = 2, Faction = 4, Health = 8, Gear = 16, Status = 32 }
public sealed record ResidentComparison(string Id, ResidentDifference Difference);
public sealed record ResidentBaselineComparison(bool CityOwnerChanged, long ObservedTick, IReadOnlyList<ResidentComparison> Residents);
public sealed record ResidentBaselineSave(int Version, string City, string Owner, long Tick, ResidentEvidence[] Residents);

/// <summary>Bounded diagnostic evidence only. Never consent, transfer authority or execution proof.</summary>
public sealed class ResidentBaseline
{
    public const int Capacity = 1000;
    public string City { get; }
    public string Owner { get; }
    public long Tick { get; }
    public IReadOnlyList<ResidentEvidence> Residents { get; }
    private ResidentBaseline(string city, string owner, long tick, ResidentEvidence[] residents)
    { City = city; Owner = owner; Tick = tick; Residents = Array.AsReadOnly(residents); }
    private static void Key(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 120 || key.Any(char.IsControl)) throw new ArgumentException("Invalid baseline identity.");
    }
    public static ResidentBaseline Capture(string city, string owner, long tick, IEnumerable<ResidentEvidence> residents)
    {
        Key(city); Key(owner); RuntimeCompat.NotNull(residents);
        var entries = residents.Take(Capacity + 1).ToArray();
        if (tick < 0 || tick > int.MaxValue || entries.Length > Capacity) throw new ArgumentException("Invalid baseline size or clock.");
        foreach (var entry in entries)
        {
            RuntimeCompat.NotNull(entry); Key(entry.Id); if (entry.Faction != null) Key(entry.Faction);
            if (!CityStockProof.Valid(entry.Health) || !CityStockProof.Valid(entry.Gear) || !CityStockProof.Valid(entry.Status))
                throw new ArgumentException("Invalid resident evidence.");
        }
        if (entries.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != entries.Length) throw new ArgumentException("Repeated resident identity.");
        return new(city, owner, tick, entries.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray());
    }
    public ResidentBaselineComparison Compare(string city, string owner, long tick, IEnumerable<ResidentEvidence> current)
    {
        if (city != City || tick < Tick) throw new ArgumentException("Different city or reversed baseline clock.");
        var observed = Capture(city, owner, tick, current);
        var before = Residents.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var after = observed.Residents.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var result = new List<ResidentComparison>();
        foreach (string id in before.Keys.Union(after.Keys).OrderBy(id => id, StringComparer.Ordinal))
        {
            ResidentDifference difference = ResidentDifference.None;
            if (!before.TryGetValue(id, out var old)) difference = ResidentDifference.Added;
            else if (!after.TryGetValue(id, out var next)) difference = ResidentDifference.Removed;
            else
            {
                if (old.Faction != next.Faction) difference |= ResidentDifference.Faction;
                if (old.Health != next.Health) difference |= ResidentDifference.Health;
                if (old.Gear != next.Gear) difference |= ResidentDifference.Gear;
                if (old.Status != next.Status) difference |= ResidentDifference.Status;
            }
            result.Add(new(id, difference));
        }
        return new(Owner != owner, tick, result.AsReadOnly());
    }
    public string Export() => JsonSerializer.Serialize(new ResidentBaselineSave(1, City, Owner, Tick, Residents.ToArray()));
    public static ResidentBaseline Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 2000000) throw new ArgumentException("Invalid baseline snapshot.");
        var save = JsonSerializer.Deserialize<ResidentBaselineSave>(json);
        if (save?.Version != 1) throw new ArgumentException("Unsupported baseline version.");
        return Capture(save.City, save.Owner, save.Tick, save.Residents);
    }
}
