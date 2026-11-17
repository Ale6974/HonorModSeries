using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record WorkStorageContinuation(string Shelf,int Previous,string Action,NeutralMapThing[] Before,NeutralMapThing Input,string Target,int Cell,int Requested,string Calls)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? QuantityV21 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapThing[] After { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapThing Carried { get; init; }
}
public static class WorkStorageContinuations
{
    public static void Check(TerritoryTransfer t,LoadedMapScope map,WorkProgressSave work,int sequence)
    {
        var ev=work.Events[sequence];var row=ev.StorageContinuationV20;if(row==null)return;
        var scope=map.Storage?.SingleOrDefault(s=>s.Id==row.Shelf);
        var prior=work.Events.Take(sequence).LastOrDefault(e=>e.StorageSplitV19?.Shelf==row.Shelf||e.StorageContinuationV20?.Shelf==row.Shelf);
        var before=prior?.StorageContinuationV20?.After??prior?.StorageSplitV19?.After;var calls=prior?.StorageContinuationV20?.Calls??prior?.StorageSplitV19?.Calls;
        bool quantity=row.Action is "split" or "partial-merge",pickup=row.Action is "take" or "split";
        if(quantity?row.QuantityV21!=1:row.QuantityV21!=null)throw new ArgumentException("New explicit quantity scope required; old continuation authority is unchanged.");
        if(work.Version!=3||ev.GrantV4==null||scope==null||prior==null||row.Previous!=prior.Sequence||before==null||row.Before==null||row.Input==null||row.Calls!=calls||JsonSerializer.Serialize(row.Before)!=JsonSerializer.Serialize(before)||ev.Inputs.Length!=1||ev.Inputs[0]!=row.Input.Id||!quantity&&row.Action is not ("take" or "merge" or "put")||pickup&&ev.Kind!="haul-pickup"||!pickup&&ev.Kind!="haul-place"||ev.StoragePickupV18!=null||ev.StorageSplitV19!=null||ev.StorageMergeV17!=null)throw new ArgumentException("New explicit continuation of the exact latest positive original split/flow required.");
        MapAssetTransfers.Pair(row.Calls,t.LoadedMap,t.FromOwner,t.TargetOwner);
        var known=new HashSet<string>(work.Seal.Objects,StringComparer.Ordinal);foreach(var fact in work.Events.Take(sequence)){known.ExceptWith(fact.Removed??[]);known.UnionWith(fact.Added??[]);}
        if(!known.Contains(row.Input.Id)||row.Before.Any(p=>!known.Contains(p.Id)))throw new ArgumentException("Live original/positive object lineage required.");
        if(pickup)
        {
            if(row.Input!=row.Before.SingleOrDefault(p=>p.Id==row.Input.Id)||row.Target!=null||(quantity?row.Requested<=0||row.Requested>=row.Input.Count:row.Requested<row.Input.Count))throw new ArgumentException("Exact original remainder and explicit whole/partial request required.");
        }
        else
        {
            if(row.Input.Cell!=-1||row.Input.Count<=0||(quantity?row.Requested<=0||row.Requested>=row.Input.Count:row.Requested!=row.Input.Count)||!scope.Cells.Contains(row.Cell)||row.Before.Any(p=>p.Id==row.Input.Id)||!work.Events.Take(sequence).Any(e=>e.StorageSplitV19?.Carried==row.Input||e.StorageContinuationV20?.Carried==row.Input))throw new ArgumentException("Exact previously observed carried input required; no replacement adoption.");
            if(row.Action is "merge" or "partial-merge")
            {
                var target=row.Before.SingleOrDefault(p=>p.Id==row.Target);
                if(target==null||target.Cell!=row.Cell||target.Def!=row.Input.Def||target.Stuff!=row.Input.Stuff||target.HitPoints!=row.Input.HitPoints||target.Quality!=row.Input.Quality||(long)target.Count+row.Requested>int.MaxValue)throw new ArgumentException("Unique compatible original restack target required.");
            }
            else if(row.Target!=null||!map.Things.Any(p=>p.Id==row.Input.Id))throw new ArgumentException("Only the same original whole-picked object may be placed back without merging.");
        }
        if(row.After==null){if(row.Carried!=null||ev.Returned.HasValue)throw new ArgumentException("Unknown continuation cannot contain results.");return;}
        if(!ev.Returned.HasValue||!ev.BodyReturned.HasValue||ev.Removed==null||ev.Added==null||row.Action!="split"&&ev.Added.Length!=0)throw new ArgumentException("Independent positive native continuation output required.");
        NeutralMapThing[] expected;
        if(row.Action=="split")
        {
            int taken=ev.PickupTakenV18??0;
            if(taken<=0||taken>row.Requested||taken>=row.Input.Count||ev.Removed.Length!=0||row.Carried==null||string.IsNullOrWhiteSpace(row.Carried.Id)||work.Seal.Objects.Contains(row.Carried.Id)||known.Contains(row.Carried.Id)||work.Events.Take(sequence).Any(e=>e.Added?.Contains(row.Carried.Id)==true)||!ev.Added.SequenceEqual([row.Carried.Id])||row.Carried!=row.Input with{Id=row.Carried.Id,Count=taken,Cell=-1})throw new ArgumentException("Fresh native split identity and exact partial output required.");
            expected=row.Before.Select(p=>p.Id==row.Input.Id?p with{Count=p.Count-taken}:p).ToArray();
        }
        else if(row.Action=="partial-merge")
        {
            if(ev.PlacementResultV4!=true||ev.Removed.Length!=0||row.Carried!=row.Input with{Count=row.Input.Count-row.Requested})throw new ArgumentException("Same carried original remainder and exact positive partial placement required.");
            expected=row.Before.Select(p=>p.Id==row.Target?p with{Count=p.Count+row.Requested}:p).ToArray();
        }
        else if(row.Action=="take")
        {
            if(ev.PickupTakenV18!=row.Input.Count||ev.Removed.Length!=0||row.Carried!=row.Input with{Cell=-1})throw new ArgumentException("Whole pickup must retain the same object and exact quantity.");
            expected=row.Before.Where(p=>p.Id!=row.Input.Id).ToArray();
        }
        else
        {
            if(ev.PlacementResultV4!=true||row.Carried!=null||row.Action=="merge"&&!ev.Removed.SequenceEqual([row.Input.Id])||row.Action=="put"&&ev.Removed.Length!=0)throw new ArgumentException("Exact true native placement lineage required.");
            expected=row.Action=="merge"?row.Before.Select(p=>p.Id==row.Target?p with{Count=p.Count+row.Input.Count}:p).ToArray():row.Before.Append(row.Input with{Cell=row.Cell}).OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray();
        }
        if(JsonSerializer.Serialize(row.After)!=JsonSerializer.Serialize(expected))throw new ArgumentException("Exact remaining original objects/quantities/physical output required.");
    }
    public static void Entry(WorkAssetResolution entry,WorkProgressSave work)
    {
        var rows=entry.StorageContinuationsV20;
        if(entry.StorageContinuationCallV20.HasValue&&(!entry.Started||rows==null||entry.StorageContinuationCallV20<=0||entry.StorageContinuationCallV20>rows.Length))throw new ArgumentException("Original continuation call prefix changed.");
        if(rows==null)return;
        if(entry.StorageSplitsV19==null||rows.Length is <1 or >128||!rows.SequenceEqual(rows.Distinct().OrderBy(i=>i))||rows[0]<=entry.StorageSplitsV19.Last())throw new ArgumentException("Explicit continuation after retained original split history required.");
        var all=work.Events.Where(e=>e.StorageContinuationV20?.Shelf==entry.Original&&e.StorageContinuationV20.After!=null).Select(e=>e.Sequence);
        if(!rows.SequenceEqual(all.Take(rows.Length)))throw new ArgumentException("Continuation outputs cannot be skipped.");
        int previous=entry.StorageSplitsV19.Last();
        foreach(int index in rows)
        {
            if(work.Events[index].StorageContinuationV20.Previous!=previous)throw new ArgumentException("Every continuation must retain its exact linked predecessor.");
            previous=index;
        }
    }
    public static NeutralMapThing[] Current(WorkAssetResolution entry,WorkProgressSave work)=>entry.StorageContinuationsV20==null?null:work.Events[entry.StorageContinuationsV20.Last()].StorageContinuationV20.After;
}
