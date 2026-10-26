using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record CharterProtectionCompact(string Charter, RecordedCharter Original, string Source, string Representative, long Tick,
    NpcRealmDecisionEvidence Decision, int Goodwill, bool ReciprocalAlliance);
public sealed record CharterAidPlan(string Id, string Charter, string Source, string Representative, string Map,
    string[] Threats, string[] BeforePawns, long Tick, string BookProof, string PoliticsProof, NpcRealmDecisionEvidence Decision);
public enum CharterAidOutcome { Applying, ArrivalsObserved, ReturnedFalse, ReturnedUnverified }
public sealed record CharterAidReceipt(CharterAidPlan Plan, CharterAidOutcome Outcome = CharterAidOutcome.Applying, long? ReturnedTick = null, string[] Arrivals = null);
public sealed record CharterProtectionSave(int Version, long LastTick, CharterProtectionCompact[] Compacts, CharterAidReceipt[] Requests);

/// <summary>Explicit supplementary protection agreement and one-shot native aid calls. An arrival is not victory or breach adjudication.</summary>
public sealed class CharterProtection
{
    public const int Capacity = 128;
    public const long Cooldown = PoliticalWorld.Day;
    private readonly Dictionary<string, CharterProtectionCompact> compacts = new(StringComparer.Ordinal);
    private readonly List<CharterAidReceipt> requests = [];
    public IReadOnlyCollection<CharterProtectionCompact> Compacts => compacts.Values.ToArray();
    public IReadOnlyList<CharterAidReceipt> Requests => requests.Select(r => r with { Plan = Clone(r.Plan), Arrivals = r.Arrivals?.ToArray() }).ToArray();
    public long LastTick { get; private set; }
    public bool Pending => requests.Any(r => r.Outcome == CharterAidOutcome.Applying);
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static bool Keys(string[] ids, bool empty) => ids != null && ids.Length <= 10000 && (empty || ids.Length > 0) && ids.All(Key) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length;
    private static CharterAidPlan Clone(CharterAidPlan p) => p with { Threats = p.Threats.ToArray(), BeforePawns = p.BeforePawns.ToArray() };
    private static bool SameOriginal(RecordedCharter current, RecordedCharter original) => current with {
        Phase = original.Phase, PlayerReleaseTick = original.PlayerReleaseTick, NpcReleaseTick = original.NpcReleaseTick,
        TerritoryLostTick = original.TerritoryLostTick, Replacement = original.Replacement, PoliticalLoss = original.PoliticalLoss } == original;
    private static RecordedCharter Active(CharterBook book, string id)
    {
        var c = book.Charter(id);
        if (c.Phase != RecordedCharterPhase.Active || c.NpcAuthority != null || c.PlayerReleaseTick.HasValue || c.NpcReleaseTick.HasValue ||
            !book.Entity(c.Terms.Subject).Player || book.Entity(c.Terms.Liege).Player)
            throw new ArgumentException("An active unreleased player-subject charter is required.");
        return c;
    }
    public void ValidateBook(CharterBook book)
    {
        foreach (var a in compacts.Values)
        {
            var c = book.Charter(a.Charter);
            if (!SameOriginal(c, a.Original) || book.Entity(c.Terms.Liege).NativeOwner != a.Source || !book.Entity(c.Terms.Subject).Player)
                throw new ArgumentException("Protection agreement differs from its original signed charter.");
            long? ended = c.Phase == RecordedCharterPhase.Released ? Math.Max(c.PlayerReleaseTick.Value, c.NpcReleaseTick.Value) : c.TerritoryLostTick ?? c.Replacement?.Tick;
            if (ended.HasValue && (a.Tick > ended.Value || requests.Any(r => r.Plan.Charter == c.Id && r.Plan.Tick > ended.Value)))
                throw new ArgumentException("Protection cannot be accepted or requested after the actual original charter ended.");
        }
    }
    public CharterProtection Agree(CharterBook book, string charter, NpcRealmDecisionEvidence decision, int goodwill, bool reciprocalAlliance, long now)
    {
        ValidateBook(book); var c = Active(book, charter); NpcRealmDecision.Consent(decision);
        if (Pending || now < LastTick || now < book.LastTick || now > int.MaxValue || goodwill is < 75 or > 100 || !reciprocalAlliance ||
            decision.Realm != book.Entity(c.Terms.Liege).NativeOwner || compacts.ContainsKey(charter) || compacts.Count >= Capacity)
            throw new ArgumentException("Fresh ordinary allied NPC and separate player acceptance are required.");
        var copy = Restore(Export()); copy.compacts.Add(charter, new(charter, c, decision.Realm, decision.Representative, now, decision, goodwill, reciprocalAlliance)); copy.LastTick = now;
        copy.ValidateBook(book); copy.Bounded(); return copy;
    }
    public void Review(CharterBook book, CharterAidPlan plan, NpcRealmDecisionEvidence decision, bool reciprocalAlliance, long now)
    {
        ValidateBook(book); RuntimeCompat.NotNull(plan); NpcRealmDecision.Consent(decision); var c = Active(book, plan.Charter);
        if (!compacts.TryGetValue(c.Id, out var agreement) || Pending || !reciprocalAlliance || decision.Realm != agreement.Source ||
            plan.Source != decision.Realm || plan.Representative != decision.Representative || plan.Decision != decision || !Key(plan.Id) || !Key(plan.Map) ||
            !Keys(plan.Threats, false) || !Keys(plan.BeforePawns, true) || !plan.Threats.All(plan.BeforePawns.Contains) ||
            !CityStockProof.Valid(plan.BookProof) || !CityStockProof.Valid(plan.PoliticsProof) || now < LastTick || now < book.LastTick || now < agreement.Tick ||
            now > int.MaxValue || now != plan.Tick || requests.Any(r => r.Plan.Id == plan.Id || r.Plan.Source == plan.Source && now - r.Plan.Tick < Cooldown) || requests.Count >= Capacity)
            throw new ArgumentException("Review a fresh actual shared-enemy defense request, once per source per day.");
    }
    public CharterProtection Begin(CharterBook book, CharterAidPlan plan, NpcRealmDecisionEvidence decision, bool reciprocalAlliance, long now)
    {
        Review(book, plan, decision, reciprocalAlliance, now); var copy = Restore(Export()); copy.requests.Add(new(Clone(plan))); copy.LastTick = now;
        if (copy.Export().Length > 8000000) throw new ArgumentException("Retain room for the one native arrival receipt before starting aid.");
        return copy;
    }
    public CharterProtection Returned(string id, bool returned, string[] arrivals, long now)
    {
        var row = requests.SingleOrDefault(r => r.Plan.Id == id) ?? throw new ArgumentException("Unknown aid call.");
        if (row.Outcome != CharterAidOutcome.Applying || !Keys(arrivals, true) || arrivals.Any(row.Plan.BeforePawns.Contains) || now < LastTick || now > int.MaxValue)
            throw new ArgumentException("Only the original normal return can record fresh observed arrivals once.");
        var copy = Restore(Export()); int i = copy.requests.FindIndex(r => r.Plan.Id == id);
        copy.requests[i] = row with { Outcome = !returned ? CharterAidOutcome.ReturnedFalse : arrivals.Length > 0 ? CharterAidOutcome.ArrivalsObserved : CharterAidOutcome.ReturnedUnverified,
            ReturnedTick = now, Arrivals = arrivals.ToArray() };
        copy.LastTick = now; copy.Bounded(); return copy;
    }
    private void Bounded() { if (Export().Length > 16000000) throw new ArgumentException("Protection history capacity; never evict uncertain or historical calls."); }
    public string Export() => JsonSerializer.Serialize(new CharterProtectionSave(1, LastTick, compacts.Values.ToArray(), requests.ToArray()));
    public static CharterProtection Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 16000000) throw new ArgumentException("Invalid protection archive size.");
        var s = JsonSerializer.Deserialize<CharterProtectionSave>(json);
        if (s == null || s.Version != 1 || s.LastTick < 0 || s.LastTick > int.MaxValue || s.Compacts == null || s.Requests == null ||
            s.Compacts.Length > Capacity || s.Requests.Length > Capacity) throw new ArgumentException("Invalid protection archive.");
        var result = new CharterProtection { LastTick = s.LastTick };
        foreach (var a in s.Compacts)
        {
            if (a?.Original == null || !Key(a.Charter) || a.Original.Id != a.Charter || !Key(a.Source) || !Key(a.Representative) ||
                a.Original.Phase != RecordedCharterPhase.Active || a.Original.NpcAuthority != null || !a.Original.PlayerSignedTick.HasValue || !a.Original.NpcSignedTick.HasValue ||
                a.Original.PlayerReleaseTick.HasValue || a.Original.NpcReleaseTick.HasValue || a.Tick < Math.Max(a.Original.PlayerSignedTick.Value, a.Original.NpcSignedTick.Value) ||
                a.Tick > s.LastTick || a.Decision == null || a.Decision.Realm != a.Source || a.Decision.Representative != a.Representative ||
                a.Goodwill is < 75 or > 100 || !a.ReciprocalAlliance || result.compacts.ContainsKey(a.Charter)) throw new ArgumentException("Invalid protection agreement.");
            NpcRealmDecision.Consent(a.Decision);
            result.compacts.Add(a.Charter, a);
        }
        foreach (var r in s.Requests)
        {
            var p = r?.Plan;
            if (p == null || !Key(p.Id) || !Key(p.Map) || !Key(p.Representative) || !Keys(p.Threats, false) || !Keys(p.BeforePawns, true) ||
                !p.Threats.All(p.BeforePawns.Contains) || !CityStockProof.Valid(p.BookProof) || !CityStockProof.Valid(p.PoliticsProof) ||
                !result.compacts.TryGetValue(p.Charter, out var agreement) || p.Source != agreement.Source || p.Decision == null || p.Decision.Realm != p.Source ||
                p.Decision.Representative != p.Representative || p.Tick < agreement.Tick || p.Tick > s.LastTick ||
                result.requests.Any(x => x.Plan.Id == p.Id || x.Plan.Source == p.Source && p.Tick - x.Plan.Tick < Cooldown) ||
                result.requests.Count > 0 && p.Tick < (result.requests.Last().ReturnedTick ?? result.requests.Last().Plan.Tick) || !RuntimeCompat.IsDefined(r.Outcome) ||
                (r.Outcome == CharterAidOutcome.Applying ? r.ReturnedTick.HasValue || r.Arrivals != null :
                    !r.ReturnedTick.HasValue || r.ReturnedTick < p.Tick || r.ReturnedTick > s.LastTick || !Keys(r.Arrivals, true) || r.Arrivals.Any(p.BeforePawns.Contains) ||
                    r.Outcome == CharterAidOutcome.ArrivalsObserved && r.Arrivals.Length == 0 || r.Outcome == CharterAidOutcome.ReturnedUnverified && r.Arrivals.Length != 0))
                throw new ArgumentException("Invalid aid call history.");
            NpcRealmDecision.Consent(p.Decision);
            result.requests.Add(r);
        }
        if (result.requests.Count(r => r.Outcome == CharterAidOutcome.Applying) > 1 ||
            result.requests.Take(Math.Max(0, result.requests.Count - 1)).Any(r => r.Outcome == CharterAidOutcome.Applying)) throw new ArgumentException("Uncertain aid cannot be replayed.");
        return result;
    }
}
