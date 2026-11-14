using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    private static int ContinuationPrevious(WorkAssetResolution entry)=>entry.StorageContinuationsV20?.Last()??entry.StorageSplitsV19.Last();
    private void StageStorageContinuationPickup(TerritoryTransfer t,TerritoryTransferReference r,Pawn pawn,Thing item,int requested,WorkAssetResolution entry,WorkProgressSave work)
    {
        var reference=r.WorkAssetReferencesV5.Single(p=>p.Original==entry.Original);var before=WorkStorageSplits.Current(entry,work);
        VerifyResolutionStorage(reference,entry,reference.Actual,r.LoadedMap,work);
        if(item.Faction!=null||!r.LoadedMap.Things.Any(p=>ReferenceEquals(p,item))||ResolutionPhysical(item,r.LoadedMap.Map)!=before.SingleOrDefault(p=>p.Id==item.GetUniqueLoadID()))throw new ArgumentException("Same original latest remainder required for whole pickup continuation.");
        var calls=WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5).BaseCalls;
        r.WorkProgressV1=WorkProgress.BindStorageContinuation(t,r.WorkProgressV1,new(entry.Original,ContinuationPrevious(entry),"take",before,before.Single(p=>p.Id==item.GetUniqueLoadID()),null,-1,requested,calls));
    }
    private WorkObservationState StageStorageContinuationPlace(WorkObservationState state,TerritoryTransfer t,TerritoryTransferReference r,Pawn pawn,Thing cargo,int cell,int requested,WorkAssetResolution entry,WorkProgressSave work)
    {
        var reference=r.WorkAssetReferencesV5.Single(p=>p.Original==entry.Original);VerifyResolutionStorage(reference,entry,reference.Actual,r.LoadedMap,work);
        var before=WorkStorageSplits.Current(entry,work);var input=CarriedMergePhysical(pawn,cargo);
        var prior=work.Events.Take(state.Sequence).Any(e=>e.StorageSplitV19?.Carried==input||e.StorageContinuationV20?.Carried==input);
        if(!prior||requested!=input.Count||!entry.StorageV7.Cells.Contains(cell))return state;
        var matches=before.Where(p=>p.Cell==cell&&p.Def==input.Def&&p.Stuff==input.Stuff).ToArray();string action;string target=null;
        if(matches.Length==1)
        {
            var actual=r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==matches[0].Id);
            if(matches[0].HitPoints!=input.HitPoints||matches[0].Quality!=input.Quality||(long)matches[0].Count+input.Count>cargo.def.stackLimit||actual.GetType()!=cargo.GetType()||actual.def!=cargo.def||(actual as ThingWithComps)?.AllComps.Count>0||(cargo as ThingWithComps)?.AllComps.Count>0)return state;
            foreach(var type in new[]{typeof(Thing),actual.GetType()})foreach(var name in new[]{nameof(Thing.CanStackWith),nameof(Thing.TryAbsorbStack)})
            {
                var method=AccessTools.Method(type,name);if(method==null||method.Module.Assembly!=typeof(Thing).Assembly||Harmony.GetPatchInfo(method)?.Owners.Any()==true)throw new ArgumentException("Known original restack dispatch required.");
            }
            ResidentActiveWork.Known(typeof(ThingUtility),"TryAbsorbStackNumToTake");action="merge";target=matches[0].Id;
        }
        else if(matches.Length==0&&r.LoadedMap.Things.Any(p=>ReferenceEquals(p,cargo))&&!before.Any(p=>p.Id==input.Id))action="put";
        else return state;
        string calls=WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5).BaseCalls;
        r.WorkProgressV1=WorkProgress.BindStorageContinuation(t,r.WorkProgressV1,new(entry.Original,ContinuationPrevious(entry),action,before,input,target,cell,requested,calls));return state with{MergeCargo=cargo};
    }
    private static WorkStorageContinuation FinishStorageContinuation(TerritoryTransferReference r,WorkProgressEvent ev,WorkObservationState state,int? taken,bool? placed)
    {
        var start=ev.StorageContinuationV20;if(start==null)return null;
        var pawn=r.Residents.Pawns.Single(p=>p!=null&&p.GetUniqueLoadID()==ev.Actor);var shelf=r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==start.Shelf) as Building_Storage;
        if(shelf==null)throw new ArgumentException("Same continuation shelf required.");
        NeutralMapThing carried=null;
        if(start.Action=="take")
        {
            if(taken!=start.Input.Count)throw new ArgumentException("Exact whole remainder pickup return required.");
            var original=r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==start.Input.Id);carried=CarriedMergePhysical(pawn,original);
        }
        else
        {
            if(placed!=true||state.MergeCargo?.GetUniqueLoadID()!=start.Input.Id)throw new ArgumentException("Exact same-call original carried witness and true placement return required.");
            if(start.Action=="merge"&&(!state.MergeCargo.Destroyed||state.MergeCargo.Spawned||state.MergeCargo.holdingOwner!=null))throw new ArgumentException("Restack must positively destroy the same input through native absorption.");
            if(start.Action=="put"&&(!state.MergeCargo.Spawned||state.MergeCargo.Map!=r.LoadedMap.Map||!r.LoadedMap.Things.Any(p=>ReferenceEquals(p,state.MergeCargo))))throw new ArgumentException("Same original whole-picked object must actually return to the map.");
        }
        var ids=StorageMapEvidence.CaptureOwned(shelf,r.LoadedMap.Map).Contents;
        return start with{After=StackPhysical(r,ids,shelf),Carried=carried};
    }
    private (string Document,List<WorkAssetReferenceV5> References) ObserveStorageContinuation(TerritoryTransfer t,TerritoryTransferReference r,string progress,string document,List<WorkAssetReferenceV5> refs,int sequence)
    {
        var work=WorkProgress.Validate(t,progress);var row=work.Events[sequence].StorageContinuationV20;if(row==null)return(document,refs);
        string next=WorkAssetResolutions.AppendContinuation(t,progress,document,sequence);var entry=WorkAssetResolutions.Validate(t,progress,next).Entries.Single(p=>p.Original==row.Shelf);var reference=refs.Single(p=>p.Original==row.Shelf);
        if(ResolutionPhysical(reference.Actual,r.LoadedMap.Map)!=entry.Physical||ResolutionAsset(r,reference.Actual,entry.Asset.Repair!=null,entry.Returned?r.Target:r.From)!=entry.Asset)throw new ArgumentException("Same original continuation property and independently known owner required.");
        VerifyResolutionStorage(reference,entry,reference.Actual,r.LoadedMap,work);return(next,refs);
    }
}
