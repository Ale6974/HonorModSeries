using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honor.Contracts.Rules;

public sealed record FeudalAdmissionConsent(string Player, string Charter, long Tick, NpcRealmDecisionEvidence Decision, string[] Moves);

public static class FeudalAdmissions
{
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    public static FeudalAdmissionConsent Review(CharterBook book, string player, string charter, long tick,
        NpcRealmDecisionEvidence decision, IEnumerable<string> moves)
    {
        RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(moves); NpcRealmDecision.Consent(decision);
        var c = book.Charter(charter);
        var result = new FeudalAdmissionConsent(player, charter, tick, decision, moves.OrderBy(s => s, StringComparer.Ordinal).ToArray()); Validate(result);
        if (tick < book.LastTick || c.Phase != RecordedCharterPhase.Active || c.NpcAuthority != null || c.PlayerReleaseTick.HasValue || c.NpcReleaseTick.HasValue ||
            !book.Entity(c.Terms.Subject).Player || book.Entity(c.Terms.Subject).NativeOwner != player || book.RecordedPoliticalOwner(c.Terms.Subject) != decision.Realm)
            throw new ArgumentException("Actual active player charter and current ordinary realm admission required.");
        return result;
    }
    public static void Validate(FeudalAdmissionConsent c)
    {
        if (c == null || !Key(c.Player) || !Key(c.Charter) || c.Tick is < 0 or > int.MaxValue || c.Moves == null || c.Moves.Length > CharterBook.MaxEntities ||
            c.Moves.Any(s => !Key(s)) || c.Moves.Distinct(StringComparer.Ordinal).Count() != c.Moves.Length)
            throw new ArgumentException("Invalid ordinary feudal admission.");
        NpcRealmDecision.Consent(c.Decision);
    }
    public static bool SameReview(FeudalAdmissionConsent a, FeudalAdmissionConsent b)
    { Validate(a); Validate(b); return JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b with { Tick = a.Tick }); }
    internal static void ValidateIntent(AllegianceTransition c, AllegianceCommand command, string nativePlayer)
    {
        if (c.Feudal == null)
        { if (c.FeudalReaffirmations != null) throw new ArgumentException("Feudal reaffirmation needs original ordinary consent."); return; }
        Validate(c.Feudal);
        if (command.Action != AllegianceAction.BindLiege || c.FollowerReview == null || c.Peace != null || c.Feudal.Charter != command.Contract || c.Feudal.Player != nativePlayer ||
            c.Feudal.Decision.Realm != command.Authority || c.Feudal.Tick != c.PreparedTick || !c.Feudal.Moves.All(c.Followers.Contains) ||
            c.Wars != null && (c.Wars.AuthorityKind != AdmissionAuthorityKind.FeudalLeader || c.Wars.Representative != c.Feudal.Decision.Representative) || (c.FeudalReaffirmations?.Length ?? 0) > 64)
            throw new ArgumentException("Ordinary admission must bind the exact actual player, charter and authority.");
        long last = c.AllegianceTick ?? c.PreparedTick;
        if (c.FeudalReaffirmations != null && (c.FeudalReaffirmations.Length == 0 || c.Phase is not (AllegianceTransitionPhase.AllegianceApplied or AllegianceTransitionPhase.Completed)) ||
            c.Phase == AllegianceTransitionPhase.Completed && c.FeudalReaffirmations == null)
            throw new ArgumentException("Completed ordinary admission needs its current applied re-review.");
        foreach (var r in c.FeudalReaffirmations ?? [])
        {
            Validate(r);
            if (r.Player != nativePlayer || r.Charter != c.Feudal.Charter || r.Decision.Realm != c.Feudal.Decision.Realm ||
                !r.Moves.SequenceEqual(c.Feudal.Moves) || r.Tick < last || r.Tick > (c.FinishedTick ?? int.MaxValue))
                throw new ArgumentException("Changed feudal recovery scope.");
            last = r.Tick;
        }
    }
}

public sealed partial class AllegianceTransitionLedger
{
    public void PrepareReviewedLiegeAdmission(string id, PlayerAllegiance actual, AllegianceCommand command, FollowerTransferPlan followers,
        TerritoryTransferLedger transfers, FeudalAdmissionConsent consent, UnionWarConsent wars = null)
    {
        FeudalAdmissions.Validate(consent);
        if (command?.Action != AllegianceAction.BindLiege || consent.Charter != command.Contract || consent.Tick != command.Tick ||
            consent.Player != actual.NativePlayer || consent.Decision.Realm != command.Authority ||
            !consent.Moves.SequenceEqual(followers.Moves.OrderBy(s => s, StringComparer.Ordinal)))
            throw new ArgumentException("Review actual ordinary liege admission and all original city moves.");
        var next = Restore(Export()); next.Prepare(id, actual, command, followers, transfers, wars);
        next.Current = next.Current with { Feudal = JsonSerializer.Deserialize<FeudalAdmissionConsent>(JsonSerializer.Serialize(consent)) };
        next.Current = next.Current with { FollowerReview = AdmissionFollowerReviews.Capture(next.Current, followers) };
        if (next.Export().Length > 7000000) throw new ArgumentException("Retain ordinary admission history/result space.");
        Current = Restore(next.Export()).Current;
    }
    public void ReaffirmFeudal(FeudalAdmissionConsent fresh, long now)
    {
        Time(now); FeudalAdmissions.Validate(fresh);
        if (Current.Phase != AllegianceTransitionPhase.AllegianceApplied || Current.Feudal == null || fresh.Tick != now || fresh.Player != Current.Feudal.Player ||
            fresh.Charter != Current.Feudal.Charter || fresh.Decision.Realm != Current.Feudal.Decision.Realm || !fresh.Moves.SequenceEqual(Current.Feudal.Moves))
            throw new ArgumentException("Explicit current representative consent for the same original admission required.");
        if (Current.FeudalReaffirmations?.LastOrDefault() is { } last && JsonSerializer.Serialize(last) == JsonSerializer.Serialize(fresh)) return;
        if ((Current.FeudalReaffirmations?.Length ?? 0) >= 64) throw new ArgumentException("Feudal consent history capacity.");
        var next = new AllegianceTransitionLedger { Current = Current with { FeudalReaffirmations = (Current.FeudalReaffirmations ?? []).Append(
            JsonSerializer.Deserialize<FeudalAdmissionConsent>(JsonSerializer.Serialize(fresh))).ToArray() } };
        if (next.Export().Length > 7000000) throw new ArgumentException("Keep room for subsequent admission outcomes.");
        Current = Restore(next.Export()).Current;
    }
}
