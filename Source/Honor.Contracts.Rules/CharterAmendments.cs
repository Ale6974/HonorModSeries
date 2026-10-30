using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record CharterAmendmentWitness(string Previous, string Next, long Tick, string Book, string Transfers,
    CharterOfferDraft Draft, NpcRealmDecisionEvidence Decision, int Goodwill)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string Payments { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string ScopeObservations { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string GovernmentDecision { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string ScopePayments { get; init; }
}
public sealed record CharterAmendmentPlan(CharterAmendmentWitness Witness, string Proof, string Prepared, string After);
public sealed record CharterAmendmentSave(int Version, CharterAmendmentPlan[] Records);

/// <summary>Explicit ordinary-NPC amendment: actual mutual release and new consent on copies, no native assets or affiliation changes.</summary>
public sealed class CharterAmendments
{
    public const int Capacity = 128;
    private readonly Dictionary<string, CharterAmendmentPlan> records = new(StringComparer.Ordinal);
    public IReadOnlyCollection<CharterAmendmentPlan> Records => records.Values.ToArray();
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static string Hash(CharterAmendmentWitness w) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(w))));
    public static string GovernmentScope(CharterAmendmentWitness w) => Hash(w with { GovernmentDecision = null });
    public static CharterAmendmentPlan Replay(CharterAmendmentWitness w)
    {
        var b = CharterBook.Restore(w.Book);
        return Build(w, w.ScopeObservations == null ? b.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)) : JsonSerializer.Deserialize<TerritoryObservation[]>(w.ScopeObservations));
    }
    private static CharterAmendmentPlan Build(CharterAmendmentWitness w, IEnumerable<TerritoryObservation> observed)
    {
        if (w == null || !Key(w.Previous) || !Key(w.Next) || w.Next == w.Previous || w.Tick < 0 || w.Tick > int.MaxValue || w.Draft == null)
            throw new ArgumentException("Invalid amendment witness.");
        if (w.GovernmentDecision == null)
        { if (w.ScopeObservations != null || w.ScopePayments != null) throw new ArgumentException("Unpaired amendment observations."); NpcRealmDecision.Consent(w.Decision); }
        else
        {
            if (w.Decision == null || w.Decision.Regime is not (NpcDecisionRegime.Union or NpcDecisionRegime.Royal) || w.ScopeObservations == null || w.ScopePayments == null)
                throw new ArgumentException("Exact nonordinary amendment authority required.");
            var scopedPayments = TributeLedger.Restore(w.ScopePayments);
            if (scopedPayments.LastTick > w.Tick || scopedPayments.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) || w.Payments != null && w.ScopePayments != w.Payments)
                throw new ArgumentException("Exact resolved current payment scope required.");
            var mandate = JsonSerializer.Deserialize<ScopedNpcContractDecision>(w.GovernmentDecision);
            ScopedNpcTerminationAuthority.ValidateDecisions([w.Decision], [mandate], GovernmentScope(w), w.Tick);
            if (mandate.Goodwill != w.Goodwill) throw new ArgumentException("Amendment economic response must use the same recorded current goodwill.");
            if (JsonSerializer.Serialize(observed.ToArray()) != w.ScopeObservations) throw new ArgumentException("Original scoped observations changed.");
        }
        var book = CharterBook.Restore(w.Book); var transfers = TerritoryTransferLedger.Restore(w.Transfers); var old = book.Charter(w.Previous);
        if (w.ScopeObservations != null)
        {
            var all = observed.ToArray();
            if (all.Any(o => o == null) || all.Length != book.Entities.Count || all.Select(o => o.Id).Distinct().Count() != book.Entities.Count ||
                !new HashSet<string>(book.Entities.Select(e => e.Id), StringComparer.Ordinal).SetEquals(all.Select(o => o.Id))) throw new ArgumentException("Complete scoped amendment observations required.");
        }
        bool playerSubject = book.Entity(old.Terms.Subject).Player;
        var npc = book.Entity(playerSubject ? old.Terms.Liege : old.Terms.Subject);
        bool accepted;
        if (playerSubject)
        {
            if (w.Payments == null) throw new ArgumentException("Player-subject renewal requires its exact current tribute evidence.");
            var payments = TributeLedger.Restore(w.Payments);
            if (payments.LastTick > w.Tick || payments.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared)) throw new ArgumentException("Resolve reserved or future tribute first.");
            accepted = CharterOffers.Assess(book, payments, old.Terms.Subject, old.Terms.Liege, true, w.Draft, w.Goodwill, w.Tick).Accepted;
        }
        else
        {
            if (w.Payments != null) throw new ArgumentException("Legacy NPC-town renewals retain their original witness shape.");
            accepted = w.Draft.Assess(false, w.Goodwill).Accepted;
        }
        if (w.Tick < book.LastTick || w.Tick < transfers.LastTick || old.Phase != RecordedCharterPhase.Active || old.NpcAuthority != null ||
            old.PlayerReleaseTick.HasValue || old.NpcReleaseTick.HasValue || npc.Player || book.Entity(old.Terms.Liege).Player == playerSubject ||
            book.Charters.Any(c => c.Id == w.Next || book.Phase(c, w.Tick) == RecordedCharterPhase.Offered &&
                (c.Terms.Subject == old.Terms.Subject || c.Terms.Liege == old.Terms.Subject)) ||
            transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) ||
            !transfers.VerifiedOwner(npc.Id, npc.NativeOwner, w.Decision.Realm) || w.Goodwill < 20 || !accepted)
            throw new ArgumentException("An unchanged, unreleased same-party charter and fresh willing ordinary NPC response are required.");
        var actual = observed.ToArray(); book.ValidateCurrent(old.Id, actual);
        string proof = Hash(w);
        book.ConsentToRelease(old.Id, true, w.Tick, actual); book.ConsentToRelease(old.Id, false, w.Tick, actual);
        book.Offer(w.Next, w.Draft.Bind(old.Terms.Liege, old.Terms.Subject), w.Tick, PoliticalWorld.Day * 3, actual);
        book.BindAmendment(w.Next, old.Id, proof); book.Sign(w.Next, false, w.Tick, actual);
        string prepared = book.Export(); book.Sign(w.Next, true, w.Tick, actual);
        return new(w, proof, prepared, book.Export());
    }
    public static CharterAmendmentPlan Review(CharterBook book, TerritoryTransferLedger transfers, TributeLedger payments,
        string previous, string next, CharterOfferDraft draft, NpcRealmDecisionEvidence decision, int goodwill, long now,
        IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(transfers); RuntimeCompat.NotNull(payments); RuntimeCompat.NotNull(observations);
        if (payments.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) || payments.LastTick > now)
            throw new ArgumentException("Resolve reserved physical deliveries first.");
        return Build(new CharterAmendmentWitness(previous, next, now, book.Export(), transfers.Export(), draft, decision, goodwill) {
            Payments = book.Entity(book.Charter(previous).Terms.Subject).Player ? payments.Export() : null }, observations);
    }
    public (CharterBook Book, CharterAmendments Archive) Commit(CharterAmendmentPlan plan, CharterBook book, TerritoryTransferLedger transfers,
        TributeLedger payments, NpcRealmDecisionEvidence decision, int goodwill, long now, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(plan);
        if (payments.LastTick > now || payments.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared)) throw new ArgumentException("Resolve current tribute custody first.");
        var fresh = plan.Witness.GovernmentDecision == null ? Review(book, transfers, payments, plan.Witness.Previous, plan.Witness.Next, plan.Witness.Draft, decision, goodwill, now, observations) :
            Build(plan.Witness with { Book = book.Export(), Transfers = transfers.Export(), Decision = decision, Goodwill = goodwill, Tick = now,
                Payments = book.Entity(book.Charter(plan.Witness.Previous).Terms.Subject).Player ? payments.Export() : null, ScopeObservations = JsonSerializer.Serialize(observations.ToArray()), ScopePayments = payments.Export() }, observations);
        if (fresh != plan || records.ContainsKey(plan.Witness.Next) || records.Values.Any(r => r.Witness.Previous == plan.Witness.Previous) || records.Count >= Capacity)
            throw new ArgumentException("Stale, duplicate or full amendment review.");
        var after = CharterBook.Restore(fresh.After); var copy = Restore(Export()); copy.records.Add(fresh.Witness.Next, fresh); copy.ValidateBook(after);
        if (copy.Export().Length > 32000000) throw new ArgumentException("Amendment history capacity.");
        return (after, copy);
    }
    public void ValidateBook(CharterBook book)
    {
        var currentIds = new HashSet<string>(book.Charters.Select(c => c.Id), StringComparer.Ordinal);
        foreach (var plan in records.Values)
        {
            var expected = CharterBook.Restore(plan.After); var old = book.Charter(plan.Witness.Previous); var next = book.Charter(plan.Witness.Next);
            var initial = expected.Charter(next.Id);
            if (expected.Entities.Any(e => book.Entity(e.Id) != e) || expected.Charters.Any(c => !currentIds.Contains(c.Id)) ||
                old != expected.Charter(old.Id) || next with { Phase = initial.Phase, PlayerReleaseTick = initial.PlayerReleaseTick,
                NpcReleaseTick = initial.NpcReleaseTick, TerritoryLostTick = initial.TerritoryLostTick, Replacement = initial.Replacement,
                PoliticalLoss = initial.PoliticalLoss } != initial)
                throw new ArgumentException("Negotiated terms, signatures or original release history changed.");
        }
        foreach (var c in book.Charters.Where(c => c.Amendment != null))
            if (!records.TryGetValue(c.Id, out var r) || r.Witness.Previous != c.Amendment.Previous || r.Proof != c.Amendment.Proof)
                throw new ArgumentException("An amended charter requires its original negotiation/release archive.");
    }
    public string Export() => JsonSerializer.Serialize(new CharterAmendmentSave(1, records.Values.ToArray()));
    public static CharterAmendments Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32000000) throw new ArgumentException("Invalid amendment archive size.");
        var saved = JsonSerializer.Deserialize<CharterAmendmentSave>(json);
        if (saved == null || saved.Version != 1 || saved.Records == null || saved.Records.Length > Capacity) throw new ArgumentException("Invalid amendment archive.");
        var result = new CharterAmendments();
        foreach (var p in saved.Records)
        {
            if (p?.Witness == null) throw new ArgumentException("Missing amendment witness.");
            var expected = Replay(p.Witness);
            if (p != expected || result.records.ContainsKey(p.Witness.Next) || result.records.Values.Any(r => r.Witness.Previous == p.Witness.Previous))
                throw new ArgumentException("Invalid or duplicate amendment history.");
            result.records.Add(p.Witness.Next, p);
        }
        return result;
    }
}
