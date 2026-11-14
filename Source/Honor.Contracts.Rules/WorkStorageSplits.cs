using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record WorkStorageSplit(string Shelf,string Input,NeutralMapThing[] Before,string Calls,int Requested)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapThing[] After { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapThing Carried { get; init; }
}
public static class WorkStorageSplits
{
    public static void Check(TerritoryTransfer t,LoadedMapScope map,WorkProgressSave work,int sequence)
    {
        var ev=work.Events[sequence];var row=ev.StorageSplitV19;if(row==null)return;
        var shelf=map.Storage?.SingleOrDefault(s=>s.Id==row.Shelf);var prior=work.Events.Take(sequence).Select(e=>e.StorageSplitV19).LastOrDefault(p=>p?.Shelf==row.Shelf);
        var expectedBefore=prior?.After??shelf?.Contents.Select(id=>map.Things.Single(p=>p.Id==id)).ToArray();
        if(work.Version!=3||ev.Kind!="haul-pickup"||ev.StoragePickupV18!=null||ev.GrantV4==null||shelf==null||row.Before==null||JsonSerializer.Serialize(row.Before)!=JsonSerializer.Serialize(expectedBefore)||prior!=null&&prior.After==null||ev.Inputs.Length!=1||ev.Inputs[0]!=row.Input||prior==null&&(ev.StorageOriginsV12?.Calls!=row.Calls||ev.StorageOriginsV12?.Stores.Any(s=>s.Id==row.Shelf)!=true))throw new ArgumentException("New explicit original partial-pickup scope required; never reinterpret whole pickup evidence.");
        var input=row.Before.SingleOrDefault(p=>p.Id==row.Input);
        if(input==null||row.Requested<=0||row.Requested>=input.Count)throw new ArgumentException("Positive explicitly partial original input request required.");
        MapAssetTransfers.Pair(row.Calls,t.LoadedMap,t.FromOwner,t.TargetOwner);
        if(prior!=null&&prior.Calls!=row.Calls)throw new ArgumentException("Original split calls changed.");
        if(row.After==null){if(row.Carried!=null||ev.Returned.HasValue)throw new ArgumentException("Unknown partial pickup has no positive output.");return;}
        int taken=ev.PickupTakenV18??0;
        if(!ev.BodyReturned.HasValue||!ev.Returned.HasValue||taken<=0||taken>row.Requested||taken>=input.Count||row.Carried==null||string.IsNullOrWhiteSpace(row.Carried.Id)||row.Before.Any(p=>p.Id==row.Carried.Id)||ev.Removed==null||ev.Added==null||ev.Removed.Length!=0||!ev.Added.SequenceEqual([row.Carried.Id]))throw new ArgumentException("Exact positive native partial quantity and one distinct new carried object required.");
        var known=new HashSet<string>(work.Seal.Objects,StringComparer.Ordinal);foreach(var fact in work.Events.Take(sequence)){known.UnionWith(fact.Added??[]);}
        if(known.Contains(row.Carried.Id)||row.Carried!=input with{Id=row.Carried.Id,Count=taken,Cell=-1}||JsonSerializer.Serialize(row.After)!=JsonSerializer.Serialize(row.Before.Select(p=>p.Id==row.Input?p with{Count=p.Count-taken}:p).ToArray()))throw new ArgumentException("Keep the original remainder and separately prove the new native split; no rebinding or count reset.");
    }
    public static void Entry(WorkAssetResolution entry,WorkProgressSave work)
    {
        var indices=entry.StorageSplitsV19;
        if(entry.StorageSplitCallV19.HasValue&&(!entry.Started||indices==null||entry.StorageSplitCallV19<=0||entry.StorageSplitCallV19>indices.Length))throw new ArgumentException("Original split-call prefix changed.");
        if(indices==null)return;
        if(entry.Event.HasValue||entry.StorageV7==null||entry.StorageProgressV11!=null||entry.StorageStackEventsV13!=null||entry.StorageMergeEventsV17!=null||indices.Length is <1 or >128||!indices.SequenceEqual(indices.Distinct().OrderBy(i=>i)))throw new ArgumentException("Separate same-membership partial-pickup history required.");
        var all=work.Events.Where(e=>e.StorageSplitV19?.Shelf==entry.Original&&e.StorageSplitV19.After!=null).Select(e=>e.Sequence);
        if(!indices.SequenceEqual(all.Take(indices.Length)))throw new ArgumentException("Native split results cannot be skipped.");
    }
    public static NeutralMapThing[] Current(WorkAssetResolution entry,WorkProgressSave work)=>entry.StorageSplitsV19==null?null:work.Events[entry.StorageSplitsV19.Last()].StorageSplitV19.After;
}
