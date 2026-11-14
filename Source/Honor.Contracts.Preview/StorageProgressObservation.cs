using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    private (string Document,List<WorkAssetReferenceV5> References) ObserveStorageProgress(TerritoryTransfer t,TerritoryTransferReference r,string progress,string document,List<WorkAssetReferenceV5> references,int sequence)
    {
        var work=WorkProgress.Validate(t,progress);var origins=work.Events[sequence].StorageOriginsV12;
        if(document==null&&origins==null)return(document,references);
        ResidentActiveWork.Known(typeof(Thing),"GetUniqueLoadID");
        ResidentActiveWork.Known(typeof(Thing),"get_ThingID");
        var map=LoadedMapPreservation.Restore(t.LoadedMap);var nextRefs=(references??[]).ToList();string next=document;
        foreach(var origin in origins?.Stores??[])
        {
            if(next!=null&&WorkAssetResolutions.Validate(t,progress,next).Entries.Any(e=>e.Original==origin.Id))continue;
            var original=map.Storage.Single(s=>s.Id==origin.Id);var shelf=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==origin.Id) as Building_Storage;
            if(shelf==null)throw new ArgumentException("Same original preflighted shelf reference required.");
            var current=StorageMapEvidence.CaptureOwned(shelf,r.LoadedMap.Map);
            if(JsonSerializer.Serialize(current)==JsonSerializer.Serialize(original))continue;
            var asset=map.OwnedAssets.Single(a=>a.Id==origin.Id);var physical=map.Things.Single(p=>p.Id==origin.Id);
            if(ResolutionPhysical(shelf,r.LoadedMap.Map)!=physical||ResolutionAsset(r,shelf,asset.Repair!=null,r.From)!=asset)throw new ArgumentException("Original preflighted shelf changed outside recorded contents.");
            if(next==null)next=WorkAssetResolutions.CreateStorage(t,progress,origins.Calls,origin.Id,sequence,current);
            else
            {
                if(WorkAssetResolutions.Validate(t,progress,next).BaseCalls!=origins.Calls)throw new ArgumentException("Original call prefix changed during storage observation.");
                next=WorkAssetResolutions.AddOriginal(t,progress,next,origin.Id);next=WorkAssetResolutions.AppendStorage(t,progress,next,origin.Id,sequence,current);
            }
            var entry=WorkAssetResolutions.Validate(t,progress,next).Entries.Single(e=>e.Original==origin.Id);
            var reference=new WorkAssetReferenceV5{Original=origin.Id,Actual=shelf,StorageContentsV7=original.Contents.Select(id=>r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==id)).ToList(),
                StorageProgressV11=[new(){Sequence=0,Contents=shelf.slotGroup.HeldThings.OrderBy(p=>p.GetUniqueLoadID(),StringComparer.Ordinal).ToList()}]};
            VerifyResolutionStorage(reference,entry,shelf,r.LoadedMap,work);nextRefs.Add(reference);
        }
        if(next==null)return(document,references);
        var save=WorkAssetResolutions.Validate(t,progress,next);
        foreach(var entry in save.Entries.Where(e=>e.StorageV7!=null))
        {
            var reference=nextRefs.Single(p=>p.Original==entry.Original);
            if(reference.Actual is not Building_Storage shelf)throw new ArgumentException("Same actual storage reference required.");
            var current=StorageMapEvidence.CaptureOwned(shelf,r.LoadedMap.Map);
            if(JsonSerializer.Serialize(current)==JsonSerializer.Serialize(WorkStorageProgressRules.Current(entry)))continue;
            if(ResolutionPhysical(shelf,r.LoadedMap.Map)!=entry.Physical||ResolutionAsset(r,shelf,entry.Asset.Repair!=null,entry.Returned?r.Target:r.From)!=entry.Asset)throw new ArgumentException("Storage physical state/authority changed beyond observed contents.");
            next=WorkAssetResolutions.AppendStorage(t,progress,next,entry.Original,sequence,current);
            var changed=WorkAssetResolutions.Validate(t,progress,next).Entries.Single(e=>e.Original==entry.Original);
            var refs=new WorkAssetReferenceV5{Original=reference.Original,Actual=reference.Actual,WorktableBillsV6=reference.WorktableBillsV6,StorageContentsV7=reference.StorageContentsV7,StorageProgressV11=(reference.StorageProgressV11??[]).ToList()};
            refs.StorageProgressV11.Add(new(){Sequence=changed.StorageProgressV11.Length-1,Contents=shelf.slotGroup.HeldThings.OrderBy(p=>p.GetUniqueLoadID(),StringComparer.Ordinal).ToList()});
            VerifyResolutionStorage(refs,changed,shelf,r.LoadedMap,work);
            nextRefs[nextRefs.FindIndex(p=>p.Original==entry.Original)]=refs;
        }
        return(next,nextRefs);
    }
}
