using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record FollowerCandidate(string Charter, TerritoryTransferObservation Observation);
public sealed record FollowerTransferPlan(string Target, IReadOnlyList<FollowerCandidate> Entries, IReadOnlyList<string> Moves, string Stamp, long ReviewedTick);
public static class FollowerTransfers
{
    /// <summary>Validate the complete active NPC subject set on a copy, including cumulative source losses.</summary>
    public static FollowerTransferPlan Review(CharterBook book, TerritoryTransferLedger ledger, IEnumerable<FollowerCandidate> candidates,
        string target, long now, string verifiedAdministration = null)
    {
        RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(ledger); RuntimeCompat.NotNull(candidates);
        var entries = candidates.OrderBy(c => c?.Charter, StringComparer.Ordinal).ToArray();
        var required = book.PlayerFollowers()
            .Select(c => c.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (string.IsNullOrWhiteSpace(target) || target.Length > 120 || target.Any(char.IsControl) || book.Entities.Any(e => e.Player && e.NativeOwner == target) ||
            now < book.LastTick || now < ledger.LastTick || now > int.MaxValue ||
            entries.Any(e => e?.Observation == null) || !entries.Select(e => e.Charter).SequenceEqual(required) || ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared))
            throw new ArgumentException("All active NPC subjects and resolved journals are required.");
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var o = entry.Observation;
            if (string.IsNullOrWhiteSpace(o.Owner) || o.SourceSettlements < 1 || counts.TryGetValue(o.Owner, out int oldCount) && oldCount != o.SourceSettlements)
                throw new ArgumentException("Inconsistent live source counts.");
            counts[o.Owner] = o.SourceSettlements;
        }
        var copy = TerritoryTransferLedger.Restore(ledger.Export()); var moves = new List<string>();
        foreach (var entry in entries)
        {
            var c = book.Charter(entry.Charter); var entity = book.Entity(c.Terms.Subject); var o = entry.Observation;
            if (o.Territory != entity.Id || !o.Exists || o.Player || !o.Eligible || !o.TargetEligible || o.TargetPlayer || o.PendingDelivery ||
                !copy.VerifiedOwner(entity.Id, entity.NativeOwner, o.Owner))
                throw new ArgumentException("Follower is missing, changed owner or is ineligible.");
            if (o.Owner == target) continue; // A visited city already at the destination needs no ownership change.
            string id = "follower-review-" + Guid.NewGuid().ToString("N");
            copy.Prepare(id, book, entry.Charter, o with { SourceSettlements = counts[o.Owner] }, target, target, null,
                TerritoryTransferPurpose.ActiveCharter, now, verifiedAdministration);
            copy.Confirm(id, o with { Owner = target }, now);
            counts[o.Owner]--; moves.Add(entry.Charter);
        }
        string stamp = JsonSerializer.Serialize(new { Target = target, Now = now, Administration = verifiedAdministration,
            Book = book.Export(), Ledger = ledger.Export(), Entries = entries });
        return new(target, Array.AsReadOnly(entries), moves.AsReadOnly(), stamp, now);
    }
}
