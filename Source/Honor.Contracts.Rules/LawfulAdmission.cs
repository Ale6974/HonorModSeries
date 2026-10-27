using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

/// <summary>No current NPC followers: original physical history stays intact; no town migration is authorized.</summary>
public static class LawfulAdmissions
{
    private static void Require(bool ok, string why) { if (!ok) throw new ArgumentException(why); }
    private static string Json<T>(T x) => JsonSerializer.Serialize(x);
    private static string Hash(string x) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(x)));
    public static void ValidateIntent(AllegianceTransition c)
    {
        if (c?.LawfulExit == null)
        {
            if (c?.LawfulHistoricalTransfers == true || c?.LawfulFollowerMigration == true) throw new ArgumentException("Lawful modes need their original lawful exit.");
            return;
        }
        var p = LawfulExitPreparation.Restore(c.LawfulExit);
        var scope = c.FollowerReview == null ? null : JsonSerializer.Deserialize<AdmissionFollowerSource>(c.FollowerReview.Original);
        var physical = TerritoryTransferLedger.Restore(p.BeforeTransfers);
        var before = PlayerAllegiance.Restore(p.BeforePolitics);
        Require(c.Before == p.BeforePolitics && c.PreparedTick == p.Tick && c.Peace != null && c.Peace.Accepted && c.Wars == null && c.Feudal == null &&
            c.LostFollowers == null && c.TownLosses == null && scope != null && scope.Book == p.AfterBook && scope.Ledger == p.BeforeTransfers &&
            (c.LawfulFollowerMigration ? before.State.Authority != scope.Target && scope.Entries.Length > 0 &&
                scope.Entries.All(e => e.Observation.Owner == before.State.Authority && !e.Observation.HasInhabitants && e.Observation.ResidentProof == null) : scope.Entries.Length == 0) &&
            c.TransferBaseline == Hash(p.BeforeTransfers) && !physical.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) &&
            (c.LawfulHistoricalTransfers ? physical.Transfers.Count > 0 : physical.Transfers.Count == 0) &&
            c.Phase != AllegianceTransitionPhase.Cancelled,
            "Lawful admission requires exact original closure, council and complete explicitly scoped physical evidence.");
    }
    public static void ValidatePair(AllegianceTransition c, CharterBook book, TributeLedger payments, CharterProtection protection)
    {
        ValidateIntent(c); var p = LawfulExitPreparation.Restore(c.LawfulExit); p.VerifyPair(book, protection);
        var originalBook = CharterBook.Restore(p.BeforeBook);
        foreach (var entity in originalBook.Entities)
            Require(book.Entity(entity.Id) == entity, "An original party identity was erased or reassigned.");
        foreach (var original in originalBook.Charters)
        {
            var current = book.Charter(original.Id);
            Require(current.Terms == original.Terms && current.OfferedTick == original.OfferedTick && current.ExpiresTick == original.ExpiresTick &&
                current.NpcAuthority == original.NpcAuthority && current.ConditionalOffer == original.ConditionalOffer &&
                (!original.PlayerSignedTick.HasValue || current.PlayerSignedTick == original.PlayerSignedTick) &&
                (!original.NpcSignedTick.HasValue || current.NpcSignedTick == original.NpcSignedTick),
                "An original third-party contract or signature was erased or rewritten.");
        }
        var after = TributeLedger.Restore(p.AfterPayments);
        foreach (var account in after.Accounts)
        {
            // Other old accounts may legitimately close later. The terminated account's cutoff must stay exact.
            var charter = book.Charter(account.CharterId);
            var current = payments.Account(account.CharterId);
            Require(current.Terms == account.Terms && current.StartedTick == account.StartedTick &&
                (charter.LawfulExit?.Proof != p.Proof || current.ClosedTick == account.ClosedTick), "Original account/debt basis changed.");
        }
        foreach (var receipt in after.Receipts)
            Require(payments.Receipts.Any(r => r.Id == receipt.Id && Json(r) == Json(receipt)), "Original paid/cancelled receipt was erased or relabeled.");
    }
    public static void ValidateCurrentPhysical(AllegianceTransition c, TerritoryTransferLedger current)
    {
        ValidateIntent(c);
        var p = LawfulExitPreparation.Restore(c.LawfulExit); var original = TerritoryTransferLedger.Restore(p.BeforeTransfers);
        if (!c.LawfulFollowerMigration)
        {
            Require(current.Export() == p.BeforeTransfers, "The original physical journal must stay unchanged during a no-move admission.");
            return;
        }
        var rows = current.Transfers; var old = original.Transfers;
        Require(rows.Count >= old.Count && rows.Take(old.Count).Select(Json).SequenceEqual(old.Select(Json)), "Original physical outcomes must remain an exact prefix.");
        if (rows.Count == old.Count) Require(current.Export() == p.BeforeTransfers, "An unchanged prefix cannot advance its original clock.");
        else Require(current.LastTick == (rows.Last().ResolvedTick ?? rows.Last().PreparedTick), "Physical journal clock must follow its last actual appended outcome.");
        var scope = JsonSerializer.Deserialize<AdmissionFollowerSource>(c.FollowerReview.Original);
        var book = CharterBook.Restore(p.AfterBook); var command = JsonSerializer.Deserialize<PlayerAllegianceSave>(c.After).Commands.Last();
        foreach (var row in rows.Skip(old.Count))
        {
            var entry = scope.Entries.SingleOrDefault(e => e.Charter == row.Charter);
            Require(entry != null && row.Territory == entry.Observation.Territory && row.BaselineOwner == book.Entity(row.Territory).NativeOwner &&
                row.FromOwner == entry.Observation.Owner && row.TargetOwner == command.Authority && row.Purpose == TerritoryTransferPurpose.ActiveCharter &&
                row.ReleasedRoot == null && row.PreparedTick >= c.PreparedTick && row.Phase != TerritoryTransferPhase.Destroyed &&
                row.StockProof == entry.Observation.StockProof && row.StockFormat == entry.Observation.StockFormat && row.ResidentProof == entry.Observation.ResidentProof,
                "Only the original follower's exact reviewed source, destination and preservation evidence may be appended.");
        }
    }
    public static void ValidateArchive(IEnumerable<string> entries, CharterBook book, TributeLedger payments, CharterProtection protection)
    {
        RuntimeCompat.NotNull(entries);
        var history = entries.Take(130).ToArray();
        Require(history.Length <= 129 && history.All(x => x != null && x.Length <= 8000000) && history.Sum(x => (long)x.Length) <= 40000000,
            "Bounded original political archive required.");
        var claims = history.Select(x => AllegianceTransitionLedger.Restore(x).Current).Where(c => c?.LawfulExit != null).ToArray();
        Require(claims.Length == book.Charters.Count(c => c.LawfulExit != null), "Every live legal closure needs its own execution record.");
        Require(claims.Select(c => LawfulExitPreparation.Restore(c.LawfulExit).Proof).Distinct(StringComparer.Ordinal).Count() == claims.Length,
            "Duplicate legal-exit execution history.");
        foreach (var c in claims)
        {
            Require(c.Phase is AllegianceTransitionPhase.AllegianceApplied or AllegianceTransitionPhase.Completed, "A live closure requires an applied paired political intent.");
            ValidatePair(c, book, payments, protection);
        }
        foreach (var c in book.Charters.Where(c => c.LawfulExit != null))
            Require(claims.Count(x => LawfulExitPreparation.Restore(x.LawfulExit).Proof == c.LawfulExit.Proof) == 1,
                "Unpaired lawful closure cannot authorize any live change.");
    }
}

