using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum AllegianceTransitionPhase { Prepared, AllegianceApplied, Completed, Cancelled }
public sealed record AllegianceTransition(string Id, string Before, string After, string TransferBaseline,
    IReadOnlyList<string> Followers, long PreparedTick, AllegianceTransitionPhase Phase = AllegianceTransitionPhase.Prepared,
    long? AllegianceTick = null, long? FinishedTick = null);
public sealed record AllegianceTransitionSave(int Version, AllegianceTransition Current);

public static class AllegianceChangeSource
{
    public static void Validate(CharterBook book, PlayerAllegianceState before, AllegianceCommand command, TributeLedger payments)
    {
        ArgumentNullException.ThrowIfNull(book); ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(payments);
        if (payments.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared)) throw new ArgumentException("Resolve reserved goods first.");
        if (before.Mode == PlayerAllegianceMode.Vassal)
        {
            var old = book.Charter(before.Contract);
            if (old.Phase != RecordedCharterPhase.Released || !book.Entity(old.Terms.Subject).Player ||
                book.Entity(old.Terms.Liege).NativeOwner != before.Authority)
                throw new ArgumentException("The actual recorded prior player contract must be mutually released.");
        }
        if (command.Action == AllegianceAction.JoinUnion)
        {
            if (book.Charters.Any(c => c.Phase == RecordedCharterPhase.Active && book.Entity(c.Terms.Subject).Player))
                throw new ArgumentException("No active player feudal contract may remain for union admission.");
        }
        else if (command.Action == AllegianceAction.BindLiege)
        {
            var next = book.Charter(command.Contract);
            if (next.Phase != RecordedCharterPhase.Active || !book.Entity(next.Terms.Subject).Player || book.RecordedPoliticalOwner(next.Terms.Subject) != command.Authority)
                throw new ArgumentException("The signed active player contract must match the proposed authority.");
        }
        else throw new ArgumentException("Not a political membership change.");
        // Unpaid bills and historical receipts are deliberately retained; mutual release is not debt forgiveness.
    }
}

