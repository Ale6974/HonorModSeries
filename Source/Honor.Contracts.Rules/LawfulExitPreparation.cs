using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

/// <summary>A staged unilateral closure, not permission to change allegiance or replay physical transfers.</summary>
public sealed class LawfulExitPreparation
{
    private const int Maximum = 32000000;
    private sealed record Data(string Request, long Tick, string Book, string Payments, string Politics, string Transfers, string Protection, string Observations);
    private sealed record Save(int Version, Data Original, string Proof, string AfterBook, string AfterPayments);
    private readonly Save saved;
    public string Proof => saved.Proof;
    public string Request => saved.Original.Request;
    public long Tick => saved.Original.Tick;
    public string AfterBook => saved.AfterBook;
    public string AfterPayments => saved.AfterPayments;
    public string BeforePolitics => saved.Original.Politics;
    public string BeforeBook => saved.Original.Book;
    public string BeforeTransfers => saved.Original.Transfers;
    private LawfulExitPreparation(Save value) { saved = value; }
    private static string Hash(string value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(value)));
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static void Require(bool ok, string reason) { if (!ok) throw new ArgumentException(reason); }
    public static LawfulExitPreparation Prepare(CharterBook book, TributeLedger payments, PlayerAllegiance politics,
        TerritoryTransferLedger transfers, CharterProtection protection, string request, long now, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(payments); RuntimeCompat.NotNull(politics);
        RuntimeCompat.NotNull(transfers); RuntimeCompat.NotNull(protection);
        Require(now >= book.LastTick && now >= payments.LastTick && now >= politics.LastTick && now >= transfers.LastTick && now >= protection.LastTick && now <= int.MaxValue,
            "Current nonfuture independent ledger clocks required.");
        protection.ValidateBook(book);
        var row = protection.Requests.SingleOrDefault(r => r.Plan.Id == request);
        Require(row?.Outcome == CharterAidOutcome.IntentionallyRefused && row.Refusal != null && row.Refusal.Tick <= now,
            "An original explicit owed-protection refusal is required, never failed/unknown aid.");
        var compact = protection.Compacts.Single(c => c.Charter == row.Plan.Charter); var c = book.Charter(row.Plan.Charter);
        Require(c == compact.Original && c.Phase == RecordedCharterPhase.Active && !c.PlayerReleaseTick.HasValue && !c.NpcReleaseTick.HasValue &&
            c.NpcAuthority == null && c.LawfulExit == null && c.Amendment == null && book.Entity(c.Terms.Subject).Player && !book.Entity(c.Terms.Liege).Player &&
            book.Entity(c.Terms.Subject).NativeOwner == politics.NativePlayer && book.Entity(c.Terms.Liege).NativeOwner == row.Plan.Source &&
            politics.State.Mode == PlayerAllegianceMode.Vassal && !politics.State.Suspended && politics.State.Contract == c.Id && politics.State.Authority == row.Plan.Source,
            "The exact original unreleased player contract and political/native identity must remain current.");
        Require(row.Plan.BookProof == Hash(book.Export()) && row.Plan.PoliticsProof == Hash(politics.Export()),
            "This first closure path requires the unchanged original refusal book and allegiance command history.");
        Require(!protection.Pending && !payments.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) &&
            !transfers.Transfers.Any(r => r.Phase == TerritoryTransferPhase.Prepared), "Resolve all uncertain aid and reserved physical transfers first.");
        var actual = observations?.Take(CharterBook.MaxEntities + 1).ToArray() ?? throw new ArgumentException("Current ownership observations required.");
        Require(actual.Length <= CharterBook.MaxEntities && actual.All(o => o != null) && actual.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() == actual.Length &&
            actual.All(o => book.Entities.Any(e => e.Id == o.Id)), "Bounded distinct original observation identities required.");
        actual = actual.OrderBy(o => o.Id, StringComparer.Ordinal).ToArray();
        book.ValidateCurrent(c.Id, actual);
        var original = new Data(request, now, book.Export(), payments.Export(), politics.Export(), transfers.Export(), protection.Export(), Json(actual));
        string proof = Hash(Json(original));
        var nextBook = CharterBook.Restore(original.Book);
        nextBook.StageLawfulExit(c.Id, new(request, proof, now), now, actual);
        var nextPayments = TributeLedger.Restore(original.Payments);
        if (nextPayments.Accounts.Any(a => a.CharterId == c.Id)) nextPayments.SynchronizeClosure(nextBook, c.Id, now);
        protection.ValidateBook(nextBook);
        // Outstanding bills/receipts remain real liabilities after closure, with no future accrual.
        _ = TributePerformanceReview.ForCharter(nextBook, nextPayments, c.Id, now);
        var result = new LawfulExitPreparation(new(1, original, proof, nextBook.Export(), nextPayments.Export()));
        Require(result.Export().Length <= Maximum, "Lawful closure preparation capacity; originals remain unchanged.");
        return result;
    }
    public void CheckBefore(CharterBook book, TributeLedger payments, PlayerAllegiance politics, TerritoryTransferLedger transfers, CharterProtection protection,
        long now, IEnumerable<TerritoryObservation> observations)
    {
        var d = saved.Original;
        Require(book.Export() == d.Book && payments.Export() == d.Payments && politics.Export() == d.Politics &&
            transfers.Export() == d.Transfers && protection.Export() == d.Protection, "Stale final closure review; no partial commit authorized.");
        Require(Prepare(book, payments, politics, transfers, protection, Request, now, observations).Export() == Export(),
            "Fresh original ownership and exact confirmation time are required.");
    }
    public void VerifyPair(CharterBook book, CharterProtection protection)
    {
        var oldProtection = CharterProtection.Restore(saved.Original.Protection);
        var original = oldProtection.Requests.Single(r => r.Plan.Id == Request);
        var actual = protection.Requests.SingleOrDefault(r => r.Plan.Id == Request);
        var compact = oldProtection.Compacts.Single(c => c.Charter == original.Plan.Charter);
        Require(actual != null && Json(actual) == Json(original) && protection.Compacts.Any(c => c == compact) &&
            book.Charter(original.Plan.Charter) == CharterBook.Restore(AfterBook).Charter(original.Plan.Charter),
            "Lawful closure must retain the exact original live refusal/compact and unchanged terminated charter.");
        protection.ValidateBook(book);
    }
    public string Export() => Json(saved);
    public static LawfulExitPreparation Restore(string json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && json.Length <= Maximum, "Invalid lawful closure preparation size.");
        var s = JsonSerializer.Deserialize<Save>(json);
        Require(s?.Version == 1 && s.Original != null && s.Proof == Hash(Json(s.Original)), "Invalid closure proof/version.");
        var d = s.Original; var before = CharterBook.Restore(d.Book);
        // Original observations verify the archive only; they never replace a fresh live CheckBefore.
        var source = JsonSerializer.Deserialize<TerritoryObservation[]>(d.Observations);
        var expected = Prepare(before, TributeLedger.Restore(d.Payments), PlayerAllegiance.Restore(d.Politics),
            TerritoryTransferLedger.Restore(d.Transfers), CharterProtection.Restore(d.Protection), d.Request, d.Tick, source);
        Require(expected.Export() == json, "Changed closure outputs, unrelated edits or noncanonical archive.");
        expected.VerifyPair(CharterBook.Restore(s.AfterBook), CharterProtection.Restore(d.Protection));
        return expected;
    }
}
