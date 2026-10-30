using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record PlayerCharterRenewal(string Amendment, string Before, string After, string TransitionBefore, string TransitionAfter, FollowerCandidate[] Followers);
public sealed record PlayerCharterRenewalSave(int Version, PlayerCharterRenewal[] Records);

/// <summary>Atomic same-authority contract pointer renewal. No native faction/diplomacy/asset migration is authorized.</summary>
public sealed class PlayerCharterRenewals
{
    private readonly Dictionary<string, PlayerCharterRenewal> records = new(StringComparer.Ordinal);
    public IReadOnlyCollection<PlayerCharterRenewal> Records => records.Values.Select(r => r with { Followers = r.Followers.ToArray() }).ToArray();
    private static bool Prefix(PlayerAllegiance expected, PlayerAllegiance actual)
    {
        var a = JsonSerializer.Deserialize<PlayerAllegianceSave>(expected.Export()); var b = JsonSerializer.Deserialize<PlayerAllegianceSave>(actual.Export());
        return a.NativePlayer == b.NativePlayer && a.Commands.Length <= b.Commands.Length && a.Commands.SequenceEqual(b.Commands.Take(a.Commands.Length));
    }
    public static PlayerCharterRenewal Stage(CharterAmendmentPlan amendment, PlayerAllegiance actual, AllegianceTransitionLedger journal, IEnumerable<FollowerCandidate> candidates)
    {
        RuntimeCompat.NotNull(amendment); RuntimeCompat.NotNull(actual); RuntimeCompat.NotNull(journal); RuntimeCompat.NotNull(candidates);
        var w = amendment.Witness; var before = CharterBook.Restore(w.Book); var after = CharterBook.Restore(amendment.After); var old = before.Charter(w.Previous);
        var payments = w.Payments == null ? throw new ArgumentException("Current player tribute evidence required.") : TributeLedger.Restore(w.Payments);
        var transfers = TerritoryTransferLedger.Restore(w.Transfers);
        var replay = CharterAmendments.Replay(w);
        if (replay != amendment || !before.Entity(old.Terms.Subject).Player || actual.State.Mode != PlayerAllegianceMode.Vassal || actual.State.Suspended ||
            actual.State.Contract != old.Id || actual.State.Authority != w.Decision.Realm || before.Entity(old.Terms.Subject).NativeOwner != actual.NativePlayer ||
            before.RecordedPoliticalOwner(old.Terms.Subject) != actual.State.Authority || journal.Pending)
            throw new ArgumentException("Renew only the current unchanged active vassal contract at the same actual authority.");
        if (journal.Current != null && !Prefix(PlayerAllegiance.Restore(journal.Current.Phase == AllegianceTransitionPhase.Cancelled ? journal.Current.Before : journal.Current.After), actual))
            throw new ArgumentException("Prior political command lineage must remain intact.");
        var entries = candidates.ToArray();
        var priorFollowers = FollowerTransfers.Review(before, transfers, entries, actual.State.Authority, w.Tick);
        var nextFollowers = FollowerTransfers.Review(after, transfers, entries, actual.State.Authority, w.Tick);
        if (priorFollowers.Moves.Count != 0 || nextFollowers.Moves.Count != 0)
            throw new ArgumentException("Complete existing follower migration separately; renewal cannot move any city.");
        var command = new AllegianceCommand(AllegianceAction.BindLiege, w.Tick, actual.State.Authority, w.Next, Consent: true, OldContractReleased: true);
        AllegianceChangeSource.Validate(after, actual.State, command, payments);
        var next = PlayerAllegiance.Restore(actual.Export()); next.Apply(command);
        var staged = AllegianceTransitionLedger.Restore(journal.Export()); staged.Prepare(w.Next, actual, command, nextFollowers, transfers);
        staged.ConfirmAllegiance(next, w.Tick); staged.Complete(next, nextFollowers, w.Tick);
        return new(amendment.Proof, actual.Export(), next.Export(), journal.Export(), staged.Export(),
            JsonSerializer.Deserialize<FollowerCandidate[]>(JsonSerializer.Serialize(entries)));
    }
    public PlayerCharterRenewals Append(PlayerCharterRenewal receipt)
    {
        if (receipt == null || !CityStockProof.Valid(receipt.Amendment) || records.Count >= CharterAmendments.Capacity || records.ContainsKey(receipt.Amendment))
            throw new ArgumentException("Invalid, duplicate or full renewal history.");
        var copy = Restore(Export()); copy.records.Add(receipt.Amendment, receipt with { Followers = receipt.Followers.ToArray() });
        if (copy.Export().Length > 32000000) throw new ArgumentException("Renewal history capacity.");
        return copy;
    }
    public void ValidatePair(CharterAmendments amendments, CharterBook book)
    {
        amendments.ValidateBook(book);
        foreach (var r in records.Values)
        {
            var a = amendments.Records.SingleOrDefault(a => a.Proof == r.Amendment) ?? throw new ArgumentException("Missing original player renewal consent.");
            var replay = Stage(a, PlayerAllegiance.Restore(r.Before), AllegianceTransitionLedger.Restore(r.TransitionBefore), r.Followers);
            if (JsonSerializer.Serialize(replay) != JsonSerializer.Serialize(r)) throw new ArgumentException("Renewal pointer or political journal differs from original consent.");
        }
        foreach (var a in amendments.Records.Where(a => CharterBook.Restore(a.Witness.Book).Entity(CharterBook.Restore(a.Witness.Book).Charter(a.Witness.Previous).Terms.Subject).Player))
            if (!records.ContainsKey(a.Proof)) throw new ArgumentException("Player renewal requires its atomic political history.");
    }
    public void ValidateCurrent(PlayerAllegiance actual, AllegianceTransitionLedger current)
    {
        foreach (var r in records.Values)
        {
            var after = PlayerAllegiance.Restore(r.After); var saved = AllegianceTransitionLedger.Restore(r.TransitionAfter).Current;
            if (!Prefix(after, actual) || current.Current == null ||
                (current.Current.Id == saved.Id ? current.Export() != r.TransitionAfter :
                    current.Current.PreparedTick < saved.FinishedTick || !Prefix(after, PlayerAllegiance.Restore(current.Current.Before))))
                throw new ArgumentException("Current allegiance/journal lost the exact committed renewal command lineage.");
        }
    }
    public string Export() => JsonSerializer.Serialize(new PlayerCharterRenewalSave(1, records.Values.ToArray()));
    public static PlayerCharterRenewals Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32000000) throw new ArgumentException("Invalid player renewal archive size.");
        var saved = JsonSerializer.Deserialize<PlayerCharterRenewalSave>(json);
        if (saved == null || saved.Version != 1 || saved.Records == null || saved.Records.Length > CharterAmendments.Capacity) throw new ArgumentException("Invalid player renewal archive.");
        var copy = new PlayerCharterRenewals();
        foreach (var r in saved.Records)
        {
            if (r == null || !CityStockProof.Valid(r.Amendment) || r.Followers == null || r.Followers.Length > CharterBook.MaxEntities || copy.records.ContainsKey(r.Amendment))
                throw new ArgumentException("Invalid duplicate player renewal record.");
            PlayerAllegiance.Restore(r.Before); PlayerAllegiance.Restore(r.After);
            AllegianceTransitionLedger.Restore(r.TransitionBefore); var journal = AllegianceTransitionLedger.Restore(r.TransitionAfter);
            if (journal.Current?.Phase != AllegianceTransitionPhase.Completed || journal.Current.Before != r.Before || journal.Current.After != r.After || journal.Current.Wars != null)
                throw new ArgumentException("Renewal must retain a completed nonphysical same-authority journal.");
            copy.records.Add(r.Amendment, r);
        }
        return copy;
    }
}
