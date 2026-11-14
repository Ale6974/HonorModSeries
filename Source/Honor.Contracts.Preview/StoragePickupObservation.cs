using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    internal bool CanObserveStoragePickup(Pawn pawn,Thing item,int requested)
    {
        var candidates=references.Where(r=>r.WorkProgressV1!=null&&Ledger.Transfer(r.Id).Phase==TerritoryTransferPhase.Prepared&&r.Residents?.Pawns.Contains(pawn)==true).ToArray();
        if(candidates.Length!=1)return false;var r=candidates[0];var t=Ledger.Transfer(r.Id);var work=WorkProgress.Validate(t,r.WorkProgressV1);
        if(work.Version!=3||r.LoadedMap?.Map!=pawn.Map||r.LoadedMap.AssetJournal==null||!r.LoadedMap.Things.Any(p=>ReferenceEquals(p,item)))return false;
        var map=LoadedMapPreservation.Restore(t.LoadedMap);var scope=map.Storage?.SingleOrDefault(s=>s.Contents.Contains(item.GetUniqueLoadID()));
        if(scope==null||map.OwnedAssets?.Any(p=>p.Id==scope.Id&&p.Owner==t.FromOwner)!=true||map.Groups?.Any(g=>g.Members.Contains(scope.Id))==true)return false;
        var entry=r.WorkAssetResolutionV5==null?null:WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5).Entries.SingleOrDefault(p=>p.Original==scope.Id);
        if(entry==null)return !MapAssetTransfers.Restore(r.LoadedMap.AssetJournal).Calls.Single(p=>p.Id==scope.Id).Started;
        if(requested>0&&requested<item.stackCount)return entry.StorageSplitsV19!=null;
        return !entry.Event.HasValue&&entry.StorageStackEventsV13==null&&entry.StorageMergeEventsV17==null&&entry.StorageProgressV11?.Any(row=>work.Events[row.Event].StoragePickupV18?.Shelf==scope.Id)==true&&WorkStorageProgressRules.Current(entry).Contents.Contains(item.GetUniqueLoadID());
    }
    internal void StageStoragePickup(WorkObservationState state,Pawn pawn,Thing item,int requested)
    {
        if(requested>0&&requested<item.stackCount){StageStorageSplit(state,pawn,item,requested);return;}
        var r=references.Single(p=>p.Id==state.Transfer);var t=Ledger.Transfer(r.Id);var work=WorkProgress.Validate(t,r.WorkProgressV1);var ev=work.Events.Last();
        if(state.Game!=Current.Game||work.Version!=3||ev.Kind!="haul-pickup"||ev.Actor!=pawn.GetUniqueLoadID()||ev.Sequence!=state.Sequence||ev.BodyReturned.HasValue||item.Faction!=null||item.Map!=r.LoadedMap.Map||!r.LoadedMap.Things.Any(p=>ReferenceEquals(p,item)))throw new ArgumentException("Same original mapped pickup input and actor required.");
        var map=LoadedMapPreservation.Restore(t.LoadedMap);var scope=map.Storage?.SingleOrDefault(s=>s.Contents.Contains(item.GetUniqueLoadID()));
        if(scope==null)throw new ArgumentException("Original shelf input required for this pickup scope.");
        var save=r.WorkAssetResolutionV5==null?null:WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5);var entry=save?.Entries.SingleOrDefault(p=>p.Original==scope.Id);
        if(entry!=null&&(entry.Event.HasValue||entry.StorageStackEventsV13!=null||entry.StorageMergeEventsV17!=null||entry.StorageProgressV11?.Any(row=>work.Events[row.Event].StoragePickupV18?.Shelf==scope.Id)!=true))throw new ArgumentException("Only original or explicit original-pickup shelf history supported.");
        var before=entry==null?scope:WorkStorageProgressRules.Current(entry);var shelf=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==scope.Id) as Building_Storage;
        if(shelf==null||JsonSerializer.Serialize(StorageMapEvidence.CaptureOwned(shelf,r.LoadedMap.Map))!=JsonSerializer.Serialize(before)||ResolutionPhysical(item,r.LoadedMap.Map)!=map.Things.Single(p=>p.Id==item.GetUniqueLoadID()))throw new ArgumentException("Exact unchanged original physical pickup input and current shelf scope required.");
        if(entry!=null)VerifyResolutionStorage(r.WorkAssetReferencesV5.Single(p=>p.Original==scope.Id),entry,shelf,r.LoadedMap,work);
        var origins=entry==null?CaptureStorageOrigins(t,r,"haul-place"):null;
        var start=new WorkStoragePickup(scope.Id,map.Things.Single(p=>p.Id==item.GetUniqueLoadID()),before,save?.BaseCalls??r.LoadedMap.AssetJournal,requested);
        r.WorkProgressV1=WorkProgress.BindStoragePickup(t,r.WorkProgressV1,start,origins);
    }
    private static WorkStoragePickup FinishStoragePickup(TerritoryTransferReference r,WorkProgressEvent ev,int? taken)
    {
        if(ev.Kind!="haul-pickup")return null;
        if(ev.StorageSplitV19!=null)return null;
        var start=ev.StoragePickupV18;
        if(start==null||taken!=start.Item.Count)throw new ArgumentException("Original before-body scope and exact whole pickup result required.");
        var pawn=r.Residents.Pawns.Single(p=>p!=null&&p.GetUniqueLoadID()==ev.Actor);var item=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==start.Item.Id);
        var carried=CarriedMergePhysical(pawn,item);
        if(carried!=start.Item with{Cell=-1})throw new ArgumentException("Same original whole object must remain in its original actor's exact native carry owner.");
        var shelf=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==start.Shelf) as Building_Storage;
        if(shelf==null)throw new ArgumentException("Original pickup shelf missing.");
        return start with{After=StorageMapEvidence.CaptureOwned(shelf,r.LoadedMap.Map),Carried=carried};
    }
}
