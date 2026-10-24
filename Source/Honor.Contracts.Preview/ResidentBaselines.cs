using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

/// <summary>One optional diagnostic baseline. Independent of execution/consent journals.</summary>
public sealed class ResidentBaselineStore : GameComponent
{
    private string snapshot;
    public ResidentBaseline Baseline { get; private set; }
    public bool RestoreFailed { get; private set; }
    public ResidentBaselineStore(Game game) { }
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) snapshot = Baseline?.Export();
        Scribe_Values.Look(ref snapshot, "honorResidentBaselineV1");
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        try { Baseline = snapshot == null ? null : ResidentBaseline.Restore(snapshot); }
        catch (Exception e) when (e is ArgumentException or JsonException)
        { RestoreFailed = true; Log.Warning("[Honor.Contracts.Preview] Diagnostic resident baseline unavailable; original snapshot retained."); }
    }
    public override void FinalizeInit()
    {
        if (Baseline?.Tick > Find.TickManager.TicksGame) RestoreFailed = true;
    }
    public void Save(Settlement city)
    {
        if (RestoreFailed) throw new ArgumentException("Discard unavailable diagnostic baseline first.");
        var captured = Observe(city); // Complete observation before replacing the previous baseline.
        if (Baseline != null && captured.Tick < Baseline.Tick) throw new ArgumentException("Diagnostic clock reversed.");
        Baseline = captured;
    }
    public ResidentBaselineComparison Compare(Settlement city)
    {
        if (RestoreFailed || Baseline == null) throw new ArgumentException("Baseline unavailable.");
        var observed = Observe(city);
        return Baseline.Compare(observed.City, observed.Owner, observed.Tick, observed.Residents);
    }
    public void Discard() { Baseline = null; snapshot = null; RestoreFailed = false; }
    private static string Digest(object data) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))));
    private static CityStockItem[] Gear(IEnumerable<Thing> things)
    {
        if (things == null) return [];
        var entries = things.Take(10001).ToArray();
        if (entries.Length > 10000 || entries.Any(t => CityStockPreservation.StockExclusion(t) != null))
            throw new ArgumentException("Unsupported resident equipment or nested inventory.");
        return entries.Select(t => new CityStockItem(t.GetUniqueLoadID(), t.def.defName, t.Stuff?.defName, t.stackCount, t.HitPoints,
            t.TryGetQuality(out var q) ? (int)q : null)).OrderBy(t => t.Id, StringComparer.Ordinal).ToArray();
    }
    private static ResidentBaseline Observe(Settlement city)
    {
        if (city == null || city.Destroyed || city.GetType() != typeof(Settlement) || city.HasMap || city.Faction == null || city.Faction.IsPlayer ||
            !Find.WorldObjects.AllWorldObjects.Contains(city) || city.previouslyGeneratedInhabitants == null ||
            city.previouslyGeneratedInhabitants.Count > ResidentBaseline.Capacity)
            throw new ArgumentException("Only available unloaded NPC city references can be recorded.");
        var evidence = new List<ResidentEvidence>();
        foreach (var pawn in city.previouslyGeneratedInhabitants)
            evidence.Add(Evidence(pawn));
        return ResidentBaseline.Capture(city.GetUniqueLoadID(), city.Faction.GetUniqueLoadID(), Find.TickManager.TicksGame, evidence);
    }
    internal static ResidentEvidence Evidence(Pawn pawn)
    {
            if (pawn == null || pawn.GetType() != typeof(Pawn) || pawn.health?.hediffSet?.hediffs == null || pawn.health.hediffSet.hediffs.Count > 10000)
                throw new ArgumentException("Unverifiable resident reference.");
            if (pawn.health.hediffSet.hediffs.Any(h => h == null || h.def == null || !RuntimeCompat.IsFinite(h.Severity)))
                throw new ArgumentException("Unverifiable resident health.");
            var health = pawn.health.hediffSet.hediffs.Select(h => new { Id = h.GetUniqueLoadID(), Def = h.def.defName,
                Part = h.Part?.def.defName, h.Severity }).OrderBy(h => h.Id, StringComparer.Ordinal).ToArray();
            var gear = new { Equipment = Gear(pawn.equipment?.AllEquipmentListForReading), Apparel = Gear(pawn.apparel?.WornApparel),
                Inventory = Gear(pawn.inventory?.innerContainer) };
            var status = new { pawn.Dead, pawn.Destroyed, pawn.Spawned, WorldPawn = pawn.IsWorldPawn(), Held = pawn.holdingOwner != null,
                pawn.IsPrisoner, pawn.IsSlave, Leader = PawnUtility.IsFactionLeader(pawn), pawn.InMentalState, Job = pawn.CurJob?.def.defName };
            return new(pawn.GetUniqueLoadID(), pawn.Faction?.GetUniqueLoadID(), Digest(health), Digest(gear), Digest(status));
    }
}
