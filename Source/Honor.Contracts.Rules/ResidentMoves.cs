using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record ResidentMoveEvidence(string Id, string Preserved, int? MedicalCare);
/// <summary>Faction is the changing authority. Custody profiles independently bind the prisoner's original affiliation.</summary>
public sealed record ResidentMoveActual(string Id, string Faction, string Preserved, int? MedicalCare);
public enum ResidentMoveAction { ChangeFaction, RestoreMedicalCare, Verify }

public static class ResidentMoveProfiles
{
    public static string BindMobility(string previous, string status, bool downed)
    {
        if (!CityStockProof.Valid(previous) || !CityStockProof.Valid(status)) throw new ArgumentException("Complete original clinical/mobility evidence required.");
        return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Profile = 8, Previous = previous, Status = status, Downed = downed }))));
    }
    public static string Compose(string legacy, string surgery, int profile, string ideology = null, string custody = null)
        => Compose(legacy, surgery, profile, ideology, custody, null);
    public static string Compose(string legacy, string surgery, int profile, string ideology, string custody, string genetics)
    {
        if (!CityStockProof.Valid(legacy) || profile is not (1 or 2 or 3 or 4 or 5 or 6 or 7) || (profile == 1 ? surgery != null : !CityStockProof.Valid(surgery)) ||
            (profile >= 3 ? !CityStockProof.Valid(ideology) : ideology != null) || (profile >= 4 ? !CityStockProof.Valid(custody) : custody != null) ||
            (profile == 7 ? !CityStockProof.Valid(genetics) : genetics != null))
            throw new ArgumentException("Invalid resident preservation profile.");
        if (profile == 7) return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { Profile = 7, Previous = Compose(legacy, surgery, 6, ideology, custody), Genetics = genetics }))));
        if (profile == 6) return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { Profile = 6, Previous = Compose(legacy, surgery, 5, ideology, custody) }))));
        if (profile == 5) return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { Profile = 5, Previous = Compose(legacy, surgery, 4, ideology, custody) }))));
        if (profile == 4) return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { Profile = 4, Previous = Compose(legacy, surgery, 3, ideology), Custody = custody }))));
        if (profile == 3) return RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { Profile = 3, Previous = Compose(legacy, surgery, 2), Ideology = ideology }))));
        return profile == 1 ? legacy : RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { Profile = 2, Legacy = legacy, Surgery = surgery }))));
    }
}

/// <summary>A bounded preservation contract for an executing city transfer, never a diagnostic baseline.</summary>
public sealed class ResidentMoveSet
{
    public const int Capacity = 128;
    public IReadOnlyList<ResidentMoveEvidence> Residents { get; }
    public string Proof { get; }
    private ResidentMoveSet(ResidentMoveEvidence[] rows)
    {
        Residents = Array.AsReadOnly(rows);
        Proof = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Version = 1, Residents = rows }))));
    }
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    public static ResidentMoveSet Capture(IEnumerable<ResidentMoveEvidence> residents)
    {
        RuntimeCompat.NotNull(residents); var rows = residents.Take(Capacity + 1).OrderBy(r => r?.Id, StringComparer.Ordinal).ToArray();
        if (rows.Length is < 1 or > Capacity || rows.Any(r => r == null || !Key(r.Id) || !CityStockProof.Valid(r.Preserved) || r.MedicalCare is < 0 or > 4) ||
            rows.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != rows.Length) throw new ArgumentException("Invalid resident preservation set.");
        return new(rows);
    }
    public IReadOnlyList<ResidentMoveAction> Review(IEnumerable<ResidentMoveActual> residents, string source, string target, bool permitMedicalRepair)
    {
        RuntimeCompat.NotNull(residents);
        if (!Key(source) || !Key(target) || source == target) throw new ArgumentException("Invalid resident transfer parties.");
        var current = residents.Take(Capacity + 1).OrderBy(r => r?.Id, StringComparer.Ordinal).ToArray();
        if (current.Length != Residents.Count) throw new ArgumentException("Resident set changed.");
        var actions = new List<ResidentMoveAction>();
        for (int i = 0; i < current.Length; i++)
        {
            actions.Add(Decide(Residents[i], current[i], source, target, permitMedicalRepair));
        }
        return actions.AsReadOnly();
    }
    private static ResidentMoveAction Decide(ResidentMoveEvidence before, ResidentMoveActual actual, string source, string target, bool permitMedicalRepair)
    {
        if (!Key(source) || !Key(target) || source == target || actual == null || actual.Id != before.Id || actual.Preserved != before.Preserved ||
            actual.MedicalCare.HasValue != before.MedicalCare.HasValue || actual.MedicalCare is < 0 or > 4 ||
            actual.Faction != source && actual.Faction != target) throw new ArgumentException("Resident identity/state/ownership changed.");
        if (actual.Faction == source)
        {
            if (actual.MedicalCare != before.MedicalCare) throw new ArgumentException("Unmoved medical settings changed.");
            return ResidentMoveAction.ChangeFaction;
        }
        if (actual.MedicalCare != before.MedicalCare)
        {
            if (!permitMedicalRepair) throw new ArgumentException("Medical preservation incomplete.");
            return ResidentMoveAction.RestoreMedicalCare;
        }
        return ResidentMoveAction.Verify;
    }
    public ResidentMoveAction ActionFor(ResidentMoveActual actual, string source, string target, bool permitMedicalRepair)
    {
        var before = Residents.SingleOrDefault(r => r.Id == actual?.Id) ?? throw new ArgumentException("Unrecorded resident.");
        return Decide(before, actual, source, target, permitMedicalRepair);
    }
    public void VerifyDestination(IEnumerable<ResidentMoveActual> residents, string source, string target)
    {
        if (Review(residents, source, target, false).Any(a => a != ResidentMoveAction.Verify)) throw new ArgumentException("Resident migration incomplete.");
    }
    public string Export() => JsonSerializer.Serialize(new Save(1, Residents.ToArray(), Proof));
    public static ResidentMoveSet Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 100000) throw new ArgumentException("Invalid resident execution snapshot size.");
        var saved = JsonSerializer.Deserialize<Save>(json);
        if (saved == null || saved.Version != 1) throw new ArgumentException("Unsupported resident execution snapshot.");
        var set = Capture(saved.Residents);
        if (set.Proof != saved.Proof) throw new ArgumentException("Resident execution proof mismatch.");
        return set;
    }
    private sealed record Save(int Version, ResidentMoveEvidence[] Residents, string Proof);
}
