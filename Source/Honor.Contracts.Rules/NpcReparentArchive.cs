using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record NpcReparentWitness(string Previous, string Parent, string Created, string Before,
    long Tick, NpcRealmDecisionEvidence Decision, TerritoryObservation[] Observations, string Transfers, string Payments)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RetainedRealmConsent[] RetainedRealms { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RetainedRealmConsent[] Parties { get; init; }
}
/// <summary>Current consent to retaining this exact child's existing contract, not city or realm transfer authority.</summary>
public sealed record RetainedRealmConsent(string Party, string ActualOwner, NpcRealmDecisionEvidence Decision, bool ReciprocalPeace);
public sealed record NpcReparentRecord(NpcReparentWitness Witness, string Proof, string After);
public sealed record NpcReparentSave(int Version, NpcReparentRecord[] Records);
public sealed record NpcReparentPlayerPerformance(string Charter, string Liege, string Subject, long Tick, TributePerformance Performance);

/// <summary>Bounded original ordinary-realm decisions for new subcharter replacements; no physical or personal transfer authority.</summary>
public sealed class NpcReparentArchive
{
    private readonly List<NpcReparentRecord> records = [];
    public IReadOnlyList<NpcReparentRecord> Records => records.Select(r => r with {
        Witness = r.Witness with { Observations = r.Witness.Observations.ToArray(), RetainedRealms = r.Witness.RetainedRealms?.ToArray(), Parties = r.Witness.Parties?.ToArray() } }).ToArray();
    private static string Hash(NpcReparentWitness w) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(w))));
    private static NpcReparentRecord Build(NpcReparentWitness w)
    {
        if (w == null || w.Observations == null || w.Observations.Length > CharterBook.MaxEntities || w.Observations.Any(x => x == null) ||
            w.Observations.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != w.Observations.Length)
            throw new ArgumentException("Complete unique original observations required.");
        w = w with { Observations = w.Observations.ToArray(), RetainedRealms = w.RetainedRealms?.ToArray(), Parties = w.Parties?.ToArray() };
        var before = CharterBook.Restore(w.Before);
        var transfers = TerritoryTransferLedger.Restore(w.Transfers); var payments = TributeLedger.Restore(w.Payments);
        if (transfers.LastTick > w.Tick || payments.LastTick > w.Tick || transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) ||
            payments.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared)) throw new ArgumentException("Resolve original physical and payment custody first.");
        if (w.Observations.Length != before.Entities.Count || before.Entities.Any(e => !w.Observations.Any(o => o.Id == e.Id)))
            throw new ArgumentException("Original observation set changed.");
        var original = before.Charter(w.Previous);
        var endpoints = new[] { original.Terms.Subject, original.Terms.Liege, w.Parent }.Distinct(StringComparer.Ordinal).ToArray();
        if (w.Parties != null && (w.Parties.Length is < 1 or > 3 || w.Parties.Any(p => p == null) ||
            w.Parties.Select(p => p.Party).Distinct(StringComparer.Ordinal).Count() != w.Parties.Length)) throw new ArgumentException("Invalid incoming party decision scope.");
        var descendants = before.PlayerFollowers().Where(c => before.IsBelow(c.Terms.Subject, original.Terms.Subject)).Select(c => c.Terms.Subject).ToArray();
        var affected = descendants.Append(original.Terms.Subject).Append(w.Parent).Append(original.Terms.Liege).Distinct(StringComparer.Ordinal);
        var foreign = new HashSet<string>(StringComparer.Ordinal);
        if (w.RetainedRealms != null && (w.RetainedRealms.Length is < 1 or > CharterBook.MaxEntities || w.RetainedRealms.Any(r => r == null) ||
            w.RetainedRealms.Select(r => r.Party).Distinct(StringComparer.Ordinal).Count() != w.RetainedRealms.Length))
            throw new ArgumentException("Retained realm consent must contain exact unique affected descendants.");
        foreach (string party in affected)
        {
            var entity = before.Entity(party);
            if (endpoints.Contains(party) && w.Parties != null && !entity.Player)
            {
                var p = w.Parties.SingleOrDefault(p => p.Party == party);
                if (p == null || !transfers.VerifiedOwner(party, entity.NativeOwner, p.ActualOwner)) throw new ArgumentException("Incoming party actual ownership provenance changed.");
                continue;
            }
            if (entity.Player || transfers.VerifiedOwner(party, entity.NativeOwner, w.Decision?.Realm)) continue;
            var retained = w.RetainedRealms?.SingleOrDefault(r => r.Party == party);
            if (!descendants.Contains(party) || retained == null || !retained.ReciprocalPeace || retained.ActualOwner == w.Decision?.Realm ||
                retained.Decision?.Realm != retained.ActualOwner || !transfers.VerifiedOwner(party, entity.NativeOwner, retained.ActualOwner))
                throw new ArgumentException("Original actual realm and transfer provenance required for each affected party.");
            NpcRealmDecision.Consent(retained.Decision); foreign.Add(party);
        }
        if (w.RetainedRealms != null && (!foreign.SetEquals(w.RetainedRealms.Select(r => r.Party)) ||
            w.RetainedRealms.GroupBy(r => r.ActualOwner, StringComparer.Ordinal).Any(g => g.Select(r => r.Decision).Distinct().Count() != 1)))
            throw new ArgumentException("Retained realm decision scope or representative changed.");
        var allDecisions = (w.RetainedRealms ?? []).Concat(w.Parties ?? []).Select(p => p.Decision).Append(w.Decision).ToArray();
        if (allDecisions.Any(d => d == null) || allDecisions.GroupBy(d => d.Realm, StringComparer.Ordinal).Any(g => g.Distinct().Count() != 1))
            throw new ArgumentException("Original realm decisions disagree across incoming and retained parties.");
        var plan = w.Parties == null ? NpcSubcharters.ReviewAuthorized(before, w.Previous, w.Parent, w.Created, w.Decision, w.Tick, w.Observations)
            : NpcSubcharters.ReviewWithParties(before, w.Previous, w.Parent, w.Created, w.Decision, w.Parties, w.Tick, w.Observations);
        var after = CharterBook.Restore(plan.After); var proof = Hash(w); after.BindReparent(w.Created, w.Previous, proof);
        return new(w, proof, after.Export());
    }
    public (CharterBook Book, NpcReparentArchive Archive) Append(NpcReparentWitness witness, CharterBook current)
    {
        if (current == null || witness == null || current.Export() != witness.Before || records.Count >= 128 ||
            records.Any(r => r.Witness.Previous == witness.Previous || r.Witness.Created == witness.Created))
            throw new ArgumentException("Stale or duplicate reparent review.");
        var next = Build(witness); var copy = Restore(Export()); copy.records.Add(next);
        var book = CharterBook.Restore(next.After); copy.ValidateBook(book);
        if (copy.Export().Length > 32000000) throw new ArgumentException("Reparent archive capacity.");
        return (book, copy);
    }
    public void ValidateBook(CharterBook book)
    {
        if (book == null) throw new ArgumentException("Current charter book required.");
        var currentIds = new HashSet<string>(book.Charters.Select(c => c.Id), StringComparer.Ordinal);
        foreach (var record in records)
        {
            var expected = CharterBook.Restore(record.After); var old = book.Charter(record.Witness.Previous); var next = book.Charter(record.Witness.Created);
            var initial = expected.Charter(next.Id);
            if (expected.Entities.Any(e => book.Entity(e.Id) != e) || expected.Charters.Any(c => !currentIds.Contains(c.Id)) ||
                old != expected.Charter(old.Id) || next with { Phase = initial.Phase, PlayerReleaseTick = initial.PlayerReleaseTick,
                    NpcReleaseTick = initial.NpcReleaseTick, TerritoryLostTick = initial.TerritoryLostTick,
                    Replacement = initial.Replacement, PoliticalLoss = initial.PoliticalLoss } != initial)
                throw new ArgumentException("Original reparent consent or linked terms changed.");
            foreach (var original in expected.Charters)
            {
                var current = book.Charter(original.Id);
                if (current.Terms != original.Terms || current.OfferedTick != original.OfferedTick || current.ExpiresTick != original.ExpiresTick ||
                    current.NpcAuthority != original.NpcAuthority || current.ConditionalOffer != original.ConditionalOffer ||
                    original.PlayerSignedTick.HasValue && current.PlayerSignedTick != original.PlayerSignedTick ||
                    original.NpcSignedTick.HasValue && current.NpcSignedTick != original.NpcSignedTick ||
                    original.PlayerReleaseTick.HasValue && current.PlayerReleaseTick != original.PlayerReleaseTick ||
                    original.NpcReleaseTick.HasValue && current.NpcReleaseTick != original.NpcReleaseTick ||
                    current.Reparent != original.Reparent || current.Amendment != original.Amendment || current.Replaces != original.Replaces ||
                    original.LawfulExit != null && current.LawfulExit != original.LawfulExit ||
                    original.Replacement != null && JsonSerializer.Serialize(current.Replacement) != JsonSerializer.Serialize(original.Replacement) ||
                    original.PoliticalLoss != null && current.PoliticalLoss != original.PoliticalLoss)
                    throw new ArgumentException("An original third-party contract or signature was rewritten.");
            }
        }
        foreach (var c in book.Charters.Where(c => c.Reparent != null))
            if (records.Count(r => r.Witness.Created == c.Id && r.Witness.Previous == c.Reparent.Previous && r.Proof == c.Reparent.Proof) != 1)
                throw new ArgumentException("Missing original reparent record.");
    }
    public void ValidatePeers(TerritoryTransferLedger transfers, TributeLedger payments)
    {
        if (transfers == null || payments == null) throw new ArgumentException("Original transfer and payment peers required.");
        foreach (var record in records)
        {
            var originalTransfers = TerritoryTransferLedger.Restore(record.Witness.Transfers);
            var originalPayments = TributeLedger.Restore(record.Witness.Payments);
            if (transfers.LastTick < originalTransfers.LastTick || transfers.Transfers.Count < originalTransfers.Transfers.Count ||
                !transfers.Transfers.Take(originalTransfers.Transfers.Count).Select(t => JsonSerializer.Serialize(t)).SequenceEqual(originalTransfers.Transfers.Select(t => JsonSerializer.Serialize(t))) ||
                payments.LastTick < originalPayments.LastTick)
                throw new ArgumentException("Original transfer or payment history changed.");
            foreach (var account in originalPayments.Accounts)
            {
                var current = payments.Account(account.CharterId);
                if (current.Terms != account.Terms || current.StartedTick != account.StartedTick ||
                    account.ClosedTick.HasValue && current.ClosedTick != account.ClosedTick)
                    throw new ArgumentException("Original debt account changed.");
            }
            foreach (var receipt in originalPayments.Receipts)
                if (!payments.Receipts.Any(r => r.Id == receipt.Id && JsonSerializer.Serialize(r) == JsonSerializer.Serialize(receipt)))
                    throw new ArgumentException("Original payment receipt changed.");
        }
    }
    /// <summary>Read the player's real same-counterparty payment performance as it stood at this recorded decision.</summary>
    public NpcReparentPlayerPerformance PlayerPerformanceAt(string created)
    {
        var r = records.SingleOrDefault(x => x.Witness.Created == created) ?? throw new ArgumentException("No original reparent record.");
        var book = CharterBook.Restore(r.Witness.Before); var payments = TributeLedger.Restore(r.Witness.Payments);
        var active = book.Charters.Where(c => c.Phase == RecordedCharterPhase.Active && book.Entity(c.Terms.Subject).Player).ToArray();
        if (active.Length == 0) return null;
        if (active.Length != 1) throw new ArgumentException("Ambiguous original player contract.");
        var contract = active[0];
        var result = TributePerformanceReview.ForPair(book, payments, contract.Terms.Liege, contract.Terms.Subject, r.Witness.Tick);
        return new(contract.Id, contract.Terms.Liege, contract.Terms.Subject, r.Witness.Tick, result);
    }
    public string Export() => JsonSerializer.Serialize(new NpcReparentSave(1, records.ToArray()));
    public static NpcReparentArchive Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32000000) throw new ArgumentException("Invalid reparent archive size.");
        var saved = JsonSerializer.Deserialize<NpcReparentSave>(json);
        if (saved == null || saved.Version != 1 || saved.Records == null || saved.Records.Length > 128) throw new ArgumentException("Invalid reparent archive.");
        var result = new NpcReparentArchive();
        foreach (var r in saved.Records)
        {
            if (r == null || r.Witness == null || result.records.Any(x => x.Witness.Previous == r.Witness.Previous || x.Witness.Created == r.Witness.Created) ||
                JsonSerializer.Serialize(Build(r.Witness)) != JsonSerializer.Serialize(r))
                throw new ArgumentException("Altered reparent history.");
            result.records.Add(r);
        }
        return result;
    }
}
