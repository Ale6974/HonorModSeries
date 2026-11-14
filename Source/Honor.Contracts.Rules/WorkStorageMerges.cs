using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record WorkStorageMerge(string Shelf,string Target,NeutralMapThing[] Before,NeutralMapThing Cargo,string Calls)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapThing[] After { get; init; }
}
public static class WorkStorageMerges
{
    public static void Check(TerritoryTransfer t,LoadedMapScope map,WorkProgressSave work,int sequence)
    {
        var ev=work.Events[sequence];var row=ev.StorageMergeV17;if(row==null)return;
        var shelf=map.Storage?.SingleOrDefault(s=>s.Id==row.Shelf);var prior=work.Events.Take(sequence).Select(e=>e.StorageMergeV17).LastOrDefault(r=>r?.Shelf==row.Shelf&&r.After!=null);
        var before=prior?.After??shelf?.Contents.Select(id=>map.Things.Single(p=>p.Id==id)).ToArray();
        if(work.Version!=3||ev.Kind!="haul-place"||shelf==null||row.Before==null||JsonSerializer.Serialize(row.Before)!=JsonSerializer.Serialize(before)||prior==null&&ev.StorageOriginsV12?.Stores.Any(p=>p.Id==row.Shelf)!=true||row.Cargo==null||row.Cargo.Count<=0||row.Cargo.Cell!=-1||ev.Inputs.Length!=1||ev.Inputs[0]!=row.Cargo.Id||row.Before.Any(p=>p.Id==row.Cargo.Id))throw new ArgumentException("Explicit original same-membership shelf and separate whole carried input required.");
        MapAssetTransfers.Pair(row.Calls,t.LoadedMap,t.FromOwner,t.TargetOwner);
        if(prior==null&&ev.StorageOriginsV12.Calls!=row.Calls)throw new ArgumentException("Merge must bind the exact original before-body call prefix.");
        if(prior!=null&&row.Calls!=prior.Calls)throw new ArgumentException("Original merge call prefix changed.");
        var known=new HashSet<string>(work.Seal.Objects,StringComparer.Ordinal);
        foreach(var fact in work.Events.Take(sequence).Where(e=>e.Returned.HasValue)){known.ExceptWith(fact.Removed);known.UnionWith(fact.Added);}
        if(!known.Contains(row.Cargo.Id)||row.Before.Any(p=>!known.Contains(p.Id)))throw new ArgumentException("Original or positive observed input lineage required; never adopt an unobserved replacement.");
        var target=row.Before.SingleOrDefault(p=>p.Id==row.Target);var cargo=row.Cargo;
        if(target==null||target.Count<=0||target.Def!=cargo.Def||target.Stuff!=cargo.Stuff||target.HitPoints!=cargo.HitPoints||target.Quality!=cargo.Quality||(long)target.Count+cargo.Count>int.MaxValue)throw new ArgumentException("Same-definition/quality/health whole-stack merge only.");
        if(row.After==null){if(ev.Returned.HasValue)throw new ArgumentException("Explicit merge output missing.");return;}
        if(!ev.Returned.HasValue||!ev.BodyReturned.HasValue||!ev.PlacementResultV4.HasValue||ev.Added.Length!=0||ev.PlacementResultV4==true&&!ev.Removed.SequenceEqual([cargo.Id])||ev.PlacementResultV4==false&&ev.Removed.Length!=0)throw new ArgumentException("Independent true native merge return and exact destroyed input lineage required.");
        var expected=row.Before.Select(p=>p.Id==row.Target?p with{Count=p.Count+(ev.PlacementResultV4==true?cargo.Count:0)}:p).ToArray();
        if(JsonSerializer.Serialize(row.After)!=JsonSerializer.Serialize(expected))throw new ArgumentException("Exactly the observed whole cargo quantity may increase one original stack.");
    }
    public static void Entry(WorkAssetResolution entry,WorkProgressSave work)
    {
        var indices=entry.StorageMergeEventsV17;
        if(entry.StorageMergeCallV17.HasValue&&(!entry.Started||indices==null||entry.StorageMergeCallV17<=0||entry.StorageMergeCallV17>indices.Length))throw new ArgumentException("Original merge-call prefix changed.");
        if(indices==null)return;
        if(entry.Event.HasValue||entry.StorageV7==null||entry.StorageProgressV11!=null||entry.StorageStackEventsV13!=null||indices.Length is <1 or >128||!indices.SequenceEqual(indices.Distinct().OrderBy(i=>i)))throw new ArgumentException("Separate original same-membership merge history required.");
        var all=work.Events.Where(e=>e.StorageMergeV17?.Shelf==entry.Original&&e.StorageMergeV17.After!=null&&e.PlacementResultV4==true).Select(e=>e.Sequence);
        if(!indices.SequenceEqual(all.Take(indices.Length)))throw new ArgumentException("Positive merge history cannot skip results.");
    }
    public static NeutralMapThing[] Current(WorkAssetResolution entry,WorkProgressSave work)
        =>entry.StorageMergeEventsV17==null?null:work.Events[entry.StorageMergeEventsV17.Last()].StorageMergeV17.After;
}
