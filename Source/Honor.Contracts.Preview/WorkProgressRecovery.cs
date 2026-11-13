using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    internal WorkObservationState BeginWorkObservation(Pawn pawn,string kind,Thing[] inputs)
    {
        if(pawn?.jobs?.curJob==null || nativeExecutionDepth!=0 || RestoreFailed)return null;
        var candidates=references.Where(r=>r.WorkProgressV1!=null && Ledger.Transfer(r.Id).Phase==TerritoryTransferPhase.Prepared && r.Residents?.Pawns.Contains(pawn)==true).ToArray();
        if(candidates.Length==0)return null;if(candidates.Length!=1)throw new ArgumentException("Ambiguous original native work custody.");
        var r=candidates[0];var t=Ledger.Transfer(r.Id);var saved=WorkProgressBasis(t,r,true);var job=pawn.jobs.curJob;
        var original=LoadedMapPreservation.Restore(t.LoadedMap).ActiveWorkV1.SingleOrDefault(row=>row.Pawn==pawn.GetUniqueLoadID());
        if(original==null || !original.Jobs.Contains(job.GetUniqueLoadID()) || !r.Residents.ActiveWorkJobs.Contains(job))return null;
        if(saved.Events.Any(e=>e.Job==job.GetUniqueLoadID() && e.Returned.HasValue && e.Kind is "job-end" or "recipe"))return null;
        var before=WorkProgressSurvey.Capture(r);var ids=inputs.Select(x=>x?.GetUniqueLoadID()).ToArray();
        if(ids.Any(id=>id==null || !before.Ids.Contains(id)))throw new ArgumentException("Native progress input is not current original custody.");
        string next=WorkProgress.Begin(t,r.WorkProgressV1,kind,pawn.GetUniqueLoadID(),job.GetUniqueLoadID(),ids,before.Proof,Find.TickManager.TicksGame);
        r.WorkProgressV1=next;return new(Current.Game,this,r.Id,saved.Events.Length,before,ids);
    }
    internal void EndWorkObservation(WorkObservationState state)
    {
        if(Current.Game!=state.Game || Current.ProgramState!=ProgramState.Playing || Current.Game!=ownerGame)throw new ArgumentException("Native progress returned in a changed game.");
        var r=references.Single(x=>x.Id==state.Transfer);var t=Ledger.Transfer(r.Id);var current=WorkProgress.Validate(t,r.WorkProgressV1);
        if(current.Events.Last().Sequence!=state.Sequence || current.Events.Last().Returned.HasValue)throw new ArgumentException("Original progress start changed.");
        r.WorkProgressV1=WorkProgress.ObserveReturn(t,r.WorkProgressV1,state.Sequence,Find.TickManager.TicksGame);
        var after=WorkProgressSurvey.Capture(r);var removed=state.Before.Ids.Except(after.Ids).OrderBy(id=>id,StringComparer.Ordinal).ToArray();var added=after.Ids.Except(state.Before.Ids).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        r.WorkProgressV1=WorkProgress.Returned(t,r.WorkProgressV1,state.Sequence,removed,added,after.Proof,Find.TickManager.TicksGame);
    }
    private void SealWorkBoundary(TerritoryTransferReference r)
    {
        var t=Ledger.Transfer(r.Id);
        if(t.StockFormat!=25 || !t.StandaloneActiveWorkV1 || r.WorkProgressV1!=null)return;
        if(r.CityCallStarted || r.CustodyAgreementV1!=null || Current.Game!=ownerGame)throw new ArgumentException("New original pre-city work boundary required.");
        ValidateMapReference(t,r,true);ResidentExecution.Verify(r.Residents,r.City,r.From,r.Target,t.ResidentProof);
        ResidentExecution.ValidateCensus(r.Residents,r.ResidentCensusProof,t.Territory,t.FromOwner,t.TargetOwner,true);
        WorkSurvey survey;
        try{WorkProgressHooks.RequireObservers();survey=WorkProgressSurvey.Capture(r);}
        catch(ArgumentException){Log.Warning("[Honor.Contracts.Preview] Additional progress observation unavailable; unchanged original handover checks remain required.");return;}
        r.WorkProgressV1=WorkProgress.Prepare(t,r.Residents.Census,r.ResidentCensusProof,r.Residents.AuthorityJournal.ToArray(),r.Residents.CareJournal.ToArray(),
            r.Residents.AreaRecords.Select(a=>a.Journal).ToArray(),r.LoadedMap.AssetJournal,Find.TickManager.TicksGame,survey.Proof,survey.Ids);
    }
    private WorkProgressSave WorkProgressBasis(TerritoryTransfer t,TerritoryTransferReference r,bool current)
    {
        var s=WorkProgress.Validate(t,r.WorkProgressV1);
        if(r.Residents?.Profile!=16 || r.Residents.Snapshot!=ResidentTransferCensus.Restore(s.Seal.Census).Residents || r.Residents.Census!=s.Seal.Census || r.ResidentCensusProof!=s.Seal.CensusProof ||
            JsonSerializer.Serialize(r.Residents.AuthorityJournal)!=JsonSerializer.Serialize(s.Seal.Calls) || JsonSerializer.Serialize(r.Residents.CareJournal)!=JsonSerializer.Serialize(s.Seal.Care) ||
            JsonSerializer.Serialize(r.Residents.AreaRecords.Select(a=>a.Journal))!=JsonSerializer.Serialize(s.Seal.Areas) || r.LoadedMap?.Snapshot!=t.LoadedMap || r.LoadedMap.AssetJournal!=s.Seal.Assets || !r.Residents.Started || !r.ResidentCareJournalRequired ||
            !r.CityCallV1 || r.CityCallStarted&&!r.CityCallReturned)throw new ArgumentException("Original positive work/call/care/area/assets seal changed.");
        if(!current)return s;
        WorkProgressHooks.RequireObservers();
        if(!NativeTransferBodies.WorkObservationBodies || Current.Game!=ownerGame || Current.ProgramState!=ProgramState.Playing || RestoreFailed || Charters.RestoreFailed || UnionWarCouncil.HasExternalProvider || nativeExecutionDepth>1 ||
            Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges || Current.Game.GetComponent<LeaderJoinLedger>().BlocksChanges || Current.Game.GetComponent<LeaderRelocationLedger>().BlocksChanges ||
            Current.Game.GetComponent<SubtreeExitLedger>().BlocksChanges || HasPendingTownLosses || r.City?.Destroyed!=false || r.City.Map!=r.LoadedMap.Map || r.Residents.NativeMap!=r.City.Map ||
            r.City.Faction!=r.From && r.City.Faction!=r.Target || r.City.Faction==r.Target && !r.CityCallReturned || r.Target==null || r.Target.IsPlayer || r.From==null ||
            r.Target.GetUniqueLoadID()!=t.TargetOwner || r.From.GetUniqueLoadID()!=t.FromOwner || r.From.HostileTo(r.Target) || r.Target.HostileTo(r.From) || s.Seal.Tick>Find.TickManager.TicksGame)
            throw new ArgumentException("Same original peaceful current work custody unavailable.");
        var binding=Charters.Binding(t.Territory);if(binding.PlayerGroup||binding.FactionGroup||binding.Settlement!=r.City)throw new ArgumentException("Original work city binding changed.");
        if(!Find.FactionManager.AllFactionsListForReading.Contains(r.From) || !Find.FactionManager.AllFactionsListForReading.Contains(r.Target) ||
            r.City.Faction==r.From && Cities().Count(c=>c.Faction==r.From)<2 && t.FromOwner!=AdministrationId)
            throw new ArgumentException("Current registered realms and original last-base protection required.");
        if(!Find.WorldObjects.AllWorldObjects.Contains(r.City) || "territory/"+r.City.GetUniqueLoadID()!=t.Territory ||
            r.City.previouslyGeneratedInhabitants==null || r.City.previouslyGeneratedInhabitants.Count!=r.Residents.Pawns.Count || r.City.previouslyGeneratedInhabitants.Where((p,i)=>p!=r.Residents.Pawns[i]).Any())
            throw new ArgumentException("Same original city and ordered resident aliases required.");
        var census=ResidentMoveSet.Restore(r.Residents.Snapshot);
        if(r.Residents.Pawns.Count!=census.Residents.Count || r.Residents.Pawns.Where((p,i)=>p==null || p.GetUniqueLoadID()!=census.Residents[i].Id || p.Faction!=r.Target).Any())throw new ArgumentException("Original positive resident identities changed.");
        return s;
    }
    private void ValidateWorkCompletion(TerritoryTransfer t,TerritoryTransferReference r)
    {
        if(r.WorkProgressV1!=null)
        {
            var s=WorkProgressBasis(t,r,false);
            if(s.Seal.Tick>Find.TickManager.TicksGame || s.Events.Any(e=>e.Started>Find.TickManager.TicksGame || e.BodyReturned>Find.TickManager.TicksGame || e.Returned>Find.TickManager.TicksGame))throw new ArgumentException("Future native work evidence.");
        }
        if(t.WorkCompletionV1==null)return;
        var receipt=JsonSerializer.Deserialize<WorkCompletionReceipt>(t.WorkCompletionV1);
        if(r.WorkProgressV1==null || r.CustodyAgreementV1==null)throw new ArgumentException("Progress completion lost its native archive/current consent.");
        var approval=custodyAgreements.Record(r.CustodyAgreementV1);
        if(receipt.Agreement!=approval.Proof || receipt.Approval!=approval.Witness.WorkProgressV1 || WorkProgress.Approval(t,receipt.Approval).Progress!=r.WorkProgressV1)
            throw new ArgumentException("Progress completion is not the original current signed observation.");
    }
    private bool RetainsSealedWork(TerritoryTransfer t,TerritoryTransferReference r)
    {if(r.WorkProgressV1==null)return false;_ = WorkProgressBasis(t,r,false);return t.Phase==TerritoryTransferPhase.Prepared;}
    private string CurrentWorkApproval(TerritoryTransfer t,TerritoryTransferReference r,long tick)
    {
        var s=WorkProgressBasis(t,r,true);var survey=WorkProgressSurvey.Capture(r);
        var removed=new HashSet<string>(s.Events.Where(e=>e.Returned.HasValue).SelectMany(e=>e.Removed),StringComparer.Ordinal);
        if(s.Seal.Objects.Any(id=>!survey.Ids.Contains(id)&&!removed.Contains(id)))throw new ArgumentException("Missing original object has no positive native consumption/completion lineage.");
        return WorkProgress.Approve(t,r.WorkProgressV1,survey.Proof,tick);
    }
    internal void DrawWorkProgress(Listing_Standard list,string id)
    {
        var r=references.Single(x=>x.Id==id);if(r.WorkProgressV1==null)return;
        list.Label("RE_WorkProgressPending".Translate());
        if(!list.ButtonText("RE_WorkProgressReview".Translate()))return;
        try
        {
            var t=Ledger.Transfer(id);string progress=CurrentWorkApproval(t,r,Find.TickManager.TicksGame);
            var w=CurrentCustodyWitness(id,"work-consent-"+Guid.NewGuid().ToString("N"),Find.TickManager.TicksGame,workProgress:progress);
            var review=new LiveCustodyAgreementReview(Current.Game,w,r.City.Label);
            string parties=string.Join(", ",w.Parties.Select(p=>Charters.Binding(p.Party).Settlement?.Label??(p.Player?Faction.OfPlayer.Name:Charters.Binding(p.Party).OriginalFaction?.Name)??p.Party));
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RE_WorkProgressConfirm".Translate(review.Label,parties,WorkProgress.Validate(t,r.WorkProgressV1).Events.Length),()=>CommitCustodyAgreement(review)));
        }
        catch(Exception e){Log.Warning("[Honor.Contracts.Preview] Progress review retains original evidence: "+e.GetType().Name);Reject();}
    }
    private void RecoverWorkProgress(TerritoryTransfer t,TerritoryTransferReference r,Game game)
    {
        if(r.CustodyAgreementV1==null)throw new ArgumentException("Separate current work approval required.");
        var consent=custodyAgreements.Record(r.CustodyAgreementV1);
        if(consent.Witness.WorkProgressV1==null)throw new ArgumentException("Legacy custody consent grants no progressed-work permission.");
        RequireCurrentCustody(r);WorkProgressBasis(t,r,true);
        if(r.City.Faction==r.From)
        {
            TerritoryCallGuard.RequireNewCall(r.CityCallV1,r.CityCallStarted,r.CityCallReturned);
            RequireCurrentCustody(r);r.CityCallStarted=true;r.City.SetFaction(r.Target);
            if(Current.Game!=game || Current.ProgramState!=ProgramState.Playing)throw new ArgumentException("Current game changed during original city return.");
            r.CityCallReturned=true;
        }
        TerritoryCallGuard.RequireStartedForTarget(r.CityCallV1,r.CityCallStarted,r.CityCallReturned);
        RequireCurrentCustody(r);CityStockPreservation.InvalidateMaterial(r.City);
        Ledger.ConfirmWorkProgress(t.Id,consent.Witness.WorkProgressV1,consent,r.City.Faction.GetUniqueLoadID(),r.CityCallStarted,r.CityCallReturned,Find.TickManager.TicksGame);
        ResidentExecution.ReleaseReferences(r.Residents);LoadedMapRecords.Release(r.LoadedMap);
    }
}
