using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record WorkStoragePickup(string Shelf,NeutralMapThing Item,NeutralMapStorage Before,string Calls,int Requested)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapStorage After { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapThing Carried { get; init; }
}
public static class WorkStoragePickups
{
    public static void Check(TerritoryTransfer t,LoadedMapScope map,WorkProgressSave work,int sequence)
    {
        var ev=work.Events[sequence];var row=ev.StoragePickupV18;
        if(ev.PickupTakenV18.HasValue&&(ev.Kind!="haul-pickup"||!ev.BodyReturned.HasValue||ev.PickupTakenV18<0))throw new ArgumentException("Independent native pickup count required.");
        if(row==null){if(ev.Kind=="haul-pickup"&&ev.Returned.HasValue&&ev.StorageSplitV19==null&&ev.StorageContinuationV20==null)throw new ArgumentException("Pickup cannot finish without its original before-body scope.");return;}
        var original=map.Storage?.SingleOrDefault(s=>s.Id==row.Shelf);var prior=work.Events.Take(sequence).Select(e=>e.StoragePickupV18).LastOrDefault(p=>p?.Shelf==row.Shelf);
        if(work.Version!=3||ev.Kind!="haul-pickup"||ev.GrantV4==null||original==null||row.Item==null||row.Item!=map.Things.SingleOrDefault(p=>p.Id==row.Item.Id)||row.Item.Count<=0||row.Requested<row.Item.Count||ev.Inputs.Length!=1||ev.Inputs[0]!=row.Item.Id||row.Before==null||JsonSerializer.Serialize(row.Before)!=JsonSerializer.Serialize(prior?.After??original)||!row.Before.Contents.Contains(row.Item.Id)||prior!=null&&prior.After==null||prior==null&&ev.StorageOriginsV12?.Calls!=row.Calls)throw new ArgumentException("Whole same original shelf item and explicit current job pickup scope required.");
        MapAssetTransfers.Pair(row.Calls,t.LoadedMap,t.FromOwner,t.TargetOwner);
        if(prior==null&&ev.StorageOriginsV12?.Stores.Any(p=>p.Id==row.Shelf)!=true)throw new ArgumentException("Exact original source shelf authority required before pickup.");
        if(prior!=null&&prior.Calls!=row.Calls)throw new ArgumentException("Original pickup call prefix changed.");
        if(row.After==null){if(row.Carried!=null||ev.Returned.HasValue)throw new ArgumentException("Unverified pickup cannot contain positive carried output.");return;}
        var expected=row.Before with{Contents=row.Before.Contents.Where(id=>id!=row.Item.Id).ToArray()};
        if(!ev.BodyReturned.HasValue||!ev.Returned.HasValue||ev.PickupTakenV18!=row.Item.Count||ev.Removed==null||ev.Added==null||ev.Removed.Length!=0||ev.Added.Length!=0||JsonSerializer.Serialize(row.After)!=JsonSerializer.Serialize(expected)||row.Carried!=row.Item with{Cell=-1})throw new ArgumentException("Exact normal whole-item pickup must keep its identity and physical state in the same actor's native carry owner.");
    }
}
