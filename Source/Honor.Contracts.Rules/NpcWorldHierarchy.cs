namespace Honor.Contracts.Rules;

public sealed record NpcWorldCity(string City, string Owner, string Representative, bool Eligible = true);
public sealed record NpcWorldLink(string City, string Owner, string Liege, string Representative);

public static class NpcWorldHierarchy
{
    public static IReadOnlyList<NpcWorldLink> Plan(IEnumerable<NpcWorldCity> cities, IEnumerable<string> registeredOwners, int percent, int cap = 30)
    {
        RuntimeCompat.NotNull(cities); RuntimeCompat.NotNull(registeredOwners);
        var all = cities.Take(10001).ToArray(); var excluded = new HashSet<string>(registeredOwners, StringComparer.Ordinal);
        static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 100 && !s.Any(char.IsControl);
        if (percent < 0 || percent > 100 || cap < 1 || cap > 30 || all.Length > 10000 ||
            all.Any(c => c == null || !Key(c.City) || !Key(c.Owner) || !Key(c.Representative)) ||
            all.Select(c => c.City).Distinct(StringComparer.Ordinal).Count() != all.Length)
            throw new ArgumentException("Invalid NPC world candidates or setup limits.");
        var result = new List<NpcWorldLink>();
        foreach (var group in all.Where(c => c.Eligible && !excluded.Contains(c.Owner)).GroupBy(c => c.Owner).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            if (group.Select(c => c.Representative).Distinct(StringComparer.Ordinal).Count() != 1)
                throw new ArgumentException("One current faction representative is required for internal setup.");
            var selected = group.OrderBy(c => c.City, StringComparer.Ordinal).Take((group.Count() * percent + 99) / 100).ToArray();
            for (int i = 0; i < selected.Length && result.Count < cap; i++)
            {
                var c = selected[i];
                result.Add(new(c.City, c.Owner, i % 3 == 0 ? "faction/" + c.Owner : selected[i - 1].City, c.Representative));
            }
            if (result.Count == cap) break;
        }
        return result.AsReadOnly();
    }
}
