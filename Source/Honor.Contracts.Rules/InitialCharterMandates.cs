using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record InitialCharterMandate(string Charter, string Before, string After, string Payments,
    TerritoryObservation[] Observations, long Tick, bool Final, NpcRealmDecisionEvidence Decision, ScopedNpcContractDecision Mandate);
public sealed record InitialCharterMandateSave(int Version, InitialCharterMandate[] Records);

/// <summary>Current collective/royal decisions for accepted proposals and final signatures, never future asset authority.</summary>
public sealed class InitialCharterMandates
{
    private readonly List<InitialCharterMandate> records = [];
    private static InitialCharterMandate Copy(InitialCharterMandate r) => r with { Observations = r.Observations.ToArray(), Mandate = r.Mandate with { Ballots = r.Mandate.Ballots.ToArray() } };
    public IReadOnlyList<InitialCharterMandate> Records => records.Select(Copy).ToArray();
    public static string Scope(InitialCharterMandate r) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r with { Mandate = null }))));
    public static string Marker(InitialCharterMandate r) => r.Charter + ":" + r.Final + ":" + RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r))));
    private static void Validate(InitialCharterMandate r)
    {
        if (r?.Decision == null || r.Mandate == null || r.Observations == null || r.Observations.Any(o => o == null) ||
            r.Decision.Regime is not (NpcDecisionRegime.Union or NpcDecisionRegime.Royal)) throw new ArgumentException("Exact nonordinary initial charter decision required.");
        ScopedNpcTerminationAuthority.ValidateDecisions([r.Decision], [r.Mandate], Scope(r), r.Tick);
        var before = CharterBook.Restore(r.Before); var after = CharterBook.Restore(r.After); var c = after.Charter(r.Charter);
        if (r.Observations.Length != before.Entities.Count || r.Observations.Select(o => o.Id).Distinct().Count() != before.Entities.Count ||
            !new HashSet<string>(before.Entities.Select(e => e.Id), StringComparer.Ordinal).SetEquals(r.Observations.Select(o => o.Id)))
            throw new ArgumentException("Complete original proposal observations required.");
        bool playerSubject = after.Entity(c.Terms.Subject).Player;
        string player = playerSubject ? c.Terms.Subject : c.Terms.Liege, npc = playerSubject ? c.Terms.Liege : c.Terms.Subject;
        if (after.Entity(npc).Player || !after.Entity(player).Player || c.NpcAuthority != null ||
            !r.Observations.Any(o => o.Id == npc && o.Exists && !o.Player && o.NativeOwner == r.Decision.Realm))
            throw new ArgumentException("Decision does not bind the actual original NPC party.");
        var payments = TributeLedger.Restore(r.Payments);
        var replay = r.Final ? CharterOffers.Confirm(before, r.Charter, r.Mandate.Goodwill, r.Tick, r.Observations, payments) :
            CharterOffers.Propose(before, r.Charter, player, npc, playerSubject, CharterOffers.Draft(c.Terms), r.Mandate.Goodwill, r.Tick, r.Observations, payments);
        if (!replay.Assessment.Accepted || replay.Book.Export() != r.After) throw new ArgumentException("Original proposal/signature output differs.");
    }
    public InitialCharterMandates Append(InitialCharterMandate r)
    {
        Validate(r); var next = Restore(Export());
        if (records.Count >= 128 || records.Any(p => p.Charter == r.Charter && p.Final == r.Final) || records.LastOrDefault()?.Tick > r.Tick)
            throw new ArgumentException("Duplicate, future or full initial charter decision history.");
        next.records.Add(Copy(r));
        if (next.Export().Length > 32000000) throw new ArgumentException("Initial charter decision capacity.");
        return next;
    }
    public void ValidateBook(CharterBook book)
    {
        foreach (var r in records)
        {
            var old = CharterBook.Restore(r.After).Charter(r.Charter); var c = book.Charter(r.Charter);
            if (c.Terms != old.Terms || c.NpcSignedTick != old.NpcSignedTick || c.OfferedTick != old.OfferedTick || c.ExpiresTick != old.ExpiresTick ||
                r.Final && c.PlayerSignedTick != old.PlayerSignedTick || book.LastTick < r.Tick)
                throw new ArgumentException("Original initial charter mandate history changed.");
        }
    }
    public string Export() => JsonSerializer.Serialize(new InitialCharterMandateSave(1, records.ToArray()));
    public void ValidateAcceptances(CharterAcceptances acceptances)
    {
        foreach (var r in records.Where(r => r.Final))
        {
            var a = acceptances.Find(r.Charter);
            if (a == null || a.SignedTick != r.Tick || a.NpcFaction != r.Decision.Realm || a.Representative != r.Decision.Representative)
                throw new ArgumentException("Final government mandate requires the exact actual final acceptance audit.");
        }
    }
    public static InitialCharterMandates Restore(string json)
    {
        if (json == null || json.Length > 32000000) throw new ArgumentException("Invalid initial charter mandate size.");
        var save = JsonSerializer.Deserialize<InitialCharterMandateSave>(json);
        if (save?.Version != 1 || save.Records == null || save.Records.Length > 128) throw new ArgumentException("Invalid initial charter mandate archive.");
        var result = new InitialCharterMandates();
        foreach (var r in save.Records)
        {
            Validate(r);
            if (result.records.Any(p => p.Charter == r.Charter && p.Final == r.Final) || result.records.LastOrDefault()?.Tick > r.Tick) throw new ArgumentException("Initial charter decision sequence changed.");
            result.records.Add(Copy(r));
        }
        return result;
    }
}
