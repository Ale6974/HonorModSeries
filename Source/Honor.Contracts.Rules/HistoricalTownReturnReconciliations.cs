using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record HistoricalTownReturnReview(TownSuccessionMandate Current, string Returns, string Transfers, string Amendments,
    Dictionary<string, string> ActualOwners, Dictionary<string, string> Facts);
public sealed record HistoricalTownReturnRecord(HistoricalTownReturnReview Witness, string Proof);
public sealed record HistoricalTownReturnSave(int Version, HistoricalTownReturnRecord[] Records);

/// <summary>New current whole-scope consent using unchanged prior native facts. Old receipts never become wider permissions.</summary>
public sealed class HistoricalTownReturnReconciliations
{
    private readonly List<HistoricalTownReturnRecord> records = [];
    private static T Clone<T>(T x) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(x));
    private static string Hash<T>(T x) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(x))));
    public IReadOnlyList<HistoricalTownReturnRecord> Records => records.Select(Clone).ToArray();
    private static TownSuccessionReceipt Basic(TownSuccessionReceipt r) => r == null ? null : r with {
        GovernmentProof = null, HistoricalReturnProof = null, OwnershipReturnProof = null, OwnershipReturnScopeProof = null, OwnershipReturnGroup = null };
    public static (CharterBook Book, TownSuccessionArchive Archive) Review(HistoricalTownReturnReview w)
    {
        if (w?.Current == null || w.ActualOwners == null || w.Facts == null || w.Facts.Count == 0) throw new ArgumentException("Whole current scope and original positive facts required.");
        var result = TownSuccessionMandates.ReviewCurrentPlan(w.Current); var plan = w.Current.Plan;
        var losses = TownSuccessionArchive.Restore(w.Current.Losses); var before = CharterBook.Restore(plan.Before);
        var returns = TownOwnershipReturns.Restore(w.Returns); returns.ValidatePair(losses, before);
        if (w.Transfers != null)
        {
            var transfers = TerritoryTransferLedger.Restore(w.Transfers);
            if (transfers.LastTick > plan.Tick || transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared)) throw new ArgumentException("Unresolved or future physical custody cannot authorize reconciliation.");
        }
        if (w.Amendments != null) CharterAmendments.Restore(w.Amendments).ValidateBook(before);
        var scope = losses.ScopeLosses(plan.Root).Where(p => p.Kind == TownLossKind.HostileNativeOwnershipChange).ToArray();
        if (!new HashSet<string>(scope.Select(p => p.Root), StringComparer.Ordinal).SetEquals(w.ActualOwners.Keys)) throw new ArgumentException("Every original hostile-loss city's actual owner required.");
        var used = new HashSet<string>(StringComparer.Ordinal); bool historical = false;
        foreach (var p in scope)
        {
            string owner = w.ActualOwners[p.Root];
            if (owner == p.CapturingOwner) { if (w.Facts.ContainsKey(p.Root)) throw new ArgumentException("Captured city cannot consume a return fact."); continue; }
            if (!w.Facts.TryGetValue(p.Root, out string proof) || returns.ReviewHistoricalFact(p.Root, losses, before, owner, plan.Tick, w.Transfers, w.Amendments) != proof)
                throw new ArgumentException("Changed, unwitnessed or omitted individual return.");
            used.Add(p.Root);
            // After factual validation, only the obsolete original scope can prevent the old permission API.
            try { returns.RequireActiveWithLineage(p.Root, losses, before, p.Territory, owner, plan.Tick, w.Transfers, w.Amendments); }
            catch (ArgumentException) { historical = true; }
        }
        if (!historical || !used.SetEquals(w.Facts.Keys)) throw new ArgumentException("Separate reconciliation requires obsolete original scope and no unused facts.");
        return result;
    }
    public (CharterBook Book, TownSuccessionArchive Archive, HistoricalTownReturnReconciliations History) Execute(HistoricalTownReturnReview w,
        TownOwnershipReturns currentReturns, TownSuccessionArchive currentLosses, CharterBook currentBook)
    {
        if (w?.Current?.Plan == null || currentReturns.Export() != w.Returns || currentLosses.Export() != w.Current.Losses || currentBook.Export() != w.Current.Plan.Before ||
            records.Count >= 128 || records.Any(r => r.Witness.Current.Plan.Root == w.Current.Plan.Root) || records.LastOrDefault()?.Witness.Current.Plan.Tick > w.Current.Plan.Tick)
            throw new ArgumentException("Current reconciliation source/history changed.");
        var result = Review(w); var record = new HistoricalTownReturnRecord(Clone(w), Hash(w));
        var save = JsonSerializer.Deserialize<TownLossSave>(result.Archive.Export());
        var linked = TownSuccessionArchive.Restore(JsonSerializer.Serialize(save with { Records = save.Records.Select(p => p.Root == w.Current.Plan.Root ?
            p with { Resolution = p.Resolution with { HistoricalReturnProof = record.Proof } } : p).ToArray() }));
        var next = Restore(Export()); next.records.Add(record); next.ValidatePair(linked, result.Book, currentReturns);
        if (next.Export().Length > 32000000) throw new ArgumentException("Historical return reconciliation capacity.");
        return (result.Book, linked, next);
    }
    public void ValidatePair(TownSuccessionArchive losses, CharterBook book, TownOwnershipReturns returns)
    {
        losses.ValidateBook(book); returns.ValidatePair(losses, book);
        foreach (var record in records)
        {
            var w = record.Witness; var replay = Review(w); var original = TownSuccessionArchive.Restore(w.Current.Losses);
            foreach (var p in original.Records)
            {
                var actual = losses.Record(p.Root);
                if (TownOwnershipReturns.LossDigest(actual) != TownOwnershipReturns.LossDigest(p) ||
                    p.Resolution != null && JsonSerializer.Serialize(actual.Resolution) != JsonSerializer.Serialize(p.Resolution)) throw new ArgumentException("Original loss/third-party history changed.");
            }
            foreach (var p in original.ScopeLosses(w.Current.Plan.Root))
                if (JsonSerializer.Serialize(Basic(losses.Record(p.Root).Resolution)) != JsonSerializer.Serialize(Basic(replay.Archive.Record(p.Root).Resolution))) throw new ArgumentException("Current resolution differs from the exact reviewed scope.");
            var oldReturns = TownOwnershipReturns.Restore(w.Returns);
            if (oldReturns.Records.Any(p => !returns.Records.Any(r => r.Root == p.Root && TownOwnershipReturns.ReceiptDigest(r) == TownOwnershipReturns.ReceiptDigest(p) &&
                (w.Facts.ContainsKey(p.Root) || p.InvalidatedTick != null ? r.InvalidatedTick == p.InvalidatedTick && r.AttemptedOwner == p.AttemptedOwner : r.InvalidatedTick == null || r.InvalidatedTick >= w.Current.Plan.Tick))) ||
                losses.Record(w.Current.Plan.Root).Resolution?.HistoricalReturnProof != record.Proof || book.LastTick < w.Current.Plan.Tick)
                throw new ArgumentException("Original unchanged return history or new reconciliation marker missing.");
        }
        foreach (var p in losses.Records.Where(p => p.Resolution?.HistoricalReturnProof != null))
            if (records.Count(r => r.Witness.Current.Plan.Root == p.Root && r.Proof == p.Resolution.HistoricalReturnProof) != 1) throw new ArgumentException("Missing separate historical return reconciliation.");
    }
    public string Export() => JsonSerializer.Serialize(new HistoricalTownReturnSave(1, records.ToArray()));
    public static HistoricalTownReturnReconciliations Restore(string json)
    {
        if (json == null || json.Length > 32000000) throw new ArgumentException("Invalid historical reconciliation size.");
        var save = JsonSerializer.Deserialize<HistoricalTownReturnSave>(json);
        if (save?.Version != 1 || save.Records == null || save.Records.Length > 128) throw new ArgumentException("Invalid historical reconciliation archive.");
        var result = new HistoricalTownReturnReconciliations();
        foreach (var r in save.Records)
        {
            if (r?.Witness == null || r.Proof != Hash(r.Witness)) throw new ArgumentException("Historical reconciliation witness changed.");
            Review(r.Witness);
            if (result.records.Any(p => p.Witness.Current.Plan.Root == r.Witness.Current.Plan.Root) ||
                result.records.LastOrDefault()?.Witness.Current.Plan.Tick > r.Witness.Current.Plan.Tick) throw new ArgumentException("Historical reconciliation sequence changed.");
            result.records.Add(Clone(r));
        }
        return result;
    }
}
