using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record NpcReparentWitness(string Previous, string Parent, string Created, string Before,
    long Tick, NpcRealmDecisionEvidence Decision, TerritoryObservation[] Observations, string Transfers, string Payments);
public sealed record NpcReparentRecord(NpcReparentWitness Witness, string Proof, string After);
public sealed record NpcReparentSave(int Version, NpcReparentRecord[] Records);

/// <summary>Bounded original ordinary-realm decisions for new subcharter replacements; no physical or personal transfer authority.</summary>
public sealed class NpcReparentArchive
{
    private readonly List<NpcReparentRecord> records = [];
    public IReadOnlyList<NpcReparentRecord> Records => records.Select(r => r with {
        Witness = r.Witness with { Observations = r.Witness.Observations.ToArray() } }).ToArray();
    private static string Hash(NpcReparentWitness w) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(w))));
    private static NpcReparentRecord Build(NpcReparentWitness w)
    {
        if (w == null || w.Observations == null || w.Observations.Length > CharterBook.MaxEntities || w.Observations.Any(x => x == null) ||
            w.Observations.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != w.Observations.Length)
            throw new ArgumentException("Complete unique original observations required.");
        w = w with { Observations = w.Observations.ToArray() };
        var before = CharterBook.Restore(w.Before);
        var transfers = TerritoryTransferLedger.Restore(w.Transfers); var payments = TributeLedger.Restore(w.Payments);
        if (transfers.LastTick > w.Tick || payments.LastTick > w.Tick || transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) ||
            payments.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared)) throw new ArgumentException("Resolve original physical and payment custody first.");
        if (w.Observations.Length != before.Entities.Count || before.Entities.Any(e => !w.Observations.Any(o => o.Id == e.Id)))
            throw new ArgumentException("Original observation set changed.");
        var original = before.Charter(w.Previous);
        var affected = before.PlayerFollowers().Where(c => c.Terms.Subject == original.Terms.Subject || before.IsBelow(c.Terms.Subject, original.Terms.Subject))
            .Select(c => c.Terms.Subject).Append(w.Parent).Append(original.Terms.Liege).Distinct(StringComparer.Ordinal);
        foreach (string party in affected)
        {
            var entity = before.Entity(party);
            if (!entity.Player && !transfers.VerifiedOwner(party, entity.NativeOwner, w.Decision?.Realm))
                throw new ArgumentException("Original actual realm and transfer provenance required for each affected party.");
        }
        var plan = NpcSubcharters.ReviewAuthorized(before, w.Previous, w.Parent, w.Created, w.Decision, w.Tick, w.Observations);
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
