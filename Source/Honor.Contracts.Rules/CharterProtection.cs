using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace Honor.Contracts.Rules;

public sealed record CharterProtectionCompact(string Charter, RecordedCharter Original, string Source, string Representative, long Tick,
    NpcRealmDecisionEvidence Decision, int Goodwill, bool ReciprocalAlliance);
public sealed record CharterAidPlan(string Id, string Charter, string Source, string Representative, string Map,
    string[] Threats, string[] BeforePawns, long Tick, string BookProof, string PoliticsProof, NpcRealmDecisionEvidence Decision);
public enum CharterAidOutcome { Applying, ArrivalsObserved, ReturnedFalse, ReturnedUnverified, GenerationAcknowledged, IntentionallyRefused }
public sealed record CharterAidRefusal(string PlanProof, NpcRealmDecisionEvidence Decision, string Trait, int Policy, decimal NativePoints, long Tick);
public sealed record CharterAidGeneration(string PlanProof, long Tick, string[] Pawns)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool RepeatedReturn { get; init; }
}
public sealed record CharterAidReceipt(CharterAidPlan Plan, CharterAidOutcome Outcome = CharterAidOutcome.Applying, long? ReturnedTick = null, string[] Arrivals = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CharterAidGeneration Generation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? AcknowledgedTick { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CharterAidRefusal Refusal { get; init; }
}
public sealed record CharterProtectionSave(int Version, long LastTick, CharterProtectionCompact[] Compacts, CharterAidReceipt[] Requests);

/// <summary>Explicit supplementary protection agreement and one-shot native aid calls. An arrival is not victory or breach adjudication.</summary>
public sealed class CharterProtection
{
    public const int Capacity = 128;
    public const long Cooldown = PoliticalWorld.Day;
    private const int MaxCharacters = 32000000, OutcomeReserve = 16000000;
    private readonly Dictionary<string, CharterProtectionCompact> compacts = new(StringComparer.Ordinal);
    private readonly List<CharterAidReceipt> requests = [];
    public IReadOnlyCollection<CharterProtectionCompact> Compacts => compacts.Values.ToArray();
    public IReadOnlyList<CharterAidReceipt> Requests => requests.Select(r => r with { Plan = Clone(r.Plan), Arrivals = r.Arrivals?.ToArray(),
        Generation = r.Generation == null ? null : r.Generation with { Pawns = r.Generation.Pawns.ToArray() } }).ToArray();
    public long LastTick { get; private set; }
    public bool Pending => requests.Any(r => r.Outcome == CharterAidOutcome.Applying);
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static bool Keys(string[] ids, bool empty) => ids != null && ids.Length <= 10000 && (empty || ids.Length > 0) && ids.All(Key) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length;
    private static CharterAidPlan Clone(CharterAidPlan p) => p with { Threats = p.Threats.ToArray(), BeforePawns = p.BeforePawns.ToArray() };
    private static string PlanProof(CharterAidPlan p) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(p))));
    private static bool SameIds(string[] a, string[] b) => a.OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(b.OrderBy(s => s, StringComparer.Ordinal));
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
        if (copy.Export().Length > MaxCharacters - OutcomeReserve) throw new ArgumentException("Retain room for both native generation and final arrival receipts before starting aid.");
        return copy;
    }
    // A fresh NPC response, never a reinterpretation of an Applying/failed/unknown dispatch.
    public CharterProtection Refuse(CharterBook book, CharterAidPlan plan, NpcRealmDecisionEvidence decision, bool reciprocalAlliance,
        string trait, int policy, decimal nativePoints, long now)
    {
        Review(book, plan, decision, reciprocalAlliance, now);
        if (trait != "Honor_Vile" || policy != 1 || nativePoints <= 0 || nativePoints > 100000)
            throw new ArgumentException("Supported explicit NPC refusal policy and positive native capability evidence required.");
        var copy = Restore(Export());
        copy.requests.Add(new(Clone(plan), CharterAidOutcome.IntentionallyRefused) {
            Refusal = new(PlanProof(plan), decision, trait, policy, nativePoints, now) });
        copy.LastTick = now; copy.Bounded(); return copy;
    }
    public CharterProtection Returned(string id, bool returned, string[] arrivals, long now)
    {
        var row = requests.SingleOrDefault(r => r.Plan.Id == id) ?? throw new ArgumentException("Unknown aid call.");
        if (row.Outcome != CharterAidOutcome.Applying || !Keys(arrivals, true) || arrivals.Any(row.Plan.BeforePawns.Contains) || now < LastTick || now > int.MaxValue ||
            row.Generation != null && (row.Generation.RepeatedReturn || arrivals.Any(p => !row.Generation.Pawns.Contains(p))))
            throw new ArgumentException("Only the original normal return can record fresh observed arrivals once.");
        var copy = Restore(Export()); int i = copy.requests.FindIndex(r => r.Plan.Id == id);
        copy.requests[i] = row with { Outcome = !returned ? CharterAidOutcome.ReturnedFalse : arrivals.Length > 0 ? CharterAidOutcome.ArrivalsObserved : CharterAidOutcome.ReturnedUnverified,
            ReturnedTick = now, Arrivals = arrivals.ToArray() };
        copy.LastTick = now; copy.Bounded(); return copy;
    }
    public CharterProtection MarkRepeatedGenerationReturn(string id)
    {
        var row = requests.SingleOrDefault(r => r.Plan.Id == id) ?? throw new ArgumentException("Unknown aid call.");
        if (row.Outcome != CharterAidOutcome.Applying || row.Generation == null) throw new ArgumentException("An original pending generation receipt is required.");
        var copy = Restore(Export()); int i = copy.requests.FindIndex(r => r.Plan.Id == id);
        copy.requests[i] = row with { Generation = row.Generation with { RepeatedReturn = true } }; copy.Bounded(); return copy;
    }
    public CharterProtection RecordGeneration(string id, string source, string map, string[] pawns, long now)
    {
        var row = requests.SingleOrDefault(r => r.Plan.Id == id) ?? throw new ArgumentException("Unknown aid call.");
        if (row.Outcome != CharterAidOutcome.Applying || row.Generation != null || source != row.Plan.Source || map != row.Plan.Map ||
            !Keys(pawns, false) || pawns.Any(row.Plan.BeforePawns.Contains) || now < LastTick || now > int.MaxValue)
            throw new ArgumentException("Only the exact first normal generation return may record original new arrivals.");
        var copy = Restore(Export()); int i = copy.requests.FindIndex(r => r.Plan.Id == id);
        copy.requests[i] = row with { Generation = new(PlanProof(row.Plan), now, pawns.ToArray()) };
        copy.LastTick = now; copy.Bounded(); return copy;
    }
    public void ReviewGenerationAcknowledgement(CharterBook book, string id, string source, string map, string[] present, long now)
    {
        ValidateBook(book);
        var row = requests.SingleOrDefault(r => r.Plan.Id == id) ?? throw new ArgumentException("Unknown aid call.");
        if (row.Outcome != CharterAidOutcome.Applying || row.Generation == null || row.Generation.RepeatedReturn || source != row.Plan.Source || map != row.Plan.Map ||
            !Keys(present, false) || !SameIds(row.Generation.Pawns, present) || now < LastTick || now < book.LastTick || now > int.MaxValue)
            throw new ArgumentException("A saved positive generation return and every exact current source/map identity are required.");
    }
    public CharterProtection AcknowledgeGeneration(CharterBook book, string id, string source, string map, string[] present, long now)
    {
        ReviewGenerationAcknowledgement(book, id, source, map, present, now);
        var copy = Restore(Export()); int i = copy.requests.FindIndex(r => r.Plan.Id == id);
        copy.requests[i] = copy.requests[i] with { Outcome = CharterAidOutcome.GenerationAcknowledged, AcknowledgedTick = now, Arrivals = present.ToArray() };
        copy.LastTick = now; copy.Bounded(); return copy;
    }
    private void Bounded() { if (Export().Length > MaxCharacters) throw new ArgumentException("Protection history capacity; never evict uncertain or historical calls."); }
    public string Export() => JsonSerializer.Serialize(new CharterProtectionSave(1, LastTick, compacts.Values.ToArray(), requests.ToArray()));
    public static CharterProtection Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxCharacters) throw new ArgumentException("Invalid protection archive size.");
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
                result.requests.Count > 0 && p.Tick < (result.requests.Last().AcknowledgedTick ?? result.requests.Last().ReturnedTick ?? result.requests.Last().Plan.Tick) || !RuntimeCompat.IsDefined(r.Outcome))
                throw new ArgumentException("Invalid aid call history.");
            NpcRealmDecision.Consent(p.Decision);
            var g = r.Generation;
            if (r.Outcome == CharterAidOutcome.IntentionallyRefused)
            {
                var f = r.Refusal;
                if (f == null || f.PlanProof != PlanProof(p) || f.Decision != p.Decision || f.Trait != "Honor_Vile" || f.Policy != 1 ||
                    f.NativePoints <= 0 || f.NativePoints > 100000 || f.Tick != p.Tick || f.Tick > s.LastTick ||
                    g != null || r.ReturnedTick.HasValue || r.Arrivals != null || r.AcknowledgedTick.HasValue)
                    throw new ArgumentException("Refusal is a separate original NPC decision, not a native return or inferred inability.");
                result.requests.Add(r); continue;
            }
            if (r.Refusal != null) throw new ArgumentException("Only an original explicit refusal can carry refusal evidence.");
            if (g != null && (g.PlanProof != PlanProof(p) || !Keys(g.Pawns, false) || g.Pawns.Any(p.BeforePawns.Contains) || g.Tick < p.Tick || g.Tick > s.LastTick))
                throw new ArgumentException("Invalid original native generation receipt.");
            if (r.Outcome == CharterAidOutcome.Applying)
            {
                if (r.ReturnedTick.HasValue || r.Arrivals != null || r.AcknowledgedTick.HasValue) throw new ArgumentException("Unknown full call cannot claim a final outcome.");
            }
            else if (r.Outcome == CharterAidOutcome.GenerationAcknowledged)
            {
                if (g == null || g.RepeatedReturn || r.ReturnedTick.HasValue || !r.AcknowledgedTick.HasValue || r.AcknowledgedTick < g.Tick || r.AcknowledgedTick > s.LastTick ||
                    !Keys(r.Arrivals, false) || !SameIds(r.Arrivals, g.Pawns)) throw new ArgumentException("Generation acknowledgement must preserve the exact original partial outcome, never a full return.");
            }
            else if (g?.RepeatedReturn == true || r.AcknowledgedTick.HasValue || !r.ReturnedTick.HasValue || r.ReturnedTick < (g?.Tick ?? p.Tick) || r.ReturnedTick > s.LastTick ||
                !Keys(r.Arrivals, true) || r.Arrivals.Any(p.BeforePawns.Contains) || g != null && r.Arrivals.Any(a => !g.Pawns.Contains(a)) ||
                r.Outcome == CharterAidOutcome.ArrivalsObserved && r.Arrivals.Length == 0 || r.Outcome == CharterAidOutcome.ReturnedUnverified && r.Arrivals.Length != 0)
                throw new ArgumentException("Invalid normal final native return.");
            result.requests.Add(r);
        }
        if (result.requests.Count(r => r.Outcome == CharterAidOutcome.Applying) > 1 ||
            result.requests.Take(Math.Max(0, result.requests.Count - 1)).Any(r => r.Outcome == CharterAidOutcome.Applying)) throw new ArgumentException("Uncertain aid cannot be replayed.");
        return result;
    }
}
