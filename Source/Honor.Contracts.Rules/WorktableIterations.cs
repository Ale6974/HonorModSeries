using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record WorktableIteration(string Table,string Bill,NativeWorktable Before,string Stable,int Count,string Calls)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NativeWorktable After { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string AfterStable { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? AfterCount { get; init; }
}
public static class WorktableIterations
{
    public static void Check(TerritoryTransfer t,LoadedMapScope map,WorkProgressSave work,int index)
    {
        var ev=work.Events[index];var row=ev.WorktableIterationV15;if(row==null)return;
        var original=map.WorktablesV1?.SingleOrDefault(p=>p.Id==row.Table);var prior=work.Events.Take(index).Select(e=>e.WorktableIterationV15).LastOrDefault(p=>p?.Table==row.Table);
        if(work.Version!=3||ev.Kind!="recipe"||original==null||row.Before==null||row.Before.Id!=row.Table||!original.Bills.Contains(row.Bill)||row.Count<=0||!CityStockProof.Valid(row.Stable)||JsonSerializer.Serialize(row.Before)!=JsonSerializer.Serialize(prior?.After??original)||prior!=null&&prior.After==null)throw new ArgumentException("Explicit same original production table and positive before-iteration count required.");
        if(prior?.Bill==row.Bill&&(row.Count!=prior.AfterCount||row.Stable!=prior.AfterStable))throw new ArgumentException("Same active bill must continue its last positive count and stable settings.");
        MapAssetTransfers.Pair(row.Calls,t.LoadedMap,t.FromOwner,t.TargetOwner);var calls=MapAssetTransfers.Restore(row.Calls).Calls;
        var originalCall=calls.SingleOrDefault(c=>c.Id==row.Table);
        if(originalCall==null||calls.Any(c=>c.Started&&!c.Returned)||prior==null&&originalCall.Started||prior!=null&&row.Calls!=prior.Calls)throw new ArgumentException("Known original table call prefix required.");
        if(row.After==null){if(row.AfterStable!=null||row.AfterCount.HasValue||ev.Returned.HasValue)throw new ArgumentException("Unknown iteration cannot contain positive output.");return;}
        if(!ev.BodyReturned.HasValue||!ev.Returned.HasValue||row.After.Id!=row.Table||row.After.Bills==null||!row.After.Bills.SequenceEqual(original.Bills)||!CityStockProof.Valid(row.After.Configuration)||row.After.Configuration==row.Before.Configuration||row.AfterStable!=row.Stable||row.AfterCount!=row.Count-1)throw new ArgumentException("Exactly one native iteration may change only its repeat count.");
    }
    public static void Entry(WorkAssetResolution entry,WorkProgressSave work)
    {
        var indices=entry.WorktableIterationsV15;
        if(entry.WorktableCallV15.HasValue&&(!entry.Started||indices==null||entry.WorktableCallV15<=0||entry.WorktableCallV15>indices.Length))throw new ArgumentException("Original iteration-call prefix changed.");
        if(indices==null)return;
        if(entry.Event.HasValue||entry.WorktableV6==null||indices.Length is <1 or >128||!indices.SequenceEqual(indices.Distinct().OrderBy(i=>i)))throw new ArgumentException("Original table iteration history required.");
        var all=work.Events.Where(ev=>ev.WorktableIterationV15?.Table==entry.Original&&ev.WorktableIterationV15.After!=null).Select(ev=>ev.Sequence);
        if(!indices.SequenceEqual(all.Take(indices.Length)))throw new ArgumentException("Iteration history may not skip native results.");
    }
    public static NativeWorktable Current(WorkAssetResolution entry,WorkProgressSave work)
        =>entry.WorktableIterationsV15==null?entry.WorktableV6:work.Events[entry.WorktableIterationsV15.Last()].WorktableIterationV15.After;
}
