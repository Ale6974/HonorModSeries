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
        WorkEventGrantV4 grant=null;
        if(kind=="haul-place" || original==null || !original.Jobs.Contains(job.GetUniqueLoadID()) || r.Residents.ActiveWorkJobs?.Contains(job)!=true)
        {
            RequireWorkJobIdentity();
            var record=r.WorkJobObservationsV4?.LastOrDefault(record=>record.Matches(pawn,job));if(record==null)return null;
            grant=JsonSerializer.Deserialize<WorkEventGrantV4>(record.Grant);
        }
        if(saved.Events.Any(e=>e.Job==job.GetUniqueLoadID() && e.Returned.HasValue && e.Kind is "job-end" or "recipe"))return null;
        var before=WorkProgressSurvey.Capture(r);var ids=inputs.Select(x=>x?.GetUniqueLoadID()).ToArray();
        if(ids.Any(id=>id==null || !before.Ids.Contains(id)))throw new ArgumentException("Native progress input is not current original custody.");
        var filth=FrameFilthEvidence.Current(t,r,kind,inputs);
        if(filth?.Any(f=>!before.Ids.Contains(f.Id))==true)throw new ArgumentException("Original ancillary filth is absent from the actual before-survey.");
        string next=WorkProgress.Begin(t,r.WorkProgressV1,kind,pawn.GetUniqueLoadID(),job.GetUniqueLoadID(),ids,before.Proof,Find.TickManager.TicksGame,grant,filth);
        r.WorkProgressV1=next;return new(Current.Game,this,r.Id,saved.Events.Length,before,ids);
    }
    internal void EndWorkObservation(WorkObservationState state,bool? placed=null)
    {
        if(Current.Game!=state.Game || Current.ProgramState!=ProgramState.Playing || Current.Game!=ownerGame)throw new ArgumentException("Native progress returned in a changed game.");
        var r=references.Single(x=>x.Id==state.Transfer);var t=Ledger.Transfer(r.Id);var current=WorkProgress.Validate(t,r.WorkProgressV1);
        if(current.Events.Last().Sequence!=state.Sequence || current.Events.Last().Returned.HasValue)throw new ArgumentException("Original progress start changed.");
        r.WorkProgressV1=placed.HasValue?WorkProgress.ObservePlacementReturn(t,r.WorkProgressV1,state.Sequence,Find.TickManager.TicksGame,placed.Value):WorkProgress.ObserveReturn(t,r.WorkProgressV1,state.Sequence,Find.TickManager.TicksGame);
        FrameFilthEvidence.After(r,current.Events.Last());
        var candidate=ObserveFrameSuccessor(t,r,state,current.Events.Last());
        var after=WorkProgressSurvey.Capture(r,candidate?.Reference);var removed=state.Before.Ids.Except(after.Ids).OrderBy(id=>id,StringComparer.Ordinal).ToArray();var added=after.Ids.Except(state.Before.Ids).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        string next=WorkProgress.Returned(t,r.WorkProgressV1,state.Sequence,removed,added,after.Proof,Find.TickManager.TicksGame);
        string resolution=r.WorkAssetResolutionV5;var actualRefs=r.WorkAssetReferencesV5;
        if(candidate!=null)
        {
            resolution=candidate.Terrain==null?WorkAssetResolutions.AddSuccessor(t,next,resolution,r.LoadedMap.AssetJournal,candidate.Reference.Original,state.Sequence,candidate.Asset,candidate.Physical,candidate.Worktable,candidate.Storage):
                WorkAssetResolutions.AddTerrain(t,next,resolution,r.LoadedMap.AssetJournal,candidate.Reference.Original,state.Sequence,candidate.Terrain);
            actualRefs=(actualRefs??[]).Append(candidate.Reference).ToList();
        }
        (resolution,actualRefs)=ObserveStorageProgress(t,r,next,resolution,actualRefs,state.Sequence);
        r.WorkProgressV1=next;r.WorkAssetResolutionV5=resolution;r.WorkAssetReferencesV5=actualRefs;
    }
    private void SealWorkBoundary(TerritoryTransferReference r,bool beforeAssets=false)
    {
        var t=Ledger.Transfer(r.Id);
        if(t.StockFormat!=25 || !t.StandaloneActiveWorkV1 || r.WorkProgressV1!=null)return;
        if(r.CityCallStarted || r.CustodyAgreementV1!=null || Current.Game!=ownerGame)throw new ArgumentException("New original pre-city work boundary required.");
        ValidateMapReference(t,r,true);ResidentExecution.Verify(r.Residents,r.City,r.From,r.Target,t.ResidentProof);
        ResidentExecution.ValidateCensus(r.Residents,r.ResidentCensusProof,t.Territory,t.FromOwner,t.TargetOwner,true);
        WorkSurvey survey;
        try{WorkProgressHooks.RequireObservers();survey=WorkProgressSurvey.Capture(r);}
        catch(ArgumentException){Log.Warning("[Honor.Contracts.Preview] Additional progress observation unavailable; unchanged original handover checks remain required.");return;}
        var calls=r.Residents.AuthorityJournal.ToArray();var care=r.Residents.CareJournal.ToArray();var areas=r.Residents.AreaRecords.Select(a=>a.Journal).ToArray();
        r.WorkProgressV1=beforeAssets?WorkProgress.PrepareBeforeAssets(t,r.Residents.Census,r.ResidentCensusProof,calls,care,areas,r.LoadedMap.AssetJournal,Find.TickManager.TicksGame,survey.Proof,survey.Ids):
            WorkProgress.Prepare(t,r.Residents.Census,r.ResidentCensusProof,calls,care,areas,r.LoadedMap.AssetJournal,Find.TickManager.TicksGame,survey.Proof,survey.Ids);
        if(beforeAssets)r.WorkAssetVerifiedV2=[];
    }
    private WorkProgressSave WorkProgressBasis(TerritoryTransfer t,TerritoryTransferReference r,bool current)
    {
        var s=WorkProgress.Validate(t,r.WorkProgressV1);
        if(r.Residents?.Profile!=16 || r.Residents.Pawns==null || t.Phase==TerritoryTransferPhase.Prepared&&r.Residents.Pawns.Any(p=>p==null) || r.Residents.Snapshot!=ResidentTransferCensus.Restore(s.Seal.Census).Residents || r.Residents.Census!=s.Seal.Census || r.ResidentCensusProof!=s.Seal.CensusProof ||
            s.Version!=3&&(JsonSerializer.Serialize(r.Residents.AuthorityJournal)!=JsonSerializer.Serialize(s.Seal.Calls) || JsonSerializer.Serialize(r.Residents.CareJournal)!=JsonSerializer.Serialize(s.Seal.Care) ||
            JsonSerializer.Serialize(r.Residents.AreaRecords.Select(a=>a.Journal))!=JsonSerializer.Serialize(s.Seal.Areas)) || r.LoadedMap?.Snapshot!=t.LoadedMap || !r.Residents.Started || !r.ResidentCareJournalRequired ||
            !r.CityCallV1 || r.CityCallStarted&&!r.CityCallReturned)throw new ArgumentException("Original positive work/call/care/area/assets seal changed.");
        WorkProgress.AssetAdvance(t,r.WorkProgressV1,r.LoadedMap.AssetJournal,t.Phase==TerritoryTransferPhase.Applied,r.WorkAssetResolutionV5);
        if(s.Version==3)WorkProgress.ClinicalAdvance(t,r.WorkProgressV1,ClinicalState(r.Residents),t.Phase==TerritoryTransferPhase.Applied);
        if(s.Version==3)
        {
            if(r.WorkNativeRosterV3==null || (t.Phase==TerritoryTransferPhase.Prepared || r.WorkNativeRosterV3.Count!=0) &&
                !r.WorkNativeRosterV3.Select(p=>p?.GetUniqueLoadID()).SequenceEqual(s.RosterV3))throw new ArgumentException("Original native roster reference order changed.");
            if(t.Phase==TerritoryTransferPhase.Prepared && (r.WorkNativeRosterV3.Distinct().Count()!=r.WorkNativeRosterV3.Count || r.WorkNativeRosterV3.Any(p=>!r.Residents.Pawns.Contains(p))))throw new ArgumentException("Original native roster must retain the same original Pawn references.");
            if(r.WorkResidentStartsV3==null || r.WorkResidentStartsV3.Count>r.Residents.AuthorityJournal.Count || r.WorkResidentStartsV3.Any(raw=>raw==null||raw.Length>2048))throw new ArgumentException("Missing or oversized current resident starts.");
            var starts=r.WorkResidentStartsV3.Select(raw=>JsonSerializer.Deserialize<WorkResidentStart>(raw)).ToArray();
            if(starts.Any(p=>p==null)||starts.Select(p=>p.Pawn).Distinct().Count()!=starts.Length)throw new ArgumentException("Duplicate current resident start.");
            foreach(var start in starts)
            {
                var consent=custodyAgreements.Record(start.Agreement);var a=WorkProgress.Approval(t,consent.Witness.WorkProgressV1);
                int i=ResidentMoveSet.Restore(r.Residents.Snapshot).Residents.ToList().FindIndex(p=>p.Id==start.Pawn);
                if(i<0 || !ResidentTransferCensus.Call(r.Residents.AuthorityJournal[i]).Started || !CityStockProof.Valid(start.CurrentProof) || start.Tick<consent.Witness.Tick || start.Tick>Find.TickManager.TicksGame ||
                    a.PendingV3?.SingleOrDefault(p=>p.Pawn==start.Pawn)?.Proof!=start.CurrentProof)throw new ArgumentException("Current resident start lost its exact approved current proof.");
            }
        }
        else if(r.WorkResidentStartsV3!=null || r.WorkNativeRosterV3!=null)throw new ArgumentException("Unexpected current resident starts or roster.");
        if(s.Version==1 && r.WorkAssetVerifiedV2!=null || s.Version>=2 && (r.WorkAssetVerifiedV2==null || r.WorkAssetVerifiedV2.Count>1000 || r.WorkAssetVerifiedV2.Distinct().Count()!=r.WorkAssetVerifiedV2.Count ||
            r.WorkAssetVerifiedV2.Any(id=>r.LoadedMap.AssetJournal==null||!MapAssetTransfers.Restore(r.LoadedMap.AssetJournal).Calls.Any(c=>c.Id==id&&c.Returned))))throw new ArgumentException("Unpaired original partial asset verification facts.");
        if(s.Version>=2 && s.Seal.Assets!=null && (current || t.Phase==TerritoryTransferPhase.Applied))
        {
            var initial=MapAssetTransfers.Restore(s.Seal.Assets);
            if(MapAssetTransfers.Restore(r.LoadedMap.AssetJournal).Calls.Where((c,i)=>c.Returned&&!initial.Calls[i].Returned&&!r.WorkAssetVerifiedV2.Contains(c.Id)).Any())
                throw new ArgumentException("Normal asset return needs independent positive preservation verification.");
        }
        ValidateWorkObservations(t,r);
        ValidateAssetResolutions(t,r,false);
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
            r.City.previouslyGeneratedInhabitants==null || r.City.previouslyGeneratedInhabitants.Count!=r.Residents.Pawns.Count || !r.City.previouslyGeneratedInhabitants.SequenceEqual(s.Version==3?r.WorkNativeRosterV3:r.Residents.Pawns))
            throw new ArgumentException("Same original city and ordered resident aliases required.");
        var census=ResidentMoveSet.Restore(r.Residents.Snapshot);
        if(r.Residents.Pawns.Count!=census.Residents.Count || r.Residents.Pawns.Where((p,i)=>p==null || p.GetUniqueLoadID()!=census.Residents[i].Id || p.Faction!=(s.Version==3&&!ResidentTransferCensus.Call(r.Residents.AuthorityJournal[i]).Returned?r.From:r.Target)).Any())throw new ArgumentException("Original positive resident identities changed.");
        ValidateAssetResolutions(t,r,true);return s;
    }
    private void ValidateWorkCompletion(TerritoryTransfer t,TerritoryTransferReference r)
    {
        if(r.WorkProgressV1==null&&r.WorkJobObservationsV4!=null)throw new ArgumentException("Work observation archive has no original custody seal.");
        if(r.WorkProgressV1==null&&(r.WorkAssetResolutionV5!=null||r.WorkAssetReferencesV5!=null))throw new ArgumentException("Actual property has no original custody seal.");
        if(r.WorkProgressV1!=null)
        {
            var s=WorkProgressBasis(t,r,false);
            if(s.Seal.Tick>Find.TickManager.TicksGame || s.Events.Any(e=>e.Started>Find.TickManager.TicksGame || e.BodyReturned>Find.TickManager.TicksGame || e.Returned>Find.TickManager.TicksGame))throw new ArgumentException("Future native work evidence.");
        }
        if(t.WorkCompletionV1==null)return;
        var receipt=JsonSerializer.Deserialize<WorkCompletionReceipt>(t.WorkCompletionV1);
        var progress=WorkProgress.Validate(t,r.WorkProgressV1);
        if(progress.Version==3){WorkProgress.ClinicalAdvance(t,r.WorkProgressV1,receipt.ClinicalV3,true);if(JsonSerializer.Serialize(receipt.ClinicalV3)!=JsonSerializer.Serialize(ClinicalState(r.Residents)))throw new ArgumentException("Terminal original clinical outputs changed.");}
        else if(receipt.ClinicalV3!=null)throw new ArgumentException("Unexpected clinical completion scope.");
        if(receipt.ResolutionsV5!=r.WorkAssetResolutionV5)throw new ArgumentException("Terminal actual-property ledger changed.");
        if(progress.Version>=2){WorkProgress.AssetAdvance(t,r.WorkProgressV1,receipt.AssetCallsV2,true,receipt.ResolutionsV5);if(receipt.AssetCallsV2!=r.LoadedMap.AssetJournal)throw new ArgumentException("Terminal original asset calls changed.");}
        else if(receipt.AssetCallsV2!=null)throw new ArgumentException("Unexpected partial asset completion scope.");
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
        var pending=s.Version==3?r.Residents.Pawns.Where((p,i)=>!ResidentTransferCensus.Call(r.Residents.AuthorityJournal[i]).Started).Select(p=>new WorkPendingResident(p.GetUniqueLoadID(),ResidentExecution.FreshClinical(p).Preserved)).ToArray():null;
        return WorkProgress.Approve(t,r.WorkProgressV1,survey.Proof,tick,pending,s.Version==3?CurrentObservedJobs(r):null,r.WorkAssetResolutionV5);
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
            string key=WorkProgress.Validate(t,r.WorkProgressV1).Version>=2 && r.LoadedMap.AssetJournal!=null && MapAssetTransfers.Restore(r.LoadedMap.AssetJournal).Calls.Any(c=>!c.Returned)?"RE_WorkAssetConfirm":"RE_WorkProgressConfirm";
            if(WorkProgress.Validate(t,r.WorkProgressV1).Version==3 && r.Residents.AuthorityJournal.Where((raw,i)=>!ResidentTransferCensus.Call(raw).Returned || !ResidentCareJournal.Restore(r.Residents.CareJournal[i]).State.Consumed || !ResidentAreas.Restore(r.Residents.AreaRecords[i].Journal).Consumed).Any())key="RE_WorkClinicalConfirm";
            else if(r.WorkAssetResolutionV5!=null)key="RE_WorkSuccessorConfirm";
            string message=key.Translate(review.Label,parties,WorkProgress.Validate(t,r.WorkProgressV1).Events.Length);
            int observed=WorkProgress.Approval(t,progress).JobsV4?.Length??0;
            if(observed>0)message+="\n\n"+"RE_WorkJobConsent".Translate(observed);
            int filth=WorkProgress.Validate(t,r.WorkProgressV1).Events.Sum(e=>e.FilthV10?.Length??0);
            if(filth>0)message+="\n\n"+"RE_WorkFrameFilthReview".Translate(filth);
            int storage=r.WorkAssetResolutionV5==null?0:WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5).Entries.Sum(e=>e.StorageProgressV11?.Length??0);
            if(storage>0)message+="\n\n"+"RE_WorkStorageProgressReview".Translate(storage);
            if(r.WorkAssetResolutionV5!=null)
            {
                var terrain=WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5).Entries.Where(e=>e.TerrainV8?.LayersV9!=null).ToArray();
                var originalMap=LoadedMapPreservation.Restore(t.LoadedMap);
                foreach(var entry in terrain.Take(4))message+="\n\n"+TerrainWorkEvidence.Review(r.LoadedMap.Map,entry.TerrainV8,originalMap.WorkFramesV1.Single(f=>f.Id==entry.Original).TerrainLayersV9);
                if(terrain.Length>4)message+="\n"+"RE_WorkTerrainMoreReview".Translate(terrain.Length-4);
            }
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(message,()=>CommitCustodyAgreement(review)));
        }
        catch(Exception e){Log.Warning("[Honor.Contracts.Preview] Progress review retains original evidence: "+e.GetType().Name);Reject();}
    }
    private void RecoverWorkProgress(TerritoryTransfer t,TerritoryTransferReference r,Game game)
    {
        if(r.CustodyAgreementV1==null)throw new ArgumentException("Separate current work approval required.");
        var consent=custodyAgreements.Record(r.CustodyAgreementV1);
        if(consent.Witness.WorkProgressV1==null)throw new ArgumentException("Legacy custody consent grants no progressed-work permission.");
        RequireCurrentCustody(r);WorkProgressBasis(t,r,true);
        if(WorkProgress.Validate(t,r.WorkProgressV1).Version==3 && ResidentExecution.ResumeClinicalOriginal(r)){Messages.Message("RE_WorkClinicalProgress".Translate(),MessageTypeDefOf.NeutralEvent);return;}
        if(r.WorkAssetResolutionV5!=null && ResumeAssetResolutions(t,r))return;
        if(r.WorkAssetResolutionV5==null && WorkProgress.Validate(t,r.WorkProgressV1).Version>=2 && r.LoadedMap.AssetJournal!=null && MapAssetTransfers.Restore(r.LoadedMap.AssetJournal).Calls.Any(c=>!c.Returned))
        {
            MapAssetRecords.ApplyProgressNext(r);
            Messages.Message("RE_WorkAssetProgress".Translate(),MessageTypeDefOf.NeutralEvent);
            return; // A changed physical survey requires a fresh explicit current agreement for the next step.
        }
        if(r.City.Faction==r.From)
        {
            TerritoryCallGuard.RequireNewCall(r.CityCallV1,r.CityCallStarted,r.CityCallReturned);
            RequireCurrentCustody(r);r.CityCallStarted=true;r.City.SetFaction(r.Target);
            if(Current.Game!=game || Current.ProgramState!=ProgramState.Playing)throw new ArgumentException("Current game changed during original city return.");
            r.CityCallReturned=true;
        }
        TerritoryCallGuard.RequireStartedForTarget(r.CityCallV1,r.CityCallStarted,r.CityCallReturned);
        RequireCurrentCustody(r);CityStockPreservation.InvalidateMaterial(r.City);
        Ledger.ConfirmWorkProgress(t.Id,consent.Witness.WorkProgressV1,consent,r.City.Faction.GetUniqueLoadID(),r.CityCallStarted,r.CityCallReturned,Find.TickManager.TicksGame,
            WorkProgress.Validate(t,r.WorkProgressV1).Version>=2?r.LoadedMap.AssetJournal:null,
            WorkProgress.Validate(t,r.WorkProgressV1).Version==3?ClinicalState(r.Residents):null,r.WorkAssetResolutionV5);
        ResidentExecution.ReleaseReferences(r.Residents);LoadedMapRecords.Release(r.LoadedMap);
        r.WorkNativeRosterV3?.Clear();
        ReleaseWorkObservationReferences(r);
    }
}
