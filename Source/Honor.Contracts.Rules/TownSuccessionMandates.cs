using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record TownSuccessionMandate(TownSuccessionPlan Plan, string Losses, TerritoryObservation[] Observations,
    string PlayerParty, string NativePlayer, string ActualHigherOwner, NpcRealmDecisionEvidence Decision, ScopedNpcContractDecision Mandate);
public sealed record TownSuccessionMandateRecord(TownSuccessionMandate Witness, string Proof);
public sealed record TownSuccessionMandateSave(int Version, TownSuccessionMandateRecord[] Records);

/// <summary>Scope-specific government consent for contractual succession, never native ownership or old-contract revival.</summary>
public sealed class TownSuccessionMandates
{
    private readonly List<TownSuccessionMandateRecord> records = [];
    private static T Clone<T>(T x) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(x));
    public IReadOnlyList<TownSuccessionMandateRecord> Records => records.Select(Clone).ToArray();
    private static string Hash<T>(T x) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(x))));
    public static string Scope(TownSuccessionMandate w) => Hash(w with { Mandate = null });
    private static TownSuccessionReceipt Basic(TownSuccessionReceipt r) => r == null ? null : r with {
        GovernmentProof = null, HistoricalReturnProof = null, OwnershipReturnProof = null, OwnershipReturnScopeProof = null, OwnershipReturnGroup = null };
    private static (CharterBook Book, TownSuccessionArchive Archive) Replay(TownSuccessionMandate w, bool allowOrdinary = false)
    {
        if (w?.Plan == null || w.Plan.Replacements == null || w.Plan.Descendants == null || w.Decision == null || w.Observations == null || w.Observations.Any(o => o == null)) throw new ArgumentException("Exact succession government decision required.");
        if (allowOrdinary && w.Decision.Regime == NpcDecisionRegime.Ordinary && w.Mandate == null) NpcRealmDecision.Consent(w.Decision);
        else
        {
            if (w.Mandate == null || w.Decision.Regime is not (NpcDecisionRegime.Union or NpcDecisionRegime.Royal)) throw new ArgumentException("Exact scoped succession mandate required.");
            ScopedNpcTerminationAuthority.ValidateDecisions([w.Decision], [w.Mandate], Scope(w), w.Plan.Tick);
        }
        var before = CharterBook.Restore(w.Plan.Before); var losses = TownSuccessionArchive.Restore(w.Losses); losses.ValidateBook(before);
        if (w.Observations.Length != before.Entities.Count || w.Observations.Select(o => o.Id).Distinct().Count() != before.Entities.Count ||
            !new HashSet<string>(before.Entities.Select(e => e.Id), StringComparer.Ordinal).SetEquals(w.Observations.Select(o => o.Id))) throw new ArgumentException("Complete original succession observations required.");
        var loss = losses.Record(w.Plan.Root); var source = CharterBook.Restore(loss.Before); string parent = source.Charter(loss.Root).Terms.Liege;
        bool player = before.Entity(parent).Player;
        if (w.Decision.Realm != loss.OwnerAtLoss || (player ? parent != w.PlayerParty || before.Entity(parent).NativeOwner != w.NativePlayer || w.ActualHigherOwner != w.NativePlayer : w.ActualHigherOwner != w.Decision.Realm))
            throw new ArgumentException("Original higher party and actual loss realm differ.");
        var authority = new NpcCharterAuthority(player ? parent : w.Decision.Representative, w.Decision.Representative);
        string prefix = w.Plan.ScopePrefix;
        if (prefix == null)
        {
            string sample = w.Plan.Replacements.Values.FirstOrDefault() ?? throw new ArgumentException("Missing succession replacements.");
            int separator = sample.LastIndexOf('/'); if (separator < 1) throw new ArgumentException("Invalid succession replacement identity."); prefix = sample.Substring(0, separator);
        }
        var replay = losses.Review(w.Plan.Root, before, prefix, authority, w.Plan.Tick, w.Observations);
        if (JsonSerializer.Serialize(replay) != JsonSerializer.Serialize(w.Plan)) throw new ArgumentException("Government mandate differs from the exact original full succession plan.");
        return losses.Commit(before, replay, w.Plan.Tick, w.Observations);
    }
    public static void ValidateWitness(TownSuccessionMandate w) => Replay(w);
    public static (CharterBook Book, TownSuccessionArchive Archive) ReviewCurrentPlan(TownSuccessionMandate w) => Replay(w, true);
    private static void Pair(TownSuccessionMandateRecord record, TownSuccessionArchive current, CharterBook book, bool marker)
    {
        var w = record.Witness; var replay = Replay(w); var original = TownSuccessionArchive.Restore(w.Losses);
        foreach (var p in original.Records)
        {
            var actual = current.Record(p.Root);
            if (TownOwnershipReturns.LossDigest(actual) != TownOwnershipReturns.LossDigest(p) ||
                p.Resolution != null && JsonSerializer.Serialize(p.Resolution) != JsonSerializer.Serialize(actual.Resolution)) throw new ArgumentException("Original succession history changed.");
        }
        foreach (var p in original.ScopeLosses(w.Plan.Root))
            if (JsonSerializer.Serialize(Basic(current.Record(p.Root).Resolution)) != JsonSerializer.Serialize(Basic(replay.Archive.Record(p.Root).Resolution)))
                throw new ArgumentException("Government decision lost part of the original resolved scope.");
        var root = current.Record(w.Plan.Root);
        if ((marker ? root.Resolution.GovernmentProof != record.Proof : root.Resolution.GovernmentProof != null) || book.LastTick < w.Plan.Tick)
            throw new ArgumentException("Succession government marker missing or changed.");
    }
    public (TownSuccessionMandates History, TownSuccessionArchive Archive) Attach(TownSuccessionMandate w, TownSuccessionArchive completed, CharterBook book)
    {
        if (w?.Plan == null || records.Count >= 128 || records.Any(r => r.Witness.Plan.Root == w.Plan.Root) || records.LastOrDefault()?.Witness.Plan.Tick > w.Plan.Tick || book.Export() != w.Plan.After)
            throw new ArgumentException("Duplicate, stale or full succession government history.");
        var record = new TownSuccessionMandateRecord(Clone(w), Hash(w)); Pair(record, completed, book, false);
        var save = JsonSerializer.Deserialize<TownLossSave>(completed.Export());
        var paired = TownSuccessionArchive.Restore(JsonSerializer.Serialize(save with { Records = save.Records.Select(p => p.Root == w.Plan.Root ? p with { Resolution = p.Resolution with { GovernmentProof = record.Proof } } : p).ToArray() }));
        var next = Restore(Export()); next.records.Add(record); next.ValidatePair(paired, book);
        if (next.Export().Length > 32000000) throw new ArgumentException("Succession government history capacity.");
        return (next, paired);
    }
    public void ValidatePair(TownSuccessionArchive losses, CharterBook book)
    {
        losses.ValidateBook(book);
        foreach (var r in records) Pair(r, losses, book, true);
        foreach (var p in losses.Records.Where(p => p.Resolution?.GovernmentProof != null))
            if (records.Count(r => r.Witness.Plan.Root == p.Root && r.Proof == p.Resolution.GovernmentProof) != 1) throw new ArgumentException("Missing original government witness.");
    }
    public string Export() => JsonSerializer.Serialize(new TownSuccessionMandateSave(1, records.ToArray()));
    public static TownSuccessionMandates Restore(string json)
    {
        if (json == null || json.Length > 32000000) throw new ArgumentException("Invalid succession government size.");
        var save = JsonSerializer.Deserialize<TownSuccessionMandateSave>(json);
        if (save?.Version != 1 || save.Records == null || save.Records.Length > 128) throw new ArgumentException("Invalid succession government archive.");
        var result = new TownSuccessionMandates();
        foreach (var r in save.Records)
        {
            if (r?.Witness == null || r.Proof != Hash(r.Witness)) throw new ArgumentException("Succession government witness changed.");
            Replay(r.Witness);
            if (result.records.Any(p => p.Witness.Plan.Root == r.Witness.Plan.Root) || result.records.LastOrDefault()?.Witness.Plan.Tick > r.Witness.Plan.Tick)
                throw new ArgumentException("Succession government history changed.");
            result.records.Add(Clone(r));
        }
        return result;
    }
}
