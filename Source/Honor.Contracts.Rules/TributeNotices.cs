using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record TributeNotice(string CharterId, int Period, long DueTick, long GraceUntil, decimal RequiredValue, long NotifiedTick);
public sealed record TributeNoticeSave(int Version, bool Enabled, long LastScan, TributeNotice[] Notices);

/// <summary>Saved, bounded delivery of informational reminders. Never creates or changes an obligation.</summary>
public sealed class TributeNotices
{
    public const int MaxNotices = 10000;
    private readonly Dictionary<(string, int), TributeNotice> notices = [];
    public IReadOnlyCollection<TributeNotice> Notices => notices.Values.ToArray();
    public bool Enabled { get; private set; } = true;
    public long LastScan { get; private set; } = -1;
    private void Time(long now)
    { if (now < 0 || now < LastScan || now > int.MaxValue) throw new ArgumentException("Invalid reminder time."); }
    public void SetEnabled(bool enabled, long now)
    { Time(now); if (Enabled != enabled) { Enabled = enabled; LastScan = now; } }
    public bool BeginScan(long now)
    {
        Time(now);
        if (LastScan < 0 || !Enabled) { LastScan = now; return false; }
        if (now - LastScan < PoliticalWorld.Day) return false;
        LastScan = now; return true;
    }
    public IReadOnlyList<TributeNotice> RecordBatch(IEnumerable<TributeBill> verifiedOverdue, long now)
    {
        RuntimeCompat.NotNull(verifiedOverdue); Time(now);
        if (!Enabled || LastScan != now) throw new ArgumentException("Start the current reminder scan first.");
        var bills = verifiedOverdue.Take(MaxNotices + 1).ToArray();
        if (bills.Length > MaxNotices || bills.Any(b => b == null || string.IsNullOrWhiteSpace(b.CharterId) || b.CharterId.Length > 120 ||
            b.CharterId.Any(char.IsControl) || b.Period is < 1 or > TributeLedger.MaxPeriods || b.DueTick < 0 || b.GraceUntil < b.DueTick ||
            b.GraceUntil >= now || b.RequiredValue is <= 0 or > 15000000) ||
            bills.Select(b => (b.CharterId, b.Period)).Distinct().Count() != bills.Length)
            throw new ArgumentException("Invalid actual overdue reminder batch.");
        var added = bills.Where(b => !notices.ContainsKey((b.CharterId, b.Period)))
            .OrderBy(b => b.DueTick).ThenBy(b => b.CharterId, StringComparer.Ordinal).ThenBy(b => b.Period)
            .Take(MaxNotices - notices.Count).Select(b => new TributeNotice(b.CharterId, b.Period, b.DueTick, b.GraceUntil, b.RequiredValue, now)).ToArray();
        foreach (var n in added) notices.Add((n.CharterId, n.Period), n);
        return Array.AsReadOnly(added);
    }
    public string Export() => JsonSerializer.Serialize(new TributeNoticeSave(1, Enabled, LastScan, notices.Values.ToArray()));
    public static TributeNotices Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 12000000) throw new ArgumentException("Invalid reminder snapshot size.");
        var save = JsonSerializer.Deserialize<TributeNoticeSave>(json);
        if (save == null || save.Version != 1 || save.LastScan < -1 || save.LastScan > int.MaxValue || save.Notices == null ||
            save.Notices.Length > MaxNotices || save.Notices.Any(n => n == null)) throw new ArgumentException("Invalid reminder snapshot.");
        var result = new TributeNotices();
        foreach (var n in save.Notices)
        {
            if (n.NotifiedTick > save.LastScan || n.NotifiedTick < 0) throw new ArgumentException("Invalid reminder chronology.");
            result.LastScan = n.NotifiedTick;
            var added = result.RecordBatch(new[] { new TributeBill(n.CharterId, n.Period, n.DueTick, n.GraceUntil, n.RequiredValue, n.RequiredValue) }, n.NotifiedTick);
            if (added.Count != 1) throw new ArgumentException("Duplicate reminder identity.");
        }
        result.Enabled = save.Enabled; result.LastScan = save.LastScan; return result;
    }
}
