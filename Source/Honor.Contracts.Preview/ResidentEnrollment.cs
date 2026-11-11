using System.Text;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using UnityEngine;
namespace Honor.Contracts.Preview;
public sealed class ResidentEnrollmentRecord : IExposable
{
    public string Journal;
    public Settlement City;
    public Map Map;
    public Faction Source;
    public Pawn Representative;
    public List<Pawn> Before, Residents;
    public void ExposeData()
    {
        Scribe_Values.Look(ref Journal, "originalEnrollmentV1");
        Scribe_References.Look(ref City, "originalTown"); Scribe_References.Look(ref Map, "originalMap");
        Scribe_References.Look(ref Source, "originalSource"); Scribe_References.Look(ref Representative, "originalRepresentative");
        Scribe_Collections.Look(ref Before, "originalRoster", LookMode.Reference);
        Scribe_Collections.Look(ref Residents, "selectedOriginalResidents", LookMode.Reference);
    }
    public void Release() { City = null; Map = null; Source = null; Representative = null; Before = null; Residents = null; }
}
public sealed partial class TerritoryAdministrationLedger
{
    private List<ResidentEnrollmentRecord> residentEnrollments;
    private static string EnrollmentDigest(string value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(value)));
    private void ExposeResidentEnrollments()
    {
        Scribe_Collections.Look(ref residentEnrollments, "honorExplicitResidentEnrollmentsV1", LookMode.Deep);
        if (Scribe.mode != LoadSaveMode.PostLoadInit || residentEnrollments == null) return;
        try
        {
            if (residentEnrollments.Count > 128 || residentEnrollments.Any(r => r == null)) throw new ArgumentException("Enrollment archive capacity.");
            foreach (var record in residentEnrollments) _ = ResidentEnrollment.Restore(record.Journal);
            if (residentEnrollments.Count(r => !ResidentEnrollment.Restore(r.Journal).Verified) > 1) throw new ArgumentException("Overlapping enrollment authority.");
        }
        catch (Exception e) when (e is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Original enrollment journal retained after restore failure: " + e.GetType().Name); }
    }
    private void ValidateEnrollmentReferences()
    {
        foreach (var record in residentEnrollments ?? [])
        {
            var s = ResidentEnrollment.Restore(record.Journal);
            if (s.Tick > Find.TickManager.TicksGame) throw new ArgumentException("Future enrollment clock.");
            if (s.Verified) { record.Release(); continue; }
            if (record.City == null || record.Map == null || record.Source == null || record.Representative == null || record.Before == null || record.Residents == null ||
                s.Town != "territory/" + record.City.GetUniqueLoadID() || s.Map != record.Map.GetUniqueLoadID() || s.Source != record.Source.GetUniqueLoadID() ||
                s.Authority.Representative != record.Representative.GetUniqueLoadID() || record.Before.Any(p => p == null) || record.Residents.Any(p => p == null) ||
                !record.Before.Select(p => p.GetUniqueLoadID()).SequenceEqual(s.Before) || !record.Residents.Select(p => p.GetUniqueLoadID()).SequenceEqual(s.Residents.Select(p => p.Id)))
                throw new ArgumentException("Original enrollment reference census unavailable.");
        }
    }
    private void EnrollmentReady()
    {
        if (Current.Game != ownerGame || Current.ProgramState != ProgramState.Playing || RestoreFailed || Charters.RestoreFailed || Charters.UnresolvedTownLosses || nativeExecutionDepth != 0 ||
            UnionWarCouncil.HasExternalProvider || Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) || HasPendingTownLosses ||
            Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges || Current.Game.GetComponent<LeaderJoinLedger>().BlocksChanges ||
            Current.Game.GetComponent<LeaderRelocationLedger>().BlocksChanges || Current.Game.GetComponent<SubtreeExitLedger>().BlocksChanges)
            throw new ArgumentException("Resolve existing authority/custody before a new explicit enrollment.");
    }
    private void VerifyEnrollment(ResidentEnrollmentRecord r)
    {
        EnrollmentReady(); var state = ResidentEnrollment.Restore(r.Journal);
        if (state.Tick > Find.TickManager.TicksGame) throw new ArgumentException("Future enrollment decision.");
        if (state.Verified) throw new ArgumentException("Enrollment is already complete.");
        var binding = Charters.Binding(Charters.Book.Charter(state.Charter).Terms.Subject);
        if (r.City == null || r.City.GetType() != typeof(Settlement) || r.City.Destroyed || !Find.WorldObjects.AllWorldObjects.Contains(r.City) || !r.City.HasMap || r.City.Map != r.Map ||
            r.Map == null || r.Map.GetType() != typeof(Map) || !Find.Maps.Contains(r.Map) || r.Map.Parent != r.City || r.Map.IsPlayerHome || r.City.Faction != r.Source || r.Source?.leader != r.Representative ||
            state.Town != "territory/" + r.City.GetUniqueLoadID() || state.Map != r.Map.GetUniqueLoadID() || state.Source != r.Source.GetUniqueLoadID() ||
            binding.PlayerGroup || binding.FactionGroup || binding.Settlement != r.City || r.Before == null || r.Residents == null ||
            r.Before.Any(p => p == null) || r.Residents.Any(p => p == null) || !r.Before.Select(p => p.GetUniqueLoadID()).SequenceEqual(state.Before) ||
            !r.Residents.Select(p => p.GetUniqueLoadID()).SequenceEqual(state.Residents.Select(p => p.Id)) ||
            r.City.previouslyGeneratedInhabitants == null || !r.City.previouslyGeneratedInhabitants.SequenceEqual(r.Before.Concat(r.Residents.Take(state.Added))))
            throw new ArgumentException("Original roster/selected pawn/map/reference changed.");
        var charter = Charters.Book.Charter(state.Charter);
        if (charter.Phase != RecordedCharterPhase.Active || !Charters.Book.PlayerManaged(charter) || IndependentlyLostTerritory(charter.Terms.Subject) ||
            !Ledger.VerifiedOwner(charter.Terms.Subject, Charters.Book.Entity(charter.Terms.Subject).NativeOwner, state.Source)) throw new ArgumentException("Original active city contract required.");
        ResidentEnrollment.Review(r.Journal, r.City.previouslyGeneratedInhabitants.Select(p => p.GetUniqueLoadID()), NpcDecisionAuthority.Read(r.Source), EnrollmentDigest(Charters.Book.Export()));
        foreach (var pawn in r.Residents)
        {
            if (pawn.Faction != r.Source || pawn.Map != r.Map || pawn.GetLord() != null || Find.QuestManager.IsReservedByAnyQuest(pawn)) throw new ArgumentException("Not an independently eligible current civilian.");
            var actual = ResidentExecution.ReadEnrollment(pawn); var original = state.Residents.Single(p => p.Id == actual.Id);
            if (actual.Preserved != original.Preserved || actual.MedicalCare != original.MedicalCare) throw new ArgumentException("Original clinical state changed.");
            if (Cities().Any(c => c != r.City && c.previouslyGeneratedInhabitants?.Contains(pawn) == true)) throw new ArgumentException("Resident already belongs to another original town.");
        }
    }
    private ResidentEnrollmentRecord ReviewEnrollment(string charterId)
    {
        EnrollmentReady();
        if ((residentEnrollments?.Count ?? 0) >= 128 || residentEnrollments?.Any(r => !ResidentEnrollment.Restore(r.Journal).Verified) == true) throw new ArgumentException("Resolve the original enrollment first.");
        var charter = Charters.Book.Charter(charterId); var b = Charters.Binding(charter.Terms.Subject); var city = b.Settlement;
        if (charter.Phase != RecordedCharterPhase.Active || !Charters.Book.PlayerManaged(charter) || b.PlayerGroup || b.FactionGroup || city?.HasMap != true || city.previouslyGeneratedInhabitants == null)
            throw new ArgumentException("Current single city with a known original roster required.");
        var source = Charters.SubcharterRepresentative(charter.Terms.Subject); var authority = NpcDecisionAuthority.Read(source);
        var candidates = ResidentMapActors.SpawnedRoster(city.Map).Where(p => p != null && p.Faction == source && !city.previouslyGeneratedInhabitants.Contains(p))
            .OrderBy(p => p.GetUniqueLoadID(), StringComparer.Ordinal).ToList();
        if (candidates.Count == 0 || candidates.Count + city.previouslyGeneratedInhabitants.Count > 128 || city.previouslyGeneratedInhabitants.Any(p => p == null)) throw new ArgumentException("No bounded selected civilian roster.");
        if (candidates.Any(p => ReservesResident(p) || Current.Game.GetComponent<LeaderJoinLedger>().ReservesResident(p) || Current.Game.GetComponent<LeaderRelocationLedger>().ReservesResident(p)))
            throw new ArgumentException("Another original execution owns this resident.");
        var rows = candidates.Select(p => { var a = ResidentExecution.ReadEnrollment(p); return new ResidentMoveEvidence(a.Id, a.Preserved, a.MedicalCare); }).ToArray();
        var record = new ResidentEnrollmentRecord { City = city, Map = city.Map, Source = source, Representative = source.leader,
            Before = city.previouslyGeneratedInhabitants.ToList(), Residents = candidates,
            Journal = ResidentEnrollment.Prepare(charterId, "territory/" + city.GetUniqueLoadID(), city.Map.GetUniqueLoadID(), source.GetUniqueLoadID(),
                EnrollmentDigest(Charters.Book.Export()), authority, city.previouslyGeneratedInhabitants.Select(p => p.GetUniqueLoadID()), rows, Find.TickManager.TicksGame) };
        VerifyEnrollment(record); return record;
    }
    private void ApplyEnrollment(ResidentEnrollmentRecord record, bool prepare)
    {
        try
        {
            VerifyEnrollment(record);
            if (prepare) { if (residentEnrollments?.Any(r => !ResidentEnrollment.Restore(r.Journal).Verified) == true) throw new ArgumentException("Pending enrollment exists."); residentEnrollments ??= []; residentEnrollments.Add(record); }
            else if (residentEnrollments?.Contains(record) != true) throw new ArgumentException("Original enrollment journal missing.");
            var originalGame = Current.Game; var roster = record.City.previouslyGeneratedInhabitants;
            while (!ResidentEnrollment.Restore(record.Journal).Complete)
            {
                VerifyEnrollment(record); var state = ResidentEnrollment.Restore(record.Journal);
                if (Current.Game != originalGame || record.City.previouslyGeneratedInhabitants != roster) throw new ArgumentException("Original game/list changed.");
                record.Journal = ResidentEnrollment.Begin(record.Journal, roster.Select(p => p.GetUniqueLoadID()), NpcDecisionAuthority.Read(record.Source), EnrollmentDigest(Charters.Book.Export()));
                // Exact native List.Add is callback-free. Preserve the same list and every original Pawn; never deep-save or recreate a person.
                roster.Add(record.Residents[state.Added]);
                record.Journal = ResidentEnrollment.Returned(record.Journal, roster.Select(p => p.GetUniqueLoadID()));
            }
            VerifyEnrollment(record);
            foreach (var pawn in record.Residents)
            {
                var actual = ResidentExecution.ReadEnrollment(pawn); var original = ResidentEnrollment.Restore(record.Journal).Residents.Single(p => p.Id == actual.Id);
                if (actual.Preserved != original.Preserved || actual.MedicalCare != original.MedicalCare) throw new ArgumentException("Unexpected clinical mutation after append.");
            }
            record.Journal = ResidentEnrollment.Finish(record.Journal, record.Residents.Select(p => { var a = ResidentExecution.ReadEnrollment(p); return new ResidentMoveEvidence(a.Id, a.Preserved, a.MedicalCare); }));
            record.Release(); Messages.Message("RE_EnrollmentCompleted".Translate(), MessageTypeDefOf.NeutralEvent);
        }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Enrollment stopped; original journals/references retained: " + e.GetType().Name); Messages.Message("RE_EnrollmentBlocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
    internal void DrawResidentEnrollment(Listing_Standard list, string charter)
    {
        if (RestoreFailed) return;
        if (list.ButtonText("RE_EnrollmentReview".Translate()))
        {
            try
            {
                var r = ReviewEnrollment(charter);
                string names = string.Join(", ", r.Residents.Select(p => p.Name?.ToStringFull ?? p.GetUniqueLoadID()));
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RE_EnrollmentConfirm".Translate(r.Source.Name, r.Representative.Name?.ToStringFull, names), () => ApplyEnrollment(r, true)));
            }
            catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Enrollment review unavailable: " + e.GetType().Name); Messages.Message("RE_EnrollmentBlocked".Translate(), MessageTypeDefOf.RejectInput); }
        }
        foreach (var r in (residentEnrollments ?? []).Where(r => { var s = ResidentEnrollment.Restore(r.Journal); return s.Charter == charter && !s.Verified; }).ToArray())
            if (list.ButtonText("RE_EnrollmentRecover".Translate())) ApplyEnrollment(r, false);
    }
}
