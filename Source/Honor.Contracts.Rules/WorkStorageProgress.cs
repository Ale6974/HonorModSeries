using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record WorkStorageProgress(int Sequence,int Event,string Previous,NeutralMapStorage Current);
public static class WorkStorageProgressRules
{
    public static NeutralMapStorage Current(WorkAssetResolution entry)=>entry.StorageProgressV11?.Last().Current??entry.StorageV7;
    public static void Check(WorkAssetResolution entry,WorkProgressSave work)
    {
        var revisions=entry.StorageProgressV11;
        if(entry.StorageCallV11.HasValue&&(!entry.Started||entry.StorageCallV11<=0||revisions==null||entry.StorageCallV11>revisions.Length))throw new ArgumentException("Original storage call prefix is unpaired.");
        if(revisions==null)return;
        if(entry.StorageV7==null||revisions.Length is <1 or >128)throw new ArgumentException("Bounded original storage history required.");
        var previous=entry.StorageV7;int last=entry.Event??-1;
        for(int i=0;i<revisions.Length;i++)
        {
            var row=revisions[i];var scope=row?.Current;
            if(row==null||row.Sequence!=i||row.Event<=last||row.Event>=work.Events.Length||row.Previous!=WorkProgress.Hash(previous)||scope==null||
                scope.Id!=previous.Id||scope.Configuration!=previous.Configuration||scope.Cells==null||!scope.Cells.SequenceEqual(previous.Cells)||scope.Contents==null||scope.Contents.Length>10000||
                scope.Contents.Any(id=>string.IsNullOrWhiteSpace(id)||id==scope.Id)||!scope.Contents.SequenceEqual(scope.Contents.Distinct().OrderBy(id=>id,StringComparer.Ordinal))||JsonSerializer.Serialize(scope)==JsonSerializer.Serialize(previous))throw new ArgumentException("Storage history cannot replace its original settings or skip an observed change.");
            var ev=work.Events[row.Event];
            if(!ev.Returned.HasValue||!ev.BodyReturned.HasValue||ev.Kind is not ("recipe" or "haul-place")||ev.Kind=="haul-place"&&ev.PlacementResultV4!=true)throw new ArgumentException("Positive original production/placement output required.");
            if(previous.Contents.Except(scope.Contents).Any(id=>!ev.Removed.Contains(id))||scope.Contents.Except(previous.Contents).Any(id=>!ev.Added.Contains(id)&&!(ev.Kind=="haul-place"&&ev.Inputs.Contains(id))))throw new ArgumentException("Storage membership change lacks exact native input/output lineage.");
            var known=new HashSet<string>(work.Seal.Objects,StringComparer.Ordinal);
            foreach(var fact in work.Events.Where(fact=>fact.Returned.HasValue&&fact.Sequence<=row.Event)){known.ExceptWith(fact.Removed);known.UnionWith(fact.Added);}
            if(scope.Contents.Any(id=>!known.Contains(id)))throw new ArgumentException("Current stored objects lack original/observed provenance.");
            previous=scope;last=row.Event;
        }
    }
    public static WorkAssetResolution CallPreparation(WorkAssetResolution entry)
    {
        int count=entry.Started?(entry.StorageCallV11??0):(entry.StorageProgressV11?.Length??0);
        int stacks=entry.Started?(entry.StorageStackCallV13??0):(entry.StorageStackEventsV13?.Length??0);
        return entry with{Started=false,Returned=false,Verified=false,Approval=null,StorageCallV11=null,StorageProgressV11=count==0?null:entry.StorageProgressV11.Take(count).ToArray(),StorageStackCallV13=null,StorageStackEventsV13=stacks==0?null:entry.StorageStackEventsV13.Take(stacks).ToArray()};
    }
}
