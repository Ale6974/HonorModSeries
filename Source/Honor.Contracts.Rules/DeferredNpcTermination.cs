using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record DeferredNpcRequest(string Id, string Root, string Book, string Transfers, string Payments, long Tick);
public sealed record DeferredNpcClosure(string Request, string Proof);
public sealed record DeferredNpcState(DeferredNpcRequest Request, string Proof, int Phase = 0, long? ClosedTick = null, string ClosureProof = null);
public sealed record DeferredNpcSave(int Version, DeferredNpcState[] Records);

/// <summary>Player requests only. No NPC signature, closure, custody recovery or permission is inferred from waiting.</summary>
public sealed class DeferredNpcTerminations
{
    private readonly List<DeferredNpcState> records = [];
    public IReadOnlyList<DeferredNpcState> Records => records.ToArray();
    public DeferredNpcState Pending => records.LastOrDefault(r => r.Phase == 0);
    private static string Hash(DeferredNpcRequest r) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r))));
    public static string Marker(DeferredNpcState r) => r.Request.Id + ":" + RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { r.Request.Id, r.Proof, r.Phase, r.ClosedTick, r.ClosureProof }))));
    private static void ValidateRequest(DeferredNpcRequest r)
    {
        if (r == null || string.IsNullOrWhiteSpace(r.Id) || r.Id.Length > 120 || r.Id.Any(char.IsControl) || r.Tick is < 0 or > int.MaxValue)
            throw new ArgumentException("Invalid deferred request.");
        var book = CharterBook.Restore(r.Book); var c = book.Charter(r.Root);
        if (c.NpcAuthority == null || c.Phase != RecordedCharterPhase.Active || !book.PlayerManaged(c) || book.LastTick > r.Tick ||
            TerritoryTransferLedger.Restore(r.Transfers).LastTick > r.Tick || TributeLedger.Restore(r.Payments).LastTick > r.Tick)
            throw new ArgumentException("Original active NPC contract and original ledgers required.");
    }
    private void Add(DeferredNpcState s)
    {
        ValidateRequest(s.Request);
        if (s.Proof != Hash(s.Request) || s.Phase is < 0 or > 2 || records.Any(r => r.Request.Id == s.Request.Id) || Pending != null ||
            records.Count >= 32 || records.Count > 0 && records[records.Count - 1].ClosedTick > s.Request.Tick ||
            (s.Phase == 0 ? s.ClosedTick != null || s.ClosureProof != null : s.ClosedTick < s.Request.Tick || s.ClosedTick > int.MaxValue || s.ClosedTick == null) ||
            (s.Phase == 1 ? !CityStockProof.Valid(s.ClosureProof) : s.ClosureProof != null))
            throw new ArgumentException("Invalid deferred request history.");
        records.Add(s);
    }
    public DeferredNpcTerminations Queue(DeferredNpcRequest request)
    {
        var copy = Restore(Export()); copy.Add(new(request, Hash(request)));
        if (copy.Export().Length > 32000000) throw new ArgumentException("Deferred request capacity.");
        return copy;
    }
    public void RequireReady(CharterBook book, TerritoryTransferLedger transfers, TributeLedger payments, long now)
    {
        var pending = Pending ?? throw new ArgumentException("No saved request."); var r = pending.Request;
        if (book.Export() != r.Book || now < r.Tick || transfers.LastTick > now || payments.LastTick > now ||
            transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) || payments.Receipts.Any(p => p.Phase == TributeReceiptPhase.Prepared))
            throw new ArgumentException("Original contract changed or original custody is unresolved.");
        var oldTransfers = TerritoryTransferLedger.Restore(r.Transfers); var oldPayments = TributeLedger.Restore(r.Payments);
        if (transfers.Transfers.Count < oldTransfers.Transfers.Count || transfers.LastTick < oldTransfers.LastTick || payments.LastTick < oldPayments.LastTick) throw new ArgumentException("Original transfer history missing or clock reversed.");
        for (int i = 0; i < oldTransfers.Transfers.Count; i++)
        {
            var old = oldTransfers.Transfers[i]; var actual = transfers.Transfers[i];
            var normalized = old.Phase == TerritoryTransferPhase.Prepared ? actual with { Phase = old.Phase, ResolvedTick = old.ResolvedTick, Destruction = old.Destruction } : actual;
            if (JsonSerializer.Serialize(old) != JsonSerializer.Serialize(normalized) ||
                old.Phase == TerritoryTransferPhase.Prepared && (actual.Phase == TerritoryTransferPhase.Destroyed || actual.ResolvedTick < r.Tick || actual.ResolvedTick == null))
                throw new ArgumentException("Original physical result changed or requires separate loss reconciliation.");
        }
        foreach (var a in oldPayments.Accounts)
            if (payments.Account(a.CharterId) != a) throw new ArgumentException("Original payment account changed.");
        foreach (var old in oldPayments.Receipts)
        {
            var actual = payments.Receipts.SingleOrDefault(p => p.Id == old.Id) ?? throw new ArgumentException("Original receipt missing.");
            var normalized = old.Phase == TributeReceiptPhase.Prepared ? actual with { Phase = old.Phase, ResolvedTick = old.ResolvedTick } : actual;
            if (JsonSerializer.Serialize(old) != JsonSerializer.Serialize(normalized) || old.Phase == TributeReceiptPhase.Prepared && (actual.ResolvedTick < r.Tick || actual.ResolvedTick == null))
                throw new ArgumentException("Original payment receipt changed.");
        }
    }
    public DeferredNpcTerminations Close(long now, string closureProof = null)
    {
        var p = Pending ?? throw new ArgumentException("No saved request.");
        var save = new DeferredNpcSave(1, records.Select(r => r == p ? r with { Phase = closureProof == null ? 2 : 1, ClosedTick = now, ClosureProof = closureProof } : r).ToArray());
        return Restore(JsonSerializer.Serialize(save));
    }
    public void ValidatePairs(NpcSubtreeTerminations archive)
    {
        var closures = archive.Records;
        foreach (var r in records.Where(r => r.Phase == 1))
            if (closures.Count(c => c.Witness.Deferred?.Request == r.Request.Id && c.Witness.Deferred.Proof == r.Proof && c.Proof == r.ClosureProof && c.Witness.Root == r.Request.Root && c.Witness.Before == r.Request.Book && c.Witness.Tick == r.ClosedTick) != 1)
                throw new ArgumentException("Deferred completion has no exact original termination.");
        foreach (var c in closures.Where(c => c.Witness.Deferred != null))
            if (records.Count(r => r.Phase == 1 && r.Request.Id == c.Witness.Deferred.Request && r.Proof == c.Witness.Deferred.Proof && r.ClosureProof == c.Proof) != 1)
                throw new ArgumentException("Termination lost its original deferred request.");
    }
    public string Export() => JsonSerializer.Serialize(new DeferredNpcSave(1, records.ToArray()));
    public static DeferredNpcTerminations Restore(string json)
    {
        if (json == null || json.Length > 32000000) throw new ArgumentException("Invalid deferred request size.");
        var save = JsonSerializer.Deserialize<DeferredNpcSave>(json);
        if (save?.Version != 1 || save.Records == null || save.Records.Length > 32) throw new ArgumentException("Invalid deferred requests.");
        var result = new DeferredNpcTerminations(); foreach (var r in save.Records) { if (r == null) throw new ArgumentException("Missing request."); result.Add(r); } return result;
    }
}
