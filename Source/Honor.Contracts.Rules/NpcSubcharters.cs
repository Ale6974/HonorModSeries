namespace Honor.Contracts.Rules;

public sealed record NpcSubcharterPlan(string Previous, string Parent, string Created, string Before, string After, long Tick, NpcCharterAuthority Authority)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NpcRealmDecisionEvidence Decision { get; init; }
}

public static class NpcSubcharters
{
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
