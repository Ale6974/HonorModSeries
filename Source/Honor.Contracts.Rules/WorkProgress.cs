using System.Text;
using System.Text.Json;
namespace Honor.Contracts.Rules;

public sealed record WorkProgressSeal(string Original, string Census, string CensusProof, string[] Calls, string[] Care, string[] Areas, string Assets, long Tick,string Survey,string[] Objects);
public sealed record WorkProgressEvent(int Sequence, string Kind, string Actor, string Job, string[] Inputs, string Before, long Started, string[] Removed = null, string[] Added = null, string After = null, long? Returned = null,long? BodyReturned = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkEventGrantV4 GrantV4 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public bool? PlacementResultV4 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkFrameFilth[] FilthV10 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkStorageOrigins StorageOriginsV12 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkStorageStacks[] StorageStacksV13 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkStorageRetirement[] StorageRetirementsV14 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorktableIteration WorktableIterationV15 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkStorageMixture[] StorageMixturesV16 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkStorageMerge StorageMergeV17 { get; init; }
}
public sealed record WorkProgressSave(int Version, WorkProgressSeal Seal, WorkProgressEvent[] Events)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string[] RosterV3 { get; init; }
}
public sealed record WorkPendingResident(string Pawn,string Proof);
public sealed record WorkProgressApproval(string Progress, string Current, long Tick)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkPendingResident[] PendingV3 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public ActiveWorkRow[] JobsV4 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string ResolutionsV5 { get; init; }
}
public sealed record WorkCompletionReceipt(string Approval,string Agreement,long Tick)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string AssetCallsV2 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public WorkClinicalState ClinicalV3 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string ResolutionsV5 { get; init; }
}
/// <summary>Append-only post-handover observations. Never replaces original clinical/map/call proofs.</summary>
public static partial class WorkProgress
{
    public static string Hash(object value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    public static TerritoryTransfer Original(TerritoryTransfer t) => t with { Phase=TerritoryTransferPhase.Prepared,ResolvedTick=null,WorkCompletionV1=null };
    private static bool Key(string value) => !string.IsNullOrWhiteSpace(value) && value.Length<=120 && !value.Any(char.IsControl);
    public static string Prepare(TerritoryTransfer t,string census,string censusProof,string[] calls,string[] care,string[] areas,string assets,long tick,string survey,string[] objects)
    {
        var s=new WorkProgressSave(1,new(Hash(Original(t)),census,censusProof,calls,care,areas,assets,tick,survey,objects),[]);
        string json=JsonSerializer.Serialize(s); Validate(t,json);return json;
    }
    public static string PrepareBeforeAssets(TerritoryTransfer t,string census,string censusProof,string[] calls,string[] care,string[] areas,string assets,long tick,string survey,string[] objects)
    {
        var s=new WorkProgressSave(2,new(Hash(Original(t)),census,censusProof,calls,care,areas,assets,tick,survey,objects),[]);
        string json=JsonSerializer.Serialize(s);Validate(t,json);return json;
    }
    public static void AssetAdvance(TerritoryTransfer t,string progress,string current,bool complete=false)
        =>AssetAdvance(t,progress,current,complete,null);
    public static void AssetAdvance(TerritoryTransfer t,string progress,string current,bool complete,string resolutions)
    {
        var s=Validate(t,progress);
        if(resolutions!=null){var resolution=WorkAssetResolutions.Validate(t,progress,resolutions,complete);if(resolution.BaseCalls!=current)throw new ArgumentException("Original calls changed after separate property resolution.");return;}
        if(s.Version==1){if(current!=s.Seal.Assets)throw new ArgumentException("Original complete asset seal changed.");return;}
        if(s.Seal.Assets==null){if(current!=null)throw new ArgumentException("Unexpected asset calls in resident-only scope.");return;}
        MapAssetTransfers.Pair(current,t.LoadedMap,t.FromOwner,t.TargetOwner,complete);
        var before=MapAssetTransfers.Restore(s.Seal.Assets);var after=MapAssetTransfers.Restore(current);
        if(after.Calls.Where((c,i)=>c.Started&&!c.Returned || before.Calls[i].Returned&&!c.Returned).Any())throw new ArgumentException("Unknown or regressed original asset calls.");
    }
    public static WorkProgressSave Validate(TerritoryTransfer t,string json)
    {
        if(json==null || json.Length>4000000 || t.StockFormat!=25 || !t.StandaloneActiveWorkV1 || t.Purpose!=TerritoryTransferPurpose.ActiveCharter || t.PreviousTransferV1!=null || t.RoyalResidentsV1!=null || !t.CityCallJournalV1)
            throw new ArgumentException("New standalone original work custody required.");
        var s=JsonSerializer.Deserialize<WorkProgressSave>(json);
        if(s?.Version is not (1 or 2 or 3) || s.Seal==null || s.Events==null || s.Events.Length>256 || s.Seal.Original!=Hash(Original(t)) || s.Seal.Tick<t.PreparedTick || s.Seal.Tick>int.MaxValue)
            throw new ArgumentException("Original verified work boundary is unpaired.");
        if(!CityStockProof.Valid(s.Seal.Survey)||s.Seal.Objects==null||s.Seal.Objects.Length>10000||s.Seal.Objects.Any(id=>!Key(id))||!s.Seal.Objects.SequenceEqual(s.Seal.Objects.Distinct().OrderBy(id=>id,StringComparer.Ordinal)))throw new ArgumentException("Original verified physical identity set required.");
        var c=ResidentTransferCensus.Restore(s.Seal.Census);var set=ResidentMoveSet.Restore(c.Residents);
        if(s.Version==3){if(s.RosterV3==null || !s.RosterV3.OrderBy(id=>id,StringComparer.Ordinal).SequenceEqual(set.Residents.Select(p=>p.Id)))throw new ArgumentException("Original ordered native roster required.");}
        else if(s.RosterV3!=null)throw new ArgumentException("Legacy work scope has no new roster authority.");
        if(c.Profile!=16 || set.Proof!=t.ResidentProof || s.Seal.Areas?.Length!=set.Residents.Count)throw new ArgumentException("Original work census missing.");
        ResidentTransferCensus.Check(s.Seal.Census,s.Seal.CensusProof,t.Territory,t.FromOwner,t.TargetOwner,16,set,s.Seal.Calls,s.Seal.Care,true,s.Version!=3);
        ResidentTransferCensus.RequireRecoverable(s.Seal.Calls,s.Seal.Care);
        if(s.Version==3&&!s.Seal.Calls.Any(raw=>ResidentTransferCensus.Call(raw).Returned))throw new ArgumentException("At least one original positive resident return required.");
        for(int i=0;i<set.Residents.Count;i++){var a=ResidentAreas.Restore(s.Seal.Areas[i]);var call=ResidentTransferCensus.Call(s.Seal.Calls[i]);if(a.Id!=set.Residents[i].Id || s.Version!=3&&!a.Consumed || a.Started!=call.Started || a.Started&&!a.Returned)throw new ArgumentException("Original area preservation is incomplete or uncertain.");}
        var map=LoadedMapPreservation.Restore(t.LoadedMap);
        if(map.OwnedAssets==null){if(s.Seal.Assets!=null)throw new ArgumentException("Unexpected asset calls.");}
        else MapAssetTransfers.Pair(s.Seal.Assets,t.LoadedMap,t.FromOwner,t.TargetOwner,s.Version==1);
        if(s.Version==2&&map.OwnedAssets==null || s.Version>=2&&s.Seal.Assets!=null&&MapAssetTransfers.Restore(s.Seal.Assets).Calls.Any(c=>c.Started&&!c.Returned))throw new ArgumentException("Known partial original assets required.");
        var actors=map.ActiveWorkV1?.ToDictionary(r=>r.Pawn,StringComparer.Ordinal)??throw new ArgumentException("Original active work missing.");
        long previous=s.Seal.Tick;var removed=new HashSet<string>(StringComparer.Ordinal);string previousStorageCalls=s.Seal.Assets;
        for(int i=0;i<s.Events.Length;i++)
        {
            var e=s.Events[i];
            if(e==null || e.Sequence!=i || e.Kind is not ("frame" or "job-end" or "recipe" or "haul-place") ||
                !(actors.TryGetValue(e.Actor,out var owner)&&owner.Jobs.Contains(e.Job) || e.GrantV4!=null&&s.Version==3&&set.Residents.Any(p=>p.Id==e.Actor)) ||
                e.Inputs==null || e.Inputs.Length>10000 || e.Inputs.Any(id=>!Key(id)) || e.Inputs.Distinct().Count()!=e.Inputs.Length || !CityStockProof.Valid(e.Before) ||
                e.Started<previous || e.Started>int.MaxValue || !e.Returned.HasValue && i!=s.Events.Length-1)throw new ArgumentException("Original ordered native progress start is invalid.");
            if(e.GrantV4!=null){if(s.Version!=3)throw new ArgumentException("Legacy events grant no new work authority.");WorkJobObservations.Check(e.GrantV4,e.Actor,e.Job,s.Seal.Tick,e.Started);}
            if(e.StorageOriginsV12!=null)
            {
                if(s.Version!=3||e.Kind is not ("recipe" or "haul-place"))throw new ArgumentException("Only explicit clinical production/placement may capture new storage origins.");
                WorkStorageOriginRules.Check(t,map,e.StorageOriginsV12,previousStorageCalls);previousStorageCalls=e.StorageOriginsV12.Calls;
            }
            WorkStorageStackRules.Check(map,s,i);
            WorkStorageRetirements.Check(s,i);
            WorkStorageMixtures.Check(s,i);
            WorkStorageMerges.Check(t,map,s,i);
            WorktableIterations.Check(t,map,s,i);
            var originalFilth=e.Kind=="frame"&&e.Inputs.Length>0?map.WorkFramesV1?.SingleOrDefault(frame=>frame.Id==e.Inputs[0])?.FilthV10:null;
            if(originalFilth!=null||e.FilthV10!=null)
            {
                if(e.Kind!="frame")throw new ArgumentException("Only explicit original frame observations may contain ancillary filth.");
                WorkFrameFilthRules.Pair(originalFilth,e.FilthV10);
                if(e.FilthV10.Any(filth=>e.Inputs.Contains(filth.Id)))throw new ArgumentException("Primary and ancillary native inputs overlap.");
            }
            var ancillary=e.FilthV10?.Select(filth=>filth.Id).ToArray()??[];
            if(e.Kind=="haul-place"&&(e.GrantV4==null||e.Inputs.Length!=1) || e.PlacementResultV4.HasValue&&(e.Kind!="haul-place"||!e.BodyReturned.HasValue))throw new ArgumentException("Explicit native placement grant/result required.");
            if(e.BodyReturned.HasValue && (e.BodyReturned<e.Started || e.BodyReturned>int.MaxValue))throw new ArgumentException("Invalid native progress body-return clock.");
            if(!e.Returned.HasValue){if(e.Removed!=null || e.Added!=null || e.After!=null)throw new ArgumentException("Unverified progress output cannot contain results.");previous=e.Started;continue;}
            if(!e.BodyReturned.HasValue || e.BodyReturned<e.Started || e.Returned<e.BodyReturned || e.Returned>int.MaxValue || e.Removed==null || e.Added==null || !CityStockProof.Valid(e.After) || e.Removed.Length>10000 || e.Added.Length>10000 ||
                e.Removed.Any(id=>!Key(id)||!e.Inputs.Contains(id)&&!ancillary.Contains(id)||!removed.Add(id)) || e.Removed.Distinct().Count()!=e.Removed.Length || e.Added.Any(id=>!Key(id)||removed.Contains(id)) || e.Added.Distinct().Count()!=e.Added.Length ||
                e.Kind=="job-end" && (e.Removed.Length!=0 || e.Added.Length!=0) || e.Kind=="frame" && (e.Inputs.Length==0 || e.Removed.Length!=e.Inputs.Length+ancillary.Length || e.Added.Length>1))
                throw new ArgumentException("Native progress normal return has invalid object lineage.");
            if(e.Kind=="haul-place" && (!e.PlacementResultV4.HasValue || e.PlacementResultV4==false&&(e.Removed.Length!=0||e.Added.Length!=0)))throw new ArgumentException("Failed placement cannot prove physical identity changes.");
            previous=e.Returned.Value;
        }
        if(JsonSerializer.Serialize(s)!=json)throw new ArgumentException("Canonical original progress archive required.");return s;
    }
    public static string Begin(TerritoryTransfer t,string json,string kind,string actor,string job,string[] inputs,string before,long tick)
        =>Begin(t,json,kind,actor,job,inputs,before,tick,null);
    public static string Begin(TerritoryTransfer t,string json,string kind,string actor,string job,string[] inputs,string before,long tick,WorkEventGrantV4 grant)
        =>Begin(t,json,kind,actor,job,inputs,before,tick,grant,null);
    public static string Begin(TerritoryTransfer t,string json,string kind,string actor,string job,string[] inputs,string before,long tick,WorkEventGrantV4 grant,WorkFrameFilth[] filth)
        =>Begin(t,json,kind,actor,job,inputs,before,tick,grant,filth,null);
    public static string Begin(TerritoryTransfer t,string json,string kind,string actor,string job,string[] inputs,string before,long tick,WorkEventGrantV4 grant,WorkFrameFilth[] filth,WorkStorageOrigins storage)
    {
        var s=Validate(t,json);if(s.Events.LastOrDefault()?.Returned==null && s.Events.Length!=0)throw new ArgumentException("Unknown progress operation is never replayed.");
        string next=JsonSerializer.Serialize(s with {Events=s.Events.Append(new WorkProgressEvent(s.Events.Length,kind,actor,job,inputs,before,tick){GrantV4=grant,FilthV10=filth,StorageOriginsV12=storage}).ToArray()});Validate(t,next);return next;
    }
    public static string Returned(TerritoryTransfer t,string json,int sequence,string[] removed,string[] added,string after,long tick)
        =>Returned(t,json,sequence,removed,added,after,tick,null);
    public static string Returned(TerritoryTransfer t,string json,int sequence,string[] removed,string[] added,string after,long tick,WorkStorageStacks[] stacks)
        =>Returned(t,json,sequence,removed,added,after,tick,stacks,null);
    public static string Returned(TerritoryTransfer t,string json,int sequence,string[] removed,string[] added,string after,long tick,WorkStorageStacks[] stacks,WorktableIteration iteration)
        =>Returned(t,json,sequence,removed,added,after,tick,stacks,iteration,null);
    public static string Returned(TerritoryTransfer t,string json,int sequence,string[] removed,string[] added,string after,long tick,WorkStorageStacks[] stacks,WorktableIteration iteration,WorkStorageMixture[] mixtures)
        =>Returned(t,json,sequence,removed,added,after,tick,stacks,iteration,mixtures,null);
    public static string Returned(TerritoryTransfer t,string json,int sequence,string[] removed,string[] added,string after,long tick,WorkStorageStacks[] stacks,WorktableIteration iteration,WorkStorageMixture[] mixtures,WorkStorageMerge merge)
    {
        var s=Validate(t,json);var last=s.Events.LastOrDefault();if(last==null || last.Sequence!=sequence || last.Returned.HasValue)throw new ArgumentException("Exact original unreturned progress start required.");
        if(stacks!=null&&(last.StorageStacksV13==null||stacks.Length!=last.StorageStacksV13.Length||stacks.Where((row,i)=>row==null||JsonSerializer.Serialize(row with{After=null})!=JsonSerializer.Serialize(last.StorageStacksV13[i])).Any()))throw new ArgumentException("Original stack before-evidence cannot be replaced after return.");
        if(iteration!=null&&(last.WorktableIterationV15==null||JsonSerializer.Serialize(iteration with{After=null,AfterStable=null,AfterCount=null})!=JsonSerializer.Serialize(last.WorktableIterationV15)))throw new ArgumentException("Original table iteration start cannot be replaced after return.");
        if(mixtures!=null&&(last.StorageMixturesV16==null||mixtures.Length!=last.StorageMixturesV16.Length||mixtures.Where((row,i)=>row==null||JsonSerializer.Serialize(row with{After=null})!=JsonSerializer.Serialize(last.StorageMixturesV16[i])).Any()))throw new ArgumentException("Original mixture before-evidence cannot be replaced after return.");
        if(merge!=null&&(last.StorageMergeV17==null||JsonSerializer.Serialize(merge with{After=null})!=JsonSerializer.Serialize(last.StorageMergeV17)))throw new ArgumentException("Original merge input proof cannot be replaced.");
        var rows=s.Events.ToArray();rows[rows.Length-1]=last with{Removed=removed,Added=added,After=after,Returned=tick,StorageStacksV13=stacks??last.StorageStacksV13,WorktableIterationV15=iteration??last.WorktableIterationV15,StorageMixturesV16=mixtures??last.StorageMixturesV16,StorageMergeV17=merge??last.StorageMergeV17};string next=JsonSerializer.Serialize(s with{Events=rows});Validate(t,next);return next;
    }
    public static string BindStorageStacks(TerritoryTransfer t,string json,WorkStorageStacks[] stacks)
    {
        var s=Validate(t,json);var last=s.Events.LastOrDefault();
        if(last==null||last.BodyReturned.HasValue||last.Returned.HasValue||last.StorageStacksV13!=null||stacks==null||stacks.Any(row=>row?.After!=null))throw new ArgumentException("New original before-body stack observation required.");
        var rows=s.Events.ToArray();rows[rows.Length-1]=last with{StorageStacksV13=stacks};string next=JsonSerializer.Serialize(s with{Events=rows});Validate(t,next);return next;
    }
    public static string BindStorageRetirements(TerritoryTransfer t,string json,WorkStorageRetirement[] starts)
    {
        var s=Validate(t,json);var last=s.Events.LastOrDefault();
        if(last==null||last.BodyReturned.HasValue||last.Returned.HasValue||last.StorageRetirementsV14!=null||starts==null)throw new ArgumentException("New explicit before-body retirement required; never backfill outputs.");
        var rows=s.Events.ToArray();rows[rows.Length-1]=last with{StorageRetirementsV14=starts};string next=JsonSerializer.Serialize(s with{Events=rows});Validate(t,next);return next;
    }
    public static string BindWorktableIteration(TerritoryTransfer t,string json,WorktableIteration start)
    {
        var s=Validate(t,json);var last=s.Events.LastOrDefault();
        if(last==null||last.BodyReturned.HasValue||last.Returned.HasValue||last.WorktableIterationV15!=null||start==null||start.After!=null||start.AfterStable!=null||start.AfterCount.HasValue)throw new ArgumentException("New before-body table iteration required; never backfill results.");
        var rows=s.Events.ToArray();rows[rows.Length-1]=last with{WorktableIterationV15=start};string next=JsonSerializer.Serialize(s with{Events=rows});Validate(t,next);return next;
    }
    public static string BindStorageMixtures(TerritoryTransfer t,string json,WorkStorageMixture[] starts)
    {
        var s=Validate(t,json);var last=s.Events.LastOrDefault();
        if(last==null||last.BodyReturned.HasValue||last.Returned.HasValue||last.StorageMixturesV16!=null||starts==null||starts.Any(r=>r?.After!=null))throw new ArgumentException("New before-body mixed-consumption evidence required.");
        var rows=s.Events.ToArray();rows[rows.Length-1]=last with{StorageMixturesV16=starts};string next=JsonSerializer.Serialize(s with{Events=rows});Validate(t,next);return next;
    }
    public static string BindStorageMerge(TerritoryTransfer t,string json,WorkStorageMerge start)
    {
        var s=Validate(t,json);var last=s.Events.LastOrDefault();
        if(last==null||last.BodyReturned.HasValue||last.Returned.HasValue||last.StorageMergeV17!=null||start==null||start.After!=null)throw new ArgumentException("New before-body merge evidence required.");
        var rows=s.Events.ToArray();rows[rows.Length-1]=last with{StorageMergeV17=start};string next=JsonSerializer.Serialize(s with{Events=rows});Validate(t,next);return next;
    }
    public static string ObserveReturn(TerritoryTransfer t,string json,int sequence,long tick)
    {
        var s=Validate(t,json);var last=s.Events.LastOrDefault();if(last==null || last.Sequence!=sequence || last.BodyReturned.HasValue || tick<last.Started || tick>int.MaxValue)throw new ArgumentException("Exact original native progress return required.");
        var rows=s.Events.ToArray();rows[rows.Length-1]=last with{BodyReturned=tick};string next=JsonSerializer.Serialize(s with{Events=rows});Validate(t,next);return next;
    }
    public static string ObservePlacementReturn(TerritoryTransfer t,string json,int sequence,long tick,bool placed)
    {
        var s=Validate(t,json);if(s.Events.LastOrDefault()?.Kind!="haul-place")throw new ArgumentException("Exact native placement start required.");
        var returned=Validate(t,ObserveReturn(t,json,sequence,tick));var rows=returned.Events.ToArray();rows[rows.Length-1]=rows[rows.Length-1] with{PlacementResultV4=placed};
        string next=JsonSerializer.Serialize(returned with{Events=rows});Validate(t,next);return next;
    }
    public static string Approve(TerritoryTransfer t,string progress,string current,long tick)=>Approve(t,progress,current,tick,null);
    public static string Approve(TerritoryTransfer t,string progress,string current,long tick,WorkPendingResident[] pending)
        =>Approve(t,progress,current,tick,pending,null);
    public static string Approve(TerritoryTransfer t,string progress,string current,long tick,WorkPendingResident[] pending,ActiveWorkRow[] jobs)
        =>Approve(t,progress,current,tick,pending,jobs,null);
    public static string Approve(TerritoryTransfer t,string progress,string current,long tick,WorkPendingResident[] pending,ActiveWorkRow[] jobs,string resolutions)
    {
        var s=Validate(t,progress);if(s.Events.Any(e=>!e.Returned.HasValue)||!CityStockProof.Valid(current)||tick<s.Seal.Tick||s.Events.Any(e=>e.Returned>tick))throw new ArgumentException("Only positive completed progress and a current survey may be approved.");
        if(s.Version==3){var ids=ResidentMoveSet.Restore(ResidentTransferCensus.Restore(s.Seal.Census).Residents).Residents.Select(r=>r.Id).ToArray();if(pending==null || pending.Length>ids.Length || pending.Any(p=>p==null||!ids.Contains(p.Pawn)||!CityStockProof.Valid(p.Proof))||pending.Select(p=>p.Pawn).Distinct().Count()!=pending.Length)throw new ArgumentException("Exact current pending resident proofs required.");}
        else if(pending!=null)throw new ArgumentException("Legacy approval grants no new current resident authority.");
        if(jobs!=null){if(s.Version!=3)throw new ArgumentException("Current work observations require the explicit clinical scope.");if(jobs.Length!=0)ActiveWork.Validate(jobs,ResidentMoveSet.Restore(ResidentTransferCensus.Restore(s.Seal.Census).Residents).Residents.Select(p=>p.Id));}
        if(resolutions!=null)WorkAssetResolutions.Validate(t,progress,resolutions);
        return JsonSerializer.Serialize(new WorkProgressApproval(progress,current,tick){PendingV3=pending,JobsV4=jobs,ResolutionsV5=resolutions});
    }
    public static WorkProgressApproval Approval(TerritoryTransfer t,string json)
    {
        if(json==null || json.Length>4500000)throw new ArgumentException("Bounded current progress approval required.");var a=JsonSerializer.Deserialize<WorkProgressApproval>(json);
        if(a==null || Approve(t,a.Progress,a.Current,a.Tick,a.PendingV3,a.JobsV4,a.ResolutionsV5)!=json)throw new ArgumentException("Current progress approval differs.");return a;
    }
}
