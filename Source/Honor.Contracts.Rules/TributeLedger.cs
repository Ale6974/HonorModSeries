using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum TributeReceiptPhase { Prepared, Committed, Cancelled }
public sealed record TributeAccount(string CharterId, RecordedCharterTerms Terms, long StartedTick, long? ClosedTick = null);
public sealed record TributeBill(string CharterId, int Period, long DueTick, long GraceUntil, decimal Principal, decimal RequiredValue);
public sealed record TributeLine(string SourceId, int Count, decimal UnitValue);
public sealed record TributeReceipt(string Id, string CharterId, int Period, long PreparedTick,
    IReadOnlyList<TributeLine> Lines, TributeReceiptPhase Phase = TributeReceiptPhase.Prepared, long? ResolvedTick = null)
{
    public decimal TotalValue => Lines.Sum(l => l.Count * l.UnitValue);
}
public sealed record TributeLedgerSave(int Version, long LastTick, TributeAccount[] Accounts, TributeReceipt[] Receipts);

/// <summary>Explicit opt-in billing and two-phase receipts; no inventory or automatic currency exists here.</summary>
public sealed class TributeLedger
{
    public const int MaxPeriods = 1200, MaxReceipts = 4000, Version = 1;
    private readonly Dictionary<string, TributeAccount> accounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TributeReceipt> receipts = new(StringComparer.Ordinal);
    public IReadOnlyCollection<TributeAccount> Accounts => accounts.Values.ToArray();
    public IReadOnlyCollection<TributeReceipt> Receipts => receipts.Values.ToArray();
    public long LastTick { get; private set; }
    public long Revision { get; private set; } // Process-local read-cache invalidation, never part of saved JSON.
    private static void Require(bool valid, string reason) { if (!valid) throw new ArgumentException(reason); }
    private static void Key(string id) => Require(!string.IsNullOrWhiteSpace(id) && id.Length <= 120 && !id.Any(char.IsControl), "Invalid receipt identity.");
    private void Time(long now) => Require(now >= LastTick && now <= int.MaxValue, "Invalid or reversed payment time.");
    private static void Terms(RecordedCharterTerms t)
    {
        RuntimeCompat.NotNull(t); Key(t.Liege); Key(t.Subject);
        Require(t.Liege != t.Subject && t.TributeValue is > 0 and <= 10000000 &&
            t.IntervalTicks is >= PoliticalWorld.Day and <= PoliticalWorld.Day * 60 && t.GraceTicks is >= 0 and <= PoliticalWorld.Day * 30 &&
            RuntimeCompat.IsDefined(t.Transport) && t.TransportRate is >= 0 and <= .5m, "Invalid tribute schedule.");
    }
    public void Enable(CharterBook book, string charterId, long now)
    {
        RuntimeCompat.NotNull(book); Time(now); Key(charterId);
        var charter = book.Charter(charterId); Terms(charter.Terms);
        Require(charter.Phase == RecordedCharterPhase.Active && book.Entity(charter.Terms.Subject).Player &&
            charter.PlayerSignedTick.HasValue && charter.NpcSignedTick.HasValue &&
            now >= Math.Max(charter.PlayerSignedTick.Value, charter.NpcSignedTick.Value), "Only an active player tribute obligation can opt in.");
        Require(!accounts.ContainsKey(charterId) && accounts.Count < CharterBook.MaxCharters && now <= int.MaxValue - charter.Terms.IntervalTicks,
            "An enabled account cannot be reset or enabled twice.");
        accounts.Add(charterId, new(charterId, charter.Terms, now)); LastTick = now; Revision++;
    }
    public void SynchronizeClosure(CharterBook book, string id, long now)
    {
        Time(now); var account = Account(id); var charter = book.Charter(id);
        Require(charter.Terms == account.Terms, "Terms cannot be changed in place.");
        if (charter.Phase != RecordedCharterPhase.Released || account.ClosedTick.HasValue) return;
        long closed = Math.Max(charter.PlayerReleaseTick.Value, charter.NpcReleaseTick.Value);
        Require(closed >= account.StartedTick && closed <= now && receipts.Values.Where(r => r.CharterId == id)
            .All(r => account.StartedTick + account.Terms.IntervalTicks * r.Period <= closed), "Invalid account closing time or post-closure receipt.");
        accounts[id] = account with { ClosedTick = closed }; LastTick = now; Revision++;
    }
    public TributeAccount Account(string id) => accounts.TryGetValue(id ?? "", out var account) ? account : throw new ArgumentException("No enabled tribute account.");
    public TributeBill Bill(string charterId, int period)
    {
        var account = Account(charterId);
        Require(period is >= 1 and <= MaxPeriods, "Unsupported tribute period.");
        long due = checked(account.StartedTick + account.Terms.IntervalTicks * period);
        Require(due <= int.MaxValue && (!account.ClosedTick.HasValue || due <= account.ClosedTick), "No obligation after agreed release.");
        decimal value = account.Terms.Transport == TributeTransport.SubjectDelivery
            ? account.Terms.TributeValue * (1 - account.Terms.TransportRate) : account.Terms.TributeValue * (1 + account.Terms.TransportRate);
        return new(charterId, period, due, due + account.Terms.GraceTicks, account.Terms.TributeValue, value);
    }
    public IEnumerable<TributeBill> Bills(string charterId, long now)
    {
        var account = Account(charterId);
        long cutoff = Math.Min(now, account.ClosedTick ?? now);
        int periods = (int)RuntimeCompat.Clamp((cutoff - account.StartedTick) / account.Terms.IntervalTicks + 1, 0, MaxPeriods);
        for (int period = 1; period <= periods; period++)
        {
            long due = account.StartedTick + account.Terms.IntervalTicks * period;
            if (due > int.MaxValue || account.ClosedTick.HasValue && due > account.ClosedTick) yield break;
            yield return Bill(charterId, period);
        }
    }
    public TributeReceipt Receipt(string id) => receipts.TryGetValue(id ?? "", out var receipt) ? receipt : throw new ArgumentException("Unknown receipt.");
    public bool IsPaid(string charterId, int period) => receipts.Values.Any(r => r.CharterId == charterId && r.Period == period && r.Phase == TributeReceiptPhase.Committed);
    private static TributeLine[] Lines(IEnumerable<TributeLine> lines)
    {
        RuntimeCompat.NotNull(lines); var copy = lines.ToArray();
        Require(copy.Length is >= 1 and <= 100 && copy.All(l => l != null), "Select a bounded set of real goods.");
        foreach (var line in copy)
        { Key(line.SourceId); Require(line.Count is >= 1 and <= 1000000 && line.UnitValue is > 0 and <= 10000000, "Invalid quoted count or trade price."); }
        Require(copy.Select(l => l.SourceId).Distinct(StringComparer.Ordinal).Count() == copy.Length && copy.Sum(l => l.Count * l.UnitValue) <= 100000000,
            "Duplicate source stack or excessive payment value.");
        return copy;
    }
    public TributeReceipt Prepare(string id, string charterId, int period, IEnumerable<TributeLine> lines, long now)
    {
        Key(id); Time(now); var bill = Bill(charterId, period); var copy = Lines(lines);
        Require(now >= bill.DueTick && copy.Sum(l => l.Count * l.UnitValue) >= bill.RequiredValue,
            "The bill must be due and the actual quoted delivery must cover it.");
        Require(receipts.Count < MaxReceipts && !receipts.ContainsKey(id) &&
            !receipts.Values.Any(r => r.CharterId == charterId && r.Period == period && r.Phase != TributeReceiptPhase.Cancelled),
            "The obligation is already paid or reserved; receipt IDs cannot be reused.");
        var receipt = new TributeReceipt(id, charterId, period, now, Array.AsReadOnly(copy));
        receipts.Add(id, receipt); LastTick = now; Revision++; return receipt;
    }
    public void Commit(string id, IEnumerable<TributeLine> verifiedDelivered, long now)
    {
        Time(now); var receipt = Receipt(id); var proof = Lines(verifiedDelivered);
        Require(receipt.Phase == TributeReceiptPhase.Prepared && now >= receipt.PreparedTick &&
            proof.Length == receipt.Lines.Count && proof.All(line => receipt.Lines.Contains(line)),
            "Every prepared stack and exact count/price must be verified in the receiver's container.");
        receipts[id] = receipt with { Phase = TributeReceiptPhase.Committed, ResolvedTick = now }; LastTick = now; Revision++;
    }
    public void CancelAfterVerifiedReturn(string id, bool allGoodsReturned, long now)
    {
        Time(now); var receipt = Receipt(id);
        Require(receipt.Phase == TributeReceiptPhase.Prepared && allGoodsReturned && now >= receipt.PreparedTick,
            "A reserved obligation can only reopen after all goods are verified returned.");
        receipts[id] = receipt with { Phase = TributeReceiptPhase.Cancelled, ResolvedTick = now }; LastTick = now; Revision++;
    }
    public string Export() => JsonSerializer.Serialize(new TributeLedgerSave(Version, LastTick, accounts.Values.ToArray(), receipts.Values.ToArray()));
    public static TributeLedger Restore(string json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 8000000, "Invalid payment snapshot size.");
        var save = JsonSerializer.Deserialize<TributeLedgerSave>(json);
        Require(save != null && save.Version == Version && save.LastTick is >= 0 and <= int.MaxValue && save.Accounts != null &&
            save.Accounts.Length <= CharterBook.MaxCharters && save.Receipts != null && save.Receipts.Length <= MaxReceipts, "Invalid payment snapshot.");
        var ledger = new TributeLedger();
        foreach (var account in save.Accounts)
        {
            RuntimeCompat.NotNull(account); Key(account.CharterId); Terms(account.Terms);
            Require(!ledger.accounts.ContainsKey(account.CharterId) && account.StartedTick >= 0 && account.StartedTick <= save.LastTick &&
                account.StartedTick <= int.MaxValue - account.Terms.IntervalTicks && (!account.ClosedTick.HasValue ||
                account.ClosedTick >= account.StartedTick && account.ClosedTick <= save.LastTick), "Invalid saved account.");
            ledger.accounts.Add(account.CharterId, account);
        }
        foreach (var receipt in save.Receipts)
        {
            RuntimeCompat.NotNull(receipt); Key(receipt.Id); var lines = Lines(receipt.Lines); var bill = ledger.Bill(receipt.CharterId, receipt.Period);
            Require(!ledger.receipts.ContainsKey(receipt.Id) && RuntimeCompat.IsDefined(receipt.Phase) && receipt.PreparedTick >= bill.DueTick && receipt.PreparedTick <= save.LastTick &&
                lines.Sum(l => l.Count * l.UnitValue) >= bill.RequiredValue &&
                (receipt.Phase == TributeReceiptPhase.Prepared ? !receipt.ResolvedTick.HasValue : receipt.ResolvedTick >= receipt.PreparedTick && receipt.ResolvedTick <= save.LastTick),
                "Invalid saved receipt.");
            Require(receipt.Phase == TributeReceiptPhase.Cancelled || !ledger.receipts.Values.Any(r => r.CharterId == receipt.CharterId && r.Period == receipt.Period &&
                r.Phase != TributeReceiptPhase.Cancelled), "Duplicate payment or reservation.");
            ledger.receipts.Add(receipt.Id, receipt with { Lines = Array.AsReadOnly(lines) });
        }
        ledger.LastTick = save.LastTick; return ledger;
    }
}
