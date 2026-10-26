using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record PoliticalTownLoss(string Id, string Charter, string City, string Owner, string Label, long LossTick, long ObservedTick,
    string BookBefore, string TransfersBefore, string PoliticalBefore, EmptyTownStock Stock, long? ResolvedTick = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string AcknowledgedBefore { get; init; }
}
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
        if (r.AcknowledgedBefore != null)
        {
            var prior = AllegianceTransitionLedger.Restore(r.AcknowledgedBefore).Current;
            if (!r.ResolvedTick.HasValue || prior == null ||
                JsonSerializer.Serialize(prior with { TownLosses = intent?.TownLosses }) != JsonSerializer.Serialize(intent) ||
                !(intent.TownLosses ?? []).SequenceEqual((prior.TownLosses ?? []).Take(intent.TownLosses?.Length ?? 0)) ||
                (prior.TownLosses ?? []).Any(l => l.Loss == r.Id || l.ResolvedTick > r.ResolvedTick))
                throw new ArgumentException("Acknowledgement must retain the original intent and only previously acknowledged independent losses.");
        }
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
    private static string Digest(PoliticalTownLoss r) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r with { ResolvedTick = null, AcknowledgedBefore = null }))));
    private static bool SameEpoch(PoliticalTownLoss a, PoliticalTownLoss b) => a.BookBefore == b.BookBefore &&
        a.TransfersBefore == b.TransfersBefore && a.PoliticalBefore == b.PoliticalBefore;
    private (CharterBook Book, TerritoryTransferLedger Ledger, AllegianceTransitionLedger Political) CurrentSource(PoliticalTownLoss r)
    {
        var source = Source(r);
        // Only disjoint, positively witnessed leaf closures from the SAME original epoch may advance this source.
        // The exact acknowledged command head establishes order, including several confirmations on one tick.
        foreach (var sibling in records.Values.Where(s => s.Id != r.Id && s.ResolvedTick.HasValue && SameEpoch(s, r))
            .OrderBy(s => AllegianceTransitionLedger.Restore(s.AcknowledgedBefore ?? s.PoliticalBefore).Current.TownLosses?.Length ?? 0))
        {
            Source(sibling);
            if ((sibling.AcknowledgedBefore ?? sibling.PoliticalBefore) != source.Political.Export() || sibling.Charter == r.Charter || sibling.City == r.City)
                throw new ArgumentException("Sibling loss must retain the exact preceding command head and distinct territory.");
            var c = source.Book.Charter(sibling.Charter);
            source.Book.ClosePoliticalLostLeaf(c.Id, c.Terms.Subject, source.Book.Entity(c.Terms.Subject).NativeOwner,
                sibling.LossTick, sibling.ResolvedTick.Value, new(sibling.Id, Digest(sibling)));
            source.Political = AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(new AllegianceTransitionSave(1,
                source.Political.Current with { TownLosses = (source.Political.Current.TownLosses ?? []).Append(Receipt(sibling.Id)).ToArray() })));
        }
        return source;
    }
    public AllegianceTownLoss Receipt(string id)
    {
        if (!records.TryGetValue(id, out var r) || !r.ResolvedTick.HasValue) throw new ArgumentException("No resolved independent loss.");
        return new(r.Id, r.Charter, Digest(r), r.ObservedTick, r.ResolvedTick.Value);
    }
    public PoliticalTownLossArchive Observe(PoliticalTownLoss r, CharterBook book, TerritoryTransferLedger transfers, AllegianceTransitionLedger journal)
    {
        Source(r);
        if (r.ResolvedTick != null || records.Count >= Capacity || records.ContainsKey(r.Id) ||
            records.Values.Any(old => old.Charter == r.Charter) || book.Export() != r.BookBefore || transfers.Export() != r.TransfersBefore || journal.Export() != r.PoliticalBefore)
            throw new ArgumentException("Duplicate, overlapping, full or changed political town-loss observation.");
        if (records.Values.Any(s => s.ResolvedTick == null && !SameEpoch(s, r)))
            throw new ArgumentException("A new observation epoch cannot bypass unresolved original town losses.");
        var copy = Restore(Export()); copy.records.Add(r.Id, r); copy.ValidatePair(book, transfers); copy.ValidatePolitical(journal);
        if (copy.Export().Length > 32000000) throw new ArgumentException("Political town-loss archive capacity.");
        return copy;
    }
    public PoliticalTownLossPlan Review(string id, CharterBook book, TerritoryTransferLedger transfers, AllegianceTransitionLedger journal, long now)
    {
        if (!records.TryGetValue(id, out var r) || r.ResolvedTick != null || now < r.ObservedTick || now > int.MaxValue ||
            now < book.LastTick || now < transfers.LastTick)
            throw new ArgumentException("Independent loss source changed or is unavailable.");
        var source = CurrentSource(r); var c = source.Book.Charter(r.Charter);
        if (source.Book.Export() != book.Export() || source.Ledger.Export() != transfers.Export() || source.Political.Export() != journal.Export())
            throw new ArgumentException("Independent source changed beyond its exact acknowledged sibling losses.");
        source.Book.ClosePoliticalLostLeaf(c.Id, c.Terms.Subject, source.Book.Entity(c.Terms.Subject).NativeOwner, r.LossTick, now, new(r.Id, Digest(r)));
        return new(id, now, Digest(r), book.Export(), transfers.Export(), journal.Export(), source.Book.Export());
    }
    public (CharterBook Book, PoliticalTownLossArchive Archive, AllegianceTransitionLedger Political) Commit(PoliticalTownLossPlan plan, CharterBook book,
        TerritoryTransferLedger transfers, AllegianceTransitionLedger journal, long now)
    {
        RuntimeCompat.NotNull(plan); var fresh = Review(plan.Id, book, transfers, journal, now);
        if (fresh != plan) throw new ArgumentException("Stale or changed independent loss confirmation.");
        var after = CharterBook.Restore(plan.BookAfter); var copy = Restore(Export()); var original = copy.records[plan.Id];
        copy.records[plan.Id] = original with { ResolvedTick = now, AcknowledgedBefore = original.PoliticalBefore == journal.Export() ? null : journal.Export() };
        copy.ValidatePair(after, transfers);
        var political = AllegianceTransitionLedger.Restore(journal.Export()); political.AcknowledgeTownLoss(copy, after, transfers, plan.Id, now);
        if (copy.Export().Length > 32000000) throw new ArgumentException("Political town-loss receipt capacity.");
        return (after, copy, political);
    }
    public void ValidatePair(CharterBook book, TerritoryTransferLedger transfers)
    {
        PoliticalTownLoss pendingOrigin = null; string pendingBook = null, pendingTransfers = null;
        foreach (var r in records.Values)
        {
            var original = Source(r); var c = original.Book.Charter(r.Charter);
            if (r.ResolvedTick == null)
            {
                if (pendingOrigin == null) { var expected = CurrentSource(r); pendingOrigin = r; pendingBook = expected.Book.Export(); pendingTransfers = expected.Ledger.Export(); }
                if (!SameEpoch(pendingOrigin, r) || book.Export() != pendingBook || transfers.Export() != pendingTransfers)
                    throw new ArgumentException("Pending independent loss source changed.");
            }
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
        PoliticalTownLoss pendingOrigin = null; string pendingHead = null;
        if (matching.Count(r => r.ResolvedTick != null) != receipts.Length) throw new ArgumentException("Independent political receipts require their native witnesses.");
        foreach (var r in matching)
        {
            var original = AllegianceTransitionLedger.Restore(r.PoliticalBefore).Current; AllegianceTransitionLedger.ValidateLossLineage(original, current);
            if (r.ResolvedTick == null)
            {
                if (pendingOrigin == null) { pendingOrigin = r; pendingHead = CurrentSource(r).Political.Export(); }
                if (!SameEpoch(pendingOrigin, r) || pendingHead != journal.Export()) throw new ArgumentException("Pending independent loss intent changed.");
            }
            else if (receipts.SingleOrDefault(l => l.Loss == r.Id) != Receipt(r.Id) ||
                !(AllegianceTransitionLedger.Restore(r.AcknowledgedBefore ?? r.PoliticalBefore).Current.TownLosses ?? []).SequenceEqual(receipts.TakeWhile(l => l.Loss != r.Id)))
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
        var pending = result.Records.Where(r => r.ResolvedTick == null).ToArray();
        if (pending.Skip(1).Any(r => !SameEpoch(pending[0], r))) throw new ArgumentException("Overlapping independent observation epochs.");
        return result;
    }
}
