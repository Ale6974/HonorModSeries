using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record NpcTerminationWitness(string Root, string Before, long Tick, NpcRealmDecisionEvidence Authority,
    TerritoryObservation[] Observations, RetainedRealmConsent[] Parties, RetainedRealmConsent[] Children, string Transfers, string Payments)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DeferredNpcClosure Deferred { get; init; }
}
public sealed record NpcTerminationRecord(NpcTerminationWitness Witness, string Proof, string After);
public sealed record NpcTerminationsSave(int Version, NpcTerminationRecord[] Records);

/// <summary>End one NPC incoming bond with current consent; preserve all internal rights and physical/economic history.</summary>
public sealed class NpcSubtreeTerminations
{
    private readonly List<NpcTerminationRecord> records = [];
    private static NpcTerminationWitness Copy(NpcTerminationWitness w) => w with {
        Observations = w.Observations.ToArray(), Parties = w.Parties.ToArray(), Children = w.Children.ToArray() };
    public IReadOnlyList<NpcTerminationRecord> Records => records.Select(r => r with { Witness = Copy(r.Witness) }).ToArray();
    private static NpcTerminationRecord Build(NpcTerminationWitness w)
    {
        if (w == null || w.Observations == null || w.Parties == null || w.Children == null || w.Parties.Length != 2 ||
            w.Children.Length > CharterBook.MaxEntities || w.Observations.Length > CharterBook.MaxEntities || w.Observations.Any(o => o == null))
            throw new ArgumentException("Complete original NPC termination scope required.");
        w = Copy(w); var before = CharterBook.Restore(w.Before); var root = before.Charter(w.Root);
        if (w.Deferred != null && (string.IsNullOrWhiteSpace(w.Deferred.Request) || w.Deferred.Request.Length > 120 || !CityStockProof.Valid(w.Deferred.Proof)))
            throw new ArgumentException("Invalid original deferred request link.");
        var transfers = TerritoryTransferLedger.Restore(w.Transfers); var payments = TributeLedger.Restore(w.Payments);
        NpcRealmDecision.Consent(w.Authority);
        if (root.NpcAuthority == null || root.Phase != RecordedCharterPhase.Active || !before.PlayerManaged(root) ||
            before.Entity(root.Terms.Liege).Player || before.Entity(root.Terms.Subject).Player ||
            w.Tick < before.LastTick || w.Tick < transfers.LastTick || w.Tick < payments.LastTick || w.Tick > int.MaxValue ||
            transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) || payments.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) ||
            payments.Accounts.Any(a => a.CharterId == w.Root))
            throw new ArgumentException("Only an active NPC-only managed incoming bond with resolved custody may terminate.");
        var allIds = before.Entities.Select(e => e.Id).ToArray();
        if (w.Observations.Length != allIds.Length || w.Observations.Select(o => o.Id).Distinct().Count() != allIds.Length ||
            !new HashSet<string>(allIds, StringComparer.Ordinal).SetEquals(w.Observations.Select(o => o.Id)))
            throw new ArgumentException("Complete original observations required.");
        var children = before.Charters.Where(c => c.Phase == RecordedCharterPhase.Active && before.IsBelow(c.Terms.Subject, root.Terms.Subject)).Select(c => c.Terms.Subject).ToArray();
        void Scope(RetainedRealmConsent[] actual, string[] expected)
        {
            if (actual.Length != expected.Length || actual.Any(p => p == null) || actual.Select(p => p.Party).Distinct().Count() != actual.Length ||
                !new HashSet<string>(expected, StringComparer.Ordinal).SetEquals(actual.Select(p => p.Party)))
                throw new ArgumentException("Every exact original party and descendant needs a separate current decision.");
            foreach (var p in actual)
            {
                NpcRealmDecision.Consent(p.Decision);
                if (!p.ReciprocalPeace || p.Decision.Realm != p.ActualOwner || !transfers.VerifiedOwner(p.Party, before.Entity(p.Party).NativeOwner, p.ActualOwner))
                    throw new ArgumentException("Current peaceful ownership and original transfer provenance required.");
            }
        }
        Scope(w.Parties, [root.Terms.Liege, root.Terms.Subject]); Scope(w.Children, children);
        if (w.Parties.Concat(w.Children).Select(p => p.Decision).Append(w.Authority).GroupBy(d => d.Realm, StringComparer.Ordinal).Any(g => g.Distinct().Count() != 1))
            throw new ArgumentException("A realm's current decisions disagree.");
        var plan = SubtreeRelease.Review(before, w.Root, w.Tick, w.Observations);
        var after = CharterBook.Restore(plan.After);
        if (before.Charters.Where(c => c.Id != w.Root).Any(c => after.Charter(c.Id) != c)) throw new ArgumentException("Termination rewrote a third-party charter.");
        var proof = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(w))));
        return new(w, proof, after.Export());
    }
    public (CharterBook Book, NpcSubtreeTerminations Archive) Append(NpcTerminationWitness witness, CharterBook current)
    {
        if (witness == null || current == null || current.Export() != witness.Before || records.Count >= 128 || records.Any(r => r.Witness.Root == witness.Root) ||
            records.Count > 0 && witness.Tick < records[records.Count - 1].Witness.Tick)
            throw new ArgumentException("Duplicate or stale NPC termination.");
        var record = Build(witness); var copy = Restore(Export()); copy.records.Add(record);
        var after = CharterBook.Restore(record.After); copy.ValidateBook(after);
        if (copy.Export().Length > 32000000) throw new ArgumentException("NPC termination archive capacity reached.");
        return (after, copy);
    }
    public void ValidateBook(CharterBook current)
    {
        foreach (var r in records)
        {
            var original = CharterBook.Restore(r.After);
            if (current.Charter(r.Witness.Root) != original.Charter(r.Witness.Root) || original.Entities.Any(e => current.Entity(e.Id) != e))
                throw new ArgumentException("Original termination reason, signatures or identities changed.");
            foreach (var old in original.Charters)
            {
                var now = current.Charter(old.Id);
                if (now.Terms != old.Terms || now.OfferedTick != old.OfferedTick || now.ExpiresTick != old.ExpiresTick || now.NpcAuthority != old.NpcAuthority ||
                    now.Reparent != old.Reparent || now.Amendment != old.Amendment || now.Replaces != old.Replaces || now.ConditionalOffer != old.ConditionalOffer ||
                    old.PlayerSignedTick.HasValue && now.PlayerSignedTick != old.PlayerSignedTick || old.NpcSignedTick.HasValue && now.NpcSignedTick != old.NpcSignedTick ||
                    old.PlayerReleaseTick.HasValue && now.PlayerReleaseTick != old.PlayerReleaseTick || old.NpcReleaseTick.HasValue && now.NpcReleaseTick != old.NpcReleaseTick ||
                    old.LawfulExit != null && now.LawfulExit != old.LawfulExit || old.PoliticalLoss != null && now.PoliticalLoss != old.PoliticalLoss ||
                    old.Replacement != null && JsonSerializer.Serialize(now.Replacement) != JsonSerializer.Serialize(old.Replacement))
                    throw new ArgumentException("A retained third-party contract or signature was rewritten.");
            }
        }
    }
    public void ValidatePeers(TerritoryTransferLedger transfers, TributeLedger payments)
    {
        foreach (var r in records)
        {
            var oldTransfers = TerritoryTransferLedger.Restore(r.Witness.Transfers); var oldPayments = TributeLedger.Restore(r.Witness.Payments);
            if (transfers.LastTick < oldTransfers.LastTick || transfers.Transfers.Count < oldTransfers.Transfers.Count || payments.LastTick < oldPayments.LastTick ||
                !transfers.Transfers.Take(oldTransfers.Transfers.Count).Select(t => JsonSerializer.Serialize(t)).SequenceEqual(oldTransfers.Transfers.Select(t => JsonSerializer.Serialize(t))))
                throw new ArgumentException("Original physical/payment history changed.");
            foreach (var a in oldPayments.Accounts)
            {
                var actual = payments.Account(a.CharterId);
                if (actual.Terms != a.Terms || actual.StartedTick != a.StartedTick || a.ClosedTick.HasValue && actual.ClosedTick != a.ClosedTick)
                    throw new ArgumentException("An original debt account was reset.");
            }
            foreach (var receipt in oldPayments.Receipts)
                if (!payments.Receipts.Any(p => p.Id == receipt.Id && JsonSerializer.Serialize(p) == JsonSerializer.Serialize(receipt))) throw new ArgumentException("An original payment receipt was lost.");
        }
    }
    public string Export() => JsonSerializer.Serialize(new NpcTerminationsSave(1, records.ToArray()));
    public static NpcSubtreeTerminations Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32000000) throw new ArgumentException("Invalid NPC termination archive size.");
        var save = JsonSerializer.Deserialize<NpcTerminationsSave>(json);
        if (save?.Version != 1 || save.Records == null || save.Records.Length > 128) throw new ArgumentException("Invalid NPC termination archive.");
        var result = new NpcSubtreeTerminations();
        foreach (var r in save.Records)
        {
            if (r == null || r.Witness == null || result.records.Any(x => x.Witness.Root == r.Witness.Root) ||
                result.records.Count > 0 && r.Witness.Tick < result.records[result.records.Count - 1].Witness.Tick || JsonSerializer.Serialize(Build(r.Witness)) != JsonSerializer.Serialize(r))
                throw new ArgumentException("Altered NPC termination history.");
            result.records.Add(r);
        }
        return result;
    }
}
