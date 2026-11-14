using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    private (string Document,List<WorkAssetReferenceV5> References) ObserveStorageProgress(TerritoryTransfer t,TerritoryTransferReference r,string progress,string document,List<WorkAssetReferenceV5> references,int sequence)
    {
        if(document==null)return(document,references);
        ResidentActiveWork.Known(typeof(Thing),"GetUniqueLoadID");
        ResidentActiveWork.Known(typeof(Thing),"get_ThingID");
        var save=WorkAssetResolutions.Validate(t,progress,document);var work=WorkProgress.Validate(t,progress);var nextRefs=references.ToList();string next=document;
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
