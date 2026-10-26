using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record UnionPeaceConsent(string Union, string Player, string Representative, long Tick, bool Founding,
    int Goodwill, UnionAdmissionVote[] Votes, string[] IncomingCharters)
{
    public int TotalWeight => Votes.Sum(v => v.Weight);
    public int ApprovedWeight => Votes.Where(v => v.Approve).Sum(v => v.Weight);
    public bool Accepted => ApprovedWeight * 2 > TotalWeight;
}

/// <summary>Membership-only NPC base-vote policy, separate from consent to assume wars.</summary>
public static class UnionPeaceAdmission
{
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    public static UnionPeaceConsent Review(string union, string player, string representative, long tick, int goodwill, IEnumerable<string> seats, IEnumerable<string> incoming)
    {
        RuntimeCompat.NotNull(seats); RuntimeCompat.NotNull(incoming); var actual = seats.OrderBy(s => s, StringComparer.Ordinal).ToArray(); bool founding = actual.Length == 0;
        if (founding) actual = ["founding/" + union];
        var reason = goodwill < (founding ? -50 : 50) ? UnionAdmissionVoteReason.Relationship : UnionAdmissionVoteReason.Accepted;
        var result = new UnionPeaceConsent(union, player, representative, tick, founding, goodwill,
            actual.Select(s => new UnionAdmissionVote(s, 1, reason == UnionAdmissionVoteReason.Accepted, reason)).ToArray(), incoming.OrderBy(s => s, StringComparer.Ordinal).ToArray());
        Validate(result); return result;
    }
    public static void Validate(UnionPeaceConsent c)
    {
        if (c == null || !Key(c.Union) || !Key(c.Player) || c.Union == c.Player || !Key(c.Representative) || c.Tick is < 0 or > int.MaxValue ||
            c.Goodwill is < -100 or > 100 || c.Votes == null || c.Votes.Length is < 1 or > 1000 || c.Votes.Any(v => v == null || !Key(v.Seat)) ||
            c.IncomingCharters == null || c.IncomingCharters.Length > CharterBook.MaxEntities || c.IncomingCharters.Any(s => !Key(s)) ||
            c.IncomingCharters.Distinct(StringComparer.Ordinal).Count() != c.IncomingCharters.Length ||
            c.Votes.Select(v => v.Seat).Distinct(StringComparer.Ordinal).Count() != c.Votes.Length ||
            c.Founding != (c.Votes.Length == 1 && c.Votes[0].Seat == "founding/" + c.Union) ||
            c.Votes.Any(v => v.Weight != 1 || v.Approve != (c.Goodwill >= (c.Founding ? -50 : 50)) ||
                v.Reason != (v.Approve ? UnionAdmissionVoteReason.Accepted : UnionAdmissionVoteReason.Relationship)))
            throw new ArgumentException("Invalid actual membership-only council vote.");
    }
    public static bool SameReview(UnionPeaceConsent original, UnionPeaceConsent fresh)
    {
        Validate(original); Validate(fresh);
        return JsonSerializer.Serialize(original) == JsonSerializer.Serialize(fresh with { Tick = original.Tick, Goodwill = original.Goodwill });
    }
    public static void RequireIncomingReceipt(AllegianceTransition intent, CharterBook book, TerritoryTransferLedger transfers, string charter, string actualOwner)
    {
        RuntimeCompat.NotNull(intent); RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(transfers); Validate(intent.Peace);
        var c = book.Charter(charter); var entity = book.Entity(c.Terms.Subject);
        var row = transfers.Transfers.LastOrDefault(t => t.Territory == entity.Id);
        if (intent.Phase != AllegianceTransitionPhase.AllegianceApplied || !intent.Peace.IncomingCharters.Contains(charter) || !intent.Followers.Contains(charter) ||
            c.Phase != RecordedCharterPhase.Active || !book.PlayerManaged(c) || entity.Player || actualOwner != intent.Peace.Union ||
            row == null || row.Charter != charter || row.Purpose != TerritoryTransferPurpose.ActiveCharter || row.Phase != TerritoryTransferPhase.Applied ||
            row.TargetOwner != actualOwner || row.PreparedTick < intent.PreparedTick ||
            !transfers.VerifiedOwner(entity.Id, entity.NativeOwner, actualOwner))
            throw new ArgumentException("Only an original incoming city with its actual current completed migration may remain outside the admission electorate.");
    }
}

