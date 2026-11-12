using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    private sealed record ContinuationReview(Game Game, string Previous, TerritoryTransferLedger Physical, TerritoryTransferReference Reference, CustodyAgreement Witness);
    private ContinuationReview ReviewContinuation(string previous, string id = null, long? reviewedTick = null)
    {
        if (Current.Game != ownerGame || Current.ProgramState != ProgramState.Playing || nativeExecutionDepth != 0 || RestoreFailed || Charters.RestoreFailed || Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared)) throw new ArgumentException("Complete original custody first.");
        var prior = Ledger.Transfer(previous); var original = references.Single(r => r.Id == previous);
        if (prior.Phase != TerritoryTransferPhase.Applied || !original.CityCallV1 || !original.CityCallReturned || original.City == null ||
            original.City.Faction?.GetUniqueLoadID() != prior.TargetOwner || Ledger.Transfers.Last(t => t.Territory == prior.Territory).Id != prior.Id)
            throw new ArgumentException("The last original positive handover must still own the same town.");
        ValidateMapReference(prior, original, false);
        ValidateCustodyAgreements();
        if (original.Residents != null)
            ResidentExecution.ValidateCensus(original.Residents, original.ResidentCensusProof, prior.Territory, prior.FromOwner, prior.TargetOwner, true);
        var current = Charters.Book.Charters.Single(c => c.Phase == RecordedCharterPhase.Active && c.Terms.Subject == prior.Territory);
        if (Charters.Book.PlayerManaged(current) || current.NpcAuthority == null) throw new ArgumentException("Current signed foreign master required.");
        var liege = Charters.Binding(current.Terms.Liege);
        var target = liege.FactionGroup ? liege.OriginalFaction : liege.Settlement?.Faction;
        bool royal = original.City.Faction?.def.royalTitleTags?.Count > 0 || target?.def.royalTitleTags?.Count > 0 || original.City.previouslyGeneratedInhabitants?.Any(RoyalRightsAdapter.HasTitles) == true;
        if (target == null || target == original.City.Faction || !(royal ? RoyalEligible(target) && RoyalEligible(original.City.Faction) : Eligible(target))) throw new ArgumentException("A distinct current eligible destination is required.");
        var city = original.City; var source = city.Faction;
        bool titledWorld = royal && !city.HasMap && city.previouslyGeneratedInhabitants?.Any(RoyalRightsAdapter.HasTitles) == true;
        bool titledMap = royal && city.HasMap && city.previouslyGeneratedInhabitants?.Any(RoyalRightsAdapter.HasTitles) == true;
        var originalMap = titledMap ? LoadedMapRecords.Capture(city, true) : null;
        var observed = Observe(city, prior.Territory, target, residentProfile: titledMap ? 13 : titledWorld ? 12 : original.Residents?.Profile ?? 7);
        if (royal) observed = observed with { Eligible = true, TargetEligible = true };
        var residents = observed.HasInhabitants ? (titledMap ? ResidentExecution.CaptureRoyalMapped(city, originalMap.Snapshot) : titledWorld ? ResidentExecution.CaptureRoyalWorld(city) : ResidentExecution.CaptureForTransfer(city)) : null;
        var privateRights = royal ? RoyalRightsAdapter.Capture(residents?.Pawns) : null;
        if (privateRights != null) RoyalRightsAdapter.Attach(residents, privateRights);
        var loaded = observed.LoadedMap == null ? null : LoadedMapRecords.Capture(city, titledMap);
        if (residents?.Set().Proof != observed.ResidentProof || loaded?.Snapshot != observed.LoadedMap || residents != null && residents.LoadedMapScope != observed.LoadedMap)
            throw new ArgumentException("Fresh complete clinical/map evidence changed.");
        if (residents != null) residents.PrepareCareJournal(source, target, city);
        if (loaded != null) MapAssetRecords.Prepare(loaded, source, target);
        long tick = reviewedTick ?? Find.TickManager.TicksGame; id ??= "continuation-" + Guid.NewGuid().ToString("N");
        var physical = TerritoryTransferLedger.Restore(Ledger.Export());
        if (privateRights != null) physical.PreparePrivateRoyalContinuation(id, previous, Charters.Book, current.Id, observed, target.GetUniqueLoadID(), privateRights.Document, tick, AdministrationId);
        else physical.PrepareForeignContinuation(id, previous, Charters.Book, current.Id, observed, target.GetUniqueLoadID(), tick, AdministrationId);
        var reference = new TerritoryTransferReference { Id = id, City = city, From = source, Target = target, Residents = residents,
            ResidentCareJournalRequired = residents != null, ResidentCensusProof = residents == null ? null : ResidentTransferCensus.Restore(residents.Census).Proof,
            CityCallV1 = true, LoadedMap = loaded };
        var witness = CurrentCustodyWitness(id, "approval-" + id, tick, proposed: physical, proposedReference: reference);
        return new(Current.Game, previous, physical, reference, witness);
    }
    private void CommitContinuation(ContinuationReview reviewed)
    {
        try
        {
            if (reviewed.Game != Current.Game) throw new ArgumentException("Review belongs to another game.");
            var fresh = ReviewContinuation(reviewed.Previous, reviewed.Reference.Id, reviewed.Witness.Tick);
            if (JsonSerializer.Serialize(fresh.Witness) != JsonSerializer.Serialize(reviewed.Witness)) throw new ArgumentException("Current approval or complete physical scope changed.");
            var history = CustodyAgreements.Restore(custodyAgreements.Export()); history.Add(fresh.Witness);
            history.ValidateHistory(Charters.Book, fresh.Physical, Current.Game.GetComponent<TributePaymentLedger>().Ledger);
            fresh.Reference.CustodyAgreementV1 = fresh.Witness.Id;
            // Stage ALL ledgers/reference and approval before any native mutation. No old record is retargeted.
            if (fresh.Game != Current.Game || nativeExecutionDepth != 0) throw new ArgumentException("Execution context changed.");
            Ledger = fresh.Physical; references.Add(fresh.Reference); custodyAgreements = history;
            Recover(fresh.Reference.Id);
        }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Follow-up custody deferred; original history retained: " + e.GetType().Name); Messages.Message("RE_CustodyBlocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
    internal void DrawContinuation(Listing_Standard list, string previous)
    {
        if (!list.ButtonText("RE_Continuation".Translate(references.Single(r => r.Id == previous).City?.Label ?? Ledger.Transfer(previous).Territory))) return;
        try
        {
            var reviewed = ReviewContinuation(previous);
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RE_ContinuationConfirm".Translate(reviewed.Reference.City.Label, reviewed.Reference.From.Name, reviewed.Reference.Target.Name).ToString() + (reviewed.Witness.RoyalCustodyV1 ? "\n\n" + "RE_RoyalContinuation".Translate().ToString() : ""), () => CommitContinuation(reviewed)));
        }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Follow-up review blocked: " + e.GetType().Name); Messages.Message("RE_CustodyBlocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
}
