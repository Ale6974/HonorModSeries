namespace Honor.Contracts.Rules;
public sealed record WorkStorageOrigin(string Id,string Storage,string Asset,string Physical,string Goods);
public sealed record WorkStorageOrigins(string Calls,WorkStorageOrigin[] Stores);
/// <summary>Before-call proof that a previously untracked original shelf still had its complete original scope.</summary>
public static class WorkStorageOriginRules
{
    public static void Check(TerritoryTransfer t,LoadedMapScope map,WorkStorageOrigins context,string previousCalls)
    {
        if(context?.Stores==null||context.Stores.Length is <1 or >1000||context.Stores.Any(row=>row==null)||!context.Stores.Select(row=>row.Id).SequenceEqual(context.Stores.Select(row=>row.Id).Distinct().OrderBy(id=>id,StringComparer.Ordinal)))throw new ArgumentException("Complete ordered original shelf origins required.");
        MapAssetTransfers.Pair(context.Calls,t.LoadedMap,t.FromOwner,t.TargetOwner);
        MapAssetTransfers.Pair(previousCalls,t.LoadedMap,t.FromOwner,t.TargetOwner);
        var calls=MapAssetTransfers.Restore(context.Calls).Calls;var previous=MapAssetTransfers.Restore(previousCalls).Calls;
        if(calls.Any(call=>call.Started&&!call.Returned)||calls.Where((call,i)=>previous[i].Returned&&!call.Returned).Any())throw new ArgumentException("Known monotone original call prefix required.");
        foreach(var row in context.Stores)
        {
            var storage=map.Storage?.SingleOrDefault(s=>s.Id==row.Id);var asset=map.OwnedAssets?.SingleOrDefault(a=>a.Id==row.Id);var physical=map.Things.SingleOrDefault(p=>p.Id==row.Id);var call=calls.SingleOrDefault(c=>c.Id==row.Id);
            if(storage==null||asset?.Owner!=t.FromOwner||physical==null||call==null||call.Started||map.Groups?.Any(g=>g.Members.Contains(row.Id))==true||
                storage.Contents.Any(id=>map.OwnedAssets.Any(a=>a.Id==id))||row.Storage!=WorkProgress.Hash(storage)||row.Asset!=WorkProgress.Hash(asset)||row.Physical!=WorkProgress.Hash(physical)||
                row.Goods!=WorkProgress.Hash(storage.Contents.Select(id=>map.Things.Single(p=>p.Id==id)).ToArray()))throw new ArgumentException("Unstarted same original ungrouped shelf scope and neutral original goods required.");
        }
    }
}
