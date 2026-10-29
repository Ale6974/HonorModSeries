using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record TownSuccessionReceipt(string Parent, long Tick, NpcCharterAuthority Authority, Dictionary<string, string> Replacements,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string ScopeCompletion = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string OwnershipReturnProof { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string OwnershipReturnScopeProof { get; init; }
}
public enum TownLossKind { Destroyed, HostileNativeOwnershipChange }
public sealed record TownLossProof(string Root, string Territory, string BaselineOwner, string OwnerAtLoss, string Label,
    long LossTick, long ObservedTick, string Before, TownSuccessionReceipt Resolution = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] TownLossKind Kind = TownLossKind.Destroyed,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string CapturingOwner = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string ScopeRoot = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? ScopeOrdinal = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string[] PriorScopes = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? ClosureTick { get; init; }
}
public sealed record TownSuccessionPlan(string Root, string Before, string After, long Tick, string Parent,
    NpcCharterAuthority Authority, Dictionary<string, string> Replacements, string[] Descendants,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string ScopePrefix = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string[] ScopeRoots = null);
public sealed record TownLossSave(int Version, TownLossProof[] Records);

/// <summary>Verified native town loss followed by explicit same-realm contractual succession; no native ownership output.</summary>
public sealed partial class TownSuccessionArchive
{
    public const int Capacity = 128;
    private readonly Dictionary<string, TownLossProof> records = new(StringComparer.Ordinal);
    public IReadOnlyCollection<TownLossProof> Records => records.Values.ToArray();
    public bool HasUnresolved => records.Values.Any(r => r.Resolution == null);
    public TownLossProof Record(string root) => records.TryGetValue(root ?? "", out var value) ? value : throw new ArgumentException("Unknown observed town loss.");
    private static bool Key(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 120 && !id.Any(char.IsControl);
    private static RecordedCharter[] Descendants(CharterBook book, string territory) => book.Charters
        .Where(c => c.Phase == RecordedCharterPhase.Active && book.IsBelow(c.Terms.Subject, territory)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
    private CharterBook Source(TownLossProof proof)
    {
        if (proof == null || !Key(proof.Root) || !Key(proof.Territory) || !Key(proof.BaselineOwner) || !Key(proof.OwnerAtLoss) ||
            string.IsNullOrWhiteSpace(proof.Label) || proof.Label.Length > 200 || proof.LossTick < 0 || proof.ObservedTick < proof.LossTick || proof.ObservedTick > int.MaxValue ||
            !RuntimeCompat.IsDefined(proof.Kind) || (proof.Kind == TownLossKind.HostileNativeOwnershipChange ? !Key(proof.CapturingOwner) || proof.CapturingOwner == proof.OwnerAtLoss : proof.CapturingOwner != null) ||
            proof.ClosureTick.HasValue && (proof.Kind != TownLossKind.Destroyed || proof.ScopeRoot != null || proof.ScopeOrdinal != null || proof.PriorScopes != null ||
                proof.ClosureTick < proof.ObservedTick || proof.ClosureTick > int.MaxValue))
            throw new ArgumentException("Invalid observed town loss.");
        var before = CharterBook.Restore(proof.Before); var root = before.Charter(proof.Root);
        if (before.LastTick > proof.LossTick || root.Terms.Subject != proof.Territory || proof.ScopeRoot == null && (!before.PlayerManaged(root) || proof.ScopeOrdinal != null) ||
            before.Entity(proof.Territory).NativeOwner != proof.BaselineOwner || proof.ScopeRoot == null && proof.PriorScopes == null && Descendants(before, proof.Territory).Length == 0)
            throw new ArgumentException("No original managed upper-town scope.");
        var expected = CharterBook.Restore(before.Export());
        Close(expected, proof);
        return before;
    }
    private void Close(CharterBook book, TownLossProof proof)
    {
        if (proof.PriorScopes != null) MergeOrigin(proof, book);
        if (proof.ScopeRoot != null)
            book.CloseRecordedDescendantLoss(proof.Root, proof.Territory, proof.BaselineOwner, proof.LossTick, proof.ObservedTick, CascadeOrigin(proof, book));
        else if (proof.Kind == TownLossKind.Destroyed)
            book.CloseDestroyedTown(proof.Root, proof.Territory, proof.BaselineOwner, proof.LossTick, proof.ClosureTick ?? proof.ObservedTick, true, true);
        else book.CloseHostileTransferredTown(proof.Root, proof.Territory, proof.BaselineOwner, proof.OwnerAtLoss,
            proof.CapturingOwner, proof.LossTick, proof.ObservedTick, true, true);
    }
    public CharterBook CloseForObservation(TownLossProof proof)
    { var copy = Source(proof); Close(copy, proof); return copy; }
    public TownSuccessionArchive Observe(TownLossProof proof, CharterBook after)
    {
        if (proof?.PriorScopes != null && proof.PriorScopes.Any(id => Record(id).Resolution != null || !IsReviewRoot(id)))
            throw new ArgumentException("Only unresolved current scopes may be adopted; old completion receipts are immutable.");
        var before = Source(proof);
        if (records.Count >= Capacity || records.ContainsKey(proof.Root) || proof.Resolution != null) throw new ArgumentException("Duplicate town loss or archive capacity.");
        if (proof.ScopeRoot != null && (Record(proof.ScopeRoot).Resolution != null || ScopeParent(proof.ScopeRoot) != null || proof.ScopeOrdinal != LocalScopeRecords(proof.ScopeRoot).Length))
            throw new ArgumentException("New loss must append to the unresolved original scope.");
        Close(before, proof);
        if (before.Export() != after.Export()) throw new ArgumentException("Observed loss cannot hide other book changes.");
        var copy = Restore(Export()); copy.records.Add(proof.Root, proof); copy.ValidateScopeTopology(); copy.ValidateBook(after);
        if (copy.Export().Length > 32000000) throw new ArgumentException("Town loss archive byte capacity.");
        return copy;
    }
    public TownSuccessionPlan Review(string rootId, CharterBook current, string prefix, NpcCharterAuthority authority,
        long now, IEnumerable<TerritoryObservation> observations)
    {
        var proof = Record(rootId); var source = Source(proof); RuntimeCompat.NotNull(authority); RuntimeCompat.NotNull(observations);
        ValidateBook(current);
        if (proof.Resolution != null || !Key(prefix) || prefix.Length > 90 || now < current.LastTick || now < proof.ObservedTick || now > int.MaxValue)
            throw new ArgumentException("Already resolved loss or invalid succession review.");
        if (!IsReviewRoot(rootId)) throw new ArgumentException("Review the current upper loss and every retained prior scope together.");
        if (ScopeRecords(rootId).Length > 1) return ReviewCascade(proof, current, prefix, authority, now, observations.ToArray());
        var original = Descendants(source, proof.Territory); var actual = Descendants(current, proof.Territory);
        if (!original.SequenceEqual(actual) || original.Any(c => c.PlayerReleaseTick.HasValue || c.NpcReleaseTick.HasValue))
            throw new ArgumentException("The original full subtree must remain unchanged and not awaiting release.");
        var parties = new HashSet<string>(original.Select(c => c.Terms.Subject), StringComparer.Ordinal) { proof.Territory };
        if (current.Charters.Any(c => current.Phase(c, now) == RecordedCharterPhase.Offered && (parties.Contains(c.Terms.Subject) || parties.Contains(c.Terms.Liege))))
            throw new ArgumentException("Resolve pending subtree proposals before succession.");
        var parent = source.Charter(proof.Root).Terms.Liege;
        var replacements = original.Where(c => c.Terms.Liege == proof.Territory).Select((c, i) => (Old: c.Id, New: prefix + "/" + i))
            .ToDictionary(p => p.Old, p => p.New, StringComparer.Ordinal);
        var copy = CharterBook.Restore(current.Export()); copy.ReplaceLostChildren(rootId, replacements, parent, authority, now, observations.ToArray());
        // Revalidate all successor/history links only after the full atomic replacement has been assembled.
        copy = CharterBook.Restore(copy.Export());
        return new(rootId, current.Export(), copy.Export(), now, parent, authority, replacements, original.Select(c => c.Id).ToArray());
    }
    public (CharterBook Book, TownSuccessionArchive Archive) Commit(CharterBook current, TownSuccessionPlan plan, long now,
        IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(plan);
        if (plan.Replacements == null || plan.Replacements.Count == 0 && plan.ScopePrefix == null || plan.Replacements.Values.Any(v => !Key(v)) || plan.Before != current.Export() || now != plan.Tick)
            throw new ArgumentException("Succession source changed.");
        string prefix = plan.ScopePrefix;
        if (prefix == null) { string sample = plan.Replacements.Values.First(); int separator = sample.LastIndexOf('/');
            if (separator < 1) throw new ArgumentException("Invalid replacement namespace."); prefix = sample.Substring(0, separator); }
        var fresh = Review(plan.Root, current, prefix, plan.Authority, now, observations);
        if (fresh.After != plan.After || fresh.Parent != plan.Parent || plan.Descendants == null || !fresh.Descendants.SequenceEqual(plan.Descendants) ||
            !SameIds(fresh.ScopeRoots, plan.ScopeRoots) || fresh.ScopePrefix != plan.ScopePrefix ||
            fresh.Replacements.Count != plan.Replacements.Count || fresh.Replacements.Any(p => !plan.Replacements.TryGetValue(p.Key, out var next) || next != p.Value))
            throw new ArgumentException("Altered succession plan.");
        var book = CharterBook.Restore(fresh.After); var archive = Restore(Export()); var proof = archive.Record(plan.Root);
        if (fresh.ScopeRoots != null) archive.CommitCascadeReceipts(book, fresh);
        else archive.records[plan.Root] = proof with { Resolution = new(plan.Parent, now, plan.Authority, new(plan.Replacements, StringComparer.Ordinal)) };
        archive.ValidateBook(book);
        if (archive.Export().Length > 32000000) throw new ArgumentException("Town succession receipt byte capacity.");
        return (book, archive);
    }
    public void ValidateBook(CharterBook current)
    {
        ValidateScopeTopology();
        foreach (var proof in records.Values)
        {
            var source = Source(proof); var expected = source.Charter(proof.Root) with { Phase = RecordedCharterPhase.TerritoryLost, TerritoryLostTick = proof.LossTick };
            if (current.Charter(proof.Root) != expected || (proof.ClosureTick ?? proof.ObservedTick) > current.LastTick) throw new ArgumentException("Historical town loss differs from book.");
            if (proof.Resolution == null) continue;
            if (proof.Resolution.OwnershipReturnScopeProof != null && (proof.Resolution.OwnershipReturnProof == null ||
                !CityStockProof.Valid(proof.Resolution.OwnershipReturnScopeProof) || proof.Resolution.ScopeCompletion != proof.Root || ScopeRecords(proof.Root).Length < 2))
                throw new ArgumentException("Compound return needs its distinct original scope witness.");
            if (proof.Resolution.OwnershipReturnProof != null && (!CityStockProof.Valid(proof.Resolution.OwnershipReturnProof) ||
                proof.Kind != TownLossKind.HostileNativeOwnershipChange || proof.ScopeRoot != null || proof.PriorScopes != null ||
                proof.Resolution.OwnershipReturnScopeProof == null && ScopeRecords(proof.Root).Length != 1))
                throw new ArgumentException("Ownership-return proof requires one isolated hostile-loss succession.");
            if (proof.Resolution.Tick < (proof.ClosureTick ?? proof.ObservedTick)) throw new ArgumentException("Succession cannot precede actual contractual loss closure.");
            if (proof.ScopeRoot != null || proof.Resolution.ScopeCompletion != null) { ValidateCascadeReceipt(current, proof); continue; }
            var r = proof.Resolution;
            var children = Descendants(source, proof.Territory).Where(c => c.Terms.Liege == proof.Territory).ToArray();
            if (r.Authority == null || !Key(r.Authority.LiegeRepresentative) || !Key(r.Authority.SubjectRepresentative) || r.Tick < (proof.ClosureTick ?? proof.ObservedTick) ||
                r.Tick > current.LastTick || r.Parent != expected.Terms.Liege || r.Replacements == null || children.Length != r.Replacements.Count ||
                r.Replacements.Values.Distinct(StringComparer.Ordinal).Count() != children.Length) throw new ArgumentException("Invalid historical succession scope.");
            foreach (var child in children)
            {
                if (!r.Replacements.TryGetValue(child.Id, out string next)) throw new ArgumentException("Missing successor contract.");
                var old = current.Charter(child.Id); var created = current.Charter(next);
                if (old != child with { Phase = RecordedCharterPhase.Replaced, Replacement = new(proof.Root, next, r.Tick) } ||
                    created.Terms != child.Terms with { Liege = r.Parent } || created.Replaces != child.Id || created.PlayerSignedTick != r.Tick || created.NpcSignedTick != r.Tick ||
                    !current.Entity(r.Parent).Player && created.NpcAuthority != r.Authority) throw new ArgumentException("Succession history altered.");
            }
        }
        foreach (var c in current.Charters.Where(c => c.Replacement != null))
            if (!records.TryGetValue(c.Replacement.LostRoot, out var proof) || proof.Resolution == null ||
                !proof.Resolution.Replacements.TryGetValue(c.Id, out var next) || next != c.Replacement.Next)
                throw new ArgumentException("Replacement lacks its recorded native loss and reviewed resolution.");
        ValidateCascadeGroups();
    }
    public string Export() => JsonSerializer.Serialize(new TownLossSave(1, records.Values.ToArray()));
    public static TownSuccessionArchive Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32000000) throw new ArgumentException("Invalid town-loss archive size.");
        var saved = JsonSerializer.Deserialize<TownLossSave>(json);
        if (saved == null || saved.Version != 1 || saved.Records == null || saved.Records.Length > Capacity) throw new ArgumentException("Invalid town-loss archive.");
        var result = new TownSuccessionArchive();
        foreach (var record in saved.Records)
        {
            if (record == null || !Key(record.Root)) throw new ArgumentException("Invalid town loss.");
            if (result.records.ContainsKey(record.Root)) throw new ArgumentException("Duplicate town loss.");
            result.records.Add(record.Root, record);
        }
        result.ValidateScopeTopology();
        foreach (var record in result.records.Values) result.Source(record);
        result.ValidateCascadeGroups();
        return result;
    }
}
