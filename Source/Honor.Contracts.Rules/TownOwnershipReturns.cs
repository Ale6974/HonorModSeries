using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honor.Contracts.Rules;

public sealed record TownOwnershipReturn(string Root, string Territory, string FromOwner, string ReturnedOwner, string LossProof,
    long OccurredTick, long ObservedTick,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? InvalidatedTick = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string AttemptedOwner = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Transfers { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Amendments { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string ScopeSnapshot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string PeerScope { get; init; }
}
public sealed record TownOwnershipReturnSave(int Version, TownOwnershipReturn[] Records);

/// <summary>Positive native ownership-return observations, not battle legitimacy, annexation or revived contracts.</summary>
public sealed class TownOwnershipReturns
{
    public const int Capacity = 128;
    private const int MaxCharacters = 32000000, InvalidationReserve = Capacity * 1024;
    private readonly Dictionary<string, TownOwnershipReturn> records = new(StringComparer.Ordinal);
    public IReadOnlyCollection<TownOwnershipReturn> Records => records.Values.ToArray();
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static string Hash<T>(T value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    public static string LossDigest(TownLossProof loss) => Hash(loss with { Resolution = null });
    public static string ReceiptDigest(TownOwnershipReturn r) => Hash(r with { InvalidatedTick = null, AttemptedOwner = null });
    public static string ScopeDigest(string snapshot) => snapshot == null ? null : Hash(snapshot);
    private static void ValidateScope(TownOwnershipReturn r, TownSuccessionArchive losses, CharterBook book, bool exact)
    {
        if (r.ScopeSnapshot == null)
        { if (exact && losses.ScopeLosses(r.Root).Length != 1) throw new ArgumentException("Legacy return cannot expand to a later loss scope."); return; }
        var original = TownSuccessionArchive.Restore(r.ScopeSnapshot); losses.ValidateBook(book);
        if (original.Records.Any(p => !losses.Records.Any(c => c.Root == p.Root && LossDigest(c) == LossDigest(p) &&
            (p.Resolution == null || JsonSerializer.Serialize(c.Resolution) == JsonSerializer.Serialize(p.Resolution)))))
            throw new ArgumentException("Original full loss history or prior resolved rights changed.");
        var scope = original.ScopeLosses(r.PeerScope ?? r.Root); var current = losses.ScopeLosses(r.PeerScope ?? r.Root);
        if (scope.Length < 2 || scope.Any(p => p.Resolution != null || p.ObservedTick > r.OccurredTick) ||
            scope.Any(p => !current.Any(c => c.Root == p.Root && LossDigest(c) == LossDigest(p))) ||
            exact && !new HashSet<string>(scope.Select(p => p.Root), StringComparer.Ordinal).SetEquals(current.Select(p => p.Root)))
            throw new ArgumentException("Original compound return scope changed or is incomplete.");
    }
    private static void Validate(TownOwnershipReturn r)
    {
        if (r == null || !Key(r.Root) || !Key(r.Territory) || !Key(r.FromOwner) || !Key(r.ReturnedOwner) || r.FromOwner == r.ReturnedOwner ||
            !CityStockProof.Valid(r.LossProof) || r.OccurredTick < 0 || r.ObservedTick < r.OccurredTick || r.ObservedTick > int.MaxValue ||
            r.InvalidatedTick.HasValue && (r.InvalidatedTick < r.ObservedTick || r.InvalidatedTick > int.MaxValue || r.AttemptedOwner != null && !Key(r.AttemptedOwner)) ||
            !r.InvalidatedTick.HasValue && r.AttemptedOwner != null)
            throw new ArgumentException("Invalid native ownership-return observation.");
    }
    private static TownLossProof Source(TownOwnershipReturn r, TownSuccessionArchive losses, CharterBook book)
    {
        Validate(r); var p = losses.Record(r.Root);
        bool peer = r.PeerScope != null;
        if (peer && (r.ScopeSnapshot == null ||
            !losses.ScopeLosses(r.PeerScope).Any(c => c.Root == p.Root))) throw new ArgumentException("Exact original containing scope required for a child return.");
        if (p.Kind != TownLossKind.HostileNativeOwnershipChange || !peer && (p.ScopeRoot != null || p.ScopeOrdinal != null || p.PriorScopes != null) ||
            r.Territory != p.Territory || r.FromOwner != p.CapturingOwner ||
            r.ReturnedOwner != p.OwnerAtLoss || r.LossProof != LossDigest(p) || r.OccurredTick < p.ObservedTick ||
            book.Charter(p.Root).Phase != RecordedCharterPhase.TerritoryLost)
            throw new ArgumentException("Return must match one exact original hostile loss.");
        ValidateScope(r, losses, book, false);
        if (p.OwnerAtLoss == p.BaselineOwner)
        {
            if (r.Transfers != null || r.Amendments != null) throw new ArgumentException("An unchanged baseline must retain its original return proof shape.");
        }
        else
        {
            if (r.Transfers == null) throw new ArgumentException("A migrated owner requires its exact original completed transfer journal.");
            var transfers = TerritoryTransferLedger.Restore(r.Transfers);
            var last = transfers.Transfers.LastOrDefault(t => t.Territory == p.Territory && t.Phase == TerritoryTransferPhase.Applied);
            var originalBook = CharterBook.Restore(p.Before);
            var original = originalBook.Charter(p.Root);
            if (transfers.LastTick > p.LossTick || transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) ||
                !transfers.VerifiedOwner(p.Territory, p.BaselineOwner, p.OwnerAtLoss) || last == null ||
                last.Purpose != TerritoryTransferPurpose.ActiveCharter)
                throw new ArgumentException("Return requires the matching root's completed pre-loss transfer, never a new normalization authority.");
            if (last.Charter == p.Root)
            {
                if (r.Amendments != null) throw new ArgumentException("A same-root transfer must retain its original proof shape.");
            }
            else original = AmendmentOrigin(r, p, originalBook, transfers, last);
            if (last.PreparedTick < Math.Max(original.PlayerSignedTick.Value, original.NpcSignedTick.Value))
                throw new ArgumentException("Transfer must follow the actual original charter signatures.");
        }
        return p;
    }
    private static RecordedCharter AmendmentOrigin(TownOwnershipReturn r, TownLossProof loss, CharterBook before,
        TerritoryTransferLedger transfers, TerritoryTransfer transfer)
    {
        if (r.Amendments == null) throw new ArgumentException("A different charter requires its exact negotiated same-party lineage.");
        var archive = CharterAmendments.Restore(r.Amendments); archive.ValidateBook(before);
        var current = before.Charter(loss.Root); var visited = new HashSet<string>(StringComparer.Ordinal);
        while (current.Id != transfer.Charter)
        {
            if (!visited.Add(current.Id) || current.Amendment == null) throw new ArgumentException("Missing or circular negotiated ownership lineage.");
            var row = archive.Records.SingleOrDefault(a => a.Witness.Next == current.Id && a.Proof == current.Amendment.Proof);
            if (row == null || row.Witness.Decision.Realm != loss.OwnerAtLoss || row.Witness.Tick > loss.LossTick ||
                !transfer.ResolvedTick.HasValue || transfer.ResolvedTick > row.Witness.Tick)
                throw new ArgumentException("Negotiation must follow the completed original ownership transfer and retain its actual realm.");
            var prior = TerritoryTransferLedger.Restore(row.Witness.Transfers);
            if (prior.LastTick > transfers.LastTick || !prior.Transfers.SequenceEqual(transfers.Transfers.Take(prior.Transfers.Count)) ||
                !prior.Transfers.Contains(transfer) || !prior.VerifiedOwner(loss.Territory, loss.BaselineOwner, loss.OwnerAtLoss))
                throw new ArgumentException("Negotiation must retain the exact original transfer lineage, not relabel a later receipt.");
            current = before.Charter(current.Amendment.Previous);
        }
        return current;
    }
    public TownOwnershipReturns Observe(TownOwnershipReturn r, TownSuccessionArchive losses, CharterBook book)
    {
        losses.ValidateBook(book); var p = Source(r, losses, book);
        ValidateScope(r, losses, book, true);
        if (p.Resolution != null || book.LastTick > r.OccurredTick ||
            r.InvalidatedTick.HasValue || records.ContainsKey(r.Root) || records.Count >= Capacity)
            throw new ArgumentException("Duplicate, resolved or unavailable ownership-return scope.");
        var copy = Restore(Export()); copy.records.Add(r.Root, r); copy.ValidatePair(losses, book);
        if (copy.Export().Length > MaxCharacters - InvalidationReserve) throw new ArgumentException("Ownership-return archive capacity; retain room for future invalidations.");
        return copy;
    }
    public TownOwnershipReturns Invalidate(string root, long tick, string attemptedOwner)
    {
        var r = records.TryGetValue(root ?? "", out var found) ? found : throw new ArgumentException("Unknown ownership-return observation.");
        if (r.InvalidatedTick.HasValue) return Restore(Export());
        var next = r with { InvalidatedTick = tick, AttemptedOwner = attemptedOwner }; Validate(next);
        var copy = Restore(Export()); copy.records[root] = next; return copy;
    }
    public string RequireActive(string root, TownSuccessionArchive losses, CharterBook book, string territory, string actualOwner, long now)
        => RequireActiveWithTransfers(root, losses, book, territory, actualOwner, now, null);
    public string RequireActiveWithTransfers(string root, TownSuccessionArchive losses, CharterBook book, string territory, string actualOwner, long now, string currentTransfers)
        => RequireActiveWithLineage(root, losses, book, territory, actualOwner, now, currentTransfers, null);
    public string RequireActiveWithLineage(string root, TownSuccessionArchive losses, CharterBook book, string territory, string actualOwner, long now, string currentTransfers, string currentAmendments)
    {
        var r = records.TryGetValue(root ?? "", out var found) ? found : throw new ArgumentException("No positive native ownership-return observation.");
        var p = Source(r, losses, book);
        ValidateScope(r, losses, book, true);
        if (p.Resolution != null || r.InvalidatedTick.HasValue || now < r.ObservedTick || now > int.MaxValue || territory != r.Territory || actualOwner != r.ReturnedOwner ||
            r.Transfers != null && r.Transfers != currentTransfers || r.Amendments != null && r.Amendments != currentAmendments)
            throw new ArgumentException("The observed ownership return is unavailable, interrupted or changed.");
        return ReceiptDigest(r);
    }
    public (CharterBook Book, TownSuccessionArchive Archive) CommitSuccession(TownSuccessionArchive losses, CharterBook book,
        TownSuccessionPlan plan, long now, IEnumerable<TerritoryObservation> observations, string proof)
        => CommitSuccessionWithTransfers(losses, book, plan, now, observations, proof, null, null);
    public (CharterBook Book, TownSuccessionArchive Archive) CommitSuccessionWithTransfers(TownSuccessionArchive losses, CharterBook book,
        TownSuccessionPlan plan, long now, IEnumerable<TerritoryObservation> observations, string proof, string actualOwner, string currentTransfers)
        => CommitSuccessionWithLineage(losses, book, plan, now, observations, proof, actualOwner, currentTransfers, null);
    public (CharterBook Book, TownSuccessionArchive Archive) CommitSuccessionWithLineage(TownSuccessionArchive losses, CharterBook book,
        TownSuccessionPlan plan, long now, IEnumerable<TerritoryObservation> observations, string proof, string actualOwner, string currentTransfers, string currentAmendments)
    {
        RuntimeCompat.NotNull(plan); RuntimeCompat.NotNull(observations);
        var actual = observations.ToArray(); var territory = losses.Record(plan.Root).Territory;
        var owner = actual.SingleOrDefault(o => o.Id == territory);
        if (owner == null || !owner.Exists || owner.NativeOwner != book.Entity(territory).NativeOwner ||
            RequireActiveWithLineage(plan.Root, losses, book, territory, actualOwner ?? owner.NativeOwner, now, currentTransfers, currentAmendments) != proof)
            throw new ArgumentException("Review the exact active return and current territory first.");
        var completed = losses.Commit(book, plan, now, actual);
        var saved = JsonSerializer.Deserialize<TownLossSave>(completed.Archive.Export());
        var linked = TownSuccessionArchive.Restore(JsonSerializer.Serialize(saved with { Records = saved.Records.Select(p => p.Root == plan.Root ?
            p with { Resolution = p.Resolution with { OwnershipReturnProof = proof, OwnershipReturnScopeProof = ScopeDigest(records[plan.Root].ScopeSnapshot) } } : p).ToArray() }));
        linked.ValidateBook(completed.Book); ValidatePair(linked, completed.Book); return (completed.Book, linked);
    }
    public void ValidatePair(TownSuccessionArchive losses, CharterBook book)
    {
        losses.ValidateBook(book);
        foreach (var r in records.Values)
        {
            var p = Source(r, losses, book);
            if (p.Resolution?.OwnershipReturnProof != null)
            {
                ValidateScope(r, losses, book, true);
                if (r.InvalidatedTick.HasValue || p.Resolution.OwnershipReturnProof != ReceiptDigest(r) || p.Resolution.Tick < r.ObservedTick ||
                    p.Resolution.OwnershipReturnScopeProof != ScopeDigest(r.ScopeSnapshot) || p.Resolution.OwnershipReturnGroup != r.PeerScope)
                    throw new ArgumentException("Succession differs from its exact observed return.");
            }
        }
        foreach (var p in losses.Records.Where(p => p.Resolution?.OwnershipReturnProof != null))
            if (!records.TryGetValue(p.Root, out var r) || ReceiptDigest(r) != p.Resolution.OwnershipReturnProof)
                throw new ArgumentException("Returned-ownership succession requires its original positive native witness.");
    }
    public (CharterBook Book, TownSuccessionArchive Archive) CommitSuccessionGroup(TownSuccessionArchive losses, CharterBook book,
        TownSuccessionPlan plan, long now, TerritoryObservation[] observations, IReadOnlyDictionary<string, string> proofs,
        IReadOnlyDictionary<string, string> actualOwners, string transfers, string amendments)
    {
        var scope = losses.ScopeLosses(plan.Root);
        if (proofs == null || proofs.Count == 0 || actualOwners == null || proofs.Keys.Any(id => !scope.Any(p => p.Root == id && p.Kind == TownLossKind.HostileNativeOwnershipChange)) ||
            !new HashSet<string>(scope.Where(p => p.Kind == TownLossKind.HostileNativeOwnershipChange).Select(p => p.Root), StringComparer.Ordinal).SetEquals(actualOwners.Keys))
            throw new ArgumentException("Exact returned members of this original scope required.");
        foreach (var p in scope.Where(p => p.Kind == TownLossKind.HostileNativeOwnershipChange))
        {
            if (!actualOwners.TryGetValue(p.Root, out string owner)) throw new ArgumentException("Missing actual hostile-loss town owner.");
            if (owner == p.CapturingOwner) { if (proofs.ContainsKey(p.Root)) throw new ArgumentException("No return proof is consumed for a still captured town."); continue; }
            if (!proofs.TryGetValue(p.Root, out string proof) || RequireActiveWithLineage(p.Root, losses, book, p.Territory, owner, now, transfers, amendments) != proof)
                throw new ArgumentException("Every individually returned city requires its own positive native receipt.");
        }
        if (proofs.Keys.Any(id => !actualOwners.ContainsKey(id))) throw new ArgumentException("Unused returned city evidence.");
        var completed = losses.Commit(book, plan, now, observations);
        var saved = JsonSerializer.Deserialize<TownLossSave>(completed.Archive.Export());
        var paired = TownSuccessionArchive.Restore(JsonSerializer.Serialize(saved with { Records = saved.Records.Select(p => proofs.TryGetValue(p.Root, out var proof) ?
            p with { Resolution = p.Resolution with { OwnershipReturnProof = proof, OwnershipReturnScopeProof = ScopeDigest(records[p.Root].ScopeSnapshot), OwnershipReturnGroup = records[p.Root].PeerScope } } : p).ToArray() }));
        paired.ValidateBook(completed.Book); ValidatePair(paired, completed.Book); return (completed.Book, paired);
    }
    public void ValidateAmendmentPair(CharterAmendments current, CharterBook book)
    {
        RuntimeCompat.NotNull(current); current.ValidateBook(book);
        foreach (var r in records.Values.Where(r => r.Amendments != null))
        {
            var original = CharterAmendments.Restore(r.Amendments); original.ValidateBook(book);
            if (original.Records.Any(p => !current.Records.Contains(p)))
                throw new ArgumentException("Returned ownership needs its unchanged original live negotiation history.");
        }
    }
    public string Export() => JsonSerializer.Serialize(new TownOwnershipReturnSave(1, records.Values.ToArray()));
    public static TownOwnershipReturns Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxCharacters) throw new ArgumentException("Invalid ownership-return archive size.");
        var saved = JsonSerializer.Deserialize<TownOwnershipReturnSave>(json);
        if (saved == null || saved.Version != 1 || saved.Records == null || saved.Records.Length > Capacity) throw new ArgumentException("Invalid ownership-return archive.");
        var copy = new TownOwnershipReturns();
        foreach (var r in saved.Records) { Validate(r); if (copy.records.ContainsKey(r.Root)) throw new ArgumentException("Duplicate ownership return."); copy.records.Add(r.Root, r); }
        return copy;
    }
}
