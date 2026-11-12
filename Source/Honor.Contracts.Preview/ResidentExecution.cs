using System.Collections;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse.AI;
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
    // Null identifies the original legacy execution format; never backfill an interrupted old call.
    public List<string> CareJournal;
    public bool CareJournalV1;
    public string Census;
    public List<string> AuthorityJournal;
    public List<ResidentAreaRecord> AreaRecords;
    public string LoadedMapScope;
    public Map NativeMap;
    public string MapBindings;
    public List<Job> GroundRestJobs;
    public string RoyalRightsDocument;
    public List<Faction> RoyalFactions;
    public List<Pawn> RoyalHeirs;
    public List<Ability> RoyalAbilities;
    public void ExposeData()
    {
        Scribe_Values.Look(ref Snapshot, "preservationV1"); Scribe_Values.Look(ref Started, "started");
        Scribe_Collections.Look(ref Pawns, "residentReferences", LookMode.Reference);
        Scribe_Values.Look(ref Profile, "profile", 1);
        Scribe_Collections.Look(ref Surgery, "surgicalCustody", LookMode.Deep);
        Scribe_Collections.Look(ref Prisoners, "prisonerCustody", LookMode.Deep);
        Scribe_Collections.Look(ref CareJournal, "residentCareJournalV1", LookMode.Value);
        Scribe_Values.Look(ref CareJournalV1, "residentCareJournalEnabledV1", false);
        Scribe_Values.Look(ref Census, "residentCensusV1");
        Scribe_Collections.Look(ref AuthorityJournal, "residentAuthorityJournalV1", LookMode.Value);
        Scribe_Collections.Look(ref AreaRecords, "residentOriginalAreaReferencesV1", LookMode.Deep);
        Scribe_Values.Look(ref LoadedMapScope, "residentOriginalNeutralMapV1");
        Scribe_References.Look(ref NativeMap, "originalNativeResidentMapV1");
        Scribe_Values.Look(ref MapBindings, "originalNativeResidentBindingsV1");
        Scribe_Collections.Look(ref GroundRestJobs, "originalNativeGroundRestJobsV1", LookMode.Reference);
        Scribe_Values.Look(ref RoyalRightsDocument, "originalPrivateRoyalRightsV2");
        Scribe_Collections.Look(ref RoyalFactions, "originalRoyalFactionsV2", LookMode.Reference);
        Scribe_Collections.Look(ref RoyalHeirs, "originalRoyalHeirsV2", LookMode.Reference);
        Scribe_Collections.Look(ref RoyalAbilities, "originalRoyalAbilitiesV2", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) { Pawns ??= []; Surgery ??= []; Prisoners ??= []; }
    }
    public ResidentMoveSet Set() => Profile is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 ? ResidentMoveSet.Restore(Snapshot) : throw new ArgumentException("Unsupported resident execution profile.");
    public void PrepareCareJournal(Faction source, Faction target)
    {
        if (Started || CareJournalV1 || CareJournal != null || source == null || target == null) throw new ArgumentException("New resident preparation required.");
        CareJournal = Set().Residents.Select(r => ResidentCareJournal.Prepare(r.Id, source.GetUniqueLoadID(), target.GetUniqueLoadID(), r.MedicalCare).Export()).ToList();
        CareJournalV1 = true;
    }
    public void PrepareCareJournal(Faction source, Faction target, Settlement city)
    {
        if (Census != null || AuthorityJournal != null || city?.Faction != source) throw new ArgumentException("Original resident town required.");
        ResidentExecution.ValidateReferences(this, city, Set().Proof);
        if (Current.Game == null || Pawns.Any(p => Current.Game.GetComponent<TerritoryAdministrationLedger>().ReservesResident(p) ||
            Current.Game.GetComponent<LeaderJoinLedger>().ReservesResident(p) || Current.Game.GetComponent<LeaderRelocationLedger>().ReservesResident(p)))
            throw new ArgumentException("Resident is already reserved by another original execution.");
        string census = ResidentTransferCensus.Prepare("territory/" + city.GetUniqueLoadID(), source.GetUniqueLoadID(), target.GetUniqueLoadID(), Profile, Set());
        var calls = Set().Residents.Select(r => JsonSerializer.Serialize(new ResidentAuthorityState(r.Id))).ToList();
        PrepareCareJournal(source, target); Census = census; AuthorityJournal = calls;
    }
}

