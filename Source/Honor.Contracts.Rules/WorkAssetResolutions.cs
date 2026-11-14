using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record WorkTerrainAcceptance(int Cell,string Def,string Under,string Roof,bool Accepted=false,string Approval=null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkTerrainLayers LayersV9 { get; init; }
}
public sealed record WorkAssetResolution(string Original,string Actual,int? Event,MapAsset Asset,NeutralMapThing Physical,bool Started=false,bool Returned=false,bool Verified=false,string Approval=null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NativeWorktable WorktableV6 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NeutralMapStorage StorageV7 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkTerrainAcceptance TerrainV8 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkStorageProgress[] StorageProgressV11 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? StorageCallV11 { get; init; }
}
public sealed record WorkAssetResolutionSave(int Version,string Original,string BaseCalls,WorkAssetResolution[] Entries,string Proof);
/// <summary>Separate actual-object calls. Original missing-frame calls remain unstarted forever.</summary>
public static class WorkAssetResolutions
{
    private static string Write(WorkAssetResolutionSave s)=>JsonSerializer.Serialize(s with{Proof=WorkProgress.Hash(s with{Proof=null})});
    public static WorkAssetResolutionSave Validate(TerritoryTransfer t,string progress,string json,bool complete=false)
    {
        if(json==null||json.Length>3000000)throw new ArgumentException("Bounded separate property resolution required.");
        var work=WorkProgress.Validate(t,progress);var map=LoadedMapPreservation.Restore(t.LoadedMap);var s=JsonSerializer.Deserialize<WorkAssetResolutionSave>(json);
        if(work.Version!=3||s?.Version is not (1 or 2)||s.Original!=WorkProgress.Hash(WorkProgress.Original(t))||s.Entries==null||s.Entries.Length is <1 or >1000||s.Entries.Any(e=>e==null)||Write(s)!=json)
            throw new ArgumentException("Separate current-object ledger is not paired to the original transfer.");
        MapAssetTransfers.Pair(s.BaseCalls,t.LoadedMap,t.FromOwner,t.TargetOwner);var calls=MapAssetTransfers.Restore(s.BaseCalls).Calls;
        WorkProgress.AssetAdvance(t,progress,s.BaseCalls);
        if(s.Entries.Select(e=>e.Original).Distinct().Count()!=s.Entries.Length||s.Entries.Where(e=>e.Actual!=null).Select(e=>e.Actual).Distinct().Count()!=s.Entries.Count(e=>e.Actual!=null)||
            (s.Version==1?!s.Entries.Any(e=>e.Event.HasValue):!s.Entries.Any(e=>e.Event==null&&e.StorageV7!=null&&e.StorageProgressV11?.Length>0)))throw new ArgumentException("Unique actual objects plus explicit positive original provenance required.");
        foreach(var e in s.Entries)
        {
            WorkStorageProgressRules.Check(e,work);
            if(s.Version==2&&e.Event==null&&e.StorageProgressV11!=null)
            {
                var start=work.Events[e.StorageProgressV11[0].Event].StorageOriginsV12;
                if(start?.Calls!=s.BaseCalls||start?.Stores.Any(row=>row.Id==e.Original)!=true)throw new ArgumentException("New storage ledger needs its actual original before-call origin.");
            }
            if(e.TerrainV8!=null)
            {
                var terrain=e.TerrainV8;var frame=map.WorkFramesV1?.SingleOrDefault(f=>f.Id==e.Original);var terrainCall=calls.SingleOrDefault(c=>c.Id==e.Original);
                if(e.Actual!=null||e.Asset!=null||e.Physical!=null||e.WorktableV6!=null||e.StorageV7!=null||e.Started||e.Returned||e.Verified||e.Approval!=null||
                    frame?.TerrainV8!=terrain.Def||terrainCall==null||terrainCall.Started||!e.Event.HasValue||e.Event<0||e.Event>=work.Events.Length||
                    terrain.Cell!=map.Things.Single(p=>p.Id==e.Original).Cell||string.IsNullOrWhiteSpace(terrain.Def)||terrain.Under!=null&&string.IsNullOrWhiteSpace(terrain.Under)||terrain.Roof!=null&&string.IsNullOrWhiteSpace(terrain.Roof)||!terrain.Accepted&&terrain.Approval!=null||terrain.Accepted!=(!string.IsNullOrWhiteSpace(terrain.Approval)))throw new ArgumentException("Explicit original terrain-frame acceptance required; no synthetic native asset call.");
                var ev=work.Events[e.Event.Value];if(ev.Kind!="frame"||!ev.Returned.HasValue||ev.Inputs[0]!=e.Original||!ev.Removed.Contains(e.Original)||ev.Added.Length!=0)throw new ArgumentException("Positive native terrain completion required.");
                WorkTerrainLayerRules.Pair(frame.TerrainLayersV9,terrain.LayersV9,terrain.Def);
                continue;
            }
            if(e.StorageV7!=null)
            {
                var storage=e.StorageV7;
                if(e.Physical==null||e.WorktableV6!=null||storage.Id!=e.Actual||!CityStockProof.Valid(storage.Configuration)||storage.Cells==null||storage.Cells.Length is <1 or >4096||
                    storage.Cells.Any(c=>c<0||c>=map.Width*map.Height)||!storage.Cells.SequenceEqual(storage.Cells.Distinct().OrderBy(c=>c))||!storage.Cells.Contains(e.Physical.Cell)||
                    storage.Contents==null||storage.Contents.Length>10000||storage.Contents.Any(id=>string.IsNullOrWhiteSpace(id)||id==e.Actual)||!storage.Contents.SequenceEqual(storage.Contents.Distinct().OrderBy(id=>id,StringComparer.Ordinal)))
                    throw new ArgumentException("Explicit complete ungrouped storage scope required.");
                if(map.Groups?.Any(g=>g.Members.Contains(e.Original))==true)throw new ArgumentException("Grouped storage requires separate whole-group authority.");
                var known=new HashSet<string>(work.Seal.Objects,StringComparer.Ordinal);
                foreach(var ev in work.Events.Where(ev=>e.Event.HasValue&&ev.Sequence<=e.Event.Value&&ev.Returned.HasValue)){known.ExceptWith(ev.Removed);known.UnionWith(ev.Added);}
                if(storage.Contents.Any(id=>!known.Contains(id)))throw new ArgumentException("Stored item lacks original or observed object provenance.");
            }
            if(e.WorktableV6!=null&&(e.WorktableV6.Id!=e.Actual||!CityStockProof.Valid(e.WorktableV6.Configuration)||e.WorktableV6.Bills==null||e.WorktableV6.Bills.Length>15||e.WorktableV6.Bills.Any(string.IsNullOrWhiteSpace)||e.WorktableV6.Bills.Distinct().Count()!=e.WorktableV6.Bills.Length))throw new ArgumentException("Explicit actual worktable evidence required.");
            var call=calls.SingleOrDefault(c=>c.Id==e.Original);var original=map.OwnedAssets?.SingleOrDefault(a=>a.Id==e.Original);
            if(call==null||call.Started||original==null||e.Asset==null||e.Physical==null||e.Actual!=e.Asset.Id||e.Actual!=e.Physical.Id||e.Asset.Owner!=t.FromOwner||!CityStockProof.Valid(e.Asset.Configuration)||
                string.IsNullOrWhiteSpace(e.Physical.Def)||e.Physical.Count!=1||e.Physical.HitPoints<=0||e.Physical.Cell<0||e.Physical.Cell>=map.Width*map.Height||e.Physical.Rotation is <0 or >3||
                e.Returned&&!e.Started||e.Verified&&!e.Returned||e.Started!=(!string.IsNullOrWhiteSpace(e.Approval)))throw new ArgumentException("Original call or independent actual-object facts changed.");
            if(e.Event.HasValue)
            {
                int i=e.Event.Value;if(i<0||i>=work.Events.Length||map.WorkFramesV1?.Any(f=>f.Id==e.Original)!=true)throw new ArgumentException("Original owned frame event required.");
                if(e.StorageV7!=null&&map.WorkFramesV1.Single(f=>f.Id==e.Original).StorageV7==null)throw new ArgumentException("Original frame lacks explicit storage-settings scope.");
                var ev=work.Events[i];if(ev.Kind!="frame"||!ev.Returned.HasValue||ev.Inputs[0]!=e.Original||!ev.Removed.Contains(e.Original)||ev.Added.Length!=1||ev.Added[0]!=e.Actual||map.Things.Any(p=>p.Id==e.Actual))
                    throw new ArgumentException("Positive natural completion must name the exact new object.");
            }
            else if(e.Actual!=e.Original||e.Asset!=original||e.Physical!=map.Things.SingleOrDefault(p=>p.Id==e.Original)||JsonSerializer.Serialize(e.WorktableV6)!=JsonSerializer.Serialize(map.WorktablesV1?.SingleOrDefault(p=>p.Id==e.Original))||JsonSerializer.Serialize(e.StorageV7)!=JsonSerializer.Serialize(map.Storage?.SingleOrDefault(p=>p.Id==e.Original)))throw new ArgumentException("Original remainder property cannot be replaced or rebased.");
        }
        foreach(var e in s.Entries.Where(e=>e.StorageV7!=null))
        {
            var current=WorkStorageProgressRules.Current(e);
            if(s.Entries.Any(other=>other.Original!=e.Original&&other.StorageV7!=null&&(WorkStorageProgressRules.Current(other).Cells.Intersect(current.Cells).Any()||WorkStorageProgressRules.Current(other).Contents.Intersect(current.Contents).Any()))||
                map.Storage?.Any(other=>!s.Entries.Any(row=>row.Original==other.Id&&row.StorageV7!=null)&&(other.Cells.Intersect(current.Cells).Any()||other.Contents.Intersect(current.Contents).Any()))==true)throw new ArgumentException("Storage custody overlaps another original shelf.");
        }
        if(complete&&calls.Any(c=>!c.Returned&&!s.Entries.Any(e=>e.Original==c.Id&&Resolved(e))))throw new ArgumentException("Every original property needs positive original return or separately verified actual resolution.");
        return s;
    }
    public static bool Resolved(WorkAssetResolution entry)=>entry.Verified||entry.TerrainV8?.Accepted==true;
    public static string CreateStorage(TerritoryTransfer t,string progress,string originalCalls,string id,int sequence,NeutralMapStorage current)
    {
        var work=WorkProgress.Validate(t,progress);var map=LoadedMapPreservation.Restore(t.LoadedMap);
        if(sequence<0||sequence>=work.Events.Length||work.Events[sequence].StorageOriginsV12==null||work.Events[sequence].StorageOriginsV12.Calls!=originalCalls||work.Events[sequence].StorageOriginsV12.Stores.Any(row=>row.Id==id)!=true)throw new ArgumentException("Positive work with an original before-call storage origin required.");
        var storage=map.Storage.Single(row=>row.Id==id);var asset=map.OwnedAssets.Single(row=>row.Id==id);var physical=map.Things.Single(row=>row.Id==id);
        var entry=new WorkAssetResolution(id,id,null,asset,physical){StorageV7=storage,StorageProgressV11=[new(0,sequence,WorkProgress.Hash(storage),current)]};
        string next=Write(new(2,WorkProgress.Hash(WorkProgress.Original(t)),originalCalls,[entry],null));Validate(t,progress,next);return next;
    }
    public static string AppendStorage(TerritoryTransfer t,string progress,string json,string id,int eventIndex,NeutralMapStorage current)
        =>Update(t,progress,json,id,e=>
        {
            if(e.StorageV7==null||e.Started&&!e.Returned)throw new ArgumentException("Known same storage custody required; never explain an unknown property call.");
            var rows=e.StorageProgressV11??[];return e with{StorageProgressV11=rows.Append(new WorkStorageProgress(rows.Length,eventIndex,WorkProgress.Hash(WorkStorageProgressRules.Current(e)),current)).ToArray()};
        });
    public static string AddTerrain(TerritoryTransfer t,string progress,string current,string originalCalls,string frame,int eventIndex,WorkTerrainAcceptance terrain)
    {
        if(terrain==null||terrain.Accepted||terrain.Approval!=null)throw new ArgumentException("Fresh observed terrain scope required.");
        var s=current==null?new WorkAssetResolutionSave(1,WorkProgress.Hash(WorkProgress.Original(t)),originalCalls,[],null):Validate(t,progress,current);
        if(s.BaseCalls!=originalCalls)throw new ArgumentException("Original calls remain frozen.");
        string next=Write(s with{Entries=s.Entries.Append(new WorkAssetResolution(frame,null,eventIndex,null,null){TerrainV8=terrain}).ToArray()});Validate(t,progress,next);return next;
    }
    public static string AcceptTerrain(TerritoryTransfer t,string progress,string json,string id,CustodyAgreementReceipt consent)
        =>Update(t,progress,json,id,e=>{if(e.TerrainV8==null||e.TerrainV8.Accepted)throw new ArgumentException("New explicit terrain acceptance required.");Approved(t,progress,e,consent);return e with{TerrainV8=e.TerrainV8 with{Accepted=true,Approval=consent.Witness.Id}};});
    public static string AddSuccessor(TerritoryTransfer t,string progress,string current,string originalCalls,string frame,int eventIndex,MapAsset asset,NeutralMapThing physical)
        =>AddSuccessor(t,progress,current,originalCalls,frame,eventIndex,asset,physical,null);
    public static string AddSuccessor(TerritoryTransfer t,string progress,string current,string originalCalls,string frame,int eventIndex,MapAsset asset,NeutralMapThing physical,NativeWorktable table)
        =>AddSuccessor(t,progress,current,originalCalls,frame,eventIndex,asset,physical,table,null);
    public static string AddSuccessor(TerritoryTransfer t,string progress,string current,string originalCalls,string frame,int eventIndex,MapAsset asset,NeutralMapThing physical,NativeWorktable table,NeutralMapStorage storage)
    {
        var s=current==null?new WorkAssetResolutionSave(1,WorkProgress.Hash(WorkProgress.Original(t)),originalCalls,[],null):Validate(t,progress,current);
        if(s.BaseCalls!=originalCalls)throw new ArgumentException("Original asset-call prefix is frozen independently.");
        string next=Write(s with{Entries=s.Entries.Append(new WorkAssetResolution(frame,asset.Id,eventIndex,asset,physical){WorktableV6=table,StorageV7=storage}).ToArray()});Validate(t,progress,next);return next;
    }
    public static string AddOriginal(TerritoryTransfer t,string progress,string current,string id)
    {
        var s=Validate(t,progress,current);var map=LoadedMapPreservation.Restore(t.LoadedMap);var asset=map.OwnedAssets.Single(a=>a.Id==id);var physical=map.Things.Single(p=>p.Id==id);
        string next=Write(s with{Entries=s.Entries.Append(new WorkAssetResolution(id,id,null,asset,physical){WorktableV6=map.WorktablesV1?.SingleOrDefault(p=>p.Id==id),StorageV7=map.Storage?.SingleOrDefault(p=>p.Id==id)}).ToArray()});Validate(t,progress,next);return next;
    }
    public static void Approved(TerritoryTransfer t,string progress,WorkAssetResolution entry,CustodyAgreementReceipt receipt)
    {
        WorkStorageProgressRules.Check(entry,WorkProgress.Validate(t,progress));
        if(receipt==null||receipt.Proof!=WorkProgress.Hash(receipt.Witness)||CustodyAgreements.Validate(receipt.Witness)!=WorkProgress.Original(t))throw new ArgumentException("Current full-party property approval required.");
        var a=WorkProgress.Approval(t,receipt.Witness.WorkProgressV1);var before=Validate(t,a.Progress,a.ResolutionsV5);
        var old=before.Entries.SingleOrDefault(e=>e.Original==entry.Original);var clean=WorkStorageProgressRules.CallPreparation(entry) with{TerrainV8=entry.TerrainV8==null?null:entry.TerrainV8 with{Accepted=false,Approval=null}};
        if(old!=null){if(JsonSerializer.Serialize(old)!=JsonSerializer.Serialize(clean))throw new ArgumentException("Actual property differs from its approved preparation.");}
        else if(entry.Event.HasValue || JsonSerializer.Serialize(Validate(t,progress,AddOriginal(t,progress,a.ResolutionsV5,entry.Original)).Entries.Last())!=JsonSerializer.Serialize(clean))throw new ArgumentException("Only unchanged original remainder can be prepared under this scope.");
    }
    private static string Update(TerritoryTransfer t,string progress,string json,string id,Func<WorkAssetResolution,WorkAssetResolution> action)
    {var s=Validate(t,progress,json);var e=s.Entries.Single(row=>row.Original==id);string next=Write(s with{Entries=s.Entries.Select(row=>row.Original==id?action(e):row).ToArray()});Validate(t,progress,next);return next;}
    public static string Begin(TerritoryTransfer t,string progress,string json,string id,string owner,CustodyAgreementReceipt consent)
        =>Update(t,progress,json,id,e=>{if(e.TerrainV8!=null||e.Started||owner!=t.FromOwner)throw new ArgumentException("Never replay an actual-object call.");Approved(t,progress,e,consent);return e with{Started=true,Approval=consent.Witness.Id,StorageCallV11=e.StorageProgressV11?.Length};});
    public static string Returned(TerritoryTransfer t,string progress,string json,string id,string owner)
        =>Update(t,progress,json,id,e=>!e.Started||e.Returned||owner!=t.TargetOwner?throw new ArgumentException("Independent normal return required."):e with{Returned=true});
    public static string Verify(TerritoryTransfer t,string progress,string json,string id)
        =>Update(t,progress,json,id,e=>!e.Returned||e.Verified?throw new ArgumentException("Independent returned property verification required."):e with{Verified=true});
}
