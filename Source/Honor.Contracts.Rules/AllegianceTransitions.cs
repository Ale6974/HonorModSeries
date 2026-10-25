using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum AllegianceTransitionPhase { Prepared, AllegianceApplied, Completed, Cancelled }
public sealed record AllegianceTransition(string Id, string Before, string After, string TransferBaseline,
    IReadOnlyList<string> Followers, long PreparedTick, AllegianceTransitionPhase Phase = AllegianceTransitionPhase.Prepared,
    long? AllegianceTick = null, long? FinishedTick = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] UnionWarConsent Wars = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] UnionWarResult[] WarResults = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] UnionWarConsent[] WarReaffirmations = null);
public sealed record AllegianceTransitionSave(int Version, AllegianceTransition Current);

public static class AllegianceChangeSource
{
    public static string ReleasedMigrationSource(CharterBook book, PlayerAllegianceState before, string target, long now)
    {
        RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(before);
        if (before.Mode != PlayerAllegianceMode.Vassal || before.Authority == target || string.IsNullOrWhiteSpace(target) || target.Length > 120 || target.Any(char.IsControl) ||
            book.Entities.Any(e => e.Player && e.NativeOwner == target) || now < book.LastTick || now > int.MaxValue)
            return null;
        var old = book.Charter(before.Contract);
        return old.Phase == RecordedCharterPhase.Released && old.NpcAuthority == null && book.Entity(old.Terms.Subject).Player &&
            book.Entity(old.Terms.Liege).NativeOwner == before.Authority && book.RecordedPoliticalOwner(old.Terms.Liege) == before.Authority &&
            old.PlayerReleaseTick.HasValue && old.NpcReleaseTick.HasValue &&
            Math.Max(old.PlayerReleaseTick.Value, old.NpcReleaseTick.Value) <= now ? before.Authority : null;
    }
    public static void Validate(CharterBook book, PlayerAllegianceState before, AllegianceCommand command, TributeLedger payments)
    {
        RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(before);
        RuntimeCompat.NotNull(command); RuntimeCompat.NotNull(payments);
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
    public bool WarsReady => Current?.Wars == null || Current.WarResults.All(r => r.Step == UnionWarStep.Verified);
    private static void Require(bool value, string reason) { if (!value) throw new ArgumentException(reason); }
    private static bool Key(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 120 && !id.Any(char.IsControl);
    private static string Digest(string value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(value)));
    private void Time(long now) => Require(Current != null && now >= (Current.FinishedTick ?? Current.AllegianceTick ?? Current.PreparedTick) && now <= int.MaxValue &&
        (Current.WarResults == null || Current.WarResults.All(r => !r.ObservedTick.HasValue || r.ObservedTick <= now)) &&
        (Current.WarReaffirmations == null || Current.WarReaffirmations.All(r => r.Tick <= now)),
        "Invalid transition clock.");
    public void Prepare(string id, PlayerAllegiance actual, AllegianceCommand command, FollowerTransferPlan followers, TerritoryTransferLedger transfers, UnionWarConsent wars = null)
    {
        RuntimeCompat.NotNull(actual); RuntimeCompat.NotNull(command);
        RuntimeCompat.NotNull(followers); RuntimeCompat.NotNull(transfers);
        Require(!Pending && Key(id) && (Current == null || Current.Id != id) && command.Action is AllegianceAction.JoinUnion or AllegianceAction.BindLiege &&
            command.Authority == followers.Target && command.Tick == followers.ReviewedTick && followers.Entries != null && followers.Moves != null &&
            followers.Entries.All(e => e != null && Key(e.Charter)) && followers.Entries.Count <= CharterBook.MaxEntities &&
            followers.Entries.Select(e => e.Charter).Distinct(StringComparer.Ordinal).Count() == followers.Entries.Count &&
            followers.Moves.Distinct(StringComparer.Ordinal).Count() == followers.Moves.Count &&
            followers.Moves.All(id => followers.Entries.Any(e => e.Charter == id)) &&
            !transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) && command.Tick >= transfers.LastTick &&
            (Current == null || command.Tick >= (Current.FinishedTick ?? Current.PreparedTick)), "Invalid or competing transition.");
        string before = actual.Export(); var after = PlayerAllegiance.Restore(before); after.Apply(command);
        if (wars != null)
        {
            UnionWarAdmission.Validate(wars);
            Require(wars.Accepted && wars.Union == command.Authority && wars.Player == actual.NativePlayer && wars.Tick == command.Tick &&
                command.Action == AllegianceAction.JoinUnion, "Only accepted current union admission may assume these wars.");
        }
        Current = new(id, before, after.Export(), Digest(transfers.Export()),
            Array.AsReadOnly(followers.Entries.Select(e => e.Charter).OrderBy(id => id, StringComparer.Ordinal).ToArray()), command.Tick,
            Wars: wars == null ? null : JsonSerializer.Deserialize<UnionWarConsent>(JsonSerializer.Serialize(wars)),
            WarResults: wars?.Targets.Select(t => new UnionWarResult(t.Enemy)).ToArray());
    }
    public void ReaffirmWars(UnionWarConsent fresh, long now)
    {
        Time(now); Require(Pending && Current.Wars != null, "No war admission to reaffirm.");
        Require(UnionWarAdmission.SameWars(Current.Wars, fresh) && fresh.Accepted && fresh.Tick == now &&
            (Current.WarReaffirmations?.Length ?? 0) < 64, "Fresh current council consent and identical scope required.");
        if (JsonSerializer.Serialize((Current.WarReaffirmations ?? []).LastOrDefault() ?? Current.Wars) == JsonSerializer.Serialize(fresh)) return;
        var retained = (Current.WarReaffirmations ?? []).Append(JsonSerializer.Deserialize<UnionWarConsent>(JsonSerializer.Serialize(fresh))).ToArray();
        Current = Current with { WarReaffirmations = retained };
    }
    public void BeginWar(string enemy, long now)
    {
        Time(now); Require(Current.Phase == AllegianceTransitionPhase.AllegianceApplied && Current.Wars != null, "Apply allegiance before native war writes.");
        var r = Current.WarResults.SingleOrDefault(r => r.Enemy == enemy);
        Require(r != null && r.Step != UnionWarStep.Verified, "Unreviewed or already verified enemy.");
        Current = Current with { WarResults = Current.WarResults.Select(x => x.Enemy == enemy ? x with { Step = UnionWarStep.Applying } : x).ToArray() };
    }
    public void ObserveWar(string enemy, bool reciprocalHostile, int goodwill, int reciprocal, bool completedCall, long now)
    {
        Time(now); var r = Current.WarResults?.SingleOrDefault(r => r.Enemy == enemy);
        Require(Current.Phase == AllegianceTransitionPhase.AllegianceApplied && r != null && r.Step != UnionWarStep.Verified && reciprocalHostile &&
            goodwill is >= -100 and <= 100 && reciprocal is >= -100 and <= 100 && (!completedCall || r.Step == UnionWarStep.Applying),
            "Verify actual reciprocal hostility; a failed call is not nonmutation proof.");
        Current = Current with { WarResults = Current.WarResults.Select(x => x.Enemy == enemy ?
            x with { Step = UnionWarStep.Verified, ObservedTick = now, Goodwill = goodwill, ReciprocalGoodwill = reciprocal, FromCompletedCall = completedCall } : x).ToArray() };
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
        RuntimeCompat.NotNull(current); Time(now);
        Require(Current.Phase == AllegianceTransitionPhase.AllegianceApplied && IsAfter(actual) && WarsReady && !actual.State.Suspended && actual.LastTick <= now &&
            current.Target == actual.State.Authority && current.ReviewedTick == now && current.Moves != null && current.Entries != null &&
            current.Entries.All(e => e != null && Key(e.Charter)) && current.Moves.Count == 0 &&
            current.Entries.Select(e => e.Charter).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(Current.Followers),
            "A complete current follower set at the intended authority is required.");
        Current = Current with { Phase = AllegianceTransitionPhase.Completed, FinishedTick = now };
    }
    public void CancelUnchanged(PlayerAllegiance actual, TerritoryTransferLedger transfers, long now)
    {
        RuntimeCompat.NotNull(transfers); Time(now);
        Require(Current.Phase == AllegianceTransitionPhase.Prepared && IsBefore(actual) && actual.LastTick <= now &&
            (Current.WarResults == null || Current.WarResults.All(r => r.Step == UnionWarStep.Prepared)) &&
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
        Require(Key(c.Id) && RuntimeCompat.IsDefined(c.Phase) && c.TransferBaseline?.Length == 64 && c.TransferBaseline.All(Uri.IsHexDigit) &&
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
        if (c.Wars != null)
        {
            UnionWarAdmission.Validate(c.Wars);
            Require(c.Wars.Accepted && command.Action == AllegianceAction.JoinUnion && c.Wars.Union == command.Authority && c.Wars.Player == old.NativePlayer &&
                c.Wars.Tick == c.PreparedTick && c.WarResults != null && c.WarResults.Length == c.Wars.Targets.Length && c.WarResults.All(r => r != null) &&
                c.WarResults.Select(r => r.Enemy).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(c.Wars.Targets.Select(t => t.Enemy).OrderBy(id => id, StringComparer.Ordinal)),
                "Invalid saved war admission binding.");
            foreach (var r in c.WarResults)
                Require(RuntimeCompat.IsDefined(r.Step) && (r.Step == UnionWarStep.Verified) == r.ObservedTick.HasValue &&
                    (r.Step == UnionWarStep.Verified ? r.Goodwill is >= -100 and <= 100 && r.ReciprocalGoodwill is >= -100 and <= 100 &&
                        r.ObservedTick >= c.AllegianceTick && r.ObservedTick <= (c.FinishedTick ?? int.MaxValue) : r.Goodwill == null && r.ReciprocalGoodwill == null && !r.FromCompletedCall) &&
                    (applied || r.Step == UnionWarStep.Prepared), "Invalid saved native war observation.");
            Require(c.Phase != AllegianceTransitionPhase.Completed || c.WarResults.All(r => r.Step == UnionWarStep.Verified), "Completed admission has unfinished wars.");
            Require(c.WarReaffirmations == null || c.WarReaffirmations.Length <= 64, "Too many council reaffirmations.");
            long last = c.PreparedTick;
            foreach (var fresh in c.WarReaffirmations ?? [])
            { Require(UnionWarAdmission.SameWars(c.Wars, fresh) && fresh.Accepted && fresh.Tick >= last && fresh.Tick <= (c.FinishedTick ?? int.MaxValue), "Invalid council reaffirmation."); last = fresh.Tick; }
        }
        else Require(c.WarResults == null && c.WarReaffirmations == null, "War results require their original consent.");
        result.Current = c with { Followers = Array.AsReadOnly(c.Followers.OrderBy(id => id, StringComparer.Ordinal).ToArray()) }; return result;
    }
}
