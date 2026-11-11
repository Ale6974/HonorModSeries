using System.Text;
using System.Text.Json;
namespace Honor.Contracts.Rules;
public enum DeferredTownPhase { Waiting, Started, Applied, Stopped, Cancelled }
public sealed record DeferredTownPerson(string Id, string OriginalJob, string ObservedPhysical);
public sealed record DeferredTownState(string Id, string Charter, string Town, string Map, string Source, string Target, string Book,
    NpcRealmDecisionEvidence Authority, string[] Before, string[] PreviousTransfers, DeferredTownPerson[] People, long Tick, DeferredTownPhase Phase, string Transfer, string Proof);
/// <summary>No job/reservation restoration permission: defer writes until a fresh independent native review is safe.</summary>
public static class DeferredTownTransfer
{
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static string Hash(DeferredTownState s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(s with { Proof = null }))));
    private static string Write(DeferredTownState s) { s = s with { Proof = Hash(s) }; string j = JsonSerializer.Serialize(s); _ = Restore(j); return j; }
    public static DeferredTownState Restore(string json)
    {
        if (json == null || json.Length > 100000) throw new ArgumentException("Bounded original deferred town intent required.");
        var s = JsonSerializer.Deserialize<DeferredTownState>(json);
        if (s == null || !Key(s.Id) || !Key(s.Charter) || !Key(s.Town) || !Key(s.Map) || !Key(s.Source) || !Key(s.Target) || s.Source == s.Target ||
            !CityStockProof.Valid(s.Book) || s.Authority?.Realm != s.Source || s.Tick < 0 || !RuntimeCompat.IsDefined(s.Phase) || s.Proof != Hash(s) ||
            s.PreviousTransfers == null || s.PreviousTransfers.Length > 1024 || s.PreviousTransfers.Any(id => !Key(id)) || s.PreviousTransfers.Distinct().Count() != s.PreviousTransfers.Length ||
            s.Before == null || s.Before.Length > 128 || s.Before.Any(p => !Key(p)) || s.Before.Distinct().Count() != s.Before.Length ||
            s.People == null || s.People.Length > 128 || s.People.Any(p => p == null || !Key(p.Id) || p.OriginalJob != null && !Key(p.OriginalJob) || !CityStockProof.Valid(p.ObservedPhysical)) ||
            !s.People.Select(p => p.Id).SequenceEqual(s.People.Select(p => p.Id).Distinct().OrderBy(p => p, StringComparer.Ordinal)) || s.Before.Any(p => !s.People.Any(a => a.Id == p)) ||
            (s.Phase == DeferredTownPhase.Applied) != (s.Transfer != null) || s.Transfer != null && !Key(s.Transfer)) throw new ArgumentException("Invalid original deferred town intent.");
        _ = NpcRealmDecision.Consent(s.Authority); return s;
    }
    public static string Prepare(string id, string charter, string town, string map, string source, string target, string book,
        NpcRealmDecisionEvidence authority, IEnumerable<string> before, IEnumerable<string> previousTransfers, IEnumerable<DeferredTownPerson> people, long tick)
    {
        if (before == null || previousTransfers == null || people == null) throw new ArgumentException("Original scope required.");
        return Write(new(id, charter, town, map, source, target, book, authority, before.Take(129).ToArray(), previousTransfers.Take(1025).ToArray(), people.Take(129).OrderBy(p => p?.Id, StringComparer.Ordinal).ToArray(), tick, DeferredTownPhase.Waiting, null, null));
    }
    public static void Review(string json, string book, NpcRealmDecisionEvidence authority, IEnumerable<string> people)
    {
        var s = Restore(json);
        if (s.Phase != DeferredTownPhase.Waiting || s.Book != book || s.Authority != authority || people == null || !people.OrderBy(p => p, StringComparer.Ordinal).SequenceEqual(s.People.Select(p => p.Id)))
            throw new ArgumentException("Scope or source authority changed. Do not rebase an old deferred intent.");
    }
    public static string Begin(string json, string book, NpcRealmDecisionEvidence authority, IEnumerable<string> people)
    { Review(json, book, authority, people); return Write(Restore(json) with { Phase = DeferredTownPhase.Started }); }
    public static string Stop(string json)
    { var s = Restore(json); if (s.Phase is not (DeferredTownPhase.Waiting or DeferredTownPhase.Started)) throw new ArgumentException("No pending intent."); return Write(s with { Phase = DeferredTownPhase.Stopped }); }
    public static string Cancel(string json)
    { var s = Restore(json); if (s.Phase != DeferredTownPhase.Waiting) throw new ArgumentException("Never cancel a started native execution as unmoved."); return Write(s with { Phase = DeferredTownPhase.Cancelled }); }
    public static string Applied(string json, TerritoryTransfer transfer)
    {
        var s = Restore(json);
        if (s.Phase != DeferredTownPhase.Started || transfer == null || transfer.Phase != TerritoryTransferPhase.Applied || !transfer.ResolvedTick.HasValue || transfer.ResolvedTick < transfer.PreparedTick || s.PreviousTransfers.Contains(transfer.Id) || transfer.Charter != s.Charter ||
            transfer.Territory != s.Town || transfer.FromOwner != s.Source || transfer.TargetOwner != s.Target || transfer.PreparedTick < s.Tick)
            throw new ArgumentException("A positive independent original applied migration receipt is required, never target ownership alone.");
        return Write(s with { Phase = DeferredTownPhase.Applied, Transfer = transfer.Id });
    }
}
