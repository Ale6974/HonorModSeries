using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record ResidentFaithPrecept(string Id, string Def, string Type, bool AssignedRole = false, bool KnownCallback = true, bool PatchedCallback = false,
    bool KnownGuestCallback = true, bool PatchedGuestCallback = false);
public static class ResidentFaith
{
    private static string Hash(object value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 250 && !s.Any(char.IsControl);
    public static string None => Hash(new { Version = 1, Faith = (string)null });
    public static string Capture(string id, float certainty, IEnumerable<ResidentFaithPrecept> precepts, IEnumerable<string> previous, bool ageReversal, bool patchedDispatch = false)
    {
        RuntimeCompat.NotNull(precepts); RuntimeCompat.NotNull(previous);
        var rows = precepts.Take(257).OrderBy(p => p?.Id, StringComparer.Ordinal).ToArray(); var history = previous.Take(257).ToArray();
        if (!Key(id) || !RuntimeCompat.IsFinite(certainty) || certainty is < 0 or > 1 || ageReversal || patchedDispatch || rows.Length > 256 || history.Length > 256 ||
            history.Any(s => !Key(s)) || rows.Any(p => p == null || !Key(p.Id) || !Key(p.Def) || !Key(p.Type) || p.AssignedRole || !p.KnownCallback || p.PatchedCallback ||
                !p.KnownGuestCallback || p.PatchedGuestCallback) ||
            rows.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != rows.Length)
            throw new ArgumentException("Unsupported ordinary resident faith evidence.");
        return Hash(new { Version = 1, Faith = id, Certainty = certainty, Precepts = rows.Select(p => new { p.Id, p.Def, p.Type }).ToArray(), Previous = history });
    }
}
