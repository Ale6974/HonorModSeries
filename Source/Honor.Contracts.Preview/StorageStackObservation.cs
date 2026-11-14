using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    private WorkStorageStacks[] CaptureStorageStacks(TerritoryTransfer t,TerritoryTransferReference r,Pawn pawn,string kind,WorkStorageOrigins origins)
    {
        if(kind!="recipe"||pawn.jobs.curJob.targetB.Thing is UnfinishedThing)return null;
        var work=WorkProgress.Validate(t,r.WorkProgressV1);if(work.Version!=3)return null;
        var map=LoadedMapPreservation.Restore(t.LoadedMap);var rows=new List<WorkStorageStacks>();
        foreach(var shelf in map.Storage??[])
        {
            bool first=origins?.Stores.Any(s=>s.Id==shelf.Id)==true;
            var entry=r.WorkAssetResolutionV5==null?null:WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5).Entries.SingleOrDefault(e=>e.Original==shelf.Id);
            if(!first&&entry?.StorageStackEventsV13==null)continue;
            if(shelf.Contents.Length==0||entry?.StorageProgressV11!=null)continue;
            var inputs=(pawn.jobs.curJob.placedThings??[]).Where(p=>p.thing!=null&&shelf.Contents.Contains(p.thing.GetUniqueLoadID())).ToArray();
            if(inputs.Length==0)continue;
            ResidentActiveWork.Known(typeof(ThingCountClass),"get_Count");
            if(inputs.Select(p=>p.thing).Distinct().Count()!=inputs.Length||inputs.Any(p=>p.Count<=0||p.Count>=p.thing.stackCount))continue;
            var actual=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==shelf.Id) as Building_Storage;
            if(actual==null||JsonSerializer.Serialize(StorageMapEvidence.CaptureOwned(actual,r.LoadedMap.Map))!=JsonSerializer.Serialize(shelf))throw new ArgumentException("Original same-membership shelf required for partial consumption.");
            if(entry!=null)VerifyResolutionStorage(r.WorkAssetReferencesV5.Single(p=>p.Original==shelf.Id),entry,actual,r.LoadedMap,work);
            rows.Add(new(shelf.Id,StackPhysical(r,shelf.Contents,actual)){Uses=inputs.Select(p=>new WorkStackUse(p.thing.GetUniqueLoadID(),p.Count)).OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray()});
        }
        return rows.Count==0?null:rows.OrderBy(row=>row.Shelf,StringComparer.Ordinal).ToArray();
    }
    private static NeutralMapThing[] StackPhysical(TerritoryTransferReference r,string[] ids,Building_Storage shelf)
    {
        return ids.Select(id=>
        {
            var actual=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==id);
            if(actual==null||actual.Faction!=null||actual is IThingHolder||actual.GetType()!=typeof(Thing)&&actual.GetType()!=typeof(ThingWithComps)||!shelf.slotGroup.HeldThings.Any(p=>ReferenceEquals(p,actual)))throw new ArgumentException("Same actual original basic neutral stored stack required.");
            return ResolutionPhysical(actual,r.LoadedMap.Map);
        }).ToArray();
    }
    private WorkStorageStacks[] FinishStorageStacks(TerritoryTransferReference r,WorkProgressEvent ev)
    {
        if(ev.StorageStacksV13==null)return null;
        return ev.StorageStacksV13.Select(row=>
        {
            var shelf=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==row.Shelf) as Building_Storage;
            if(shelf==null)throw new ArgumentException("Original observed shelf missing after recipe.");
            return row with{After=StackPhysical(r,row.Before.Select(p=>p.Id).ToArray(),shelf)};
        }).ToArray();
    }
    private (string Document,List<WorkAssetReferenceV5> References) ObserveStorageStacks(TerritoryTransfer t,TerritoryTransferReference r,string progress,string document,List<WorkAssetReferenceV5> refs,int sequence)
    {
        var work=WorkProgress.Validate(t,progress);var map=LoadedMapPreservation.Restore(t.LoadedMap);string next=document;var references=(refs??[]).ToList();
        foreach(var row in work.Events[sequence].StorageStacksV13??[])
        {
            if(Enumerable.SequenceEqual(row.Before,row.After))continue;
            string calls=work.Events.First(e=>e.StorageOriginsV12?.Stores.Any(p=>p.Id==row.Shelf)==true).StorageOriginsV12.Calls;
            next=WorkAssetResolutions.AppendStacks(t,progress,next,calls,row.Shelf,sequence);
            var entry=WorkAssetResolutions.Validate(t,progress,next).Entries.Single(e=>e.Original==row.Shelf);
            var reference=references.SingleOrDefault(p=>p.Original==row.Shelf);
            if(reference==null)
            {
                var scope=map.Storage.Single(s=>s.Id==row.Shelf);
                reference=new(){Original=row.Shelf,Actual=r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==row.Shelf),StorageContentsV7=scope.Contents.Select(id=>r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==id)).ToList()};references.Add(reference);
            }
            if(ResolutionPhysical(reference.Actual,r.LoadedMap.Map)!=entry.Physical||ResolutionAsset(r,reference.Actual,entry.Asset.Repair!=null,entry.Returned?r.Target:r.From)!=entry.Asset)throw new ArgumentException("Same original shelf physical state and independently verified owner required after partial consumption.");
            VerifyResolutionStorage(reference,entry,reference.Actual,r.LoadedMap,work);
        }
        return(next,references);
    }
}
