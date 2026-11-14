using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    private WorkStorageOrigins CaptureStorageOrigins(TerritoryTransfer t,TerritoryTransferReference r,string kind)
    {
        if(kind is not ("recipe" or "haul-place")||WorkProgress.Validate(t,r.WorkProgressV1).Version!=3||r.LoadedMap.AssetJournal==null)return null;
        var map=LoadedMapPreservation.Restore(t.LoadedMap);if(map.Storage==null)return null;
        var tracked=r.WorkAssetResolutionV5==null?Array.Empty<WorkAssetResolution>():WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5).Entries;
        var calls=MapAssetTransfers.Restore(r.LoadedMap.AssetJournal).Calls;var rows=new List<WorkStorageOrigin>();
        ResidentActiveWork.Known(typeof(Thing),"GetUniqueLoadID");ResidentActiveWork.Known(typeof(Thing),"get_ThingID");
        foreach(var original in map.Storage.Where(s=>!tracked.Any(e=>e.Original==s.Id)).OrderBy(s=>s.Id,StringComparer.Ordinal))
        {
            var asset=map.OwnedAssets?.SingleOrDefault(a=>a.Id==original.Id);var call=calls.SingleOrDefault(c=>c.Id==original.Id);
            if(asset==null||call==null||call.Started||map.Groups?.Any(g=>g.Members.Contains(original.Id))==true||original.Contents.Any(id=>map.OwnedAssets.Any(a=>a.Id==id)))continue;
            var goods=original.Contents.Select(id=>r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==id)).ToArray();
            if(goods.Any(p=>p!=null&&(p is IThingHolder||p.GetType()!=typeof(Thing)&&p.GetType()!=typeof(ThingWithComps))))continue;
            var physical=map.Things.Single(p=>p.Id==original.Id);var shelf=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==original.Id) as Building_Storage;
            if(shelf==null||ResolutionPhysical(shelf,r.LoadedMap.Map)!=physical||ResolutionAsset(r,shelf,asset.Repair!=null,r.From)!=asset||JsonSerializer.Serialize(StorageMapEvidence.CaptureOwned(shelf,r.LoadedMap.Map))!=JsonSerializer.Serialize(original))throw new ArgumentException("Original untracked shelf changed before the native storage observation.");
            foreach(string id in original.Contents)
            {
                var thing=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==id);
                if(thing==null||thing.Faction!=null||ResolutionPhysical(thing,r.LoadedMap.Map)!=map.Things.Single(p=>p.Id==id)||!shelf.slotGroup.HeldThings.Any(p=>ReferenceEquals(p,thing)))throw new ArgumentException("Same original basic neutral stored goods and full physical state required.");
            }
            rows.Add(new(original.Id,WorkProgress.Hash(original),WorkProgress.Hash(asset),WorkProgress.Hash(physical),WorkProgress.Hash(original.Contents.Select(id=>map.Things.Single(p=>p.Id==id)).ToArray())));
        }
        if(rows.Count==0)return null;var result=new WorkStorageOrigins(r.LoadedMap.AssetJournal,rows.ToArray());WorkStorageOriginRules.Check(t,map,result,r.LoadedMap.AssetJournal);return result;
    }
}
