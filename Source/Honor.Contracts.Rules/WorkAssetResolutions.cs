using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record WorkAssetResolution(string Original,string Actual,int? Event,MapAsset Asset,NeutralMapThing Physical,bool Started=false,bool Returned=false,bool Verified=false,string Approval=null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public NativeWorktable WorktableV6 { get; init; }
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
        if(work.Version!=3||s?.Version!=1||s.Original!=WorkProgress.Hash(WorkProgress.Original(t))||s.Entries==null||s.Entries.Length is <1 or >1000||s.Entries.Any(e=>e==null)||Write(s)!=json)
            throw new ArgumentException("Separate current-object ledger is not paired to the original transfer.");
        MapAssetTransfers.Pair(s.BaseCalls,t.LoadedMap,t.FromOwner,t.TargetOwner);var calls=MapAssetTransfers.Restore(s.BaseCalls).Calls;
        WorkProgress.AssetAdvance(t,progress,s.BaseCalls);
        if(s.Entries.Select(e=>e.Original).Distinct().Count()!=s.Entries.Length||s.Entries.Select(e=>e.Actual).Distinct().Count()!=s.Entries.Length||!s.Entries.Any(e=>e.Event.HasValue))throw new ArgumentException("Unique original and actual objects plus positive succession required.");
        foreach(var e in s.Entries)
        {
            if(e.WorktableV6!=null&&(e.WorktableV6.Id!=e.Actual||!CityStockProof.Valid(e.WorktableV6.Configuration)||e.WorktableV6.Bills==null||e.WorktableV6.Bills.Length>15||e.WorktableV6.Bills.Any(string.IsNullOrWhiteSpace)||e.WorktableV6.Bills.Distinct().Count()!=e.WorktableV6.Bills.Length))throw new ArgumentException("Explicit actual worktable evidence required.");
            var call=calls.SingleOrDefault(c=>c.Id==e.Original);var original=map.OwnedAssets?.SingleOrDefault(a=>a.Id==e.Original);
            if(call==null||call.Started||original==null||e.Asset==null||e.Physical==null||e.Actual!=e.Asset.Id||e.Actual!=e.Physical.Id||e.Asset.Owner!=t.FromOwner||!CityStockProof.Valid(e.Asset.Configuration)||
                string.IsNullOrWhiteSpace(e.Physical.Def)||e.Physical.Count!=1||e.Physical.HitPoints<=0||e.Physical.Cell<0||e.Physical.Cell>=map.Width*map.Height||e.Physical.Rotation is <0 or >3||
                e.Returned&&!e.Started||e.Verified&&!e.Returned||e.Started!=(!string.IsNullOrWhiteSpace(e.Approval)))throw new ArgumentException("Original call or independent actual-object facts changed.");
            if(e.Event.HasValue)
            {
                int i=e.Event.Value;if(i<0||i>=work.Events.Length||map.WorkFramesV1?.Any(f=>f.Id==e.Original)!=true)throw new ArgumentException("Original owned frame event required.");
                var ev=work.Events[i];if(ev.Kind!="frame"||!ev.Returned.HasValue||ev.Inputs[0]!=e.Original||!ev.Removed.Contains(e.Original)||ev.Added.Length!=1||ev.Added[0]!=e.Actual||map.Things.Any(p=>p.Id==e.Actual))
                    throw new ArgumentException("Positive natural completion must name the exact new object.");
            }
            else if(e.Actual!=e.Original||e.Asset!=original||e.Physical!=map.Things.SingleOrDefault(p=>p.Id==e.Original)||JsonSerializer.Serialize(e.WorktableV6)!=JsonSerializer.Serialize(map.WorktablesV1?.SingleOrDefault(p=>p.Id==e.Original)))throw new ArgumentException("Original remainder property cannot be replaced or rebased.");
        }
        if(complete&&calls.Any(c=>!c.Returned&&!s.Entries.Any(e=>e.Original==c.Id&&e.Verified)))throw new ArgumentException("Every original property needs positive original return or separately verified actual resolution.");
        return s;
    }
    public static string AddSuccessor(TerritoryTransfer t,string progress,string current,string originalCalls,string frame,int eventIndex,MapAsset asset,NeutralMapThing physical)
        =>AddSuccessor(t,progress,current,originalCalls,frame,eventIndex,asset,physical,null);
    public static string AddSuccessor(TerritoryTransfer t,string progress,string current,string originalCalls,string frame,int eventIndex,MapAsset asset,NeutralMapThing physical,NativeWorktable table)
    {
        var s=current==null?new WorkAssetResolutionSave(1,WorkProgress.Hash(WorkProgress.Original(t)),originalCalls,[],null):Validate(t,progress,current);
        if(s.BaseCalls!=originalCalls)throw new ArgumentException("Original asset-call prefix is frozen independently.");
        string next=Write(s with{Entries=s.Entries.Append(new WorkAssetResolution(frame,asset.Id,eventIndex,asset,physical){WorktableV6=table}).ToArray()});Validate(t,progress,next);return next;
    }
    public static string AddOriginal(TerritoryTransfer t,string progress,string current,string id)
    {
        var s=Validate(t,progress,current);var map=LoadedMapPreservation.Restore(t.LoadedMap);var asset=map.OwnedAssets.Single(a=>a.Id==id);var physical=map.Things.Single(p=>p.Id==id);
        string next=Write(s with{Entries=s.Entries.Append(new WorkAssetResolution(id,id,null,asset,physical){WorktableV6=map.WorktablesV1?.SingleOrDefault(p=>p.Id==id)}).ToArray()});Validate(t,progress,next);return next;
    }
    public static void Approved(TerritoryTransfer t,string progress,WorkAssetResolution entry,CustodyAgreementReceipt receipt)
    {
        if(receipt==null||receipt.Proof!=WorkProgress.Hash(receipt.Witness)||CustodyAgreements.Validate(receipt.Witness)!=WorkProgress.Original(t))throw new ArgumentException("Current full-party property approval required.");
        var a=WorkProgress.Approval(t,receipt.Witness.WorkProgressV1);var before=Validate(t,a.Progress,a.ResolutionsV5);
        var old=before.Entries.SingleOrDefault(e=>e.Original==entry.Original);var clean=entry with{Started=false,Returned=false,Verified=false,Approval=null};
        if(old!=null){if(JsonSerializer.Serialize(old)!=JsonSerializer.Serialize(clean))throw new ArgumentException("Actual property differs from its approved preparation.");}
        else if(entry.Event.HasValue || JsonSerializer.Serialize(Validate(t,progress,AddOriginal(t,progress,a.ResolutionsV5,entry.Original)).Entries.Last())!=JsonSerializer.Serialize(clean))throw new ArgumentException("Only unchanged original remainder can be prepared under this scope.");
    }
    private static string Update(TerritoryTransfer t,string progress,string json,string id,Func<WorkAssetResolution,WorkAssetResolution> action)
    {var s=Validate(t,progress,json);var e=s.Entries.Single(row=>row.Original==id);string next=Write(s with{Entries=s.Entries.Select(row=>row.Original==id?action(e):row).ToArray()});Validate(t,progress,next);return next;}
    public static string Begin(TerritoryTransfer t,string progress,string json,string id,string owner,CustodyAgreementReceipt consent)
        =>Update(t,progress,json,id,e=>{if(e.Started||owner!=t.FromOwner)throw new ArgumentException("Never replay an actual-object call.");Approved(t,progress,e,consent);return e with{Started=true,Approval=consent.Witness.Id};});
    public static string Returned(TerritoryTransfer t,string progress,string json,string id,string owner)
        =>Update(t,progress,json,id,e=>!e.Started||e.Returned||owner!=t.TargetOwner?throw new ArgumentException("Independent normal return required."):e with{Returned=true});
    public static string Verify(TerritoryTransfer t,string progress,string json,string id)
        =>Update(t,progress,json,id,e=>!e.Returned||e.Verified?throw new ArgumentException("Independent returned property verification required."):e with{Verified=true});
}