public sealed partial class AllegianceTransitionLedger
{
    public void PrepareLawfulAdmission(string id, PlayerAllegiance actual, AllegianceCommand command, FollowerTransferPlan followers,
        TerritoryTransferLedger transfers, LawfulExitPreparation closure, UnionPeaceConsent peace)
        => PrepareLawfulAdmissionCore(id, actual, command, followers, transfers, closure, peace, false);
    public void PrepareLawfulAdmissionWithHistory(string id, PlayerAllegiance actual, AllegianceCommand command, FollowerTransferPlan followers,
        TerritoryTransferLedger transfers, LawfulExitPreparation closure, UnionPeaceConsent peace)
        => PrepareLawfulAdmissionCore(id, actual, command, followers, transfers, closure, peace, true, false);
    public void PrepareLawfulFollowerAdmission(string id, PlayerAllegiance actual, AllegianceCommand command, FollowerTransferPlan followers,
        TerritoryTransferLedger transfers, LawfulExitPreparation closure, UnionPeaceConsent peace)
        => PrepareLawfulAdmissionCore(id, actual, command, followers, transfers, closure, peace, transfers.Transfers.Count > 0, true);
    private void PrepareLawfulAdmissionCore(string id, PlayerAllegiance actual, AllegianceCommand command, FollowerTransferPlan followers,
        TerritoryTransferLedger transfers, LawfulExitPreparation closure, UnionPeaceConsent peace, bool history, bool migrate = false)
    {
        if (actual.Export() != closure.BeforePolitics || (migrate ? followers.Entries.Count == 0 : followers.Entries.Count != 0) || transfers.Export() != closure.BeforeTransfers)
            throw new ArgumentException("Original player/physical scope changed.");
        var next = Restore(Export()); next.PreparePeace(id, actual, command, followers, transfers, peace);
        next.Current = next.Current with { LawfulExit = closure.Export(), LawfulHistoricalTransfers = history, LawfulFollowerMigration = migrate };
        next.Current = next.Current with { FollowerReview = AdmissionFollowerReviews.Capture(next.Current, followers) };
        LawfulAdmissions.ValidateIntent(next.Current);
        if (next.Export().Length > 7000000) throw new ArgumentException("Paired lawful-admission journal capacity.");
        Current = next.Current;
    }
}
