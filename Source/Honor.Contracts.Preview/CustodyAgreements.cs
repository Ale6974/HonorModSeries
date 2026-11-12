using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;
namespace Honor.Contracts.Preview;
public sealed record LiveCustodyAgreementReview(Game Context, CustodyAgreement Witness, string Label);
public sealed partial class TerritoryAdministrationLedger
{
    private CustodyAgreements custodyAgreements = new();
    private string custodyAgreementSnapshot;
    private void ExposeCustodyAgreements()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed && custodyAgreements.Records.Count > 0) custodyAgreementSnapshot = custodyAgreements.Export();
        Scribe_Values.Look(ref custodyAgreementSnapshot, "honorCurrentCustodyAgreementsV1");
        if (Scribe.mode != LoadSaveMode.PostLoadInit || custodyAgreementSnapshot == null) return;
        try { custodyAgreements = CustodyAgreements.Restore(custodyAgreementSnapshot); }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Current custody approvals unavailable; original raw archive retained: " + e.GetType().Name); }
    }
    private string CustodyFingerprint(TerritoryTransfer t, TerritoryTransferReference r)
    {
        if (r.Id != t.Id || t.Phase == TerritoryTransferPhase.Prepared && (r.City == null || r.From == null || r.Target == null ||
            "territory/" + r.City.GetUniqueLoadID() != t.Territory || r.From.GetUniqueLoadID() != t.FromOwner || r.Target.GetUniqueLoadID() != t.TargetOwner)) throw new ArgumentException("Original custody reference mismatch.");
        return EnrollmentDigest(JsonSerializer.Serialize(new { Transfer = t with { Phase = TerritoryTransferPhase.Prepared, ResolvedTick = null, Destruction = null },
            City = t.Territory, From = t.FromOwner, Target = t.TargetOwner, r.ResidentCensusProof,
            Residents = r.Residents?.Snapshot, Census = r.Residents?.Census, Map = r.LoadedMap?.Snapshot }));
    }
    private void CustodyBasis(TerritoryTransfer t, TerritoryTransferReference r)
    {
        if (Current.Game != ownerGame || Current.ProgramState != ProgramState.Playing || RestoreFailed || Charters.RestoreFailed || !NativeTransferBodies.ExtendedReadBodies ||
            UnionWarCouncil.HasExternalProvider || Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges ||
            Current.Game.GetComponent<LeaderJoinLedger>().BlocksChanges || Current.Game.GetComponent<LeaderRelocationLedger>().BlocksChanges ||
            Current.Game.GetComponent<SubtreeExitLedger>().BlocksChanges || HasPendingTownLosses ||
            t.Phase != TerritoryTransferPhase.Prepared || !t.CityCallJournalV1 || !r.CityCallV1 || r.City == null || r.City.GetType() != typeof(Settlement) || r.City.Destroyed ||
            !Find.WorldObjects.AllWorldObjects.Contains(r.City) || r.From == null || r.Target == null || r.Target.IsPlayer ||
            r.City.Faction != r.From && r.City.Faction != r.Target || r.CityCallStarted && !r.CityCallReturned ||
            r.City.Faction == r.Target && !r.CityCallReturned) throw new ArgumentException("Original returned custody/calls/current game unavailable.");
        var binding = Charters.Binding(t.Territory);
        if (binding.PlayerGroup || binding.FactionGroup || binding.Settlement != r.City) throw new ArgumentException("Original town binding changed.");
        ValidateMapReference(t, r, true); TerritoryCallGuard.Validate(r.CityCallV1, r.CityCallStarted, r.CityCallReturned);
        if (r.LoadedMap?.AssetJournal != null)
        {
            MapAssetTransfers.Pair(r.LoadedMap.AssetJournal, t.LoadedMap, t.FromOwner, t.TargetOwner);
            foreach (var a in LoadedMapPreservation.Restore(t.LoadedMap).OwnedAssets) _ = MapAssetTransfers.ExpectedOwner(r.LoadedMap.AssetJournal, a.Id);
        }
        if (r.Residents != null)
        {
            ResidentExecution.ValidateCensus(r.Residents, r.ResidentCensusProof, t.Territory, t.FromOwner, t.TargetOwner);
            ResidentExecution.ReviewCustody(r.Residents, r.City, r.From, r.Target, t.ResidentProof);
        }
        else if (t.ResidentProof != null || r.City.previouslyGeneratedInhabitants == null || r.City.previouslyGeneratedInhabitants.Count != 0) throw new ArgumentException("Original resident custody unavailable.");
        if (t.RoyalRightsV2 != null) RoyalRightsAdapter.Verify(r.Residents, t.RoyalRightsV2, t.RoyalResidentsV1);
        else if (t.RoyalResidentsV1 != null && RoyalResidentProof(r.Residents?.Pawns) != t.RoyalResidentsV1) throw new ArgumentException("Original royal civilian rights changed.");
        var actual = Observe(r.City, t.Territory, r.Target, residentProfile: r.Residents?.Profile ?? 7);
        if (!actual.Exists || actual.StockFormat != t.StockFormat || actual.LoadedMap != t.LoadedMap || actual.StockProof != t.StockProof ||
            actual.StockProof == null && actual.Visited || actual.Owner == t.FromOwner && actual.SourceSettlements < 2 && t.FromOwner != AdministrationId)
            throw new ArgumentException("Original stock/map/source base changed; current consent grants no restoration.");
    }
    private CustodyAgreement CurrentCustodyWitness(string transfer, string id, long tick, CustodyAgreement previous = null, TerritoryTransferLedger proposed = null, TerritoryTransferReference proposedReference = null)
    {
        var physical = proposed ?? Ledger;
        var t = physical.Transfer(transfer); var r = proposedReference ?? references.Single(r => r.Id == transfer); CustodyBasis(t, r);
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (payments.RestoreFailed || payments.Ledger.Receipts.Any(p => p.Phase == TributeReceiptPhase.Prepared)) throw new ArgumentException("Original liabilities unavailable.");
        string[] parties = CustodyAgreements.PartiesFor(Charters.Book, t);
        var current = Charters.ObserveForMigration(null).ToDictionary(o => o.Id, StringComparer.Ordinal);
        var actualParties = new List<CustodyParty>(); var observations = new List<TerritoryObservation>();
        foreach (string party in parties)
        {
            var entity = Charters.Book.Entity(party); var b = Charters.Binding(party);
            if (party == t.Territory)
            {
                // Preserve the representative-at-approval only after THIS original city call positively returned.
                string realm = previous != null && r.CityCallReturned && r.City.Faction == r.Target ? previous.Parties.Single(p => p.Party == party).Realm : r.City.Faction.GetUniqueLoadID();
                actualParties.Add(new(party, realm, false)); observations.Add(new(party, entity.NativeOwner, true, false, true));
            }
            else
            {
                var observation = current[party];
                string realm = b.PlayerGroup ? Faction.OfPlayer.GetUniqueLoadID() : b.FactionGroup ? b.OriginalFaction?.GetUniqueLoadID() : b.Settlement?.Faction?.GetUniqueLoadID();
                actualParties.Add(new(party, realm, entity.Player)); observations.Add(observation);
            }
        }
        string[] realmIds = actualParties.Where(p => !p.Player).Select(p => p.Realm).Concat([t.FromOwner, t.TargetOwner]).Concat(t.RoyalRightsV2 == null ? [] : RoyalRights.Realms(t.RoyalRightsV2)).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var realms = realmIds.Select(id => { var matches = Find.FactionManager.AllFactionsListForReading.Where(f => f.GetUniqueLoadID() == id).ToArray(); if (matches.Length != 1) throw new ArgumentException("Missing current party realm."); return matches[0]; }).ToArray();
        if (t.RoyalResidentsV1 != null) RoyalReadBoundaries();
        var evidence = realms.Select(NpcDecisionAuthority.Observe).ToArray();
        var w = new CustodyAgreement(id, transfer, Charters.Book.Export(), payments.Ledger.Export(), physical.Export(), CustodyFingerprint(t, r), actualParties.ToArray(), observations.ToArray(), evidence, tick);
        var foreign = Charters.Book.Charters.SingleOrDefault(c => c.Phase == RecordedCharterPhase.Active && c.Terms.Subject == t.Territory && !Charters.Book.PlayerManaged(c));
        string next = foreign == null ? null : w.Parties.Single(p => p.Party == foreign.Terms.Liege).Realm;
        if (t.PreviousTransferV1 == null && next != null && next != t.TargetOwner) w = w with { TransitDestinationV1 = next };
        w = w with { ForeignRightsV1 = CustodyAgreements.CaptureForeignRights(Charters.Book, t, w.Parties, w.TransitDestinationV1) };
        w = w with { RoyalCustodyV1 = t.RoyalResidentsV1 != null, RoyalStakeholdersV2 = t.RoyalRightsV2 == null ? null : RoyalRights.Realms(t.RoyalRightsV2) };
        w = w with { Regimes = NativeNpcContractAuthority.ReviewCustody(CustodyAgreements.Scope(w), tick, realms) };
        if (w.RoyalCustodyV1)
        {
            foreach (var d in w.Regimes ?? [])
            {
                if (d.Regime != NpcDecisionRegime.Royal) continue;
                var realm = realms.Single(f => f.GetUniqueLoadID() == d.Realm);
                var title = realm.leader.royalty.GetCurrentTitle(realm);
                if (title == null || title != DefDatabase<RoyalTitleDef>.GetNamedSilentFail(d.RoyalTitle) || title.seniority != d.Seniority)
                    throw new ArgumentException("Current registered own-realm royal title definition changed.");
            }
        }
        _ = CustodyAgreements.Validate(w); return w;
    }
    public LiveCustodyAgreementReview ReviewCustodyAgreement(string transfer)
    {
        if (nativeExecutionDepth != 0) throw new ArgumentException("Do not review during native execution.");
        var w = CurrentCustodyWitness(transfer, "custody-" + Guid.NewGuid().ToString("N"), Find.TickManager.TicksGame);
        return new(Current.Game, w, references.Single(r => r.Id == transfer).City.Label);
    }
    private void CommitCustodyAgreement(LiveCustodyAgreementReview review)
    {
        try
        {
            if (review?.Context != Current.Game || nativeExecutionDepth != 0) throw new ArgumentException("Review the current game first.");
            var fresh = CurrentCustodyWitness(review.Witness.Transfer, review.Witness.Id, review.Witness.Tick);
            if (JsonSerializer.Serialize(fresh) != JsonSerializer.Serialize(review.Witness)) throw new ArgumentException("Current party decisions or original custody changed.");
            var staged = CustodyAgreements.Restore(custodyAgreements.Export()); staged.Add(fresh);
            staged.ValidateHistory(Charters.Book, Ledger, Current.Game.GetComponent<TributePaymentLedger>().Ledger);
            var r = references.Single(r => r.Id == fresh.Transfer);
            // No original contract, obligation, medical state, call facts or physical object is replaced.
            custodyAgreements = staged; r.CustodyAgreementV1 = fresh.Id;
            Messages.Message("RE_CustodyApproved".Translate(), MessageTypeDefOf.NeutralEvent);
            Recover(fresh.Transfer);
        }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Current custody consent not committed/execution deferred; original records kept: " + e.GetType().Name); Messages.Message("RE_CustodyBlocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
    internal void RequireCurrentCustody(TerritoryTransferReference r)
    {
        if (r.CustodyAgreementV1 == null) return;
        var t = Ledger.Transfer(r.Id); var original = custodyAgreements.Record(r.CustodyAgreementV1);
        var fresh = CurrentCustodyWitness(r.Id, original.Witness.Id, original.Witness.Tick, original.Witness);
        custodyAgreements.RequireCurrent(r.CustodyAgreementV1, t, Charters.Book.Export(), Current.Game.GetComponent<TributePaymentLedger>().Ledger.Export(), CustodyFingerprint(t, r));
        if (JsonSerializer.Serialize(fresh) != JsonSerializer.Serialize(original.Witness)) throw new ArgumentException("Fresh current representative/third-party mandate changed.");
    }
    private void ValidateCustodyAgreements()
    {
        if (custodyAgreementSnapshot != null || custodyAgreements.Records.Count > 0)
            custodyAgreements.ValidateHistory(Charters.Book, Ledger, Current.Game.GetComponent<TributePaymentLedger>().Ledger);
        foreach (var receipt in custodyAgreements.Records)
        {
            var original = references.Single(r => r.Id == receipt.Witness.Transfer);
            if (receipt.Witness.Custody != CustodyFingerprint(Ledger.Transfer(original.Id), original) || original.CustodyAgreementV1 == null ||
                custodyAgreements.Records.Last(r => r.Witness.Transfer == original.Id).Witness.Id != original.CustodyAgreementV1)
                throw new ArgumentException("Unpaired original custody approval archive/reference.");
        }
        foreach (var r in references.Where(r => r.CustodyAgreementV1 != null))
        {
            var record = custodyAgreements.Record(r.CustodyAgreementV1); var t = Ledger.Transfer(r.Id);
            if (record.Witness.Transfer != r.Id || record.Witness.Custody != CustodyFingerprint(t, r) || record.Witness.Tick > Find.TickManager.TicksGame)
                throw new ArgumentException("Custody approval is not paired with its original execution.");
        }
    }
    internal void DrawCustodyAgreement(Listing_Standard list, string transfer)
    {
        if (RestoreFailed || Ledger.Transfer(transfer).Phase != TerritoryTransferPhase.Prepared) return;
        if (!list.ButtonText("RE_CustodyReview".Translate())) return;
        try
        {
            var reviewed = ReviewCustodyAgreement(transfer);
            string parties = string.Join(", ", reviewed.Witness.Parties.Select(p => Charters.Binding(p.Party).Settlement?.Label ?? (p.Player ? Faction.OfPlayer.Name : Charters.Binding(p.Party).OriginalFaction?.Name) ?? p.Party));
            string rulers = string.Join(", ", reviewed.Witness.Realms.Select(r => { var f = Find.FactionManager.AllFactionsListForReading.Single(f => f.GetUniqueLoadID() == r.Realm); return f.Name + "/" + f.leader.LabelShortCap; }));
            string text = "RE_CustodyConfirm".Translate(reviewed.Label, parties, rulers);
            if (reviewed.Witness.ForeignRightsV1 != null)
            {
                var right = JsonSerializer.Deserialize<CustodyForeignRight>(reviewed.Witness.ForeignRightsV1);
                var liege = Charters.Binding(right.Liege);
                text += "\n\n" + "RE_CustodyForeign".Translate(liege.Settlement?.Label ?? liege.OriginalFaction?.Name ?? right.Liege, right.Charter);
            }
            if (reviewed.Witness.TransitDestinationV1 != null)
                text += "\n\n" + "RE_Transit".Translate(Find.FactionManager.AllFactionsListForReading.Single(f => f.GetUniqueLoadID() == reviewed.Witness.TransitDestinationV1).Name);
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () => CommitCustodyAgreement(reviewed)));
        }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Custody completion review unavailable; original state retained: " + e.GetType().Name); Messages.Message("RE_CustodyBlocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
}
