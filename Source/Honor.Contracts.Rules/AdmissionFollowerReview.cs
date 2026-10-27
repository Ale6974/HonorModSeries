using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record AdmissionFollowerReview(string Proof, string Original);
internal sealed record AdmissionFollowerSource(string Target, long Now, string Administration, string Book, string Ledger, FollowerCandidate[] Entries);

/// <summary>The actual original full follower review, never reconstructed from later ownership or a shortened survivor list.</summary>
public static class AdmissionFollowerReviews
{
    private static string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
    public static AdmissionFollowerReview Capture(AllegianceTransition intent, FollowerTransferPlan reviewed)
    {
        RuntimeCompat.NotNull(intent); RuntimeCompat.NotNull(reviewed);
        var result = new AdmissionFollowerReview(Hash(reviewed.Stamp), reviewed.Stamp);
        Validate(intent with { FollowerReview = result }); return result;
    }
    public static void Validate(AllegianceTransition intent)
    {
        RuntimeCompat.NotNull(intent); var r = intent.FollowerReview;
        if (r == null) return; // Old receipts remain old receipts; no backfill or new preservation claim.
        if (r.Original == null || r.Original.Length > 7000000 || !CityStockProof.Valid(r.Proof) || Hash(r.Original) != r.Proof)
            throw new ArgumentException("Invalid original admission follower proof.");
        var saved = JsonSerializer.Deserialize<AdmissionFollowerSource>(r.Original);
        if (saved == null || saved.Entries == null || saved.Entries.Length > CharterBook.MaxEntities || saved.Now != intent.PreparedTick)
            throw new ArgumentException("Original admission review must match preparation scope and clock.");
        var book = CharterBook.Restore(saved.Book); var transfers = TerritoryTransferLedger.Restore(saved.Ledger);
        var before = PlayerAllegiance.Restore(intent.Before); var after = PlayerAllegiance.Restore(intent.After);
        var command = JsonSerializer.Deserialize<PlayerAllegianceSave>(intent.After).Commands.Last();
        var incoming = intent.Feudal?.Moves ?? intent.Peace?.IncomingCharters ?? intent.Wars?.IncomingCharters;
        if ((command.Action == AllegianceAction.BindLiege ? intent.Feudal == null : command.Action != AllegianceAction.JoinUnion) || incoming == null || command.Authority != saved.Target ||
            !book.Entities.Any(e => e.Player && e.NativeOwner == before.NativePlayer) || after.NativePlayer != before.NativePlayer ||
            Hash(saved.Ledger) != intent.TransferBaseline)
            throw new ArgumentException("Only the exact original admission and physical ledger can bind this review.");
        if (intent.Feudal != null)
        {
            var c = FeudalAdmissions.Review(book, before.NativePlayer, command.Contract, saved.Now, intent.Feudal.Decision, incoming);
            if (!FeudalAdmissions.SameReview(c, intent.Feudal)) throw new ArgumentException("Original actual feudal charter differs from admission consent.");
        }
        var replay = FollowerTransfers.Review(book, transfers, saved.Entries, saved.Target, saved.Now, saved.Administration);
        if (replay.Stamp != r.Original || !replay.Entries.Select(e => e.Charter).SequenceEqual(intent.Followers) ||
            !replay.Moves.OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(incoming))
            throw new ArgumentException("Original full follower graph/moves differ from the preserved council consent.");
    }
}

public sealed partial class AllegianceTransitionLedger
{
    public void PrepareReviewedAdmission(string id, PlayerAllegiance actual, AllegianceCommand command, FollowerTransferPlan followers,
        TerritoryTransferLedger transfers, UnionWarConsent wars = null, UnionPeaceConsent peace = null)
    {
        if (command?.Action != AllegianceAction.JoinUnion || (peace == null) == (wars == null))
            throw new ArgumentException("One actual union admission decision is required.");
        var next = Restore(Export());
        if (peace != null) next.PreparePeace(id, actual, command, followers, transfers, peace);
        else next.Prepare(id, actual, command, followers, transfers, wars);
        var scope = AdmissionFollowerReviews.Capture(next.Current, followers);
        next.Current = next.Current with { FollowerReview = scope };
        if (next.Export().Length > 7000000) throw new ArgumentException("Original admission scope exceeds safe journal capacity; preserve prior state.");
        Current = next.Current;
    }
}