internal static class ResidentExecution
{
    private static readonly System.Reflection.FieldInfo Areas = AccessTools.Field(typeof(Pawn_PlayerSettings), "allowedAreas");
    internal static readonly System.Reflection.MethodInfo GcMethod = AccessTools.Method(typeof(WorldPawnGC), "GetCriticalPawnReason", [typeof(Pawn)]);
    private static string Digest(object data) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))));
    private static string Mobility(Pawn p, string previous, string status)
    {
        if (p.health?.GetType() != typeof(Pawn_HealthTracker)) throw new ResidentMoveBlocked("RE_MobilityUnavailable");
        foreach (var method in new[] {
            AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.Downed)),
            AccessTools.PropertyGetter(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Downed)),
            AccessTools.Method(typeof(Pawn), nameof(Pawn.SetFaction), [typeof(Faction), typeof(Pawn)]),
            AccessTools.Method(typeof(Pawn), nameof(Pawn.ClearMind_NewTemp), [typeof(bool), typeof(bool), typeof(bool), typeof(bool)]) })
            if (method == null || Harmony.GetPatchInfo(method)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_MobilityUnavailable");
        return ResidentMoveProfiles.BindMobility(previous, status, p.Downed);
    }
    internal static NativeMapResident MapActor(Pawn p, ResidentExecutionRecord record, bool royal = false, bool groundRest = false)
    {
        if (record == null)
        {
            if (p.Faction != p.Map?.Parent?.Faction) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
            var surgery = ResidentSurgery.Capture(p); var area = ResidentAreaRecords.Capture(p);
            var fresh = ReadWithAreas(p, groundRest ? 14 : royal ? 13 : 11, surgery, null, area, true);
            return new(fresh.Id, fresh.Preserved);
        }
        if (record.Profile is not (11 or 13 or 14) || record.NativeMap != p.Map || record.Census == null) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        var census = ResidentTransferCensus.Restore(record.Census);
        ValidateCensus(record, census.Proof, census.Town, census.Source, census.Target);
        ResidentTransferCensus.RequireRecoverable(record.AuthorityJournal, record.CareJournal);
        int i = record.Pawns.IndexOf(p);
        if (i < 0) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        var call = ResidentTransferCensus.Call(record.AuthorityJournal[i]);
        if (p.Faction?.GetUniqueLoadID() != (call.Started ? census.Target : census.Source)) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        var actual = ReadWithAreas(p, record.Profile, record.Surgery[i], null, record.AreaRecords[i]);
        if (actual.Preserved != record.Set().Residents[i].Preserved) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        return new(actual.Id, actual.Preserved);
    }
    internal static void CheckTown(Settlement city)
        => CheckTown(city, null);
    private static void CheckTown(Settlement city, string loaded)
    {
        if (city == null || city.GetType() != typeof(Settlement) || city.Destroyed || city.HasMap && loaded == null || city.Faction == null || city.Faction.IsPlayer ||
            !Find.WorldObjects.AllWorldObjects.Contains(city) || city.previouslyGeneratedInhabitants == null ||
            city.previouslyGeneratedInhabitants.Count is < 1 or > ResidentMoveSet.Capacity)
            throw new ArgumentException("Unavailable resident town.");
        if (loaded != null) LoadedMapRecords.VerifyScope(city, loaded);
    }
    private static void Eligible(Pawn p, int profile, bool retainedLeader = false, bool enrollment = false)
    {
        if (profile == 14) { if (!NativeTransferBodies.WorkObservationBodies) throw new ArgumentException("Unreviewed native ongoing-work bodies."); _ = ResidentGroundRest.Capture(p); }
        if (profile == 13 && !NativeTransferBodies.RoyalCustodyBodies) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        if (profile == 12 && (!NativeTransferBodies.RoyalCustodyBodies || p == null || p.Spawned || p.IsPrisoner || p.Downed)) throw new ArgumentException("New world royal clinical scope only.");
        if (p?.IsSlave == true) throw new ResidentMoveBlocked("RE_SlaveUnavailable");
        if (!retainedLeader && p != null && PawnUtility.IsFactionLeader(p)) throw new ResidentMoveBlocked("LR_LeaderBlocked");
        if (p == null || p.GetType() != typeof(Pawn) || p.kindDef == null || p.Name == null || p.ageTracker == null || p.RaceProps.body == null || !p.RaceProps.Humanlike || p.Dead || p.Destroyed || (profile is 11 or 13 or 14 && p.Spawned ? p.Map == null : p.Spawned || !p.IsWorldPawn()) ||
            (p.holdingOwner != null && (profile is not (11 or 13 or 14) || !p.Spawned || p.holdingOwner != p.Map.GetDirectlyHeldThings())) || p.Faction == null || p.Faction.IsPlayer ||
            (profile < 4 || !p.IsPrisoner) && (p.HostFaction != null || p.IsPrisoner) || p.IsSlave ||
            (!retainedLeader && PawnUtility.IsFactionLeader(p)) || p.InMentalState || p.Downed && (profile < 8 || p.IsPrisoner || retainedLeader) || profile == 9 && p.IsPrisoner || p.CurJob != null && profile != 14 || p.carryTracker?.CarriedThing != null ||
            p.GetLord() != null || profile < 3 && p.Ideo != null || p.mechanitor != null || p.creepjoiner != null || p.IsMutant ||
            p.HasExtraHomeFaction() || p.HasExtraMiniFaction() || p.GetExtraHostFaction() != null || p.questTags?.Count > 0 ||
            profile is not (12 or 13) && (p.royalty?.AllTitlesInEffectForReading.Count ?? 0) > 0 || profile < 7 && (p.genes?.GenesListForReading.Count ?? 0) > 0 ||
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
        if (profile is 11 or 13 or 14)
        {
            if (p.IsPrisoner) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
            if (p.Spawned) { if (enrollment) ResidentMapActors.RequireEnrollment(p); else ResidentMapActors.Require(p, profile == 14); }
        }
        if (p.IsPrisoner) ResidentPrisoners.Eligible(p, profile);
        if (p.playerSettings != null && (Areas == null || Areas.IsStatic || Areas.FieldType != typeof(Dictionary<Map, Area>) ||
            Areas.GetValue(p.playerSettings) is not IDictionary areas || (profile != 9 && profile != 10 && profile is not (11 or 13 or 14) || profile == 10 && p.IsPrisoner) && areas.Count != 0 || p.playerSettings.Master != null))
            throw new ArgumentException("Unsupported persistent resident restrictions.");
    }
    internal static ResidentMoveActual Read(Pawn p, int profile = 1, ResidentSurgeryRecord surgery = null, ResidentPrisonerRecord prisoner = null)
        => ReadInternal(p, profile, surgery, prisoner, false);
    private static ResidentMoveActual ReadWithAreas(Pawn p, int profile, ResidentSurgeryRecord surgery, ResidentPrisonerRecord prisoner, ResidentAreaRecord area, bool capture = false)
        => ReadInternal(p, profile, surgery, prisoner, false, area, capture);
    internal static ResidentMoveActual ReadRetainedLeader(Pawn p)
        => ReadRetainedLeader(p, 5);
    internal static ResidentMoveActual ReadRetainedLeader(Pawn p, int profile)
        => ReadRetainedLeader(p, profile, null);
    internal static ResidentMoveActual ReadRetainedLeader(Pawn p, int profile, ResidentSurgeryRecord surgery)
    {
        if (p == null || p.Faction == null || p.Faction.leader != p || p.IsPrisoner || p.IsSlave || p.HostFaction != null ||
            !PawnUtility.IsFactionLeader(p) || Find.FactionManager.AllFactionsListForReading.Any(f => f != p.Faction && f.leader == p))
            throw new ArgumentException("Not the exact ordinary retained faction leader.");
        if (profile is not (5 or 7)) throw new ArgumentException("Unsupported leader profile.");
        return ReadInternal(p, profile, surgery, null, true);
    }
    // Dedicated separately consented join journal verifies office/succession. Ordinary resident leader guards stay unchanged.
    internal static ResidentMoveActual ReadJoiningLeader(Pawn p) => ReadInternal(p, 5, null, null, true);
    internal static ResidentMoveActual ReadJoiningLeader(Pawn p, int profile)
        => ReadJoiningLeader(p, profile, null);
    internal static ResidentMoveActual ReadJoiningLeader(Pawn p, int profile, ResidentSurgeryRecord surgery)
        => profile is 5 or 7 ? ReadInternal(p, profile, surgery, null, true) : throw new ArgumentException("Unsupported joining leader profile.");
    private static ResidentMoveActual ReadInternal(Pawn p, int profile, ResidentSurgeryRecord surgery, ResidentPrisonerRecord prisoner, bool retainedLeader, ResidentAreaRecord area = null, bool captureAreas = false, bool enrollment = false)
    {
        if (profile is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14)) throw new ArgumentException("Unknown resident profile.");
        Eligible(p, profile, retainedLeader, enrollment); var baseline = ResidentBaselineStore.Evidence(p);
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
        preserved = ResidentMoveProfiles.Compose(preserved, profile >= 2 ? ResidentSurgery.Proof(p, surgery) : null, Math.Min(profile, 7),
            profile >= 3 ? ResidentIdeology.Proof(p) : null, profile >= 4 ? ResidentPrisoners.Proof(p, prisoner, profile) : null,
            profile >= 7 ? ResidentGenes.Proof(p) : null);
        if (profile >= 8) preserved = Mobility(p, preserved, baseline.Status);
        if (profile is 9 or 10 or 11 or 13 or 14)
        {
            if (area != null)
            {
                if (area.Previous == null && captureAreas) area.Previous = preserved;
                else if (area.Previous != preserved) throw new ResidentMoveBlocked("RE_AreasUnavailable");
            }
            preserved = ResidentMoveProfiles.BindAreas(preserved, ResidentAreaRecords.Proof(p, area));
            if (profile == 10) preserved = ResidentMoveProfiles.BindMixedAreas(preserved);
            if (profile is 11 or 13 or 14) preserved = profile == 13 ? ResidentMapped.BindRoyal(preserved, ResidentMapActors.Binding(p)) : ResidentMapped.Bind(preserved, ResidentMapActors.Binding(p));
        }
        if (profile == 14) preserved = GroundRest.Bind(preserved, ResidentGroundRest.Capture(p));
        // Faction in the shared move planner denotes the changing authority; prisoner affiliation is separately proof-bound.
        return new(baseline.Id, profile >= 4 ? ResidentPrisoners.Authority(p) : p.Faction.GetUniqueLoadID(), preserved, settings == null ? null : (int)settings.medCare);
    }
    // Separate affirmative enrollment decision; ordinary transfer capture still requires the original city alias.
    internal static ResidentMoveActual ReadEnrollment(Pawn pawn)
    {
        if (!NativeTransferBodies.ExtendedReadBodies) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        var surgery = ResidentSurgery.Capture(pawn); var area = ResidentAreaRecords.Capture(pawn);
        return ReadInternal(pawn, 11, surgery, null, false, area, true, true);
    }
    internal static ResidentExecutionRecord Capture(Settlement city, int profile = 7)
        => CaptureCore(city, profile, null);
    private static ResidentExecutionRecord CaptureCore(Settlement city, int profile, string loaded)
    {
        EnsureRetention();
        if (profile == 14 && (loaded == null || LoadedMapPreservation.Restore(loaded).GroundRestV1 == null)) throw new ArgumentException("Original native rest map required.");
        if (profile == 13 && (!NativeTransferBodies.RoyalCustodyBodies || loaded == null || LoadedMapPreservation.Restore(loaded).RoyalMappedV1 != 1)) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        if (profile is 11 or 13 or 14 && (loaded == null || city?.Map == null || LoadedMapPreservation.Restore(loaded).NativeResidents == null)) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        CheckTown(city, loaded); var pawns = city.previouslyGeneratedInhabitants.OrderBy(p => p?.GetUniqueLoadID(), StringComparer.Ordinal).ToList();
        foreach (var pawn in pawns) Eligible(pawn, profile);
        var surgery = profile >= 2 ? pawns.Select(ResidentSurgery.Capture).ToList() : [];
        var prisoners = profile >= 4 ? pawns.Where(ResidentPrisoners.IsPrisoner).Select(p => ResidentPrisoners.Capture(p, profile)).ToList() : [];
        var areaRecords = profile is 9 or 10 or 11 or 13 or 14 ? pawns.Select(ResidentAreaRecords.Capture).ToList() : null;
        SurgeryOrderSet.ValidateDistinctOwners(surgery.Select(s => s.Set()));
        var rows = pawns.Select((p, i) => ReadWithAreas(p, profile, profile >= 2 ? surgery[i] : null, prisoners.SingleOrDefault(r => r.Pawn == p), areaRecords?[i], true)).ToArray();
        if (rows.Any(r => r.Faction != city.Faction.GetUniqueLoadID())) throw new ArgumentException("Resident source faction mismatch.");
        var set = ResidentMoveSet.Capture(rows.Select(r => new ResidentMoveEvidence(r.Id, r.Preserved, r.MedicalCare)));
        CheckLocations(city, set);
        return new() { Snapshot = set.Export(), Pawns = pawns, Profile = profile, Surgery = surgery, Prisoners = prisoners, AreaRecords = areaRecords, LoadedMapScope = loaded, NativeMap = profile is 11 or 13 or 14 ? city.Map : null,
            MapBindings = profile is 11 or 13 or 14 ? ResidentMapped.Prepare(pawns.Select(ResidentMapActors.Binding)) : null, GroundRestJobs = profile == 14 ? pawns.Where(p => p.CurJob != null).Select(p => p.jobs.curJob).ToList() : null };
    }
    // Explicit new clinical format; legacy Capture/Read/profile7 bytes and restrictions remain unchanged.
    internal static ResidentExecutionRecord CaptureRoyalMapped(Settlement city, string loaded) => CaptureCore(city, 13, loaded);
    internal static ResidentExecutionRecord CaptureRoyalWorld(Settlement city)
    { if (city?.HasMap != false) throw new ArgumentException("Royal clinical12 is a new unloaded scope."); return CaptureCore(city, 12, null); }
    internal static ResidentExecutionRecord CaptureForTransfer(Settlement city)
    {
        // Inspect the complete original roster. Choosing a format never bypasses each resident's eligibility checks.
        var roster = city?.previouslyGeneratedInhabitants;
        string loaded = city?.HasMap == true ? LoadedMapRecords.Capture(city).Snapshot : null;
        int profile = loaded != null && LoadedMapPreservation.Restore(loaded).NativeResidents != null ? (LoadedMapPreservation.Restore(loaded).GroundRestV1 != null ? 14 : 11) : ResidentMoveProfiles.SelectTownClinical(roster?.Any(ResidentAreaRecords.HasRestrictions) == true,
            roster?.Any(p => p?.Downed == true) == true, roster?.Any(p => p?.IsPrisoner == true) == true);
        return CaptureCore(city, profile, loaded);
    }
    internal static ResidentExecutionRecord CaptureScoped(Settlement city, int profile, string loaded) => CaptureCore(city, profile, loaded);
    internal static string Proof(Settlement city, int profile = 7, Faction target = null)
    {
        try
        {
            var record = profile == 7 ? CaptureForTransfer(city) : CaptureCore(city, profile, city?.HasMap == true ? LoadedMapRecords.Capture(city, profile == 13).Snapshot : null);
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
            var record = CaptureForTransfer(city);
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
        if (record.Profile is 12 or 13 && record.RoyalRightsDocument == null) throw new ArgumentException("Royal clinical12 needs independent original rights.");
        if (record.Profile is 11 or 13 or 14 && record.NativeMap != city.Map) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        CheckTown(city, record.LoadedMapScope);
        if (record.RoyalRightsDocument != null) RoyalRightsAdapter.Verify(record, record.RoyalRightsDocument, RoyalRights.Proof(record.RoyalRightsDocument));
        ResidentGroundRest.Validate(record);
        ValidateAreas(record, false);
        if (record.Profile is 9 or 10 or 11 or 13 or 14)
            for (int i = 0; i < record.Pawns.Count; i++)
            {
                if (record.AreaRecords[i].Pawn != record.Pawns[i]) throw new ArgumentException("Original area record belongs to another pawn reference.");
                ResidentAreaRecords.References(record.AreaRecords[i], record.Pawns[i].GetUniqueLoadID());
            }
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
        CheckLocations(city, record.Set());
    }
    private static void CheckLocations(Settlement city, ResidentMoveSet set)
    {
        var ids = new HashSet<string>(set.Residents.Select(r => r.Id), StringComparer.Ordinal);
        var locations = Find.WorldObjects.AllWorldObjects.OfType<Settlement>().SelectMany(t => (t.previouslyGeneratedInhabitants ?? new List<Pawn>())
            .Where(p => p != null && ids.Contains(p.GetUniqueLoadID())).Select(p => new ResidentLocation("territory/" + t.GetUniqueLoadID(), p.GetUniqueLoadID()))).ToArray();
        ResidentTransferCensus.RequireUniqueLocations("territory/" + city.GetUniqueLoadID(), ids, locations);
    }
    internal static void ValidateCareJournal(ResidentExecutionRecord record, Faction source, Faction target)
    {
        if (record == null || source == null || target == null) throw new ArgumentException("Original resident authorities required.");
        var set = record.Set();
        if (!record.CareJournalV1 && record.CareJournal?.Count > 0) throw new ArgumentException("Unpaired resident care journal.");
        if (!record.CareJournalV1) return;
        if (record.CareJournal == null || record.CareJournal.Count != set.Residents.Count) throw new ArgumentException("Incomplete resident care journal.");
        for (int i = 0; i < set.Residents.Count; i++)
        {
            var state = ResidentCareJournal.Restore(record.CareJournal[i]).State; var resident = set.Residents[i];
            if (state.Id != resident.Id || state.OriginalCare != resident.MedicalCare || state.Source != source.GetUniqueLoadID() ||
                state.Target != target.GetUniqueLoadID() || !record.Started && state.Started)
                throw new ArgumentException("Resident care journal does not match original transfer.");
        }
    }
    internal static void ValidateCensus(ResidentExecutionRecord record, string expected, string town, string source, string target, bool complete = false)
    {
        ValidateAreas(record, complete);
        if (record.Profile == 14 && (record.LoadedMapScope == null || LoadedMapPreservation.Restore(record.LoadedMapScope).GroundRestV1 == null || record.GroundRestJobs == null)) throw new ArgumentException("Original rest clinical/job provenance missing.");
        if (record.Profile == 13 && (record.RoyalRightsDocument == null || record.LoadedMapScope == null || LoadedMapPreservation.Restore(record.LoadedMapScope).RoyalMappedV1 != 1)) throw new ArgumentException("Royal clinical/map/private provenance missing.");
        if (record.Profile is 11 or 13 or 14) ResidentMapped.Pair(record.MapBindings, record.Set(), record.LoadedMapScope);
        else if (record.MapBindings != null || record.NativeMap != null) throw new ArgumentException("Map bindings cannot backfill an old resident profile.");
        if (record.Census == null)
        { if (expected != null || record.AuthorityJournal != null || record.Profile >= 8) throw new ArgumentException("Missing original resident census."); return; }
        if (expected == null || !record.CareJournalV1) throw new ArgumentException("Resident census has no transfer/medical peer.");
        ResidentTransferCensus.Check(record.Census, expected, town, source, target, record.Profile, record.Set(), record.AuthorityJournal, record.CareJournal, record.Started, complete);
    }
    private static void ValidateAreas(ResidentExecutionRecord record, bool complete)
    {
        if (record.Profile is not (9 or 10 or 11 or 13 or 14)) { if (record.AreaRecords != null) throw new ArgumentException("Area evidence cannot backfill a legacy resident profile."); return; }
        var set = record.Set();
        if (record.AreaRecords == null || record.AreaRecords.Count != set.Residents.Count) throw new ArgumentException("Missing original area scope.");
        if (record.AuthorityJournal != null && record.AuthorityJournal.Count != set.Residents.Count) throw new ArgumentException("Incomplete original area/call scope.");
        for (int i = 0; i < set.Residents.Count; i++)
        {
            var r = record.AreaRecords[i]; var state = ResidentAreas.Restore(r?.Journal);
            string original = ResidentMoveProfiles.BindAreas(r.Previous, ResidentAreas.Proof(r.Journal));
            if (record.Profile == 10) original = ResidentMoveProfiles.BindMixedAreas(original);
            if (record.Profile is 11 or 13 or 14) original = record.Profile == 13 ? ResidentMapped.BindRoyal(original, ResidentMapped.Restore(record.MapBindings)[i]) : ResidentMapped.Bind(original, ResidentMapped.Restore(record.MapBindings)[i]);
            if (record.Profile == 14) original = GroundRest.Bind(original, LoadedMapPreservation.Restore(record.LoadedMapScope).GroundRestV1.SingleOrDefault(row => row.Pawn == set.Residents[i].Id));
            if (state.Id != set.Residents[i].Id || original != set.Residents[i].Preserved ||
                !record.Started && state.Started || complete && !state.Consumed) throw new ArgumentException("Original resident/area evidence changed or incomplete.");
            if (record.AuthorityJournal != null)
            {
                var call = ResidentTransferCensus.Call(record.AuthorityJournal[i]);
                if (state.Started != call.Started || state.Returned && !call.Returned) throw new ArgumentException("Unpaired original area/call facts.");
            }
        }
    }
    internal static void ReviewCustody(ResidentExecutionRecord record, Settlement city, Faction source, Faction target, string proof)
    {
        ValidateReferences(record, city, proof); ValidateCareJournal(record, source, target);
        if (record.Census == null || record.AuthorityJournal == null || !record.CareJournalV1) throw new ArgumentException("Original independent resident evidence required.");
        ValidateCensus(record, ResidentTransferCensus.Restore(record.Census).Proof, "territory/" + city.GetUniqueLoadID(), source.GetUniqueLoadID(), target.GetUniqueLoadID());
        ResidentTransferCensus.RequireRecoverable(record.AuthorityJournal, record.CareJournal);
        record.Set().Review(ReadAll(record), source.GetUniqueLoadID(), target.GetUniqueLoadID(), record.Started);
    }
    internal static void Apply(ResidentExecutionRecord record, Settlement city, Faction source, Faction target, string proof, Action currentAuthority = null)
    {
        var executionGame = Current.Game;
        if (executionGame == null || Current.ProgramState != ProgramState.Playing)
            throw new ArgumentException("Resident execution requires the current game.");
        EnsureRetention();

        ValidateReferences(record, city, proof);
        ValidateCareJournal(record, source, target);
        if (record.Profile >= 8 && record.Census == null) throw new ArgumentException("Clinical resident execution requires original independent call and medical evidence.");
        if (record.Census != null)
        {
            ValidateCensus(record, ResidentTransferCensus.Restore(record.Census).Proof, "territory/" + city.GetUniqueLoadID(), source.GetUniqueLoadID(), target.GetUniqueLoadID());
            ResidentTransferCensus.RequireRecoverable(record.AuthorityJournal, record.CareJournal);
        }
        var set = record.Set(); var rows = ReadAll(record);
        set.Review(rows, source.GetUniqueLoadID(), target.GetUniqueLoadID(), record.Started);
        foreach (var prisoner in record.Prisoners) ResidentPrisoners.NeedsRestore(prisoner, source, target, record.Profile);
        record.Started = true; // Retain this before any native mutation; recovery proceeds forward, never raw-writes faction.
        Find.WorldPawns.gc.CancelGCPass();
        for (int i = 0; i < record.Pawns.Count; i++)
        {
            if (Current.Game != executionGame || Current.ProgramState != ProgramState.Playing)
                throw new ArgumentException("Current game changed during resident execution; original intent retained.");
            currentAuthority?.Invoke();
            var p = record.Pawns[i];
            var surgery = record.Profile >= 2 ? record.Surgery[i] : null;
            var prisoner = record.Prisoners.SingleOrDefault(r => r.Pawn == p);
            var area = record.Profile is 9 or 10 or 11 or 13 or 14 ? record.AreaRecords[i] : null;
            var action = set.ActionFor(ReadWithAreas(p, record.Profile, surgery, prisoner, area), source.GetUniqueLoadID(), target.GetUniqueLoadID(), true);
            var care = !record.CareJournalV1 ? null : ResidentCareJournal.Restore(record.CareJournal[i]);
            if (care != null && action == ResidentMoveAction.ChangeFaction && care.State.Started)
                throw new ArgumentException("Never replay an uncertain resident authority call.");
            if (care != null && action != ResidentMoveAction.ChangeFaction && !care.State.Started)
                throw new ArgumentException("Missing original resident authority call.");
            if (surgery != null && action == ResidentMoveAction.ChangeFaction) ResidentSurgery.Detach(surgery);
            if (Current.Game != executionGame || Current.ProgramState != ProgramState.Playing)
                throw new ArgumentException("Current game changed before resident authority change; original intent retained.");
            if (action == ResidentMoveAction.ChangeFaction)
            {
                string areaStart = area == null ? null : ResidentAreas.Begin(area.Journal, ResidentAreaRecords.Entries(p));
                if (care != null)
                {
                    if (record.Census == null) care.Begin(source.GetUniqueLoadID(), set.Residents[i].MedicalCare);
                    else care.Begin(record.Profile >= 4 ? ResidentPrisoners.Authority(p) : p.Faction?.GetUniqueLoadID(), p.playerSettings == null ? null : (int)p.playerSettings.medCare);
                    string call = record.Census == null ? null : ResidentTransferCensus.Begin(record.AuthorityJournal[i]);
                    record.CareJournal[i] = care.Export(); if (call != null) record.AuthorityJournal[i] = call;
                }
                if (area != null) area.Journal = areaStart;
                if (prisoner != null) ResidentPrisoners.ChangeHost(prisoner, source, target, record.Profile);
                else p.SetFaction(target);
                if (Current.Game != executionGame || Current.ProgramState != ProgramState.Playing)
                    throw new ArgumentException("Current game changed during resident authority change; original intent retained.");
                if (record.Census != null) record.AuthorityJournal[i] = ResidentTransferCensus.Returned(record.AuthorityJournal[i]);
            }
            if ((record.Profile >= 4 ? ResidentPrisoners.Authority(p) : p.Faction?.GetUniqueLoadID()) != target.GetUniqueLoadID())
                throw new ArgumentException("Native resident authority did not reach the target.");
            if (record.Census != null && care != null && action == ResidentMoveAction.ChangeFaction)
            { care.RecordReturn(target.GetUniqueLoadID(), p.playerSettings == null ? null : (int)p.playerSettings.medCare); record.CareJournal[i] = care.Export(); }
            if (area != null && action == ResidentMoveAction.ChangeFaction) ResidentAreaRecords.Returned(area);
            // Restore recorded care/orders and separately verified custody resets only; never repair health, gear, social state or unexplained ownership.
            var actual = ReadWithAreas(p, record.Profile, surgery, prisoner, area);
            var before = set.Residents[i];
            if (actual.Preserved != before.Preserved || actual.MedicalCare.HasValue != before.MedicalCare.HasValue)
                throw new ArgumentException("Native resident preservation mismatch; original intent retained.");
            if (Current.Game != executionGame || Current.ProgramState != ProgramState.Playing)
                throw new ArgumentException("Current game changed before resident restoration; original intent retained.");
            if (record.Census == null && care != null && action == ResidentMoveAction.ChangeFaction)
            { care.RecordReturn(target.GetUniqueLoadID(), actual.MedicalCare); record.CareJournal[i] = care.Export(); }
            if (care == null)
            { if (before.MedicalCare.HasValue) p.playerSettings.medCare = (MedicalCareCategory)before.MedicalCare.Value; }
            else if (care.MayRestore(target.GetUniqueLoadID(), actual.MedicalCare))
            { care.Consume(target.GetUniqueLoadID(), actual.MedicalCare); record.CareJournal[i] = care.Export(); p.playerSettings.medCare = (MedicalCareCategory)before.MedicalCare.Value; }
            else if (care.State.Returned && !care.State.Consumed)
            { care.SealUnchanged(target.GetUniqueLoadID(), actual.MedicalCare); record.CareJournal[i] = care.Export(); }
            if (prisoner != null) ResidentPrisoners.Restore(prisoner, source, target, record.Profile);
            if (Current.Game != executionGame || Current.ProgramState != ProgramState.Playing)
                throw new ArgumentException("Current game changed during resident restoration; original intent retained.");
            if (surgery != null) ResidentSurgery.Restore(surgery);
            if (area != null && (Current.Game != executionGame || Current.ProgramState != ProgramState.Playing))
                throw new ArgumentException("Current game changed before original area restoration.");
            if (area != null) ResidentAreaRecords.Restore(area);
        }
        if (Current.Game != executionGame || Current.ProgramState != ProgramState.Playing)
            throw new ArgumentException("Current game changed before resident verification; original intent retained.");
        ValidateReferences(record, city, proof);
        set.VerifyDestination(ReadAll(record), source.GetUniqueLoadID(), target.GetUniqueLoadID());
        if (record.Surgery.Any(s => s.Detached.Count != 0)) throw new ArgumentException("Surgical orders remain detached.");
        if (record.Census != null) ValidateCensus(record, ResidentTransferCensus.Restore(record.Census).Proof, "territory/" + city.GetUniqueLoadID(), source.GetUniqueLoadID(), target.GetUniqueLoadID(), true);
    }
    internal static void Verify(ResidentExecutionRecord record, Settlement city, Faction source, Faction target, string proof)
    {
        if (record.Census != null) ValidateCensus(record, ResidentTransferCensus.Restore(record.Census).Proof, "territory/" + city.GetUniqueLoadID(), source.GetUniqueLoadID(), target.GetUniqueLoadID(), true);
        ValidateReferences(record, city, proof);
        record.Set().VerifyDestination(ReadAll(record), source.GetUniqueLoadID(), target.GetUniqueLoadID());
        if (record.Prisoners.Any(r => ResidentPrisoners.NeedsRestore(r, source, target, record.Profile))) throw new ArgumentException("Prisoner progress not yet restored.");
        if (record.Surgery.Any(s => s.Detached.Count != 0)) throw new ArgumentException("Cannot complete with detached orders.");
    }
    private static ResidentMoveActual[] ReadAll(ResidentExecutionRecord record) => record.Pawns.Select((p, i) =>
        ReadWithAreas(p, record.Profile, record.Profile >= 2 ? record.Surgery[i] : null, record.Prisoners.SingleOrDefault(r => r.Pawn == p), record.Profile is 9 or 10 or 11 or 13 or 14 ? record.AreaRecords[i] : null)).ToArray();
    internal static void EnsureRetention()
    {
        if (GcMethod == null || GcMethod.ReturnType != typeof(string) || Harmony.GetPatchInfo(GcMethod)?.Owners.Contains("honor.series.contracts.preview") != true)
            throw new ArgumentException("Pending resident retention hook unavailable.");
    }
    internal static void ReleaseReferences(ResidentExecutionRecord record)
    {
        if (record.Census != null)
        {
            var c = ResidentTransferCensus.Restore(record.Census);
            ValidateCensus(record, c.Proof, c.Town, c.Source, c.Target, record.Started);
        }
        if (record.Surgery == null || record.Surgery.Any(s => s == null || s.Detached == null || s.Detached.Count != 0))
            throw new ArgumentException("Never discard detached surgical originals.");
        if (record.Prisoners == null || record.Prisoners.Any(r => r == null || r.ResetSnapshot != null))
            throw new ArgumentException("Never discard an unfinished native custody reset.");
        if (record.AreaRecords?.Any(r => r.Maps == null || r.Areas == null) == true) throw new ArgumentException("Malformed retained original area references.");
        record.GroundRestJobs?.Clear(); record.RoyalFactions?.Clear(); record.RoyalHeirs?.Clear(); record.RoyalAbilities?.Clear();
        record.Pawns.Clear(); record.Surgery.Clear(); record.Prisoners.Clear(); record.NativeMap = null;
        if (record.AreaRecords != null) foreach (var area in record.AreaRecords) { area.Pawn = null; area.Maps.Clear(); area.Areas.Clear(); }
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
            (Current.Game?.GetComponent<TerritoryAdministrationLedger>()?.ReservesResident(pawn) == true ||
             Current.Game?.GetComponent<LeaderRelocationLedger>()?.ReservesResident(pawn) == true ||
             Current.Game?.GetComponent<LeaderJoinLedger>()?.ReservesResident(pawn) == true))
            __result = "Honor.PendingResidentTransfer";
    }
}
