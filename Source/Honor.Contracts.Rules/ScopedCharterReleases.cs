using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record ScopedCharterReleaseWitness(string Charter, long Tick, string Book, string Payments, string Transfers,
    string Observations, NpcRealmDecisionEvidence Decision, string GovernmentDecision);
public sealed record ScopedCharterReleaseRecord(ScopedCharterReleaseWitness Witness, string Proof, CharterReleaseReview Plan);
public sealed record ScopedCharterReleaseSave(int Version, ScopedCharterReleaseRecord[] Records);

/// <summary>Historical action-specific release consent; no ownership/admission authority or erased liabilities.</summary>
public sealed class ScopedCharterReleases
{
    private readonly List<ScopedCharterReleaseRecord> records = [];
    public IReadOnlyList<ScopedCharterReleaseRecord> Records => records.ToArray();
    private static string Hash<T>(T x) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(x))));
    public static string Scope(ScopedCharterReleaseWitness w) => Hash(w with { GovernmentDecision = null });
    public static string Marker(ScopedCharterReleaseRecord r) => r.Witness.Charter + ":" + r.Proof;
    public ScopedCharterReleases Append(ScopedCharterReleaseWitness w, CharterBook after, TributeLedger payments)
    {
        var plan = CharterReleases.ReviewGovernment(w);
        if (plan.After != after.Export() || plan.PaymentsAfter != payments.Export() || records.Count >= 128 || records.Any(r => r.Witness.Charter == w.Charter) ||
            records.LastOrDefault()?.Witness.Tick > w.Tick) throw new ArgumentException("Stale, duplicate or full release history.");
        var copy = Restore(Export()); copy.records.Add(new(w, Hash(w), plan)); copy.ValidateBook(after);
        if (copy.Export().Length > 32000000) throw new ArgumentException("Release government history capacity.");
        return copy;
    }
    public void ValidateBook(CharterBook book)
    {
        foreach (var r in records)
        {
            var original = CharterBook.Restore(r.Plan.After);
            if (book.Charter(r.Witness.Charter) != original.Charter(r.Witness.Charter) || book.LastTick < r.Witness.Tick || original.Entities.Any(e => book.Entity(e.Id) != e))
                throw new ArgumentException("Original scoped release history changed.");
            foreach (var old in original.Charters)
            {
                var c = book.Charter(old.Id);
                if (c.Terms != old.Terms || c.OfferedTick != old.OfferedTick || c.ExpiresTick != old.ExpiresTick || c.NpcAuthority != old.NpcAuthority ||
                    c.Amendment != old.Amendment || c.Reparent != old.Reparent || c.Replaces != old.Replaces ||
                    old.PlayerSignedTick != null && c.PlayerSignedTick != old.PlayerSignedTick || old.NpcSignedTick != null && c.NpcSignedTick != old.NpcSignedTick ||
                    old.PlayerReleaseTick != null && c.PlayerReleaseTick != old.PlayerReleaseTick || old.NpcReleaseTick != null && c.NpcReleaseTick != old.NpcReleaseTick)
                    throw new ArgumentException("Retained third-party terms or signatures changed.");
            }
        }
    }
    public void ValidatePeers(TerritoryTransferLedger transfers, TributeLedger payments)
    {
        foreach (var r in records)
        {
            var old = TerritoryTransferLedger.Restore(r.Witness.Transfers); var paid = TributeLedger.Restore(r.Plan.PaymentsAfter);
            if (transfers.LastTick < old.LastTick || payments.LastTick < paid.LastTick || !old.Transfers.Select(t => JsonSerializer.Serialize(t)).SequenceEqual(transfers.Transfers.Take(old.Transfers.Count).Select(t => JsonSerializer.Serialize(t)))) throw new ArgumentException("Original release ledgers missing.");
            foreach (var a in paid.Accounts)
            {
                var current = payments.Account(a.CharterId);
                if (current.Terms != a.Terms || current.StartedTick != a.StartedTick || a.ClosedTick != null && current.ClosedTick != a.ClosedTick) throw new ArgumentException("Original liability reset.");
            }
            foreach (var p in paid.Receipts)
                if (!payments.Receipts.Any(c => JsonSerializer.Serialize(c) == JsonSerializer.Serialize(p))) throw new ArgumentException("Original receipt missing.");
        }
    }
    public string Export() => JsonSerializer.Serialize(new ScopedCharterReleaseSave(1, records.ToArray()));
    public static ScopedCharterReleases Restore(string json)
    {
        if (json == null || json.Length > 32000000) throw new ArgumentException("Invalid release government size.");
        var save = JsonSerializer.Deserialize<ScopedCharterReleaseSave>(json);
        if (save?.Version != 1 || save.Records == null || save.Records.Length > 128) throw new ArgumentException("Invalid release government archive.");
        var result = new ScopedCharterReleases();
        foreach (var r in save.Records)
        {
            if (r?.Witness == null || r.Proof != Hash(r.Witness) || r.Plan != CharterReleases.ReviewGovernment(r.Witness) || result.records.Any(p => p.Witness.Charter == r.Witness.Charter) ||
                result.records.LastOrDefault()?.Witness.Tick > r.Witness.Tick) throw new ArgumentException("Release government record changed.");
            result.records.Add(r);
        }
        return result;
    }
}
