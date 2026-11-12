using HarmonyLib;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    // Only a NEW scoped grant can use this predicate. Old Eligible remains unchanged.
    private static bool RoyalEligible(Faction f) => ModsConfig.RoyaltyActive && NativeTransferBodies.RoyalCustodyBodies && f != null &&
        f.GetType() == typeof(Faction) && !f.IsPlayer && !f.Hidden && !f.temporary && !f.defeated && f.HasGoodwill &&
        f.def.humanlikeFaction && !f.def.permanentEnemy && !f.HostileTo(Faction.OfPlayer) && !Faction.OfPlayer.HostileTo(f);
    private static void RoyalReadBoundaries()
    {
        if (!NativeTransferBodies.RoyalCustodyBodies) throw new ArgumentException("Unreviewed royal module.");
        foreach (var read in new MethodBase[] {
            AccessTools.Method(typeof(Pawn_RoyaltyTracker), nameof(Pawn_RoyaltyTracker.GetCurrentTitle), [typeof(Faction)]),
            AccessTools.Method(typeof(Pawn_RoyaltyTracker), nameof(Pawn_RoyaltyTracker.GetCurrentTitleInFaction), [typeof(Faction)]),
            AccessTools.Method(typeof(Pawn_RoyaltyTracker), "FindFactionTitleIndex", [typeof(Faction), typeof(bool)]),
            AccessTools.PropertyGetter(typeof(Pawn_RoyaltyTracker), nameof(Pawn_RoyaltyTracker.AllTitlesInEffectForReading)) })
            if (read == null || Harmony.GetPatchInfo(read)?.Owners.Any() == true) throw new ArgumentException("Unknown royal representative read dispatch.");
    }
    private static string RoyalResidentProof(IEnumerable<Pawn> residents)
    {
        if (!ModsConfig.RoyaltyActive || !NativeTransferBodies.RoyalCustodyBodies) throw new ArgumentException("Royal native bodies are not reviewed.");
        RoyalReadBoundaries();
        var rows = new List<object>();
        foreach (var pawn in (residents ?? []).OrderBy(p => p?.GetUniqueLoadID(), StringComparer.Ordinal))
        {
            var tracker = pawn?.royalty;
            if (pawn == null || Find.QuestManager == null || Find.QuestManager.IsReservedByAnyQuest(pawn) || tracker?.GetType() != typeof(Pawn_RoyaltyTracker) || tracker.pawn != pawn) throw new ArgumentException("Original royal tracker owner unavailable.");
            // Read actual saved fields. Existing titles/favor/heirs/permits/abilities are separate personal rights, not disposable civilian state.
            foreach (string name in new[] { "titles", "favor", "highestTitles", "heirs", "factionPermits", "abilities" })
            {
                var field = typeof(Pawn_RoyaltyTracker).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                var value = field?.GetValue(tracker);
                if (field == null || field.IsStatic || field.DeclaringType != typeof(Pawn_RoyaltyTracker) || value?.GetType() != field.FieldType ||
                    !field.FieldType.IsGenericType || field.FieldType.GetGenericTypeDefinition() != typeof(List<>) && field.FieldType.GetGenericTypeDefinition() != typeof(Dictionary<,>) ||
                    value is not ICollection collection || collection.Count != 0) throw new ArgumentException("Personal royal rights require their own preservation path.");
            }
            rows.Add(new { Id = pawn.GetUniqueLoadID(), tracker.lastDecreeTicks, tracker.allowRoomRequirements, tracker.allowApparelRequirements });
        }
        return EnrollmentDigest(JsonSerializer.Serialize(rows));
    }
    private sealed record RoyalTransferReview(Game Game, string Charter, TerritoryTransferLedger Physical, TerritoryTransferReference Reference, CustodyAgreement Witness);
    private RoyalTransferReview ReviewRoyalTransfer(string charter, string id = null, long? at = null)
    {
        EnrollmentReady();
        var c = Charters.Book.Charter(charter); var binding = Charters.Binding(c.Terms.Subject); var city = binding.Settlement;
        if (c.Phase != RecordedCharterPhase.Active || !Charters.Book.PlayerManaged(c) || binding.PlayerGroup || binding.FactionGroup || city == null || city.Destroyed ||
            !Find.WorldObjects.AllWorldObjects.Contains(city) || IndependentlyLostTerritory(c.Terms.Subject)) throw new ArgumentException("Current surviving active city charter required.");
        var target = Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryRecoveryAuthority(Charters.ObserveForMigration(null)); var source = city.Faction;
        if (source == target || !RoyalEligible(source) || !RoyalEligible(target) || source.HostileTo(target) || target.HostileTo(source)) throw new ArgumentException("Explicit current royal source/destination and reciprocal peace required.");
        Charters.Book.ValidateCurrent(charter, Charters.ObserveForMigration(null));
        bool titledWorld = !city.HasMap && city.previouslyGeneratedInhabitants?.Any(RoyalRightsAdapter.HasTitles) == true;
        bool titledMap = city.HasMap && city.previouslyGeneratedInhabitants?.Any(RoyalRightsAdapter.HasTitles) == true;
        var originalMap = titledMap ? LoadedMapRecords.Capture(city, true) : null;
        var residents = city.previouslyGeneratedInhabitants?.Count > 0 ? (titledMap ? ResidentExecution.CaptureRoyalMapped(city, originalMap.Snapshot) : titledWorld ? ResidentExecution.CaptureRoyalWorld(city) : ResidentExecution.CaptureForTransfer(city)) : null;
        var rights = RoyalRightsAdapter.Capture(residents?.Pawns);
        RoyalRightsAdapter.Attach(residents, rights);
        int profile = residents?.Profile ?? 7;
        var observed = Observe(city, c.Terms.Subject, target, residentProfile: profile) with { Eligible = true, TargetEligible = true };
        var loaded = observed.LoadedMap == null ? null : LoadedMapRecords.Capture(city, titledMap);
        if (residents?.Set().Proof != observed.ResidentProof || loaded?.Snapshot != observed.LoadedMap || residents != null && residents.LoadedMapScope != observed.LoadedMap)
            throw new ArgumentException("Original full clinical/map proof changed.");
        string royal = RoyalRights.Proof(rights.Document);
        if (residents != null) residents.PrepareCareJournal(source, target, city);
        if (loaded != null) MapAssetRecords.Prepare(loaded, source, target);
        long tick = at ?? Find.TickManager.TicksGame; id ??= "royal-" + Guid.NewGuid().ToString("N");
        var physical = TerritoryTransferLedger.Restore(Ledger.Export());
        physical.PreparePrivateRoyalJournaled(id, Charters.Book, charter, observed, target.GetUniqueLoadID(), rights.Document, tick, AdministrationId);
        var reference = new TerritoryTransferReference { Id = id, City = city, From = source, Target = target, Residents = residents, LoadedMap = loaded, CityCallV1 = true,
            ResidentCareJournalRequired = residents != null, ResidentCensusProof = residents == null ? null : ResidentTransferCensus.Restore(residents.Census).Proof };
        var witness = CurrentCustodyWitness(id, "approval-" + id, tick, proposed: physical, proposedReference: reference);
        return new(Current.Game, charter, physical, reference, witness);
    }
    private void CommitRoyalTransfer(RoyalTransferReview reviewed)
    {
        try
        {
            if (reviewed.Game != Current.Game) throw new ArgumentException("Another game was reviewed.");
            var fresh = ReviewRoyalTransfer(reviewed.Charter, reviewed.Reference.Id, reviewed.Witness.Tick);
            if (JsonSerializer.Serialize(fresh.Witness) != JsonSerializer.Serialize(reviewed.Witness)) throw new ArgumentException("Title/third-party rights or original custody changed.");
            var history = CustodyAgreements.Restore(custodyAgreements.Export()); history.Add(fresh.Witness);
            history.ValidateHistory(Charters.Book, fresh.Physical, Current.Game.GetComponent<TributePaymentLedger>().Ledger);
            fresh.Reference.CustodyAgreementV1 = fresh.Witness.Id;
            if (fresh.Game != Current.Game || nativeExecutionDepth != 0) throw new ArgumentException("Execution context changed.");
            // Records/reference/current mandate first. Existing setters then journal each normal return independently.
            Ledger = fresh.Physical; references.Add(fresh.Reference); custodyAgreements = history;
            Recover(fresh.Reference.Id);
        }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Royal custody incomplete/deferred; originals retained: " + e.GetType().Name); Messages.Message("RE_CustodyBlocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
    internal void DrawRoyalTransfer(Listing_Standard list, string charter)
    {
        if (!list.ButtonText("RE_RoyalTransfer".Translate())) return;
        try
        {
            var reviewed = ReviewRoyalTransfer(charter);
            string mandates = string.Join(", ", reviewed.Witness.Regimes.Where(d => d.Regime == NpcDecisionRegime.Royal).Select(d => {
                var f = Find.FactionManager.AllFactionsListForReading.Single(f => f.GetUniqueLoadID() == d.Realm);
                return f.Name + "/" + f.leader.LabelShortCap + "/" + (DefDatabase<RoyalTitleDef>.GetNamedSilentFail(d.RoyalTitle)?.label ?? d.RoyalTitle);
            }));
            string parties = string.Join(", ", reviewed.Witness.Parties.Select(p => Charters.Label(p.Party)));
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RE_RoyalTransferConfirm".Translate(reviewed.Reference.City.Label, reviewed.Reference.From.Name, reviewed.Reference.Target.Name, mandates, parties), () => CommitRoyalTransfer(reviewed)));
        }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Royal custody review blocked: " + e.GetType().Name); Messages.Message("RE_CustodyBlocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
}
