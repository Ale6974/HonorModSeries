using System.Collections;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace Honor.Contracts.Preview;

/// <summary>Reference-only execution record. Never deep-saves, recreates or replaces a pawn.</summary>
public sealed class ResidentExecutionRecord : IExposable
{
    public string Snapshot;
    public List<Pawn> Pawns = [];
    public bool Started;
    public int Profile = 1;
    public List<ResidentSurgeryRecord> Surgery = [];
    public List<ResidentPrisonerRecord> Prisoners = [];
    public void ExposeData()
    {
        Scribe_Values.Look(ref Snapshot, "preservationV1"); Scribe_Values.Look(ref Started, "started");
        Scribe_Collections.Look(ref Pawns, "residentReferences", LookMode.Reference);
        Scribe_Values.Look(ref Profile, "profile", 1);
        Scribe_Collections.Look(ref Surgery, "surgicalCustody", LookMode.Deep);
        Scribe_Collections.Look(ref Prisoners, "prisonerCustody", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) { Pawns ??= []; Surgery ??= []; Prisoners ??= []; }
    }
    public ResidentMoveSet Set() => Profile is 1 or 2 or 3 or 4 or 5 ? ResidentMoveSet.Restore(Snapshot) : throw new ArgumentException("Unsupported resident execution profile.");
}

