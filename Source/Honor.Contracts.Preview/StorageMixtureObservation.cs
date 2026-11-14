using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    private WorkStorageMixture[] CaptureStorageMixtures(TerritoryTransfer t,TerritoryTransferReference r,Pawn pawn,string kind)
    {
        if(kind!="recipe"||r.WorkAssetResolutionV5==null||pawn.jobs.curJob.targetB.Thing is UnfinishedThing)return null;
        var work=WorkProgress.Validate(t,r.WorkProgressV1);var save=WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5);var starts=new List<WorkStorageMixture>();
        foreach(var entry in save.Entries.Where(e=>e.StorageStackEventsV13!=null&&e.StorageProgressV11==null))
        {
            var reference=r.WorkAssetReferencesV5.Single(p=>p.Original==entry.Original);
            if(reference.Actual is not Building_Storage shelf)throw new ArgumentException("Same original count-tracked shelf required for mixed consumption.");
            VerifyResolutionStorage(reference,entry,shelf,r.LoadedMap,work);
            var before=StackPhysical(r,entry.StorageV7.Contents,shelf);
            var placed=(pawn.jobs.curJob.placedThings??[]).Where(p=>p.thing!=null&&entry.StorageV7.Contents.Contains(p.thing.GetUniqueLoadID())).OrderBy(p=>p.thing.GetUniqueLoadID(),StringComparer.Ordinal).ToArray();
            if(placed.Length==0||placed.Select(p=>p.thing).Distinct().Count()!=placed.Length)continue;
            ResidentActiveWork.Known(typeof(ThingCountClass),"get_Count");
            if(placed.Any(p=>p.Count<=0||p.Count>p.thing.stackCount))continue;
            var uses=placed.Select(p=>new WorkStackUse(p.thing.GetUniqueLoadID(),p.Count)).ToArray();
            var row=new WorkStorageMixture(entry.Original,before,uses);var remaining=WorkStorageMixtures.Remaining(row);
            if(remaining.Length==0||remaining.Length==before.Length)continue;
            starts.Add(row);
        }
        return starts.Count==0?null:starts.OrderBy(p=>p.Shelf,StringComparer.Ordinal).ToArray();
    }
    private static WorkStorageMixture[] FinishStorageMixtures(TerritoryTransferReference r,WorkProgressEvent ev)
    {
        if(ev.StorageMixturesV16==null)return null;
        return ev.StorageMixturesV16.Select(row=>
        {
            var shelf=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==row.Shelf) as Building_Storage;
            if(shelf==null)throw new ArgumentException("Same original mixed-consumption shelf missing.");
            var remaining=WorkStorageMixtures.Remaining(row);
            foreach(var removed in row.Before.Where(p=>!remaining.Any(q=>q.Id==p.Id)))
            {
                var thing=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==removed.Id);
                if(thing==null||!thing.Destroyed||thing.Spawned||thing.holdingOwner!=null||thing.Faction!=null)throw new ArgumentException("Same original fully consumed input must have native destruction output.");
            }
            return row with{After=StackPhysical(r,remaining.Select(p=>p.Id).ToArray(),shelf)};
        }).ToArray();
    }
}
