namespace Honor.Contracts.Rules;

public enum UnionAdmissionVoteReason { Accepted, Relationship, ProtectedAlly, MissingWarRoute, NoCurrentWar }
public sealed record UnionWarTarget(string Enemy, int Goodwill, SharedRelationKind Kind, int ReciprocalGoodwill,
    SharedRelationKind ReciprocalKind, bool ApplicantHostile, bool NativeRoute);
public sealed record UnionAdmissionVote(string Seat, int Weight, bool Approve, UnionAdmissionVoteReason Reason);
public enum AdmissionAuthorityKind { UnionCouncil, FeudalLeader }
public sealed record UnionWarConsent(string Union, string Player, string Representative, long Tick, bool Founding,
    UnionWarTarget[] Targets, UnionAdmissionVote[] Votes,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] AdmissionAuthorityKind AuthorityKind = AdmissionAuthorityKind.UnionCouncil)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string[] IncomingCharters { get; init; }
    public int TotalWeight => Votes.Sum(v => v.Weight);
    public int ApprovedWeight => Votes.Where(v => v.Approve).Sum(v => v.Weight);
    public bool Accepted => ApprovedWeight * 2 > TotalWeight;
}
public enum UnionWarStep { Prepared, Applying, Verified }
public sealed record UnionWarResult(string Enemy, UnionWarStep Step = UnionWarStep.Prepared, long? ObservedTick = null,
    int? Goodwill = null, int? ReciprocalGoodwill = null, bool FromCompletedCall = false);