internal static class ResidentExecution
{
    private static readonly System.Reflection.FieldInfo Areas = AccessTools.Field(typeof(Pawn_PlayerSettings), "allowedAreas");
    internal static readonly System.Reflection.MethodInfo GcMethod = AccessTools.Method(typeof(WorldPawnGC), "GetCriticalPawnReason", [typeof(Pawn)]);
    private static string Digest(object data) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))));
    internal static void CheckTown(Settlement city)
    {
        if (city == null || city.GetType() != typeof(Settlement) || city.Destroyed || city.HasMap || city.Faction == null || city.Faction.IsPlayer ||
            !Find.WorldObjects.AllWorldObjects.Contains(city) || city.previouslyGeneratedInhabitants == null ||
            city.previouslyGeneratedInhabitants.Count is < 1 or > ResidentMoveSet.Capacity)
            throw new ArgumentException("Unavailable resident town.");
    }
    private static void Eligible(Pawn p, int profile)
    {
        if (p?.IsSlave == true) throw new ResidentMoveBlocked("RE_SlaveUnavailable");
        if (p == null || p.GetType() != typeof(Pawn) || p.kindDef == null || p.Name == null || p.ageTracker == null || p.RaceProps.body == null || !p.RaceProps.Humanlike || p.Dead || p.Destroyed || p.Spawned || !p.IsWorldPawn() ||
            p.holdingOwner != null || p.Faction == null || p.Faction.IsPlayer ||
            (profile < 4 || !p.IsPrisoner) && (p.HostFaction != null || p.IsPrisoner) || p.IsSlave ||
            PawnUtility.IsFactionLeader(p) || p.InMentalState || p.Downed || p.CurJob != null || p.carryTracker?.CarriedThing != null ||
            p.GetLord() != null || profile < 3 && p.Ideo != null || p.mechanitor != null || p.creepjoiner != null || p.IsMutant ||
            p.HasExtraHomeFaction() || p.HasExtraMiniFaction() || p.GetExtraHostFaction() != null || p.questTags?.Count > 0 ||
            (p.royalty?.AllTitlesInEffectForReading.Count ?? 0) > 0 || (p.genes?.GenesListForReading.Count ?? 0) > 0 ||
            p.health?.surgeryBills == null || profile == 1 && p.health.surgeryBills.Count != 0 || p.health.hediffSet?.hediffs == null || p.needs?.AllNeeds == null || p.relations?.DirectRelations == null ||
            p.relations.DirectRelations.Count > 1000 || p.relations.DirectRelations.Any(r => r == null || r.def == null || r.otherPawn == null) ||
            (p.needs.mood?.thoughts?.memories?.Memories.Count ?? 0) > 1000 ||
            p.needs.mood?.thoughts?.memories?.Memories.Any(m => m == null || m.def == null || m.GetType().Assembly != typeof(Pawn).Assembly) == true ||
            p.AllComps.Any(c => c == null || c.GetType().Assembly != typeof(Pawn).Assembly) ||
            p.health.hediffSet.hediffs.Any(h => h == null || h.GetType().Assembly != typeof(Pawn).Assembly ||
                h is HediffWithComps hc && hc.comps != null && hc.comps.Any(c => c == null || c.GetType().Assembly != typeof(Pawn).Assembly)) ||
            p.needs.AllNeeds.Count > 128 || p.needs.AllNeeds.Select(n => n?.def?.defName).Distinct().Count() != p.needs.AllNeeds.Count ||
            p.needs.AllNeeds.Any(n => n == null || !RuntimeCompat.IsFinite(n.CurLevel) ||
                n.GetType().Assembly != typeof(Pawn).Assembly && n.GetType().Assembly.GetName().Name != "Honor.Core"))
            throw new ArgumentException("Unsupported resident status or pending surgery.");
        if (p.IsPrisoner) ResidentPrisoners.Eligible(p, profile);
        if (p.playerSettings != null && (Areas == null || Areas.IsStatic || Areas.FieldType != typeof(Dictionary<Map, Area>) ||
            Areas.GetValue(p.playerSettings) is not IDictionary areas || areas.Count != 0 || p.playerSettings.Master != null))
            throw new ArgumentException("Unsupported persistent resident restrictions.");
    }
    internal static ResidentMoveActual Read(Pawn p, int profile = 1, ResidentSurgeryRecord surgery = null, ResidentPrisonerRecord prisoner = null)
    {
        if (profile is not (1 or 2 or 3 or 4 or 5)) throw new ArgumentException("Unknown resident profile.");
        Eligible(p, profile); var baseline = ResidentBaselineStore.Evidence(p);
        var settings = p.playerSettings;
        string preserved = Digest(new {
            baseline.Id, baseline.Health, baseline.Gear,
            Name = p.Name?.ToStringFull, Kind = p.kindDef.defName, Age = p.ageTracker.AgeBiologicalTicks,
            Relations = p.relations.DirectRelations.Select(r => new { Def = r.def.defName, Other = r.otherPawn.GetUniqueLoadID() })
                .OrderBy(r => r.Def, StringComparer.Ordinal).ThenBy(r => r.Other, StringComparer.Ordinal).ToArray(),
            Memories = p.needs.mood?.thoughts?.memories?.Memories.Select(m => new { Def = m.def.defName, Stage = m.CurStageIndex,
                Other = m.otherPawn?.GetUniqueLoadID(), m.age, m.moodPowerFactor, m.moodOffset, m.permanent, m.durationTicksOverride }).ToArray(),
            Needs = p.needs.AllNeeds.Select(n => new { Def = n.def.defName, Level = n.CurLevel }).OrderBy(n => n.Def, StringComparer.Ordinal).ToArray(),
            Settings = settings == null ? null : new { settings.joinTick, settings.followDrafted, settings.followFieldwork, settings.animalsReleased,
                settings.hostilityResponse, settings.selfTend, settings.displayOrder, settings.animalForage, settings.animalDig }
        });
        preserved = ResidentMoveProfiles.Compose(preserved, profile >= 2 ? ResidentSurgery.Proof(p, surgery) : null, profile,
            profile >= 3 ? ResidentIdeology.Proof(p) : null, profile >= 4 ? ResidentPrisoners.Proof(p, prisoner, profile) : null);
        // Faction in the shared move planner denotes the changing authority; prisoner affiliation is separately proof-bound.
        return new(baseline.Id, profile >= 4 ? ResidentPrisoners.Authority(p) : p.Faction.GetUniqueLoadID(), preserved, settings == null ? null : (int)settings.medCare);
    }
    internal static ResidentExecutionRecord Capture(Settlement city, int profile = 5)
    {
        EnsureRetention();
        CheckTown(city); var pawns = city.previouslyGeneratedInhabitants.OrderBy(p => p?.GetUniqueLoadID(), StringComparer.Ordinal).ToList();
        foreach (var pawn in pawns) Eligible(pawn, profile);
        var surgery = profile >= 2 ? pawns.Select(ResidentSurgery.Capture).ToList() : [];
        var prisoners = profile >= 4 ? pawns.Where(ResidentPrisoners.IsPrisoner).Select(p => ResidentPrisoners.Capture(p, profile)).ToList() : [];
        SurgeryOrderSet.ValidateDistinctOwners(surgery.Select(s => s.Set()));
        var rows = pawns.Select((p, i) => Read(p, profile, profile >= 2 ? surgery[i] : null, prisoners.SingleOrDefault(r => r.Pawn == p))).ToArray();
        if (rows.Any(r => r.Faction != city.Faction.GetUniqueLoadID())) throw new ArgumentException("Resident source faction mismatch.");
        var set = ResidentMoveSet.Capture(rows.Select(r => new ResidentMoveEvidence(r.Id, r.Preserved, r.MedicalCare)));
        return new() { Snapshot = set.Export(), Pawns = pawns, Profile = profile, Surgery = surgery, Prisoners = prisoners };
    }
    internal static string Proof(Settlement city, int profile = 5, Faction target = null)
    {
        try
        {
            var record = Capture(city, profile);
            if (target != null && target != city.Faction)
                foreach (var prisoner in record.Prisoners) ResidentPrisoners.NeedsRestore(prisoner, city.Faction, target, profile);
            return record.Set().Proof;
        }
        catch (ArgumentException) { return null; }
    }
    internal static string BlockReasonKey(Settlement city, Faction target)
    {
        try
        {
            var record = Capture(city);
            if (target != null && target != city.Faction)
                foreach (var prisoner in record.Prisoners) ResidentPrisoners.NeedsRestore(prisoner, city.Faction, target, record.Profile);
        }
        catch (ResidentMoveBlocked e) { return e.Key; }
        catch (ArgumentException) { }
        return "SX_ResidentsUnavailable";
    }
    internal static void ValidateReferences(ResidentExecutionRecord record, Settlement city, string proof)
    {
        if (record == null || record.Pawns == null || record.Set().Proof != proof || record.Pawns.Count != record.Set().Residents.Count ||
            record.Pawns.Any(p => p == null) || !record.Pawns.Select(p => p.GetUniqueLoadID()).SequenceEqual(record.Set().Residents.Select(r => r.Id)))
            throw new ArgumentException("Resident execution reference registry mismatch.");
        CheckTown(city);
        if (record.Surgery == null || (record.Profile == 1 ? record.Surgery.Count != 0 : record.Surgery.Count != record.Pawns.Count) ||
            record.Surgery.Where((s, i) => s == null || s.LoadFailed || s.Pawn != record.Pawns[i] || s.Detached == null || !record.Started && s.Detached.Count != 0).Any())
            throw new ArgumentException("Surgical custody registry mismatch.");
        SurgeryOrderSet.ValidateDistinctOwners(record.Surgery.Select(s => s.Set()));
        if (record.Prisoners == null || record.Profile < 4 && record.Prisoners.Count != 0 ||
            record.Prisoners.Any(r => r == null || r.Pawn == null || !record.Pawns.Contains(r.Pawn) || !r.Pawn.IsPrisoner ||
                r.Evidence().Id != r.Pawn.GetUniqueLoadID() || !record.Started && r.ResetSnapshot != null) ||
            record.Prisoners.Select(r => r.Pawn).Distinct().Count() != record.Prisoners.Count ||
            record.Profile >= 4 && record.Pawns.Count(p => p.IsPrisoner) != record.Prisoners.Count)
            throw new ArgumentException("Prisoner custody registry mismatch.");
        if (city.previouslyGeneratedInhabitants.Count != record.Pawns.Count ||
            city.previouslyGeneratedInhabitants.Distinct().Count() != record.Pawns.Count || !record.Pawns.All(city.previouslyGeneratedInhabitants.Contains))
            throw new ArgumentException("Actual saved resident set changed.");
    }
    internal static void Apply(ResidentExecutionRecord record, Settlement city, Faction source, Faction target, string proof)
    {
        EnsureRetention();
        ValidateReferences(record, city, proof);
        var set = record.Set(); var rows = ReadAll(record);
        set.Review(rows, source.GetUniqueLoadID(), target.GetUniqueLoadID(), record.Started);
        foreach (var prisoner in record.Prisoners) ResidentPrisoners.NeedsRestore(prisoner, source, target, record.Profile);
        record.Started = true; // Retain this before any native mutation; recovery proceeds forward, never raw-writes faction.
        Find.WorldPawns.gc.CancelGCPass();
        for (int i = 0; i < record.Pawns.Count; i++)
        {
            var p = record.Pawns[i];
            var surgery = record.Profile >= 2 ? record.Surgery[i] : null;
            var prisoner = record.Prisoners.SingleOrDefault(r => r.Pawn == p);
            var action = set.ActionFor(Read(p, record.Profile, surgery, prisoner), source.GetUniqueLoadID(), target.GetUniqueLoadID(), true);
            if (surgery != null && action == ResidentMoveAction.ChangeFaction) ResidentSurgery.Detach(surgery);
            if (action == ResidentMoveAction.ChangeFaction)
            {
                if (prisoner != null) ResidentPrisoners.ChangeHost(prisoner, source, target, record.Profile);
                else p.SetFaction(target);
            }
            if ((record.Profile >= 4 ? ResidentPrisoners.Authority(p) : p.Faction?.GetUniqueLoadID()) != target.GetUniqueLoadID())
                throw new ArgumentException("Native resident authority did not reach the target.");
            // Restore recorded care/orders and separately verified custody resets only; never repair health, gear, social state or unexplained ownership.
            var actual = Read(p, record.Profile, surgery, prisoner);
            var before = set.Residents[i];
            if (actual.Preserved != before.Preserved || actual.MedicalCare.HasValue != before.MedicalCare.HasValue)
                throw new ArgumentException("Native resident preservation mismatch; original intent retained.");
            if (before.MedicalCare.HasValue) p.playerSettings.medCare = (MedicalCareCategory)before.MedicalCare.Value;
            if (prisoner != null) ResidentPrisoners.Restore(prisoner, source, target, record.Profile);
            if (surgery != null) ResidentSurgery.Restore(surgery);
        }
        set.VerifyDestination(ReadAll(record), source.GetUniqueLoadID(), target.GetUniqueLoadID());
        if (record.Surgery.Any(s => s.Detached.Count != 0)) throw new ArgumentException("Surgical orders remain detached.");
    }
    internal static void Verify(ResidentExecutionRecord record, Settlement city, Faction source, Faction target, string proof)
    {
        ValidateReferences(record, city, proof);
        record.Set().VerifyDestination(ReadAll(record), source.GetUniqueLoadID(), target.GetUniqueLoadID());
        if (record.Prisoners.Any(r => ResidentPrisoners.NeedsRestore(r, source, target, record.Profile))) throw new ArgumentException("Prisoner progress not yet restored.");
        if (record.Surgery.Any(s => s.Detached.Count != 0)) throw new ArgumentException("Cannot complete with detached orders.");
    }
    private static ResidentMoveActual[] ReadAll(ResidentExecutionRecord record) => record.Pawns.Select((p, i) =>
        Read(p, record.Profile, record.Profile >= 2 ? record.Surgery[i] : null, record.Prisoners.SingleOrDefault(r => r.Pawn == p))).ToArray();
    private static void EnsureRetention()
    {
        if (GcMethod == null || GcMethod.ReturnType != typeof(string) || Harmony.GetPatchInfo(GcMethod)?.Owners.Contains("honor.series.contracts.preview") != true)
            throw new ArgumentException("Pending resident retention hook unavailable.");
    }
    internal static void ReleaseReferences(ResidentExecutionRecord record)
    {
        if (record.Surgery == null || record.Surgery.Any(s => s == null || s.Detached == null || s.Detached.Count != 0))
            throw new ArgumentException("Never discard detached surgical originals.");
        if (record.Prisoners == null || record.Prisoners.Any(r => r == null || r.ResetSnapshot != null))
            throw new ArgumentException("Never discard an unfinished native custody reset.");
        record.Pawns.Clear(); record.Surgery.Clear(); record.Prisoners.Clear();
    }
}

[HarmonyPatch]
internal static class PendingResidentRetention
{
    static bool Prepare() => ResidentExecution.GcMethod != null && ResidentExecution.GcMethod.ReturnType == typeof(string);
    static System.Reflection.MethodBase TargetMethod() => ResidentExecution.GcMethod;
    static void Postfix(Pawn pawn, ref string __result)
    {
        if (__result == null && pawn != null && !pawn.Destroyed && !pawn.Discarded &&
            Current.Game?.GetComponent<TerritoryAdministrationLedger>()?.ReservesResident(pawn) == true)
            __result = "Honor.PendingResidentTransfer";
    }
}
