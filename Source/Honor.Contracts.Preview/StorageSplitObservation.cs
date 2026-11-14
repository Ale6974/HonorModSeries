using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    private void StageStorageSplit(WorkObservationState state,Pawn pawn,Thing item,int requested)
    {
        var r=references.Single(p=>p.Id==state.Transfer);var t=Ledger.Transfer(r.Id);var work=WorkProgress.Validate(t,r.WorkProgressV1);var ev=work.Events.Last();
        if(state.Game!=Current.Game||ev.Kind!="haul-pickup"||ev.Actor!=pawn.GetUniqueLoadID()||ev.BodyReturned.HasValue||ev.Sequence!=state.Sequence||!r.LoadedMap.Things.Any(p=>ReferenceEquals(p,item))||item.Map!=r.LoadedMap.Map||item.Faction!=null)throw new ArgumentException("Same original partial-pickup actor, map and item required.");
        var map=LoadedMapPreservation.Restore(t.LoadedMap);var shelfScope=map.Storage.Single(s=>s.Contents.Contains(item.GetUniqueLoadID()));var save=r.WorkAssetResolutionV5==null?null:WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5);var entry=save?.Entries.SingleOrDefault(e=>e.Original==shelfScope.Id);
        if(entry!=null&&entry.StorageSplitsV19==null)throw new ArgumentException("Only explicit original partial-pickup history supported.");
        var shelf=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==shelfScope.Id) as Building_Storage;
        if(shelf==null||JsonSerializer.Serialize(StorageMapEvidence.CaptureOwned(shelf,r.LoadedMap.Map))!=JsonSerializer.Serialize(shelfScope))throw new ArgumentException("Same original partial-pickup shelf settings/cells/membership required.");
        if(entry!=null)VerifyResolutionStorage(r.WorkAssetReferencesV5.Single(p=>p.Original==shelfScope.Id),entry,shelf,r.LoadedMap,work);
        var before=StackPhysical(r,shelfScope.Contents,shelf);var origins=entry==null?CaptureStorageOrigins(t,r,"haul-place"):null;
        r.WorkProgressV1=WorkProgress.BindStorageSplit(t,r.WorkProgressV1,new(shelfScope.Id,item.GetUniqueLoadID(),before,save?.BaseCalls??r.LoadedMap.AssetJournal,requested),origins);
    }
    private static WorkStorageSplit FinishStorageSplit(TerritoryTransferReference r,WorkProgressEvent ev,int? taken)
    {
        var start=ev.StorageSplitV19;if(start==null)return null;
        var before=start.Before.Single(p=>p.Id==start.Input);
        if(!taken.HasValue||taken<=0||taken>start.Requested||taken>=before.Count)throw new ArgumentException("Positive explicitly partial native pickup result required.");
        var pawn=r.Residents.Pawns.Single(p=>p!=null&&p.GetUniqueLoadID()==ev.Actor);var cargo=pawn.carryTracker.CarriedThing;
        var original=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==start.Input);
        if(cargo==null||ReferenceEquals(cargo,original)||r.LoadedMap.Things.Any(p=>ReferenceEquals(p,cargo)))throw new ArgumentException("New native carried split must remain separate from every original object.");
        var shelf=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==start.Shelf) as Building_Storage;
        if(shelf==null)throw new ArgumentException("Original split shelf missing.");
        return start with{After=StackPhysical(r,start.Before.Select(p=>p.Id).ToArray(),shelf),Carried=CarriedMergePhysical(pawn,cargo)};
    }
    private (string Document,List<WorkAssetReferenceV5> References) ObserveStorageSplit(TerritoryTransfer t,TerritoryTransferReference r,string progress,string document,List<WorkAssetReferenceV5> refs,int sequence)
    {
        var work=WorkProgress.Validate(t,progress);var row=work.Events[sequence].StorageSplitV19;if(row==null)return(document,refs);
        string next=WorkAssetResolutions.AppendSplit(t,progress,document,sequence);var entry=WorkAssetResolutions.Validate(t,progress,next).Entries.Single(e=>e.Original==row.Shelf);var references=(refs??[]).ToList();var old=references.SingleOrDefault(p=>p.Original==row.Shelf);
        var original=old??new WorkAssetReferenceV5{Original=row.Shelf,Actual=r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==row.Shelf),StorageContentsV7=entry.StorageV7.Contents.Select(id=>r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==id)).ToList()};
        var reference=new WorkAssetReferenceV5{Original=original.Original,Actual=original.Actual,StorageContentsV7=original.StorageContentsV7,SplitCargoV19=(original.SplitCargoV19??[]).ToList()};
        var pawn=r.Residents.Pawns.Single(p=>p!=null&&p.GetUniqueLoadID()==work.Events[sequence].Actor);var cargo=pawn.carryTracker.CarriedThing;
        if(cargo?.GetUniqueLoadID()!=row.Carried.Id)throw new ArgumentException("Same actual new native cargo reference required.");reference.SplitCargoV19.Add(cargo);
        if(ResolutionPhysical(reference.Actual,r.LoadedMap.Map)!=entry.Physical||ResolutionAsset(r,reference.Actual,entry.Asset.Repair!=null,entry.Returned?r.Target:r.From)!=entry.Asset)throw new ArgumentException("Same original split shelf and known owner required.");
        VerifyResolutionStorage(reference,entry,reference.Actual,r.LoadedMap,work);
        if(old==null)references.Add(reference);else references[references.IndexOf(old)]=reference;return(next,references);
    }
}
