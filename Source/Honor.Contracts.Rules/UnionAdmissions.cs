using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum UnionAdmissionPhase { Prepared, Applied, Cancelled }
public enum UnionExclusion { None, Player, AlreadyUnion, LoadedMap, PreviouslyVisited, RecordedCharter, Hostile, IneligibleFaction, LastSettlement }
public sealed record UnionCandidate(string SettlementId, string OwnerId, int OwnerSettlementCount, bool Player = false,
    bool AlreadyUnion = false, bool HasMap = false, bool Visited = false, bool RecordedCharter = false,
    bool Hostile = false, bool EligibleFaction = true);
public sealed record UnionAdmission(string Id, string SettlementId, string OriginalOwner, string UnionOwner, long PreparedTick,
    UnionAdmissionPhase Phase = UnionAdmissionPhase.Prepared, long? ResolvedTick = null);
public sealed record UnionAdmissionSave(int Version, long LastTick, UnionAdmission[] Admissions);

/// <summary>Initial-world configuration only. It is not surrender, NPC consent, or a lawful contract release.</summary>
public static class UnionAdmissionRules
{
    public static UnionExclusion Exclusion(UnionCandidate c)
    {
        RuntimeCompat.NotNull(c);
        if (c.Player) return UnionExclusion.Player;
        if (c.AlreadyUnion) return UnionExclusion.AlreadyUnion;
        if (c.HasMap) return UnionExclusion.LoadedMap;
        if (c.Visited) return UnionExclusion.PreviouslyVisited;
        if (c.RecordedCharter) return UnionExclusion.RecordedCharter;
        if (c.Hostile) return UnionExclusion.Hostile;
        if (!c.EligibleFaction) return UnionExclusion.IneligibleFaction;
        return c.OwnerSettlementCount < 2 ? UnionExclusion.LastSettlement : UnionExclusion.None;
    }
    public static IReadOnlyList<UnionCandidate> Plan(IEnumerable<UnionCandidate> candidates, int percent, int limit = 10)
    {
        RuntimeCompat.NotNull(candidates);
        if (percent is < 0 or > 30 || limit is < 1 or > 20) throw new ArgumentException("Invalid initial union configuration.");
        var all = candidates.ToArray();
        if (all.Any(c => c == null || string.IsNullOrWhiteSpace(c.SettlementId) || string.IsNullOrWhiteSpace(c.OwnerId)) ||
            all.Select(c => c.SettlementId).Distinct(StringComparer.Ordinal).Count() != all.Length)
            throw new ArgumentException("Duplicate or missing settlement identity.");
        var eligible = all.Where(c => Exclusion(c) == UnionExclusion.None).OrderBy(c => c.SettlementId, StringComparer.Ordinal).ToArray();
        int desired = Math.Min(limit, (int)Math.Ceiling(eligible.Length * percent / 100m));
        // At most one city per original faction. This both preserves the source and avoids a large faction
        // supplying every initial city. The adapter rechecks actual counts before each mutation.
        return Array.AsReadOnly(eligible.GroupBy(c => c.OwnerId, StringComparer.Ordinal).Select(g => g.First()).Take(desired).ToArray());
    }
}

public sealed class UnionAdmissionLedger
{
    private readonly Dictionary<string, UnionAdmission> admissions = new(StringComparer.Ordinal);
    public const int Version = 1, MaxAdmissions = 200;
    public long LastTick { get; private set; }
    public IReadOnlyCollection<UnionAdmission> Admissions => admissions.Values.ToArray();
    private static void Require(bool valid, string reason) { if (!valid) throw new ArgumentException(reason); }
    private static void Key(string id) => Require(!string.IsNullOrWhiteSpace(id) && id.Length <= 120 && !id.Any(char.IsControl), "Invalid admission identity.");
    private void Time(long now) => Require(now >= LastTick && now <= int.MaxValue, "Invalid or reversed configuration time.");
    public UnionAdmission Admission(string id) => admissions.TryGetValue(id ?? "", out var record) ? record : throw new ArgumentException("Unknown admission.");
    public UnionAdmission Prepare(string id, UnionCandidate observed, string unionOwner, long now)
    {
        Key(id); Key(unionOwner); RuntimeCompat.NotNull(observed); Key(observed.SettlementId); Key(observed.OwnerId); Time(now);
        Require(observed.OwnerId != unionOwner && UnionAdmissionRules.Exclusion(observed) == UnionExclusion.None,
            "The current settlement is not eligible for initial admission.");
        Require(admissions.Count < MaxAdmissions && !admissions.ContainsKey(id) && !admissions.Values.Any(a =>
            a.SettlementId == observed.SettlementId && a.Phase != UnionAdmissionPhase.Cancelled), "Duplicate or unresolved admission.");
        var admission = new UnionAdmission(id, observed.SettlementId, observed.OwnerId, unionOwner, now);
        admissions.Add(id, admission); LastTick = now; return admission;
    }
    public void Confirm(string id, string currentSettlement, string currentOwner, bool hasLoadedMap, bool isPlayer, long now)
    {
        Time(now); var record = Admission(id);
        Require(record.Phase == UnionAdmissionPhase.Prepared && currentSettlement == record.SettlementId &&
            currentOwner == record.UnionOwner && !hasLoadedMap && !isPlayer, "Actual nonplayer ownership must match the intended union.");
        admissions[id] = record with { Phase = UnionAdmissionPhase.Applied, ResolvedTick = now }; LastTick = now;
    }
    public void CancelUnmoved(string id, string currentSettlement, string currentOwner, long now)
    {
        Time(now); var record = Admission(id);
        Require(record.Phase == UnionAdmissionPhase.Prepared && currentSettlement == record.SettlementId && currentOwner == record.OriginalOwner,
            "Only a verified unmoved settlement can cancel a pending admission.");
        admissions[id] = record with { Phase = UnionAdmissionPhase.Cancelled, ResolvedTick = now }; LastTick = now;
    }
    public string Export() => JsonSerializer.Serialize(new UnionAdmissionSave(Version, LastTick, admissions.Values.ToArray()));
    public static UnionAdmissionLedger Restore(string json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 1000000, "Invalid admission snapshot size.");
        var save = JsonSerializer.Deserialize<UnionAdmissionSave>(json);
        Require(save != null && save.Version == Version && save.LastTick is >= 0 and <= int.MaxValue && save.Admissions != null &&
            save.Admissions.Length <= MaxAdmissions, "Unsupported admission snapshot.");
        var ledger = new UnionAdmissionLedger();
        foreach (var record in save.Admissions)
        {
            RuntimeCompat.NotNull(record); Key(record.Id); Key(record.SettlementId); Key(record.OriginalOwner); Key(record.UnionOwner);
            Require(!ledger.admissions.ContainsKey(record.Id) && record.OriginalOwner != record.UnionOwner && RuntimeCompat.IsDefined(record.Phase) &&
                record.PreparedTick >= 0 && record.PreparedTick <= save.LastTick &&
                (record.Phase == UnionAdmissionPhase.Prepared ? !record.ResolvedTick.HasValue :
                    record.ResolvedTick >= record.PreparedTick && record.ResolvedTick <= save.LastTick), "Invalid saved admission.");
            Require(record.Phase == UnionAdmissionPhase.Cancelled || ledger.admissions.Values.All(a =>
                a.SettlementId != record.SettlementId || a.Phase == UnionAdmissionPhase.Cancelled), "Multiple active admission records for one city.");
            ledger.admissions.Add(record.Id, record);
        }
        ledger.LastTick = save.LastTick; return ledger;
    }
}