public sealed partial class AllegianceTransitionLedger
{
    public void PreparePeace(string id, PlayerAllegiance actual, AllegianceCommand command, FollowerTransferPlan followers,
        TerritoryTransferLedger transfers, UnionPeaceConsent consent)
    {
        UnionPeaceAdmission.Validate(consent);
        if (!consent.Accepted || command.Action != AllegianceAction.JoinUnion || consent.Union != command.Authority ||
            consent.Player != actual.NativePlayer || consent.Tick != command.Tick || !consent.IncomingCharters.SequenceEqual(followers.Moves.OrderBy(s => s, StringComparer.Ordinal)))
            throw new ArgumentException("Current accepted union membership vote and exact incoming cities required.");
        // Validate on a copy so a failed plan cannot leave a partially replaced current intent.
        var next = Restore(Export()); next.Prepare(id, actual, command, followers, transfers);
        next.Current = next.Current with { Peace = JsonSerializer.Deserialize<UnionPeaceConsent>(JsonSerializer.Serialize(consent)) };
        if (next.Export().Length > 7000000) throw new ArgumentException("Reserve political journal room for later phase/loss receipts.");
        Current = Restore(next.Export()).Current;
    }
    public void ReaffirmPeace(UnionPeaceConsent fresh, long now)
    {
        Time(now); UnionPeaceAdmission.Validate(fresh);
        if (Current.Phase != AllegianceTransitionPhase.AllegianceApplied || Current.Peace == null || !fresh.Accepted || fresh.Union != Current.Peace.Union || fresh.Player != Current.Peace.Player || fresh.Tick != now ||
            !fresh.IncomingCharters.SequenceEqual(Current.Peace.IncomingCharters)) throw new ArgumentException("Re-review current union electorate before resuming admission.");
        if (Current.PeaceReaffirmations?.LastOrDefault() is { } last && JsonSerializer.Serialize(last) == JsonSerializer.Serialize(fresh)) return;
        if ((Current.PeaceReaffirmations?.Length ?? 0) >= 64) throw new ArgumentException("Membership re-review history capacity.");
        var next = new AllegianceTransitionLedger { Current = Current with { PeaceReaffirmations = (Current.PeaceReaffirmations ?? []).Append(
            JsonSerializer.Deserialize<UnionPeaceConsent>(JsonSerializer.Serialize(fresh))).ToArray() } };
        if (next.Export().Length > 7000000) throw new ArgumentException("Retain political journal room before recording current council consent.");
        Current = Restore(next.Export()).Current;
    }
    private static void ValidatePeace(AllegianceTransition c, AllegianceCommand command, string nativePlayer)
    {
        if (c.Peace == null)
        { if (c.PeaceReaffirmations != null) throw new ArgumentException("Council reaffirmations need original membership consent."); return; }
        UnionPeaceAdmission.Validate(c.Peace);
        if (c.Wars != null || !c.Peace.Accepted || command.Action != AllegianceAction.JoinUnion || c.Peace.Union != command.Authority ||
            c.Peace.Player != nativePlayer || c.Peace.Tick != c.PreparedTick || !c.Peace.IncomingCharters.All(c.Followers.Contains) || (c.PeaceReaffirmations?.Length ?? 0) > 64)
            throw new ArgumentException("Membership consent cannot authorize wars or another political command.");
        long last = c.PreparedTick;
        if (c.PeaceReaffirmations != null && (c.PeaceReaffirmations.Length == 0 || c.Phase is not (AllegianceTransitionPhase.AllegianceApplied or AllegianceTransitionPhase.Completed)) ||
            c.Phase == AllegianceTransitionPhase.Completed && c.PeaceReaffirmations == null)
            throw new ArgumentException("Completed membership needs its actually applied council re-review.");
        foreach (var r in c.PeaceReaffirmations ?? [])
        {
            UnionPeaceAdmission.Validate(r);
            if (!r.Accepted || r.Union != c.Peace.Union || r.Player != nativePlayer || !r.IncomingCharters.SequenceEqual(c.Peace.IncomingCharters) || r.Tick < (c.AllegianceTick ?? c.PreparedTick) || r.Tick < last || r.Tick > (c.FinishedTick ?? int.MaxValue))
                throw new ArgumentException("Invalid membership council reaffirmation history.");
            last = r.Tick;
        }
    }
}