/// <summary>Admission-specific base votes. Unknown contribution/size grants no invented influence bonus.</summary>
public static class UnionWarAdmission
{
    public static UnionWarConsent BindIncoming(UnionWarConsent consent, FollowerTransferPlan followers)
    {
        Validate(consent); RuntimeCompat.NotNull(followers);
        if (consent.AuthorityKind != AdmissionAuthorityKind.UnionCouncil || consent.IncomingCharters != null || followers.Target != consent.Union ||
            followers.ReviewedTick != consent.Tick || !consent.Accepted || followers.Moves == null)
            throw new ArgumentException("Bind the original actual incoming-city plan to accepted current union war consent.");
        var copy = System.Text.Json.JsonSerializer.Deserialize<UnionWarConsent>(System.Text.Json.JsonSerializer.Serialize(consent)) with {
            IncomingCharters = followers.Moves.OrderBy(s => s, StringComparer.Ordinal).ToArray() };
        Validate(copy); return copy;
    }
    public static void ValidateHistory(IEnumerable<string> history, AllegianceTransition current)
    {
        RuntimeCompat.NotNull(history); var rows = history.Take(129).ToArray();
        if (rows.Length > 128 || rows.Any(s => s == null || s.Length > 8000000) || rows.Sum(s => (long)s.Length) > 32000000 || rows.Length > 0 && current == null)
            throw new ArgumentException("Invalid war-admission history size.");
        var ids = new HashSet<string>(StringComparer.Ordinal); long last = 0;
        var currentHead = current == null ? null : System.Text.Json.JsonSerializer.Deserialize<PlayerAllegianceSave>(current.Before);
        foreach (string row in rows)
        {
            var prior = AllegianceTransitionLedger.Restore(row).Current;
            if (prior == null || prior.Wars == null && prior.Peace == null || prior.Phase is not (AllegianceTransitionPhase.Completed or AllegianceTransitionPhase.Cancelled) || !ids.Add(prior.Id) ||
                prior.PreparedTick < last || prior.FinishedTick > current.PreparedTick) throw new ArgumentException("Invalid historical war admission.");
            var head = System.Text.Json.JsonSerializer.Deserialize<PlayerAllegianceSave>(prior.Phase == AllegianceTransitionPhase.Cancelled ? prior.Before : prior.After);
            if (head.NativePlayer != currentHead.NativePlayer || head.Commands.Length > currentHead.Commands.Length ||
                !currentHead.Commands.Take(head.Commands.Length).SequenceEqual(head.Commands)) throw new ArgumentException("Unrelated historical command lineage.");
            last = prior.FinishedTick.Value;
        }
        if (current != null && ids.Contains(current.Id)) throw new ArgumentException("Duplicate current/historical intent.");
    }
    private static bool Key(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 120 && !id.Any(char.IsControl);
    // Historic Union/union field names denote the destination authority for both kinds; retain old serialized bytes.
    public static UnionWarConsent ReviewFeudal(string authority, string player, string representative, long now,
        IEnumerable<UnionWarTarget> targets, int goodwill)
    {
        var result = Review(authority, player, representative, now, new[] { "liege/" + authority }, targets, goodwill)
            with { AuthorityKind = AdmissionAuthorityKind.FeudalLeader };
        Validate(result); return result;
    }
    public static bool MatchesAction(UnionWarConsent consent, AllegianceAction action) => consent != null &&
        (consent.AuthorityKind == AdmissionAuthorityKind.UnionCouncil && action == AllegianceAction.JoinUnion ||
         consent.AuthorityKind == AdmissionAuthorityKind.FeudalLeader && action == AllegianceAction.BindLiege);
    public static UnionWarConsent Review(string union, string player, string representative, long now,
        IEnumerable<string> townSeats, IEnumerable<UnionWarTarget> targets, int goodwill)
    {
        RuntimeCompat.NotNull(townSeats); RuntimeCompat.NotNull(targets);
        var seats = townSeats.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        bool founding = seats.Length == 0;
        if (founding) seats = new[] { "founding/" + union };
        var wars = targets.OrderBy(t => t?.Enemy, StringComparer.Ordinal).ToArray();
        if (goodwill is < -100 or > 100) throw new ArgumentException("Invalid current relationship.");
        var reason = goodwill < (founding ? -50 : 50) ? UnionAdmissionVoteReason.Relationship :
            wars.Any(w => w?.Kind == SharedRelationKind.Ally || w?.ReciprocalKind == SharedRelationKind.Ally) ? UnionAdmissionVoteReason.ProtectedAlly :
            wars.Any(w => w != null && !w.ApplicantHostile && w.Kind != SharedRelationKind.Hostile) ? UnionAdmissionVoteReason.NoCurrentWar :
            wars.Any(w => w != null && !w.NativeRoute) ? UnionAdmissionVoteReason.MissingWarRoute : UnionAdmissionVoteReason.Accepted;
        var result = new UnionWarConsent(union, player, representative, now, founding, wars,
            seats.Select(s => new UnionAdmissionVote(s, 1, reason == UnionAdmissionVoteReason.Accepted, reason)).ToArray());
        Validate(result); return result;
    }
    public static void Validate(UnionWarConsent consent)
    {
        if (consent == null || !RuntimeCompat.IsDefined(consent.AuthorityKind) || !Key(consent.Union) || !Key(consent.Player) || consent.Player == consent.Union || !Key(consent.Representative) ||
            consent.Tick < 0 || consent.Tick > int.MaxValue || consent.Targets == null || consent.Targets.Length is < 1 or > 128 ||
            consent.Votes == null || consent.Votes.Length is < 1 or > 1000 ||
            consent.Targets.Any(t => t == null || !Key(t.Enemy) || t.Enemy == consent.Union || t.Enemy == consent.Player ||
                t.Goodwill is < -100 or > 100 || t.ReciprocalGoodwill is < -100 or > 100 || !RuntimeCompat.IsDefined(t.Kind) || !RuntimeCompat.IsDefined(t.ReciprocalKind) || t.Kind != t.ReciprocalKind) ||
            consent.Targets.Select(t => t.Enemy).Distinct(StringComparer.Ordinal).Count() != consent.Targets.Length ||
            consent.Votes.Any(v => v == null || !Key(v.Seat) || v.Weight != 1 || !RuntimeCompat.IsDefined(v.Reason) || v.Approve != (v.Reason == UnionAdmissionVoteReason.Accepted)) ||
            consent.Votes.Select(v => v.Seat).Distinct(StringComparer.Ordinal).Count() != consent.Votes.Length ||
            consent.IncomingCharters != null && (consent.AuthorityKind != AdmissionAuthorityKind.UnionCouncil || consent.IncomingCharters.Length > CharterBook.MaxEntities ||
                consent.IncomingCharters.Any(s => !Key(s)) || consent.IncomingCharters.Distinct(StringComparer.Ordinal).Count() != consent.IncomingCharters.Length) ||
            (consent.AuthorityKind == AdmissionAuthorityKind.FeudalLeader ? consent.Founding || consent.Votes.Length != 1 || consent.Votes[0].Seat != "liege/" + consent.Union :
             consent.Founding != (consent.Votes.Length == 1 && consent.Votes[0].Seat == "founding/" + consent.Union)))
            throw new ArgumentException("Invalid actual union admission consent scope.");
        if (consent.Accepted && consent.Targets.Any(t => t.Kind == SharedRelationKind.Ally || t.ReciprocalKind == SharedRelationKind.Ally ||
            !t.NativeRoute || !t.ApplicantHostile && t.Kind != SharedRelationKind.Hostile))
            throw new ArgumentException("Approval cannot waive protected relations or a missing war route.");
    }
    public static bool SameScope(UnionWarConsent original, UnionWarConsent fresh)
    {
        Validate(original); Validate(fresh);
        return SameWars(original, fresh) && original.Founding == fresh.Founding &&
            original.Votes.Select(v => v.Seat).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(fresh.Votes.Select(v => v.Seat).OrderBy(id => id, StringComparer.Ordinal));
    }
    public static bool SameWars(UnionWarConsent original, UnionWarConsent fresh)
    {
        Validate(original); Validate(fresh);
        return original.AuthorityKind == fresh.AuthorityKind && original.Union == fresh.Union && original.Player == fresh.Player &&
            (original.IncomingCharters == null ? fresh.IncomingCharters == null : fresh.IncomingCharters != null && original.IncomingCharters.SequenceEqual(fresh.IncomingCharters)) &&
            original.Targets.Select(t => t.Enemy).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(fresh.Targets.Select(t => t.Enemy).OrderBy(id => id, StringComparer.Ordinal));
    }
}
