using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record EmptyTownStock(bool Visited, bool ContainerExists, int NextRestockTick)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Proof => CityStockProof.Capture(Visited, ContainerExists, NextRestockTick, []);
}
public sealed record PendingTownLoss(string Transfer, string City, string Owner, string Label, long LossTick, long ObservedTick,
    string BookBefore, string TransfersBefore, EmptyTownStock Stock, long? ResolvedTick = null);
public sealed record PendingTownLossPlan(string Transfer, long Tick, string Proof, string BookBefore, string TransfersBefore, string BookAfter, string TransfersAfter);
public sealed record PendingTownLossSave(int Version, PendingTownLoss[] Records);

/// <summary>Positive normal-return native destruction facts; no owner assignment, goods movement or automatic recovery.</summary>
public sealed class PendingTownLossArchive
{
    public const int Capacity = 128;
    private readonly Dictionary<string, PendingTownLoss> records = new(StringComparer.Ordinal);
    public IReadOnlyCollection<PendingTownLoss> Records => records.Values.ToArray();
    public bool HasPending(string id) => records.TryGetValue(id, out var r) && r.ResolvedTick == null;
    private static bool Key(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 120 && !id.Any(char.IsControl);
    private static (CharterBook Book, TerritoryTransferLedger Ledger, TerritoryTransfer Transfer) Source(PendingTownLoss r)
    {
        if (r == null || !Key(r.Transfer) || !Key(r.City) || !Key(r.Owner) || string.IsNullOrWhiteSpace(r.Label) || r.Label.Length > 200 ||
            r.Stock == null || r.LossTick < 0 || r.ObservedTick < r.LossTick || r.ObservedTick > int.MaxValue ||
            r.ResolvedTick.HasValue && (r.ResolvedTick < r.ObservedTick || r.ResolvedTick > int.MaxValue)) throw new ArgumentException("Invalid native destruction witness.");
        var book = CharterBook.Restore(r.BookBefore); var ledger = TerritoryTransferLedger.Restore(r.TransfersBefore); var t = ledger.Transfer(r.Transfer);
        var c = book.Charter(t.Charter);
        if (t.Phase != TerritoryTransferPhase.Prepared || t.Purpose != TerritoryTransferPurpose.ActiveCharter || t.ReleasedRoot != null || t.ResidentProof != null ||
            t.StockFormat != 0 || t.Territory != "territory/" + r.City || (r.Owner != t.FromOwner && r.Owner != t.TargetOwner) ||
            t.PreparedTick > r.LossTick || book.LastTick > r.LossTick || ledger.LastTick > r.LossTick || c.Phase != RecordedCharterPhase.Active || !book.PlayerManaged(c) ||
            book.Entity(t.Territory).Player || book.Entity(t.Territory).NativeOwner != t.BaselineOwner || c.Terms.Subject != t.Territory ||
            book.Entities.Any(e => e.Player && (e.NativeOwner == t.FromOwner || e.NativeOwner == t.TargetOwner)) ||
            (t.StockProof == null ? r.Stock.Visited || r.Stock.ContainerExists || r.Stock.NextRestockTick != -1 : t.StockProof != r.Stock.Proof))
            throw new ArgumentException("Witness must match one prepared empty NPC leaf transfer and its original stock state.");
        var copy = CharterBook.Restore(book.Export()); copy.CloseDestroyedLeaf(t.Charter, t.Territory, t.BaselineOwner, r.LossTick, r.ObservedTick, true);
        return (book, ledger, t);
    }
    private static string Digest(PendingTownLoss r) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r with { ResolvedTick = null }))));
    public PendingTownLossArchive Observe(PendingTownLoss r, CharterBook book, TerritoryTransferLedger ledger)
    {
        Source(r);
        if (r.ResolvedTick != null || records.Count >= Capacity || records.ContainsKey(r.Transfer) || book.Export() != r.BookBefore || ledger.Export() != r.TransfersBefore)
            throw new ArgumentException("Duplicate, full or changed destruction witness.");
        var copy = Restore(Export()); copy.records.Add(r.Transfer, r); copy.ValidatePair(book, ledger);
        if (copy.Export().Length > 32000000) throw new ArgumentException("Destruction archive capacity.");
        return copy;
    }
    public PendingTownLossPlan Review(string id, CharterBook book, TerritoryTransferLedger ledger, long now)
    {
        if (!records.TryGetValue(id, out var r) || r.ResolvedTick != null || now < r.ObservedTick || now > int.MaxValue || now < book.LastTick || now < ledger.LastTick ||
            book.Export() != r.BookBefore || ledger.Export() != r.TransfersBefore) throw new ArgumentException("No unchanged pending destruction to review.");
        var source = Source(r); string proof = Digest(r);
        source.Book.CloseDestroyedLeaf(source.Transfer.Charter, source.Transfer.Territory, source.Transfer.BaselineOwner, r.LossTick, now, true);
        source.Ledger.ResolveDestroyed(id, new(proof, r.Owner, r.LossTick, r.ObservedTick), now);
        return new(id, now, proof, book.Export(), ledger.Export(), source.Book.Export(), source.Ledger.Export());
    }
    public (CharterBook Book, TerritoryTransferLedger Ledger, PendingTownLossArchive Archive) Commit(PendingTownLossPlan plan, CharterBook book, TerritoryTransferLedger ledger, long now)
    {
        RuntimeCompat.NotNull(plan); var fresh = Review(plan.Transfer, book, ledger, now);
        if (plan != fresh) throw new ArgumentException("Stale or changed destruction reconciliation.");
        var afterBook = CharterBook.Restore(fresh.BookAfter); var afterLedger = TerritoryTransferLedger.Restore(fresh.TransfersAfter); var copy = Restore(Export());
        copy.records[plan.Transfer] = copy.records[plan.Transfer] with { ResolvedTick = now }; copy.ValidatePair(afterBook, afterLedger);
        if (copy.Export().Length > 32000000) throw new ArgumentException("Destruction receipt capacity.");
        return (afterBook, afterLedger, copy);
    }
    public void ValidatePair(CharterBook book, TerritoryTransferLedger ledger)
    {
        foreach (var r in records.Values)
        {
            var source = Source(r); var t = source.Transfer;
            if (r.ResolvedTick == null)
            { if (book.Export() != r.BookBefore || ledger.Export() != r.TransfersBefore) throw new ArgumentException("Pending destruction source changed."); }
            else if (r.ResolvedTick > book.LastTick || r.ResolvedTick > ledger.LastTick || book.Charter(t.Charter) != source.Book.Charter(t.Charter) with {
                Phase = RecordedCharterPhase.TerritoryLost, TerritoryLostTick = r.LossTick } || ledger.Transfer(r.Transfer) != t with {
                Phase = TerritoryTransferPhase.Destroyed, ResolvedTick = r.ResolvedTick, Destruction = new(Digest(r), r.Owner, r.LossTick, r.ObservedTick) })
                throw new ArgumentException("Destruction closure differs from its original proof and receipt.");
        }
        if (ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Destroyed && (!records.TryGetValue(t.Id, out var r) || r.ResolvedTick == null)))
            throw new ArgumentException("Destroyed transfer requires its recorded native witness.");
    }
    public string Export() => JsonSerializer.Serialize(new PendingTownLossSave(1, records.Values.ToArray()));
    public static PendingTownLossArchive Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32000000) throw new ArgumentException("Invalid destruction archive size.");
        var saved = JsonSerializer.Deserialize<PendingTownLossSave>(json);
        if (saved == null || saved.Version != 1 || saved.Records == null || saved.Records.Length > Capacity) throw new ArgumentException("Invalid destruction archive.");
        var result = new PendingTownLossArchive();
        foreach (var r in saved.Records)
        { Source(r); if (result.records.ContainsKey(r.Transfer)) throw new ArgumentException("Duplicate destruction witness."); result.records.Add(r.Transfer, r); }
        return result;
    }
}
