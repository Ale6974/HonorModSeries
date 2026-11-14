using System.Text.Json;
namespace Honor.Contracts.Rules;
/// <summary>Explicit before-body transition from original count history to positively observed complete consumption.</summary>
public sealed record WorkStorageRetirement(string Shelf,NeutralMapThing[] Before,WorkStackUse[] Uses);
public static class WorkStorageRetirements
{
    public static void Check(WorkProgressSave work,int sequence)
    {
        var ev=work.Events[sequence];var rows=ev.StorageRetirementsV14;if(rows==null)return;
        if(work.Version!=3||ev.Kind!="recipe"||rows.Length is <1 or >1000||rows.Any(row=>row==null)||!rows.Select(r=>r.Shelf).SequenceEqual(rows.Select(r=>r.Shelf).Distinct().OrderBy(id=>id,StringComparer.Ordinal)))throw new ArgumentException("Explicit ordered recipe shelf retirement starts required.");
        foreach(var row in rows)
        {
            var previous=work.Events.Take(sequence).SelectMany(e=>e.StorageStacksV13??[]).LastOrDefault(r=>r.Shelf==row.Shelf);
            if(previous?.After==null||row.Before==null||JsonSerializer.Serialize(row.Before)!=JsonSerializer.Serialize(previous.After)||row.Uses==null||row.Uses.Length!=row.Before.Length||row.Uses.Where((use,i)=>use==null||use.Id!=row.Before[i].Id||use.Count!=row.Before[i].Count||!ev.Inputs.Contains(use.Id)).Any()||ev.StorageStacksV13?.Any(r=>r.Shelf==row.Shelf)==true||work.Events.Take(sequence).Any(e=>e.StorageRetirementsV14?.Any(r=>r.Shelf==row.Shelf)==true))throw new ArgumentException("Complete same original latest count snapshot and exact full input quantities required.");
            if(ev.Returned.HasValue&&(!ev.BodyReturned.HasValue||row.Before.Any(p=>!ev.Removed.Contains(p.Id))))throw new ArgumentException("Every original observed stack must have a positive native removal.");
        }
    }
    public static void Pair(WorkAssetResolution entry,WorkProgressSave work)
    {
        if(!entry.StorageBridgeV14.HasValue)
        {
            if(entry.StorageStackEventsV13!=null&&entry.StorageProgressV11!=null)throw new ArgumentException("Count and membership history require an explicit before-body bridge.");return;
        }
        int index=entry.StorageBridgeV14.Value;
        if(entry.StorageV7==null||entry.StorageStackEventsV13==null||entry.StorageProgressV11==null||index<0||index>=work.Events.Length||entry.StorageStackEventsV13.Last()>=index||entry.StorageProgressV11[0].Event!=index||!work.Events[index].Returned.HasValue||work.Events[index].StorageRetirementsV14?.Any(r=>r.Shelf==entry.Original)!=true||entry.StorageV7.Contents.Intersect(entry.StorageProgressV11[0].Current.Contents).Any())throw new ArgumentException("Positive complete retirement must be the first membership transition after original count history.");
        var before=work.Events[index].StorageRetirementsV14.Single(r=>r.Shelf==entry.Original).Before;
        var counts=work.Events[entry.StorageStackEventsV13.Last()].StorageStacksV13.Single(r=>r.Shelf==entry.Original).After;
        if(JsonSerializer.Serialize(before)!=JsonSerializer.Serialize(counts))throw new ArgumentException("Retirement must retain the exact last linked count output.");
    }
}
