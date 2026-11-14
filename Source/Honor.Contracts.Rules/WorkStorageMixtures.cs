using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record WorkStorageMixture(string Shelf,NeutralMapThing[] Before,WorkStackUse[] Uses)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapThing[] After { get; init; }
}
/// <summary>Explicit new recipe evidence for original stacks that partly disappear and partly survive.</summary>
public static class WorkStorageMixtures
{
    public static void Check(WorkProgressSave work,int sequence)
    {
        var ev=work.Events[sequence];var rows=ev.StorageMixturesV16;if(rows==null)return;
        if(work.Version!=3||ev.Kind!="recipe"||rows.Length is <1 or >1000||rows.Any(r=>r==null)||!rows.Select(r=>r.Shelf).SequenceEqual(rows.Select(r=>r.Shelf).Distinct().OrderBy(id=>id,StringComparer.Ordinal)))throw new ArgumentException("Explicit ordered mixed-consumption starts required.");
        foreach(var row in rows)
        {
            var prior=work.Events.Take(sequence).SelectMany(e=>e.StorageStacksV13??[]).LastOrDefault(r=>r.Shelf==row.Shelf);
            if(prior?.After==null||row.Before==null||JsonSerializer.Serialize(row.Before)!=JsonSerializer.Serialize(prior.After)||row.Uses==null||row.Uses.Length is <1 or >10000||row.Uses.Any(u=>u==null||u.Count<=0||!ev.Inputs.Contains(u.Id)||row.Before.SingleOrDefault(p=>p.Id==u.Id) is not NeutralMapThing p||u.Count>p.Count)||!row.Uses.Select(u=>u.Id).SequenceEqual(row.Uses.Select(u=>u.Id).Distinct().OrderBy(id=>id,StringComparer.Ordinal))||ev.StorageStacksV13?.Any(r=>r.Shelf==row.Shelf)==true||ev.StorageRetirementsV14?.Any(r=>r.Shelf==row.Shelf)==true||work.Events.Take(sequence).Any(e=>e.StorageMixturesV16?.Any(r=>r.Shelf==row.Shelf)==true||e.StorageRetirementsV14?.Any(r=>r.Shelf==row.Shelf)==true))throw new ArgumentException("Same latest count output and exact bounded mixed input quantities required.");
            var expected=Remaining(row);
            if(expected.Length==0||expected.Length==row.Before.Length)throw new ArgumentException("Mixed consumption must remove some originals while retaining others.");
            if(row.After==null){if(ev.Returned.HasValue)throw new ArgumentException("Mixed consumption lacks positive output.");continue;}
            if(!ev.BodyReturned.HasValue||!ev.Returned.HasValue||JsonSerializer.Serialize(row.After)!=JsonSerializer.Serialize(expected)||row.Before.Any(p=>ev.Removed.Contains(p.Id)==expected.Any(q=>q.Id==p.Id)))throw new ArgumentException("Exact original survivors and positive removed-input lineage required.");
        }
    }
    public static NeutralMapThing[] Remaining(WorkStorageMixture row)
        =>row.Before.Select(p=>p with{Count=p.Count-(row.Uses.SingleOrDefault(u=>u.Id==p.Id)?.Count??0)}).Where(p=>p.Count>0).ToArray();
    public static void Pair(WorkAssetResolution entry,WorkProgressSave work)
    {
        int i=entry.StorageMixtureV16??-1;
        if(entry.StorageBridgeV14.HasValue||entry.StorageV7==null||entry.StorageStackEventsV13==null||entry.StorageProgressV11==null||i<0||i>=work.Events.Length||entry.StorageStackEventsV13.Last()>=i||entry.StorageProgressV11[0].Event!=i||!work.Events[i].Returned.HasValue||work.Events[i].StorageMixturesV16?.Any(r=>r.Shelf==entry.Original)!=true)throw new ArgumentException("Explicit positive mixture bridge after original count history required.");
        var row=work.Events[i].StorageMixturesV16.Single(r=>r.Shelf==entry.Original);
        var counts=work.Events[entry.StorageStackEventsV13.Last()].StorageStacksV13.Single(r=>r.Shelf==entry.Original).After;
        if(JsonSerializer.Serialize(row.Before)!=JsonSerializer.Serialize(counts)||!entry.StorageV7.Contents.Intersect(entry.StorageProgressV11[0].Current.Contents).SequenceEqual(row.After.Select(p=>p.Id)))throw new ArgumentException("Mixture must preserve the exact original last count state and all surviving original members.");
    }
    public static NeutralMapThing[] CurrentOriginals(WorkAssetResolution entry,WorkProgressSave work)
        =>entry.StorageMixtureV16.HasValue?work.Events[entry.StorageMixtureV16.Value].StorageMixturesV16.Single(r=>r.Shelf==entry.Original).After.Where(p=>WorkStorageProgressRules.Current(entry).Contents.Contains(p.Id)).ToArray():null;
}
