namespace Honor.Contracts.Rules;

public sealed partial class TownSuccessionArchive
{
    private string ScopeParent(string root) => records.Values.SingleOrDefault(r => r.PriorScopes?.Contains(root) == true)?.Root;
    public bool IsReviewRoot(string root) => Record(root).ScopeRoot == null && ScopeParent(root) == null;
    private CharterBook Original(TownLossProof proof)
    {
        if (proof.ScopeRoot != null) return Original(Record(proof.ScopeRoot));
        if (proof.PriorScopes != null) return Original(Record(proof.PriorScopes[0]));
        return CharterBook.Restore(proof.Before);
    }
    private void ValidateScopeTopology()
    {
        var adopted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var proof in records.Values)
        {
            if (proof.ScopeRoot != null && (proof.PriorScopes != null || Record(proof.ScopeRoot).ScopeRoot != null))
                throw new ArgumentException("Nested records cannot merge independent scopes.");
            if (proof.PriorScopes == null) continue;
            if (proof.ScopeRoot != null || proof.ScopeOrdinal != null || proof.PriorScopes.Length is < 1 or >= Capacity ||
                proof.PriorScopes.Any(id => !Key(id) || id == proof.Root || Record(id).ScopeRoot != null || !adopted.Add(id)))
                throw new ArgumentException("Invalid or multiply adopted prior scope.");
        }
        void Visit(string id, HashSet<string> path)
        {
            if (!path.Add(id) || path.Count > 4) throw new ArgumentException("Cyclic or excessive scope merge depth.");
            foreach (string prior in Record(id).PriorScopes ?? []) Visit(prior, new(path, StringComparer.Ordinal));
        }
        foreach (var head in records.Values.Where(r => r.ScopeRoot == null)) Visit(head.Root, new(StringComparer.Ordinal));
    }
    // The first prior scope supplies the actual pre-loss graph. Later scopes must still be active in that exact graph.
    private CharterBook MergeOrigin(TownLossProof proof, CharterBook before)
    {
        if (proof.ScopeRoot != null || proof.ScopeOrdinal != null || proof.PriorScopes is not { Length: > 0 } ||
            proof.PriorScopes.Length >= Capacity || proof.PriorScopes.Distinct(StringComparer.Ordinal).Count() != proof.PriorScopes.Length || proof.PriorScopes.Contains(proof.Root))
            throw new ArgumentException("Invalid reverse-loss merge evidence.");
        var original = Original(Record(proof.PriorScopes[0])); var root = original.Charter(proof.Root);
        if (!original.PlayerManaged(root) || root.Terms.Subject != proof.Territory || before.Charter(proof.Root) != root ||
            before.Entity(proof.Territory) != original.Entity(proof.Territory)) throw new ArgumentException("New upper loss differs from the original managed graph.");
        var known = new List<TownLossProof>();
        foreach (string id in proof.PriorScopes)
        {
            var prior = Record(id); string parent = ScopeParent(id);
            if (prior.ScopeRoot != null || parent != null && parent != proof.Root || prior.OwnerAtLoss != proof.OwnerAtLoss ||
                prior.Resolution?.Tick < proof.ObservedTick ||
                prior.ObservedTick > proof.LossTick || !original.IsBelow(prior.Territory, proof.Territory))
                throw new ArgumentException("Prior scope is not an earlier same-realm descendant of this upper town.");
            known.AddRange(ScopeRecords(id));
        }
        if (known.Select(r => r.Root).Distinct(StringComparer.Ordinal).Count() != known.Count) throw new ArgumentException("Repeated prior loss.");
        foreach (var loss in known)
        {
            var c = original.Charter(loss.Root);
            if (loss.OwnerAtLoss != proof.OwnerAtLoss || loss.ObservedTick > proof.LossTick || loss.ObservedTick > before.LastTick ||
                c.Phase != RecordedCharterPhase.Active || c.Terms.Subject != loss.Territory || !original.IsBelow(loss.Territory, proof.Territory))
                throw new ArgumentException("The selected origin does not precede every adopted loss.");
        }
        foreach (var c in Descendants(original, proof.Territory))
        {
            var loss = known.SingleOrDefault(r => r.Root == c.Id);
            if (before.Charter(c.Id) != (loss == null ? c : Lost(c, loss)) || before.Entity(c.Terms.Subject) != original.Entity(c.Terms.Subject))
                throw new ArgumentException("Merge must preserve every original surviving contract and every earlier exact loss.");
        }
        return original;
    }
    public string[] ObservationMerges(CharterBook current, string charter, string actualOwner)
    {
        ValidateBook(current); var c = current.Charter(charter);
        var prior = records.Values.Where(r => IsReviewRoot(r.Root) && r.Resolution == null).Where(r => {
            var origin = Original(r);
            return origin.Entities.Any(e => e.Id == c.Terms.Subject) && c.Terms.Subject != r.Territory && origin.IsBelow(r.Territory, c.Terms.Subject);
        }).ToArray();
        if (prior.Length == 0) return null;
        if (c.Phase != RecordedCharterPhase.Active || !current.PlayerManaged(c) || prior.Any(r => r.OwnerAtLoss != actualOwner))
            throw new ArgumentException("No current same-realm higher loss authority.");
        // Select by actual snapshot order, including same-tick events; never guess from dictionary order or names.
        var first = prior.FirstOrDefault(candidate => {
            var origin = Original(candidate);
            return prior.All(p => origin.Charters.Any(row => row.Id == p.Root && row.Phase == RecordedCharterPhase.Active));
        }) ?? throw new ArgumentException("No verified common pre-loss graph.");
        return new[] { first.Root }.Concat(prior.Where(p => p.Root != first.Root).Select(p => p.Root).OrderBy(id => id, StringComparer.Ordinal)).ToArray();
    }
}
