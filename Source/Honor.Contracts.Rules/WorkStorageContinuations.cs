using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record WorkStorageContinuation(string Shelf,int Previous,string Action,NeutralMapThing[] Before,NeutralMapThing Input,string Target,int Cell,int Requested,string Calls)
{
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
        if(work.Version!=3||ev.GrantV4==null||scope==null||prior==null||row.Previous!=prior.Sequence||before==null||row.Before==null||row.Input==null||row.Calls!=calls||JsonSerializer.Serialize(row.Before)!=JsonSerializer.Serialize(before)||ev.Inputs.Length!=1||ev.Inputs[0]!=row.Input.Id||row.Action is not ("take" or "merge" or "put")||row.Action=="take"&&ev.Kind!="haul-pickup"||row.Action!="take"&&ev.Kind!="haul-place"||ev.StoragePickupV18!=null||ev.StorageSplitV19!=null||ev.StorageMergeV17!=null)throw new ArgumentException("New explicit continuation of the exact latest positive original split/flow required.");
        MapAssetTransfers.Pair(row.Calls,t.LoadedMap,t.FromOwner,t.TargetOwner);
        var known=new HashSet<string>(work.Seal.Objects,StringComparer.Ordinal);foreach(var fact in work.Events.Take(sequence)){known.ExceptWith(fact.Removed??[]);known.UnionWith(fact.Added??[]);}
        if(!known.Contains(row.Input.Id)||row.Before.Any(p=>!known.Contains(p.Id)))throw new ArgumentException("Live original/positive object lineage required.");
        if(row.Action=="take")
        {
            if(row.Input!=row.Before.SingleOrDefault(p=>p.Id==row.Input.Id)||row.Requested<row.Input.Count||row.Target!=null)throw new ArgumentException("Exact whole original remainder pickup required.");
        }
        else
        {
            if(row.Input.Cell!=-1||row.Input.Count<=0||row.Requested!=row.Input.Count||!scope.Cells.Contains(row.Cell)||row.Before.Any(p=>p.Id==row.Input.Id)||!work.Events.Take(sequence).Any(e=>e.StorageSplitV19?.Carried==row.Input||e.StorageContinuationV20?.Carried==row.Input))throw new ArgumentException("Exact previously observed carried input required; no replacement adoption.");
            if(row.Action=="merge")
            {
                var target=row.Before.SingleOrDefault(p=>p.Id==row.Target);
                if(target==null||target.Cell!=row.Cell||target.Def!=row.Input.Def||target.Stuff!=row.Input.Stuff||target.HitPoints!=row.Input.HitPoints||target.Quality!=row.Input.Quality||(long)target.Count+row.Input.Count>int.MaxValue)throw new ArgumentException("Unique compatible original restack target required.");
            }
            else if(row.Target!=null||!map.Things.Any(p=>p.Id==row.Input.Id))throw new ArgumentException("Only the same original whole-picked object may be placed back without merging.");
        }
        if(row.After==null){if(row.Carried!=null||ev.Returned.HasValue)throw new ArgumentException("Unknown continuation cannot contain results.");return;}
        if(!ev.Returned.HasValue||!ev.BodyReturned.HasValue||ev.Removed==null||ev.Added==null||ev.Added.Length!=0)throw new ArgumentException("Independent positive native continuation output required.");
        NeutralMapThing[] expected;
        if(row.Action=="take")
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
