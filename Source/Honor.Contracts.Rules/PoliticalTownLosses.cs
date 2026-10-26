using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record PoliticalTownLoss(string Id, string Charter, string City, string Owner, string Label, long LossTick, long ObservedTick,
    string BookBefore, string TransfersBefore, string PoliticalBefore, EmptyTownStock Stock, long? ResolvedTick = null);
public sealed record PoliticalTownLossPlan(string Id, long Tick, string Proof, string BookBefore, string TransfersBefore, string PoliticalBefore, string BookAfter);
public sealed record PoliticalTownLossSave(int Version, PoliticalTownLoss[] Records);

/// <summary>Separate positive town-loss events: never manufacture a transfer or overwrite an earlier successful move.</summary>
public sealed class PoliticalTownLossArchive
{
    public const int Capacity = 128;
    private readonly Dictionary<string, PoliticalTownLoss> records = new(StringComparer.Ordinal);
    public IReadOnlyCollection<PoliticalTownLoss> Records => records.Values.ToArray();
    public bool Pending => records.Values.Any(r => r.ResolvedTick == null);
    private static bool Key(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 120 && !id.Any(char.IsControl);
    private static (CharterBook Book, TerritoryTransferLedger Ledger, AllegianceTransitionLedger Political) Source(PoliticalTownLoss r)
    {
        if (r == null || !Key(r.Id) || !Key(r.Charter) || !Key(r.City) || !Key(r.Owner) || string.IsNullOrWhiteSpace(r.Label) || r.Label.Length > 200 ||
            r.Stock == null || r.Stock.NextRestockTick < -1 || r.LossTick < 0 || r.ObservedTick < r.LossTick || r.ObservedTick > int.MaxValue ||
            r.ResolvedTick.HasValue && (r.ResolvedTick < r.ObservedTick || r.ResolvedTick > int.MaxValue)) throw new ArgumentException("Invalid independent town-loss witness.");
        _ = r.Stock.Proof;
        var book = CharterBook.Restore(r.BookBefore); var ledger = TerritoryTransferLedger.Restore(r.TransfersBefore);
        var journal = AllegianceTransitionLedger.Restore(r.PoliticalBefore); var intent = journal.Current; var c = book.Charter(r.Charter);
        if (intent?.Phase != AllegianceTransitionPhase.AllegianceApplied || intent.AllegianceTick > r.LossTick ||
            !intent.Followers.Contains(r.Charter) || journal.LostCharters.Contains(r.Charter) ||
            (intent.LostFollowers ?? []).Any(l => l.ResolvedTick > r.LossTick) || (intent.TownLosses ?? []).Any(l => l.ResolvedTick > r.LossTick) ||
            intent.WarResults?.Any(w => w.ObservedTick > r.LossTick) == true || intent.WarReaffirmations?.Any(w => w.Tick > r.LossTick) == true ||
            c.Phase != RecordedCharterPhase.Active || !book.PlayerManaged(c) || c.Terms.Subject != "territory/" + r.City || book.Entity(c.Terms.Subject).Player ||
            book.Entities.Any(e => e.Player && e.NativeOwner == r.Owner) || book.LastTick > r.LossTick || ledger.LastTick > r.LossTick ||
            ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) || !ledger.VerifiedOwner(c.Terms.Subject, book.Entity(c.Terms.Subject).NativeOwner, r.Owner))
            throw new ArgumentException("Loss requires one original live political follower, verified ownership and no pending physical transfer.");
        var copy = CharterBook.Restore(book.Export()); copy.CloseDestroyedLeaf(c.Id, c.Terms.Subject, book.Entity(c.Terms.Subject).NativeOwner, r.LossTick, r.ObservedTick, true);
        return (book, ledger, journal);
    }
    private static string Digest(PoliticalTownLoss r) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r with { ResolvedTick = null }))));
    public AllegianceTownLoss Receipt(string id)
    {
        if (!records.TryGetValue(id, out var r) || !r.ResolvedTick.HasValue) throw new ArgumentException("No resolved independent loss.");
        return new(r.Id, r.Charter, Digest(r), r.ObservedTick, r.ResolvedTick.Value);
    }
    public PoliticalTownLossArchive Observe(PoliticalTownLoss r, CharterBook book, TerritoryTransferLedger transfers, AllegianceTransitionLedger journal)
    {
        Source(r);
        if (Pending || r.ResolvedTick != null || records.Count >= Capacity || records.ContainsKey(r.Id) ||
            records.Values.Any(old => old.Charter == r.Charter) || book.Export() != r.BookBefore || transfers.Export() != r.TransfersBefore || journal.Export() != r.PoliticalBefore)
            throw new ArgumentException("Duplicate, overlapping, full or changed political town-loss observation.");
        var copy = Restore(Export()); copy.records.Add(r.Id, r); copy.ValidatePair(book, transfers); copy.ValidatePolitical(journal);
        if (copy.Export().Length > 32000000) throw new ArgumentException("Political town-loss archive capacity.");
        return copy;
    }
    public PoliticalTownLossPlan Review(string id, CharterBook book, TerritoryTransferLedger transfers, AllegianceTransitionLedger journal, long now)
    {
        if (!records.TryGetValue(id, out var r) || r.ResolvedTick != null || now < r.ObservedTick || now > int.MaxValue ||
            now < book.LastTick || now < transfers.LastTick || book.Export() != r.BookBefore || transfers.Export() != r.TransfersBefore || journal.Export() != r.PoliticalBefore)
            throw new ArgumentException("Independent loss source changed or is unavailable.");
        var source = Source(r); var c = source.Book.Charter(r.Charter);
        source.Book.ClosePoliticalLostLeaf(c.Id, c.Terms.Subject, source.Book.Entity(c.Terms.Subject).NativeOwner, r.LossTick, now, new(r.Id, Digest(r)));
        return new(id, now, Digest(r), r.BookBefore, r.TransfersBefore, r.PoliticalBefore, source.Book.Export());
    }
    public (CharterBook Book, PoliticalTownLossArchive Archive, AllegianceTransitionLedger Political) Commit(PoliticalTownLossPlan plan, CharterBook book,
        TerritoryTransferLedger transfers, AllegianceTransitionLedger journal, long now)
    {
        RuntimeCompat.NotNull(plan); var fresh = Review(plan.Id, book, transfers, journal, now);
        if (fresh != plan) throw new ArgumentException("Stale or changed independent loss confirmation.");
        var after = CharterBook.Restore(plan.BookAfter); var copy = Restore(Export()); copy.records[plan.Id] = copy.records[plan.Id] with { ResolvedTick = now };
        copy.ValidatePair(after, transfers);
        var political = AllegianceTransitionLedger.Restore(journal.Export()); political.AcknowledgeTownLoss(copy, after, transfers, plan.Id, now);
        if (copy.Export().Length > 32000000) throw new ArgumentException("Political town-loss receipt capacity.");
        return (after, copy, political);
    }
    public void ValidatePair(CharterBook book, TerritoryTransferLedger transfers)
    {
        foreach (var r in records.Values)
        {
            var original = Source(r); var c = original.Book.Charter(r.Charter);
            if (r.ResolvedTick == null)
            { if (book.Export() != r.BookBefore || transfers.Export() != r.TransfersBefore) throw new ArgumentException("Pending independent loss source changed."); }
            else if (r.ResolvedTick > book.LastTick || book.Charter(r.Charter) != c with { Phase = RecordedCharterPhase.TerritoryLost, TerritoryLostTick = r.LossTick, PoliticalLoss = new(r.Id, Digest(r)) } ||
                transfers.Transfers.Where(t => t.Territory == c.Terms.Subject).Count() != original.Ledger.Transfers.Count(t => t.Territory == c.Terms.Subject) ||
                original.Ledger.Transfers.Where(t => t.Territory == c.Terms.Subject).Any(t => transfers.Transfer(t.Id) != t))
                throw new ArgumentException("Independent loss must preserve the old charter and all exact prior city-transfer outcomes.");
        }
        if (book.Charters.Any(c => c.PoliticalLoss != null && (!records.TryGetValue(c.PoliticalLoss.Id, out var r) || r.Charter != c.Id ||
            !r.ResolvedTick.HasValue || c.PoliticalLoss.Proof != Digest(r))))
            throw new ArgumentException("Every independently lost charter requires its exact persistent native witness, including after later political transitions.");
    }
    public void ValidatePolitical(AllegianceTransitionLedger journal)
    {
        RuntimeCompat.NotNull(journal); var current = journal.Current;
        var matching = records.Values.Where(r => AllegianceTransitionLedger.Restore(r.PoliticalBefore).Current.Id == current?.Id).ToArray();
        var receipts = current?.TownLosses ?? [];
        if (matching.Count(r => r.ResolvedTick != null) != receipts.Length) throw new ArgumentException("Independent political receipts require their native witnesses.");
        foreach (var r in matching)
        {
            var original = AllegianceTransitionLedger.Restore(r.PoliticalBefore).Current; AllegianceTransitionLedger.ValidateLossLineage(original, current);
            if (r.ResolvedTick == null)
            { if (r.PoliticalBefore != journal.Export()) throw new ArgumentException("Pending independent loss intent changed."); }
            else if (receipts.SingleOrDefault(l => l.Loss == r.Id) != Receipt(r.Id) ||
                !(original.TownLosses ?? []).SequenceEqual(receipts.TakeWhile(l => l.Loss != r.Id)))
                throw new ArgumentException("Independent political receipts changed their exact prior loss sequence.");
        }
        if (records.Values.Any(r => r.ResolvedTick == null && AllegianceTransitionLedger.Restore(r.PoliticalBefore).Current.Id != current?.Id))
            throw new ArgumentException("Cannot abandon a pending independent loss intent.");
    }
    public bool LostTerritory(string territory) => records.Values.Any(r => r.ResolvedTick.HasValue && "territory/" + r.City == territory);
    public string Export() => JsonSerializer.Serialize(new PoliticalTownLossSave(1, records.Values.ToArray()));
    public static PoliticalTownLossArchive Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32000000) throw new ArgumentException("Invalid independent loss archive size.");
        var save = JsonSerializer.Deserialize<PoliticalTownLossSave>(json);
        if (save == null || save.Version != 1 || save.Records == null || save.Records.Length > Capacity) throw new ArgumentException("Invalid independent loss archive.");
        var result = new PoliticalTownLossArchive();
        foreach (var r in save.Records)
        {
            Source(r);
            if (result.records.ContainsKey(r.Id) || result.records.Values.Any(old => old.Charter == r.Charter)) throw new ArgumentException("Duplicate independent loss.");
            result.records.Add(r.Id, r);
        }
        if (result.Records.Count(r => r.ResolvedTick == null) > 1) throw new ArgumentException("Overlapping independent pending losses.");
        return result;
    }
}
