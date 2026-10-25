namespace Honor.Contracts.Rules;

public sealed partial class TownSuccessionArchive
{
    private static bool SameIds(string[] a, string[] b) => a == null ? b == null : b != null && a.SequenceEqual(b);
    private TownLossProof[] ScopeRecords(string root) => records.Values.Where(r => r.Root == root || r.ScopeRoot == root)
        .OrderBy(r => r.ScopeOrdinal ?? 0).ToArray();
    public TownLossProof[] ScopeLosses(string root)
    {
        if (Record(root).ScopeRoot != null) throw new ArgumentException("Select the original upper-town loss.");
        return ScopeRecords(root);
    }
    public int NextScopeOrdinal(string root) => ScopeLosses(root).Length;
    private static RecordedCharter Lost(RecordedCharter original, TownLossProof proof)
        => original with { Phase = RecordedCharterPhase.TerritoryLost, TerritoryLostTick = proof.LossTick };
    private CharterBook CascadeOrigin(TownLossProof proof, CharterBook before)
    {
        if (!Key(proof.ScopeRoot) || proof.ScopeRoot == proof.Root || proof.ScopeOrdinal is not (>= 1 and < Capacity))
            throw new ArgumentException("Invalid nested loss scope.");
        var head = Record(proof.ScopeRoot);
        if (head.ScopeRoot != null || head.ObservedTick > proof.LossTick || head.OwnerAtLoss != proof.OwnerAtLoss ||
            before.LastTick < head.ObservedTick) throw new ArgumentException("No matching previously observed realm loss.");
        var origin = Source(head); var original = origin.Charter(proof.Root);
        if (!origin.IsBelow(proof.Territory, head.Territory) || original.Terms.Subject != proof.Territory ||
            before.Charter(head.Root) != Lost(origin.Charter(head.Root), head))
            throw new ArgumentException("Nested loss is outside the exact original managed scope.");
        foreach (var c in Descendants(origin, head.Territory))
        {
            var prior = records.Values.SingleOrDefault(r => r.ScopeRoot == head.Root && r.Root == c.Id && r.ScopeOrdinal < proof.ScopeOrdinal);
            if (prior != null && prior.ObservedTick > proof.LossTick || before.Charter(c.Id) != (prior == null ? c : Lost(c, prior)) ||
                before.Entity(c.Terms.Subject) != origin.Entity(c.Terms.Subject))
                throw new ArgumentException("Nested loss must retain every exact preceding scope record.");
        }
        return origin;
    }
    public string ObservationScope(CharterBook current, string charter, string actualOwner)
    {
        ValidateBook(current); var c = current.Charter(charter);
        foreach (var head in records.Values.Where(r => r.ScopeRoot == null && r.Resolution == null))
        {
            var origin = Source(head);
            if (!origin.Entities.Any(e => e.Id == c.Terms.Subject)) continue;
            if (origin.IsBelow(c.Terms.Subject, head.Territory))
            {
                if (origin.Charter(charter) != c || c.Phase != RecordedCharterPhase.Active || head.OwnerAtLoss != actualOwner)
                    throw new ArgumentException("Changed original descendant or foreign realm is not nested loss authority.");
                return head.Root;
            }
            if (c.Terms.Subject != head.Territory && origin.IsBelow(head.Territory, c.Terms.Subject))
                throw new ArgumentException("Loss above an existing unresolved scope needs a separate merge adapter.");
        }
        return null;
    }
    private void ValidateCascadeGroups()
    {
        foreach (var head in records.Values.Where(r => r.ScopeRoot == null))
        {
            var scope = ScopeRecords(head.Root); long last = head.ObservedTick;
            for (int i = 1; i < scope.Length; i++)
            {
                if (scope[i].ScopeOrdinal != i || scope[i].LossTick < last) throw new ArgumentException("Duplicate, missing or unordered nested loss sequence.");
                last = scope[i].ObservedTick;
            }
            if (scope.Length == 1)
            { if (head.Resolution?.ScopeCompletion != null) throw new ArgumentException("Cascade receipt without nested losses."); continue; }
            if (scope.Any(r => r.Resolution != null) && scope.Any(r => r.Resolution == null || r.Resolution.ScopeCompletion != head.Root ||
                r.Resolution.Tick != head.Resolution?.Tick || r.Resolution.Authority != head.Resolution?.Authority))
                throw new ArgumentException("Nested loss resolution must be one atomic reviewed scope.");
        }
    }
    private string[] Skipped(TownLossProof proof, TownLossProof[] scope)
    {
        var before = CharterBook.Restore(proof.Before); string parent = before.Charter(proof.Root).Terms.Liege;
        var result = new List<string>();
        while (scope.SingleOrDefault(r => r.Territory == parent) is { } ancestor)
        {
            if (result.Contains(ancestor.Root) || result.Count >= 3 || ancestor.Root == proof.Root)
                throw new ArgumentException("Invalid lost ancestor chain.");
            result.Add(ancestor.Root); parent = CharterBook.Restore(ancestor.Before).Charter(ancestor.Root).Terms.Liege;
        }
        return result.Count == 0 ? null : result.ToArray();
    }
    private static int Depth(CharterBook origin, string town)
    {
        int depth = 0;
        while (origin.Charters.SingleOrDefault(c => c.Phase == RecordedCharterPhase.Active && c.Terms.Subject == town) is { } parent)
        { if (++depth > 4) throw new ArgumentException("Invalid original hierarchy."); town = parent.Terms.Liege; }
        return depth;
    }
    private TownSuccessionPlan ReviewCascade(TownLossProof head, CharterBook current, string prefix, NpcCharterAuthority authority,
        long now, TerritoryObservation[] observations)
    {
        var scope = ScopeRecords(head.Root); var origin = Source(head); var original = Descendants(origin, head.Territory);
        if (scope.Any(r => r.Resolution != null || r.ObservedTick > now)) throw new ArgumentException("Scope already resolved or future-dated.");
        foreach (var c in original)
        {
            var loss = scope.SingleOrDefault(r => r.Root == c.Id);
            if (c.PlayerReleaseTick.HasValue || c.NpcReleaseTick.HasValue || current.Charter(c.Id) != (loss == null ? c : Lost(c, loss)) ||
                current.Entity(c.Terms.Subject) != origin.Entity(c.Terms.Subject)) throw new ArgumentException("Every original descendant must be unchanged or have its exact observed loss.");
        }
        var parties = new HashSet<string>(original.Select(c => c.Terms.Subject), StringComparer.Ordinal) { head.Territory };
        if (current.Charters.Any(c => current.Phase(c, now) == RecordedCharterPhase.Offered && (parties.Contains(c.Terms.Subject) || parties.Contains(c.Terms.Liege))) ||
            current.Charters.Any(c => c.Phase == RecordedCharterPhase.Active && parties.Contains(c.Terms.Liege) && original.All(o => o.Id != c.Id)))
            throw new ArgumentException("Unreviewed scope additions or pending proposals.");
        var survivors = original.Where(c => scope.All(r => r.Root != c.Id)).ToArray();
        foreach (var c in survivors)
            if (current.Availability(c.Terms.Subject, observations) != CharterAvailability.Current)
                throw new ArgumentException("Every surviving descendant needs current ownership and eligibility.");
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal); var copy = CharterBook.Restore(current.Export());
        foreach (var loss in scope.OrderBy(r => Depth(origin, r.Territory)).ThenBy(r => r.ScopeOrdinal ?? 0))
        {
            var skipped = Skipped(loss, scope); string parent = copy.ReplacementParent(loss.Root, skipped, now);
            if (copy.Availability(parent, observations) != CharterAvailability.Current || !copy.Entity(parent).Player && !copy.IsBelow(parent, copy.Entities.Single(e => e.Player).Id))
                throw new ArgumentException("No verified surviving higher authority.");
            var children = survivors.Where(c => c.Terms.Liege == loss.Territory).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
            var batch = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var child in children) { string id = prefix + "/" + replacements.Count; replacements.Add(child.Id, id); batch.Add(child.Id, id); }
            if (batch.Count > 0) copy.ReplaceLostChildren(loss.Root, batch, parent, authority, now, observations, skipped);
        }
        copy.RecordLossResolution(now); copy = CharterBook.Restore(copy.Export());
        return new(head.Root, current.Export(), copy.Export(), now, origin.Charter(head.Root).Terms.Liege, authority,
            replacements, survivors.Select(c => c.Id).ToArray(), prefix, scope.Select(r => r.Root).ToArray());
    }
    private void CommitCascadeReceipts(CharterBook book, TownSuccessionPlan plan)
    {
        var scope = ScopeRecords(plan.Root);
        foreach (var proof in scope)
        {
            var subset = plan.Replacements.Where(p => book.Charter(p.Key).Replacement?.LostRoot == proof.Root)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            records[proof.Root] = proof with { Resolution = new(book.ReplacementParent(proof.Root, Skipped(proof, scope), plan.Tick),
                plan.Tick, plan.Authority, subset, plan.Root) };
        }
    }
    private void ValidateCascadeReceipt(CharterBook current, TownLossProof proof)
    {
        var head = Record(proof.ScopeRoot ?? proof.Root); var scope = ScopeRecords(head.Root); var r = proof.Resolution;
        if (r == null || r.ScopeCompletion != head.Root || r.Tick < scope.Max(p => p.ObservedTick) || r.Tick > current.LastTick ||
            r.Authority == null || !Key(r.Authority.LiegeRepresentative) || !Key(r.Authority.SubjectRepresentative) || r.Replacements == null)
            throw new ArgumentException("Invalid nested scope receipt.");
        var origin = Source(head); var skipped = Skipped(proof, scope);
        if (r.Parent != current.ReplacementParent(proof.Root, skipped, r.Tick)) throw new ArgumentException("Altered surviving parent.");
        var children = Descendants(origin, proof.Territory).Where(c => c.Terms.Liege == proof.Territory && scope.All(l => l.Root != c.Id)).ToArray();
        if (r.Replacements.Count != children.Length || r.Replacements.Values.Distinct(StringComparer.Ordinal).Count() != children.Length)
            throw new ArgumentException("Missing or extra surviving replacements.");
        foreach (var child in children)
        {
            if (!r.Replacements.TryGetValue(child.Id, out string next)) throw new ArgumentException("Missing surviving contract.");
            var old = current.Charter(child.Id); var created = current.Charter(next); var link = old.Replacement;
            if (old with { Replacement = null } != child with { Phase = RecordedCharterPhase.Replaced } || link == null || link.LostRoot != proof.Root || link.Next != next ||
                link.Tick != r.Tick || !SameIds(link.SkippedRoots, skipped) || created.Terms != child.Terms with { Liege = r.Parent } || created.Replaces != child.Id ||
                created.PlayerSignedTick != r.Tick || created.NpcSignedTick != r.Tick || !current.Entity(r.Parent).Player && created.NpcAuthority != r.Authority)
                throw new ArgumentException("Nested successor history altered.");
        }
    }
}
