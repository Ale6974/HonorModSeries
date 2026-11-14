using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    private WorkStorageRetirement[] CaptureStorageRetirements(TerritoryTransfer t,TerritoryTransferReference r,Pawn pawn,string kind)
    {
        if(kind!="recipe"||r.WorkAssetResolutionV5==null||pawn.jobs.curJob.targetB.Thing is UnfinishedThing)return null;
        var work=WorkProgress.Validate(t,r.WorkProgressV1);var ledger=WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5);var starts=new List<WorkStorageRetirement>();
        foreach(var entry in ledger.Entries.Where(e=>e.StorageStackEventsV13!=null&&e.StorageProgressV11==null))
        {
            var reference=r.WorkAssetReferencesV5.Single(p=>p.Original==entry.Original);
            if(reference.Actual is not Building_Storage shelf)throw new ArgumentException("Same original count-tracked shelf required.");
            VerifyResolutionStorage(reference,entry,shelf,r.LoadedMap,work);
            var before=StackPhysical(r,entry.StorageV7.Contents,shelf);
            var placed=(pawn.jobs.curJob.placedThings??[]).Where(p=>p.thing!=null&&entry.StorageV7.Contents.Contains(p.thing.GetUniqueLoadID())).OrderBy(p=>p.thing.GetUniqueLoadID(),StringComparer.Ordinal).ToArray();
            if(placed.Length!=before.Length||placed.Select(p=>p.thing.GetUniqueLoadID()).Distinct().Count()!=placed.Length)continue;
            ResidentActiveWork.Known(typeof(ThingCountClass),"get_Count");
            if(placed.Where((p,i)=>p.thing.GetUniqueLoadID()!=before[i].Id||p.Count!=before[i].Count).Any())continue;
            starts.Add(new(entry.Original,before,placed.Select(p=>new WorkStackUse(p.thing.GetUniqueLoadID(),p.Count)).ToArray()));
        }
        return starts.Count==0?null:starts.OrderBy(p=>p.Shelf,StringComparer.Ordinal).ToArray();
    }
    private static void FinishStorageRetirements(TerritoryTransferReference r,WorkProgressEvent ev)
    {
        foreach(var row in ev.StorageRetirementsV14??[])
        foreach(var original in row.Before)
        {
            // Inspect the original object, including after destruction; never substitute a same-ID survivor.
            var thing=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==original.Id);
            if(thing==null||!thing.Destroyed||thing.Spawned||thing.holdingOwner!=null||thing.Faction!=null)throw new ArgumentException("Same original stack must have positive native destruction output, never movement or owner-only inference.");
        }
    }
}
