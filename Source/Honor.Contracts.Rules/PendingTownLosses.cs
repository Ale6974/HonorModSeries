using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record EmptyTownStock(bool Visited, bool ContainerExists, int NextRestockTick)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Proof => CityStockProof.Capture(Visited, ContainerExists, NextRestockTick, []);
}
public sealed record PendingTownLoss(string Transfer, string City, string Owner, string Label, long LossTick, long ObservedTick,
    string BookBefore, string TransfersBefore, EmptyTownStock Stock, long? ResolvedTick = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string PoliticalBefore = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public TownLossProof UpperScope { get; init; }
}
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
        if (r.UpperScope == null)
        { var copy = CharterBook.Restore(book.Export()); copy.CloseDestroyedLeaf(t.Charter, t.Territory, t.BaselineOwner, r.LossTick, r.ObservedTick, true); }
        else
        {
            var u = r.UpperScope;
            if (r.PoliticalBefore != null || u != new TownLossProof(t.Charter, t.Territory, t.BaselineOwner, r.Owner, r.Label, r.LossTick, r.ObservedTick, r.BookBefore))
                throw new ArgumentException("Only an exact isolated original upper scope may accompany this pending native destruction.");
            new TownSuccessionArchive().CloseForObservation(u); // Structural scope validation, never remove the leaf API guard.
        }
        if (r.PoliticalBefore != null)
        {
            var intent = AllegianceTransitionLedger.Restore(r.PoliticalBefore); var current = intent.Current;
            if (current?.Phase != AllegianceTransitionPhase.AllegianceApplied || current.AllegianceTick > r.LossTick || t.PreparedTick < current.AllegianceTick ||
                !current.Followers.Contains(t.Charter) || intent.LostCharters.Contains(t.Charter) ||
                PlayerAllegiance.Restore(current.After).State.Authority != t.TargetOwner ||
                (current.LostFollowers ?? []).Any(l => l.ResolvedTick > r.LossTick) || (current.TownLosses ?? []).Any(l => l.ResolvedTick > r.LossTick) ||
                current.WarResults?.Any(w => w.ObservedTick > r.LossTick) == true || current.WarReaffirmations?.Any(w => w.Tick > r.LossTick) == true || current.PeaceReaffirmations?.Any(p => p.Tick > r.LossTick) == true || current.FeudalReaffirmations?.Any(p => p.Tick > r.LossTick) == true)
                throw new ArgumentException("Loss must bind an applied original follower and its actual intended destination.");
        }
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
        var after = r.UpperScope == null ? source.Book : new TownSuccessionArchive().CloseForObservation(r.UpperScope with { ClosureTick = now });
        if (r.UpperScope == null) after.CloseDestroyedLeaf(source.Transfer.Charter, source.Transfer.Territory, source.Transfer.BaselineOwner, r.LossTick, now, true);
        source.Ledger.ResolveDestroyed(id, new(proof, r.Owner, r.LossTick, r.ObservedTick), now);
        return new(id, now, proof, book.Export(), ledger.Export(), after.Export(), source.Ledger.Export());
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
    public TownSuccessionArchive StageUpper(string id, TownSuccessionArchive losses, CharterBook after)
    {
        RuntimeCompat.NotNull(losses); var r = records.TryGetValue(id, out var found) ? found : throw new ArgumentException("Unknown pending loss.");
        if (r.UpperScope == null) return losses;
        if (!r.ResolvedTick.HasValue) throw new ArgumentException("Resolve original pending destruction before registering the upper scope.");
        Source(r);
        return losses.Observe(r.UpperScope with { ClosureTick = r.ResolvedTick }, after);
    }
    public void ValidateUpperPair(TownSuccessionArchive losses)
    {
        RuntimeCompat.NotNull(losses);
        foreach (var r in records.Values.Where(r => r.UpperScope != null))
        {
            Source(r);
            var actual = losses.Records.SingleOrDefault(p => p.Root == r.UpperScope.Root);
            if (!r.ResolvedTick.HasValue ? actual != null : actual == null ||
                actual with { Resolution = null } != r.UpperScope with { ClosureTick = r.ResolvedTick })
                throw new ArgumentException("Delayed upper scope differs from its original pending native witness/closure time.");
        }
        foreach (var p in losses.Records.Where(p => p.ClosureTick.HasValue))
            if (records.Values.Count(r => r.UpperScope?.Root == p.Root && r.ResolvedTick == p.ClosureTick) != 1)
                throw new ArgumentException("A delayed upper closure requires its exact resolved pending witness.");
    }
    public void ValidatePolitical(AllegianceTransitionLedger journal)
    {
        RuntimeCompat.NotNull(journal);
        var current = journal.Current; var matching = records.Values.Where(r => r.PoliticalBefore != null &&
            AllegianceTransitionLedger.Restore(r.PoliticalBefore).Current.Id == current?.Id).ToArray();
        var acknowledged = current?.LostFollowers ?? [];
        if (matching.Count(r => r.ResolvedTick != null) != acknowledged.Length) throw new ArgumentException("Political loss receipts and original witnesses differ.");
        foreach (var r in matching)
        {
            var original = AllegianceTransitionLedger.Restore(r.PoliticalBefore).Current;
            AllegianceTransitionLedger.ValidateLossLineage(original, current);
            if (r.ResolvedTick == null)
            { if (r.PoliticalBefore != journal.Export()) throw new ArgumentException("Unresolved destruction must retain its exact political command and intent head."); }
            else
            {
                var receipt = acknowledged.SingleOrDefault(l => l.Transfer == r.Transfer);
                var t = TerritoryTransferLedger.Restore(r.TransfersBefore).Transfer(r.Transfer);
                if (receipt != new AllegianceFollowerLoss(r.Transfer, t.Charter, Digest(r), r.ObservedTick, r.ResolvedTick.Value) ||
                    !(original.LostFollowers ?? []).SequenceEqual(acknowledged.TakeWhile(l => l.Transfer != r.Transfer)))
                    throw new ArgumentException("Political receipt does not preserve the exact ordered prior losses and native proof.");
            }
        }
        if (records.Values.Any(r => r.PoliticalBefore != null && r.ResolvedTick == null &&
            AllegianceTransitionLedger.Restore(r.PoliticalBefore).Current.Id != current?.Id))
            throw new ArgumentException("A pending political loss cannot be abandoned for another intent.");
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
