namespace Honor.Contracts.Rules;

public sealed record NpcSubcharterPlan(string Previous, string Parent, string Created, string Before, string After, long Tick, NpcCharterAuthority Authority)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NpcRealmDecisionEvidence Decision { get; init; }
}

public static class NpcSubcharters
{
    public static NpcSubcharterPlan ReviewWithParties(CharterBook source, string previous, string parent, string created,
        NpcRealmDecisionEvidence root, IReadOnlyList<RetainedRealmConsent> parties, long now, IEnumerable<TerritoryObservation> observations)
    {
        NpcRealmDecision.Consent(root); var old = source.Charter(previous);
        var expected = new[] { old.Terms.Subject, old.Terms.Liege, parent }.Where(p => !source.Entity(p).Player).Distinct(StringComparer.Ordinal).ToArray();
        if (parties == null || parties.Count != expected.Length || parties.Any(p => p == null) ||
            !new HashSet<string>(expected, StringComparer.Ordinal).SetEquals(parties.Select(p => p.Party)) || parties.Select(p => p.Party).Distinct().Count() != parties.Count)
            throw new ArgumentException("Exact old liege, subject and new parent decisions required.");
        foreach (var p in parties)
        {
            NpcRealmDecision.Consent(p.Decision);
            if (p.Decision.Realm != p.ActualOwner || !p.ReciprocalPeace) throw new ArgumentException("Party owner and current peaceful decision differ.");
        }
        if (parties.Any(p => p.ActualOwner == root.Realm && p.Decision != root) || parties.GroupBy(p => p.ActualOwner, StringComparer.Ordinal).Any(g => g.Select(p => p.Decision).Distinct().Count() != 1))
            throw new ArgumentException("A realm cannot present inconsistent current representatives.");
        var parentDecision = source.Entity(parent).Player ? root : parties.Single(p => p.Party == parent).Decision;
        if (parentDecision != root) throw new ArgumentException("New parent must belong to the player's actual current ordinary authority.");
        var subject = parties.Single(p => p.Party == old.Terms.Subject).Decision;
        return Review(source, previous, parent, created, new(root.Representative, subject.Representative), now, observations) with { Decision = root };
    }
    public static CharterBook CommitWithParties(CharterBook current, NpcSubcharterPlan plan, NpcRealmDecisionEvidence root,
        IReadOnlyList<RetainedRealmConsent> parties, long now, IEnumerable<TerritoryObservation> observations)
    {
        if (plan == null || plan.Tick != now || current.Export() != plan.Before || plan.Decision != root) throw new ArgumentException("Current party decision review expired.");
        var fresh = ReviewWithParties(current, plan.Previous, plan.Parent, plan.Created, root, parties, now, observations);
        if (fresh.Authority != plan.Authority || fresh.After != plan.After) throw new ArgumentException("Actual incoming party consent changed.");
        return CharterBook.Restore(fresh.After);
    }
    public static NpcSubcharterPlan ReviewAuthorized(CharterBook source, string previous, string parent, string created,
        NpcRealmDecisionEvidence current, long now, IEnumerable<TerritoryObservation> observations)
        => Review(source, previous, parent, created, NpcRealmDecision.Consent(current), now, observations) with { Decision = current };
    public static CharterBook CommitAuthorized(CharterBook current, NpcSubcharterPlan plan, NpcRealmDecisionEvidence evidence,
        long now, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(plan);
        var original = NpcRealmDecision.Consent(plan.Decision); var actual = NpcRealmDecision.Consent(evidence);
        if (plan.Decision.Realm != evidence.Realm || original != actual || plan.Authority != actual)
            throw new ArgumentException("Current realm/representative must match the separately reviewed ordinary decision.");
        return Commit(current, plan, now, observations);
    }
    /// <summary>Atomically replace the incoming contract only. Descendant contracts and all old records survive.</summary>
    public static NpcSubcharterPlan Review(CharterBook source, string previous, string parent, string created,
        NpcCharterAuthority authority, long now, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(source); RuntimeCompat.NotNull(observations); RuntimeCompat.NotNull(authority);
        var current = observations.ToArray(); var old = source.Charter(previous);
        var player = source.Entities.SingleOrDefault(e => e.Player) ?? throw new ArgumentException("No player realm.");
        if (old.Phase != RecordedCharterPhase.Active || !source.PlayerManaged(old) ||
            parent != player.Id && !source.IsBelow(parent, player.Id) || parent == old.Terms.Subject || source.IsBelow(parent, old.Terms.Subject) ||
            parent == old.Terms.Liege)
            throw new ArgumentException("Only a different existing NPC vassal in the same player subtree may become the immediate liege.");
        source.ValidateCurrent(previous, current);
        var copy = CharterBook.Restore(source.Export());
        if (!old.PlayerReleaseTick.HasValue)
        {
            if (old.NpcAuthority == null) copy.ConsentToRelease(previous, true, now, current);
            else copy.ConsentNpcRelease(previous, old.Terms.Liege, now, current);
        }
        if (!old.NpcReleaseTick.HasValue)
        {
            if (old.NpcAuthority == null) copy.ConsentToRelease(previous, false, now, current);
            else copy.ConsentNpcRelease(previous, old.Terms.Subject, now, current);
        }
        if (parent == player.Id)
        {
            copy.Offer(created, old.Terms with { Liege = parent }, now, PoliticalWorld.Day * 3, current);
            copy.Sign(created, true, now, current); copy.Sign(created, false, now, current);
        }
        else
        {
            copy.OfferNpc(created, old.Terms with { Liege = parent }, authority, now, PoliticalWorld.Day * 3, current);
            copy.SignNpc(created, parent, authority.LiegeRepresentative, now, current);
            copy.SignNpc(created, old.Terms.Subject, authority.SubjectRepresentative, now, current);
        }
        return new(previous, parent, created, source.Export(), copy.Export(), now, authority);
    }
    public static CharterBook Commit(CharterBook current, NpcSubcharterPlan plan, long now, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(current); RuntimeCompat.NotNull(plan);
        if (plan.Tick != now || current.Export() != plan.Before) throw new ArgumentException("Subcharter review expired or source book changed.");
        // Rebuild the proposed result; do not trust a caller-provided After book.
        var proposed = CharterBook.Restore(plan.After);
        var validated = Review(current, plan.Previous, plan.Parent, plan.Created, plan.Authority, now, observations);
        if (validated.After != plan.After) throw new ArgumentException("Subcharter proposal was altered.");
        return proposed;
    }
}
