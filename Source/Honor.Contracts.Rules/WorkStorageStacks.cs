using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record WorkStorageStacks(string Shelf,NeutralMapThing[] Before)
{
    public WorkStackUse[] Uses { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapThing[] After { get; init; }
}
public sealed record WorkStackUse(string Id,int Count);
/// <summary>Read-only same-object partial recipe consumption. Never a command to restore stack counts.</summary>
public static class WorkStorageStackRules
{
    public static void Check(LoadedMapScope map,WorkProgressSave work,int index)
    {
        var ev=work.Events[index];var rows=ev.StorageStacksV13;if(rows==null)return;
        if(work.Version!=3||ev.Kind!="recipe"||rows.Length is <1 or >1000||rows.Any(r=>r==null)||!rows.Select(r=>r.Shelf).SequenceEqual(rows.Select(r=>r.Shelf).Distinct().OrderBy(id=>id,StringComparer.Ordinal)))throw new ArgumentException("Ordered explicit original recipe shelf observations required.");
        foreach(var row in rows)
        {
            var shelf=map.Storage?.SingleOrDefault(s=>s.Id==row.Shelf);
            var previous=work.Events.Take(index).SelectMany(e=>e.StorageStacksV13??[]).LastOrDefault(r=>r.Shelf==row.Shelf);
            var before=previous?.After??shelf?.Contents.Select(id=>map.Things.Single(p=>p.Id==id)).ToArray();
            if(shelf==null||row.Before==null||row.Before.Length is <1 or >10000||previous!=null&&previous.After==null||previous==null&&ev.StorageOriginsV12?.Stores.Any(s=>s.Id==row.Shelf)!=true||JsonSerializer.Serialize(row.Before)!=JsonSerializer.Serialize(before))throw new ArgumentException("Same full original or positively observed stack state required.");
            if(row.Uses==null||row.Uses.Length is <1 or >10000||row.Uses.Any(u=>u==null||u.Count<=0||!ev.Inputs.Contains(u.Id)||row.Before.SingleOrDefault(p=>p.Id==u.Id) is not NeutralMapThing p||u.Count>=p.Count)||!row.Uses.Select(u=>u.Id).SequenceEqual(row.Uses.Select(u=>u.Id).Distinct().OrderBy(id=>id,StringComparer.Ordinal)))throw new ArgumentException("Exact ordered positive partial original input quantities required.");
            if(row.After==null){if(ev.Returned.HasValue)throw new ArgumentException("Explicit stack observations require complete positive output.");continue;}
            if(!ev.BodyReturned.HasValue||!ev.Returned.HasValue||row.After.Length!=row.Before.Length||row.After.Any(p=>p==null)||ev.Removed.Intersect(shelf.Contents).Any())throw new ArgumentException("Positive same-membership recipe return required.");
            for(int i=0;i<row.Before.Length;i++)
            {
                var a=row.Before[i];var b=row.After[i];
                if(a==null||a.Count<=0||b.Count<=0||b.Count!=a.Count-(row.Uses.SingleOrDefault(u=>u.Id==a.Id)?.Count??0)||b with{Count=a.Count}!=a)throw new ArgumentException("Only exact partial consumption of the same explicit native input may change its count.");
            }
        }
    }
    public static void Entry(WorkAssetResolution entry,WorkProgressSave work)
    {
        var events=entry.StorageStackEventsV13;
        if(entry.StorageStackCallV13.HasValue&&(!entry.Started||events==null||entry.StorageStackCallV13<=0||entry.StorageStackCallV13>events.Length))throw new ArgumentException("Original stack call prefix changed.");
        if(events==null)return;
        if(entry.Event.HasValue||entry.StorageV7==null||entry.StorageProgressV11!=null||events.Length is <1 or >128||!events.SequenceEqual(events.Distinct().OrderBy(i=>i)))throw new ArgumentException("Separate original same-membership stack history required.");
        var all=work.Events.Where(e=>e.StorageStacksV13?.Any(r=>r.Shelf==entry.Original&&r.After!=null&&!Enumerable.SequenceEqual(r.Before,r.After))==true).Select(e=>e.Sequence).ToArray();
        if(!events.SequenceEqual(all.Take(events.Length)))throw new ArgumentException("Positive count changes must remain linked without gaps.");
        foreach(int i in events)if(i<0||i>=work.Events.Length)throw new ArgumentException("Unknown stack event.");
    }
    public static NeutralMapThing[] Current(WorkAssetResolution entry,WorkProgressSave work)
        =>entry.StorageStackEventsV13==null?null:work.Events[entry.StorageStackEventsV13.Last()].StorageStacksV13.Single(r=>r.Shelf==entry.Original).After;
}
