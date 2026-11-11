using System.Text;
using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record CustodyParty(string Party, string Realm, bool Player);
public sealed record CustodyAgreement(string Id, string Transfer, string Book, string Payments, string Physical, string Custody,
    CustodyParty[] Parties, TerritoryObservation[] Observations, NpcRealmDecisionEvidence[] Realms, long Tick, ScopedNpcContractDecision[] Regimes = null);
public sealed record CustodyAgreementReceipt(CustodyAgreement Witness, string Proof);
public sealed record CustodyAgreementSave(int Version, CustodyAgreementReceipt[] Records);
/// <summary>Current standalone consent to finish immutable old custody. Never revives an ended charter or rewrites original calls/clinical/debt.</summary>
public sealed class CustodyAgreements
{
    private readonly List<CustodyAgreementReceipt> records = [];
    public IReadOnlyList<CustodyAgreementReceipt> Records => records.Select(r => JsonSerializer.Deserialize<CustodyAgreementReceipt>(JsonSerializer.Serialize(r))).ToArray();
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static string Hash(object value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    public static string Scope(CustodyAgreement w) => Hash(new { Action = "CurrentCustodyCompletion/v1", w.Id, w.Transfer, w.Book, w.Payments, w.Physical, w.Custody, w.Parties, w.Observations, w.Realms, w.Tick });
    public static string[] PartiesFor(CharterBook book, TerritoryTransfer t)
    {
        var all = new HashSet<string>([t.Territory, book.Charter(t.Charter).Terms.Liege], StringComparer.Ordinal); bool changed;
        do { changed = false; foreach (var c in book.Charters.Where(c => c.Phase == RecordedCharterPhase.Active))
            if (all.Contains(c.Terms.Liege) || all.Contains(c.Terms.Subject)) { changed |= all.Add(c.Terms.Liege); changed |= all.Add(c.Terms.Subject); } } while (changed);
        return all.OrderBy(p => p, StringComparer.Ordinal).ToArray();
    }
    public static TerritoryTransfer Validate(CustodyAgreement w)
    {
        if (w == null || !Key(w.Id) || !Key(w.Transfer) || w.Book?.Length > 7000000 || w.Payments?.Length > 8000000 || w.Physical?.Length > 4000000 ||
            !CityStockProof.Valid(w.Custody) || w.Parties == null || w.Observations == null || w.Realms == null || w.Tick is < 0 or > int.MaxValue) throw new ArgumentException("Bounded original custody and current consent required.");
        var book = CharterBook.Restore(w.Book); var physical = TerritoryTransferLedger.Restore(w.Physical); var payments = TributeLedger.Restore(w.Payments);
        var t = physical.Transfer(w.Transfer); var old = book.Charter(t.Charter);
        if (t.Phase != TerritoryTransferPhase.Prepared || !t.CityCallJournalV1 || t.Purpose != TerritoryTransferPurpose.ActiveCharter || t.ReleasedRoot != null ||
            old.Terms.Subject != t.Territory || book.Entity(t.Territory).Player || book.Entity(t.Territory).NativeOwner != t.BaselineOwner ||
            old.Phase is not (RecordedCharterPhase.Active or RecordedCharterPhase.Released or RecordedCharterPhase.TerritoryLost or RecordedCharterPhase.Replaced or RecordedCharterPhase.LawfullyEnded) ||
            !old.PlayerSignedTick.HasValue || !old.NpcSignedTick.HasValue || old.PlayerSignedTick > t.PreparedTick || old.NpcSignedTick > t.PreparedTick ||
            w.Tick < book.LastTick || w.Tick < physical.LastTick || w.Tick < payments.LastTick || payments.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared))
            throw new ArgumentException("Original signed pending custody and settled current ledgers required.");
        string[] scope = PartiesFor(book, t);
        if (!w.Parties.Select(p => p?.Party).SequenceEqual(scope) || !w.Observations.Select(o => o?.Id).SequenceEqual(scope) ||
            w.Parties.Any(p => p == null || !Key(p.Realm) || p.Player != book.Entity(p.Party).Player) || w.Observations.Any(o => o == null || !o.Exists || !o.Eligible ||
                o.Player != book.Entity(o.Id).Player || o.NativeOwner != book.Entity(o.Id).NativeOwner)) throw new ArgumentException("Exact current surviving party closure required.");
        foreach (var party in w.Parties)
        {
            if (party.Party == t.Territory) { if (party.Realm != t.FromOwner && party.Realm != t.TargetOwner) throw new ArgumentException("Original town custody has not returned."); }
            else if (party.Player ? party.Realm != book.Entity(party.Party).NativeOwner : !physical.VerifiedOwner(party.Party, book.Entity(party.Party).NativeOwner, party.Realm))
                throw new ArgumentException("Unverified third-party actual authority.");
        }
        foreach (var active in book.Charters.Where(c => c.Phase == RecordedCharterPhase.Active && scope.Contains(c.Terms.Subject)))
        { if (active.Terms.Subject == t.Territory && !book.PlayerManaged(active)) throw new ArgumentException("A foreign current master cannot be overridden by the former delivery."); book.ValidateCurrent(active.Id, w.Observations); }
        if (book.Charters.Any(c => book.Phase(c, w.Tick) == RecordedCharterPhase.Offered && (scope.Contains(c.Terms.Subject) || scope.Contains(c.Terms.Liege)))) throw new ArgumentException("Competing current party offers.");
        string[] realms = w.Parties.Where(p => !p.Player).Select(p => p.Realm).Concat([t.FromOwner, t.TargetOwner]).Distinct().OrderBy(r => r, StringComparer.Ordinal).ToArray();
        if (!w.Realms.Select(r => r?.Realm).SequenceEqual(realms)) throw new ArgumentException("Each current source, target and third-party representative must consent.");
        ScopedNpcTerminationAuthority.ValidateDecisions(w.Realms, w.Regimes, Scope(w), w.Tick);
        foreach (var account in payments.Accounts)
            if (book.Charter(account.CharterId).Terms != account.Terms) throw new ArgumentException("Original obligation terms changed.");
        return t;
    }
    public CustodyAgreementReceipt Add(CustodyAgreement w)
    {
        _ = Validate(w);
        if (records.Count >= 128 || records.Any(r => r.Witness.Id == w.Id) || records.LastOrDefault()?.Witness.Tick > w.Tick) throw new ArgumentException("Consent archive capacity/clock.");
        var receipt = new CustodyAgreementReceipt(JsonSerializer.Deserialize<CustodyAgreement>(JsonSerializer.Serialize(w)), Hash(w));
        if (JsonSerializer.Serialize(new CustodyAgreementSave(1, records.Append(receipt).ToArray())).Length > 32000000) throw new ArgumentException("Custody consent archive capacity.");
        records.Add(receipt); return receipt;
    }
    public CustodyAgreementReceipt Record(string id) => Records.Single(r => r.Witness.Id == id);
    public void RequireCurrent(string id, TerritoryTransfer original, string book, string payments, string custody)
    {
        var r = Record(id); var t = Validate(r.Witness);
        if (t != original || r.Witness.Book != book || r.Witness.Payments != payments || r.Witness.Custody != custody || original.Phase != TerritoryTransferPhase.Prepared)
            throw new ArgumentException("Current agreement is not permission for changed contracts, debt, original custody or another transfer.");
    }
    public void ValidateHistory(CharterBook live, TerritoryTransferLedger physical, TributeLedger payments)
    {
        foreach (var r in records)
        {
            var original = Validate(r.Witness); var now = physical.Transfer(original.Id);
            if ((now with { Phase = TerritoryTransferPhase.Prepared, ResolvedTick = null, Destruction = null }) != original) throw new ArgumentException("Original custody journal was rewritten.");
            var before = CharterBook.Restore(r.Witness.Book); var paid = TributeLedger.Restore(r.Witness.Payments);
            foreach (var c in before.Charters)
            {
                var current = live.Charter(c.Id);
                if (current.Terms != c.Terms || current.OfferedTick != c.OfferedTick || current.PlayerSignedTick != c.PlayerSignedTick || current.NpcSignedTick != c.NpcSignedTick || c.Phase != RecordedCharterPhase.Active && current.NpcAuthority != c.NpcAuthority ||
                    c.Id == original.Charter && c.Phase != RecordedCharterPhase.Active && current.Phase == RecordedCharterPhase.Active) throw new ArgumentException("Former contracts/signatures revived or changed.");
            }
            foreach (var a in paid.Accounts)
                if (!payments.Accounts.Any(c => c.CharterId == a.CharterId && c.Terms == a.Terms && c.StartedTick == a.StartedTick && (!a.ClosedTick.HasValue || c.ClosedTick == a.ClosedTick))) throw new ArgumentException("Original liability missing.");
            foreach (var receipt in paid.Receipts)
                if (!payments.Receipts.Any(p => JsonSerializer.Serialize(p) == JsonSerializer.Serialize(receipt))) throw new ArgumentException("Original payment/performance evidence missing.");
        }
    }
    public string Export() => JsonSerializer.Serialize(new CustodyAgreementSave(1, records.ToArray()));
    public static CustodyAgreements Restore(string json)
    {
        if (json == null || json.Length > 32000000) throw new ArgumentException("Bounded custody consent archive required.");
        var save = JsonSerializer.Deserialize<CustodyAgreementSave>(json);
        if (save?.Version != 1 || save.Records == null || save.Records.Length > 128) throw new ArgumentException("Invalid custody consent archive.");
        var result = new CustodyAgreements();
        foreach (var r in save.Records) { if (r == null || r.Proof != Hash(r.Witness)) throw new ArgumentException("Invalid custody consent proof."); result.Add(r.Witness); }
        return result;
    }
}
