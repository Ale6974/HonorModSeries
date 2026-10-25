using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record SubtreeReleasePlan(string Root, string Before, string After, long Tick, string[] Charters);

/// <summary>Release only the incoming bond. All internal bonds, terms and historical debts remain intact.</summary>
public static class SubtreeRelease
{
    public static string[] Members(CharterBook book, string root)
    {
        var bond = book.Charter(root); string subject = bond.Terms.Subject;
        return book.Charters.Where(c => c.Id == root || c.Phase == RecordedCharterPhase.Active && book.IsBelow(c.Terms.Subject, subject))
            .Select(c => c.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }
    public static bool Authorizes(CharterBook book, string root, string charter)
    {
        if (root == null) return false;
        var bond = book.Charter(root); var c = book.Charter(charter);
        return bond.Phase == RecordedCharterPhase.Released && bond.PlayerReleaseTick.HasValue && bond.NpcReleaseTick.HasValue &&
            !book.Entity(bond.Terms.Subject).Player && book.Root(bond.Terms.Subject) == bond.Terms.Subject &&
            (c.Id == root || c.Phase == RecordedCharterPhase.Active && book.IsBelow(c.Terms.Subject, bond.Terms.Subject));
    }
    public static SubtreeReleasePlan Review(CharterBook book, string root, long now, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(observations);
        var bond = book.Charter(root); var current = observations.ToArray();
        if (bond.Phase != RecordedCharterPhase.Active || !book.PlayerManaged(bond)) throw new ArgumentException("Only a current managed NPC subtree may be released.");
        book.ValidateCurrent(root, current);
        string[] members = Members(book, root);
        if (book.Charters.Any(c => book.Phase(c, now) == RecordedCharterPhase.Offered && members.Any(id =>
            book.Charter(id).Terms.Subject == c.Terms.Subject || book.Charter(id).Terms.Subject == c.Terms.Liege)))
            throw new ArgumentException("Resolve offers affecting this subtree first.");
        var copy = CharterBook.Restore(book.Export());
        if (!bond.PlayerReleaseTick.HasValue)
        {
            if (bond.NpcAuthority == null) copy.ConsentToRelease(root, true, now, current);
            else copy.ConsentNpcRelease(root, bond.Terms.Liege, now, current);
        }
        if (!bond.NpcReleaseTick.HasValue)
        {
            if (bond.NpcAuthority == null) copy.ConsentToRelease(root, false, now, current);
            else copy.ConsentNpcRelease(root, bond.Terms.Subject, now, current);
        }
        return new(root, book.Export(), copy.Export(), now, members);
    }
    public static CharterBook Commit(CharterBook book, SubtreeReleasePlan plan, long now, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(plan);
        if (book.Export() != plan.Before || now != plan.Tick) throw new ArgumentException("Release review changed.");
        var fresh = Review(book, plan.Root, now, observations);
        if (fresh.After != plan.After || plan.Charters == null || !fresh.Charters.SequenceEqual(plan.Charters)) throw new ArgumentException("Release plan altered.");
        return CharterBook.Restore(fresh.After);
    }
}

public enum SubtreeExitPhase { Prepared, Released, Completed, Cancelled }
public sealed record SubtreeExitIntent(string Id, string Target, SubtreeReleasePlan Plan, string TransferBaseline, SubtreeExitPhase Phase = SubtreeExitPhase.Prepared, long? CompletedTick = null);
public sealed class SubtreeExitJournal
{
    public SubtreeExitIntent Current { get; private set; }
    public bool Pending => Current != null && Current.Phase is SubtreeExitPhase.Prepared or SubtreeExitPhase.Released;
    public bool Authorizes(TerritoryTransfer transfer)
    {
        if (Current == null || transfer == null || Current.Phase is not (SubtreeExitPhase.Released or SubtreeExitPhase.Completed) ||
            transfer.Purpose != TerritoryTransferPurpose.ReleasedToUnion || Current.Plan.Root != transfer.ReleasedRoot || Current.Target != transfer.TargetOwner ||
            Current.Plan.Tick > transfer.PreparedTick || Current.CompletedTick.HasValue && transfer.PreparedTick > Current.CompletedTick ||
            !Current.Plan.Charters.Contains(transfer.Charter)) return false;
        var book = CharterBook.Restore(Current.Plan.After); var entity = book.Entity(book.Charter(transfer.Charter).Terms.Subject);
        return entity.Id == transfer.Territory && entity.NativeOwner == transfer.BaselineOwner;
    }
    public void Prepare(string id, string target, SubtreeReleasePlan plan, TerritoryTransferLedger transfers)
    {
        if (Pending || !Key(id) || !Key(target) || plan == null || Current?.CompletedTick > plan.Tick) throw new ArgumentException("Resolve prior exit or invalid identity/clock.");
        ValidatePlan(plan);
        if (CharterBook.Restore(plan.After).Entities.Any(e => e.Player && e.NativeOwner == target)) throw new ArgumentException("Native player is not a union destination.");
        RuntimeCompat.NotNull(transfers);
        if (transfers.LastTick > plan.Tick || transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared)) throw new ArgumentException("Resolve city transfers before release.");
        Current = new(id, target, plan, RuntimeCompat.Hex(RuntimeCompat.Sha256(System.Text.Encoding.UTF8.GetBytes(transfers.Export()))));
    }
    public void ConfirmRelease(CharterBook actual)
    {
        if (!Pending || actual.Export() != Current.Plan.After) throw new ArgumentException("Actual released book differs from intent.");
        Current = Current with { Phase = SubtreeExitPhase.Released };
    }
    public void CancelUnchanged(CharterBook actual, TerritoryTransferLedger transfers, long now)
    {
        if (Current?.Phase != SubtreeExitPhase.Prepared || actual.Export() != Current.Plan.Before || now < Current.Plan.Tick || now > int.MaxValue ||
            RuntimeCompat.Hex(RuntimeCompat.Sha256(System.Text.Encoding.UTF8.GetBytes(transfers.Export()))) != Current.TransferBaseline)
            throw new ArgumentException("Only wholly unchanged preparation can cancel.");
        Current = Current with { Phase = SubtreeExitPhase.Cancelled, CompletedTick = now };
    }
    public void Complete(CharterBook actual, TerritoryTransferLedger transfers, IEnumerable<TerritoryTransferObservation> observed, long now)
    {
        if (Current?.Phase != SubtreeExitPhase.Released || actual.Export() != Current.Plan.After || now < Current.Plan.Tick || now < transfers.LastTick || now > int.MaxValue)
            throw new ArgumentException("No applied release at this time.");
        var all = observed.ToArray();
        if (all.Length != Current.Plan.Charters.Length || all.Any(o => o == null) || all.Select(o => o.Territory).Distinct().Count() != all.Length)
            throw new ArgumentException("Exact subtree observations required.");
        foreach (string id in Current.Plan.Charters)
        {
            var entity = actual.Entity(actual.Charter(id).Terms.Subject); var o = all.SingleOrDefault(o => o.Territory == entity.Id);
            if (o == null || !o.Exists || o.Player || o.Owner != Current.Target || !o.Eligible || !o.TargetEligible || o.TargetPlayer || o.PendingDelivery ||
                !transfers.VerifiedOwner(entity.Id, entity.NativeOwner, o.Owner)) throw new ArgumentException("A subtree city is not verified at the union.");
        }
        Current = Current with { Phase = SubtreeExitPhase.Completed, CompletedTick = now };
    }
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static void ValidatePlan(SubtreeReleasePlan p)
    {
        if (p == null || p.Charters == null || p.Charters.Length == 0 || p.Charters.Length > CharterBook.MaxEntities || p.Tick < 0 || p.Tick > int.MaxValue)
            throw new ArgumentException("Invalid subtree plan.");
        var before = CharterBook.Restore(p.Before); var after = CharterBook.Restore(p.After);
        // Structural validation of persisted intent only, not fresh engine consent.
        var observations = before.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true));
        var checkedPlan = SubtreeRelease.Review(before, p.Root, p.Tick, observations);
        if (checkedPlan.After != after.Export() || !checkedPlan.Charters.SequenceEqual(p.Charters)) throw new ArgumentException("Altered persisted subtree intent.");
    }
    public string Export() => JsonSerializer.Serialize(new { Version = 1, Current });
    public static SubtreeExitJournal Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 24000000) throw new ArgumentException("Invalid subtree journal size.");
        var save = JsonSerializer.Deserialize<Save>(json);
        if (save == null || save.Version != 1) throw new ArgumentException("Unsupported subtree journal.");
        var result = new SubtreeExitJournal(); if (save.Current == null) return result;
        var c = save.Current;
        if (!Key(c.Id) || !Key(c.Target) || !RuntimeCompat.IsDefined(c.Phase)) throw new ArgumentException("Invalid subtree intent.");
        ValidatePlan(c.Plan);
        if (CharterBook.Restore(c.Plan.After).Entities.Any(e => e.Player && e.NativeOwner == c.Target) || c.TransferBaseline?.Length != 64 || !c.TransferBaseline.All(Uri.IsHexDigit))
            throw new ArgumentException("Invalid destination or transfer baseline.");
        if ((c.Phase is SubtreeExitPhase.Completed or SubtreeExitPhase.Cancelled) != c.CompletedTick.HasValue || c.CompletedTick < c.Plan.Tick || c.CompletedTick > int.MaxValue)
            throw new ArgumentException("Invalid completion time.");
        result.Current = c; return result;
    }
    private sealed record Save(int Version, SubtreeExitIntent Current);
}