/// <summary>One durable coordinated change. Political command history and per-city journals retain older outcomes.</summary>
public sealed class AllegianceTransitionLedger
{
    public AllegianceTransition Current { get; private set; }
    public bool Pending => Current?.Phase is AllegianceTransitionPhase.Prepared or AllegianceTransitionPhase.AllegianceApplied;
    private static void Require(bool value, string reason) { if (!value) throw new ArgumentException(reason); }
    private static bool Key(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 120 && !id.Any(char.IsControl);
    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private void Time(long now) => Require(Current != null && now >= (Current.FinishedTick ?? Current.AllegianceTick ?? Current.PreparedTick) && now <= int.MaxValue,
        "Invalid transition clock.");
    public void Prepare(string id, PlayerAllegiance actual, AllegianceCommand command, FollowerTransferPlan followers, TerritoryTransferLedger transfers)
    {
        ArgumentNullException.ThrowIfNull(actual); ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(followers); ArgumentNullException.ThrowIfNull(transfers);
        Require(!Pending && Key(id) && (Current == null || Current.Id != id) && command.Action is AllegianceAction.JoinUnion or AllegianceAction.BindLiege &&
            command.Authority == followers.Target && command.Tick == followers.ReviewedTick && followers.Entries != null && followers.Moves != null &&
            followers.Entries.All(e => e != null && Key(e.Charter)) && followers.Entries.Count <= CharterBook.MaxEntities &&
            followers.Entries.Select(e => e.Charter).Distinct(StringComparer.Ordinal).Count() == followers.Entries.Count &&
            followers.Moves.Distinct(StringComparer.Ordinal).Count() == followers.Moves.Count &&
            followers.Moves.All(id => followers.Entries.Any(e => e.Charter == id)) &&
            !transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) && command.Tick >= transfers.LastTick &&
            (Current == null || command.Tick >= (Current.FinishedTick ?? Current.PreparedTick)), "Invalid or competing transition.");
        string before = actual.Export(); var after = PlayerAllegiance.Restore(before); after.Apply(command);
        Current = new(id, before, after.Export(), Digest(transfers.Export()),
            Array.AsReadOnly(followers.Entries.Select(e => e.Charter).OrderBy(id => id, StringComparer.Ordinal).ToArray()), command.Tick);
    }
    public bool IsBefore(PlayerAllegiance actual) => Current != null && actual?.Export() == Current.Before;
    public bool IsAfter(PlayerAllegiance actual)
    {
        if (Current == null || actual == null) return false;
        var wanted = JsonSerializer.Deserialize<PlayerAllegianceSave>(Current.After);
        var live = JsonSerializer.Deserialize<PlayerAllegianceSave>(actual.Export());
        var state = PlayerAllegiance.Restore(Current.After).State;
        return live.NativePlayer == wanted.NativePlayer && live.Commands.Length >= wanted.Commands.Length &&
            live.Commands.Take(wanted.Commands.Length).SequenceEqual(wanted.Commands) &&
            live.Commands.Skip(wanted.Commands.Length).All(c => c.Action is AllegianceAction.Suspend or AllegianceAction.Reaffirm) && actual.State.Mode == state.Mode &&
            actual.State.Authority == state.Authority && actual.State.Contract == state.Contract;
    }
    public void ConfirmAllegiance(PlayerAllegiance actual, long now)
    {
        Time(now);
        Require(Current.Phase == AllegianceTransitionPhase.Prepared && IsAfter(actual) && actual.LastTick <= now, "Actual recorded allegiance is not applied.");
        Current = Current with { Phase = AllegianceTransitionPhase.AllegianceApplied, AllegianceTick = now };
    }
    public void Complete(PlayerAllegiance actual, FollowerTransferPlan current, long now)
    {
        ArgumentNullException.ThrowIfNull(current); Time(now);
        Require(Current.Phase == AllegianceTransitionPhase.AllegianceApplied && IsAfter(actual) && !actual.State.Suspended && actual.LastTick <= now &&
            current.Target == actual.State.Authority && current.ReviewedTick == now && current.Moves != null && current.Entries != null &&
            current.Entries.All(e => e != null && Key(e.Charter)) && current.Moves.Count == 0 &&
            current.Entries.Select(e => e.Charter).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(Current.Followers),
            "A complete current follower set at the intended authority is required.");
        Current = Current with { Phase = AllegianceTransitionPhase.Completed, FinishedTick = now };
    }
    public void CancelUnchanged(PlayerAllegiance actual, TerritoryTransferLedger transfers, long now)
    {
        ArgumentNullException.ThrowIfNull(transfers); Time(now);
        Require(Current.Phase == AllegianceTransitionPhase.Prepared && IsBefore(actual) && actual.LastTick <= now &&
            Digest(transfers.Export()) == Current.TransferBaseline, "Only completely unchanged preparation can be cancelled.");
        Current = Current with { Phase = AllegianceTransitionPhase.Cancelled, FinishedTick = now };
    }
    public string Export() => JsonSerializer.Serialize(new AllegianceTransitionSave(1, Current));
    public static AllegianceTransitionLedger Restore(string json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 8000000, "Invalid transition snapshot.");
        var save = JsonSerializer.Deserialize<AllegianceTransitionSave>(json);
        Require(save != null && save.Version == 1, "Unsupported transition snapshot.");
        var result = new AllegianceTransitionLedger(); var c = save.Current;
        if (c == null) return result;
        Require(Key(c.Id) && Enum.IsDefined(c.Phase) && c.TransferBaseline?.Length == 64 && c.TransferBaseline.All(Uri.IsHexDigit) &&
            c.Followers != null && c.Followers.Count <= CharterBook.MaxEntities && c.Followers.All(Key) &&
            c.Followers.Distinct(StringComparer.Ordinal).Count() == c.Followers.Count && c.PreparedTick is >= 0 and <= int.MaxValue,
            "Invalid transition intent.");
        var before = PlayerAllegiance.Restore(c.Before); var after = PlayerAllegiance.Restore(c.After);
        var old = JsonSerializer.Deserialize<PlayerAllegianceSave>(c.Before); var next = JsonSerializer.Deserialize<PlayerAllegianceSave>(c.After);
        Require(old.NativePlayer == next.NativePlayer && next.Commands.Length == old.Commands.Length + 1 &&
            next.Commands.Take(old.Commands.Length).SequenceEqual(old.Commands), "Transition must append one command to the same player.");
        var command = next.Commands.Last();
        Require(command.Action is AllegianceAction.JoinUnion or AllegianceAction.BindLiege && command.Tick == c.PreparedTick,
            "Unsupported transition command.");
        before.Apply(command); Require(before.Export() == after.Export(), "Invalid expected transition result.");
        bool applied = c.Phase is AllegianceTransitionPhase.AllegianceApplied or AllegianceTransitionPhase.Completed;
        bool finished = c.Phase is AllegianceTransitionPhase.Completed or AllegianceTransitionPhase.Cancelled;
        Require(applied == c.AllegianceTick.HasValue && finished == c.FinishedTick.HasValue &&
            (!c.AllegianceTick.HasValue || c.AllegianceTick >= c.PreparedTick && c.AllegianceTick <= int.MaxValue) &&
            (!c.FinishedTick.HasValue || c.FinishedTick >= (c.AllegianceTick ?? c.PreparedTick) && c.FinishedTick <= int.MaxValue),
            "Invalid transition phase/timestamps.");
        result.Current = c with { Followers = Array.AsReadOnly(c.Followers.OrderBy(id => id, StringComparer.Ordinal).ToArray()) }; return result;
    }
}
