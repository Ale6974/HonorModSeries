using System.Text;
using System.Text.Json;
namespace Honor.Contracts.Rules;

public sealed record WorkProgressSeal(string Original, string Census, string CensusProof, string[] Calls, string[] Care, string[] Areas, string Assets, long Tick,string Survey,string[] Objects);
public sealed record WorkProgressEvent(int Sequence, string Kind, string Actor, string Job, string[] Inputs, string Before, long Started, string[] Removed = null, string[] Added = null, string After = null, long? Returned = null,long? BodyReturned = null);
public sealed record WorkProgressSave(int Version, WorkProgressSeal Seal, WorkProgressEvent[] Events);
public sealed record WorkProgressApproval(string Progress, string Current, long Tick);
public sealed record WorkCompletionReceipt(string Approval,string Agreement,long Tick);
/// <summary>Append-only post-handover observations. Never replaces original clinical/map/call proofs.</summary>
public static class WorkProgress
{
    public static string Hash(object value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    public static TerritoryTransfer Original(TerritoryTransfer t) => t with { Phase=TerritoryTransferPhase.Prepared,ResolvedTick=null,WorkCompletionV1=null };
    private static bool Key(string value) => !string.IsNullOrWhiteSpace(value) && value.Length<=120 && !value.Any(char.IsControl);
    public static string Prepare(TerritoryTransfer t,string census,string censusProof,string[] calls,string[] care,string[] areas,string assets,long tick,string survey,string[] objects)
    {
        var s=new WorkProgressSave(1,new(Hash(Original(t)),census,censusProof,calls,care,areas,assets,tick,survey,objects),[]);
        string json=JsonSerializer.Serialize(s); Validate(t,json);return json;
    }
    public static WorkProgressSave Validate(TerritoryTransfer t,string json)
    {
        if(json==null || json.Length>4000000 || t.StockFormat!=25 || !t.StandaloneActiveWorkV1 || t.Purpose!=TerritoryTransferPurpose.ActiveCharter || t.PreviousTransferV1!=null || t.RoyalResidentsV1!=null || !t.CityCallJournalV1)
            throw new ArgumentException("New standalone original work custody required.");
        var s=JsonSerializer.Deserialize<WorkProgressSave>(json);
        if(s?.Version!=1 || s.Seal==null || s.Events==null || s.Events.Length>256 || s.Seal.Original!=Hash(Original(t)) || s.Seal.Tick<t.PreparedTick || s.Seal.Tick>int.MaxValue)
            throw new ArgumentException("Original verified work boundary is unpaired.");
        if(!CityStockProof.Valid(s.Seal.Survey)||s.Seal.Objects==null||s.Seal.Objects.Length>10000||s.Seal.Objects.Any(id=>!Key(id))||!s.Seal.Objects.SequenceEqual(s.Seal.Objects.Distinct().OrderBy(id=>id,StringComparer.Ordinal)))throw new ArgumentException("Original verified physical identity set required.");
        var c=ResidentTransferCensus.Restore(s.Seal.Census);var set=ResidentMoveSet.Restore(c.Residents);
        if(c.Profile!=16 || set.Proof!=t.ResidentProof || s.Seal.Areas?.Length!=set.Residents.Count)throw new ArgumentException("Original work census missing.");
        ResidentTransferCensus.Check(s.Seal.Census,s.Seal.CensusProof,t.Territory,t.FromOwner,t.TargetOwner,16,set,s.Seal.Calls,s.Seal.Care,true,true);
        for(int i=0;i<set.Residents.Count;i++){var a=ResidentAreas.Restore(s.Seal.Areas[i]);if(a.Id!=set.Residents[i].Id || !a.Consumed)throw new ArgumentException("Original area preservation is incomplete.");}
        var map=LoadedMapPreservation.Restore(t.LoadedMap);
        if(map.OwnedAssets==null){if(s.Seal.Assets!=null)throw new ArgumentException("Unexpected asset calls.");}
        else MapAssetTransfers.Pair(s.Seal.Assets,t.LoadedMap,t.FromOwner,t.TargetOwner,true);
        var actors=map.ActiveWorkV1?.ToDictionary(r=>r.Pawn,StringComparer.Ordinal)??throw new ArgumentException("Original active work missing.");
        long previous=s.Seal.Tick;var removed=new HashSet<string>(StringComparer.Ordinal);
        for(int i=0;i<s.Events.Length;i++)
        {
            var e=s.Events[i];
            if(e==null || e.Sequence!=i || e.Kind is not ("frame" or "job-end" or "recipe") || !actors.TryGetValue(e.Actor,out var owner) || !owner.Jobs.Contains(e.Job) ||
                e.Inputs==null || e.Inputs.Length>10000 || e.Inputs.Any(id=>!Key(id)) || e.Inputs.Distinct().Count()!=e.Inputs.Length || !CityStockProof.Valid(e.Before) ||
                e.Started<previous || e.Started>int.MaxValue || !e.Returned.HasValue && i!=s.Events.Length-1)throw new ArgumentException("Original ordered native progress start is invalid.");
            if(e.BodyReturned.HasValue && (e.BodyReturned<e.Started || e.BodyReturned>int.MaxValue))throw new ArgumentException("Invalid native progress body-return clock.");
            if(!e.Returned.HasValue){if(e.Removed!=null || e.Added!=null || e.After!=null)throw new ArgumentException("Unverified progress output cannot contain results.");previous=e.Started;continue;}
            if(!e.BodyReturned.HasValue || e.BodyReturned<e.Started || e.Returned<e.BodyReturned || e.Returned>int.MaxValue || e.Removed==null || e.Added==null || !CityStockProof.Valid(e.After) || e.Removed.Length>10000 || e.Added.Length>10000 ||
                e.Removed.Any(id=>!Key(id)||!e.Inputs.Contains(id)||!removed.Add(id)) || e.Removed.Distinct().Count()!=e.Removed.Length || e.Added.Any(id=>!Key(id)||removed.Contains(id)) || e.Added.Distinct().Count()!=e.Added.Length ||
                e.Kind=="job-end" && (e.Removed.Length!=0 || e.Added.Length!=0) || e.Kind=="frame" && (e.Inputs.Length==0 || e.Removed.Length!=e.Inputs.Length || e.Added.Length>1))
                throw new ArgumentException("Native progress normal return has invalid object lineage.");
            previous=e.Returned.Value;
        }
        if(JsonSerializer.Serialize(s)!=json)throw new ArgumentException("Canonical original progress archive required.");return s;
    }
    public static string Begin(TerritoryTransfer t,string json,string kind,string actor,string job,string[] inputs,string before,long tick)
    {
        var s=Validate(t,json);if(s.Events.LastOrDefault()?.Returned==null && s.Events.Length!=0)throw new ArgumentException("Unknown progress operation is never replayed.");
        string next=JsonSerializer.Serialize(s with {Events=s.Events.Append(new(s.Events.Length,kind,actor,job,inputs,before,tick)).ToArray()});Validate(t,next);return next;
    }
    public static string Returned(TerritoryTransfer t,string json,int sequence,string[] removed,string[] added,string after,long tick)
    {
        var s=Validate(t,json);var last=s.Events.LastOrDefault();if(last==null || last.Sequence!=sequence || last.Returned.HasValue)throw new ArgumentException("Exact original unreturned progress start required.");
        var rows=s.Events.ToArray();rows[rows.Length-1]=last with{Removed=removed,Added=added,After=after,Returned=tick};string next=JsonSerializer.Serialize(s with{Events=rows});Validate(t,next);return next;
    }
    public static string ObserveReturn(TerritoryTransfer t,string json,int sequence,long tick)
    {
        var s=Validate(t,json);var last=s.Events.LastOrDefault();if(last==null || last.Sequence!=sequence || last.BodyReturned.HasValue || tick<last.Started || tick>int.MaxValue)throw new ArgumentException("Exact original native progress return required.");
        var rows=s.Events.ToArray();rows[rows.Length-1]=last with{BodyReturned=tick};string next=JsonSerializer.Serialize(s with{Events=rows});Validate(t,next);return next;
    }
    public static string Approve(TerritoryTransfer t,string progress,string current,long tick)
    {
        var s=Validate(t,progress);if(s.Events.Any(e=>!e.Returned.HasValue)||!CityStockProof.Valid(current)||tick<s.Seal.Tick||s.Events.Any(e=>e.Returned>tick))throw new ArgumentException("Only positive completed progress and a current survey may be approved.");
        return JsonSerializer.Serialize(new WorkProgressApproval(progress,current,tick));
    }
    public static WorkProgressApproval Approval(TerritoryTransfer t,string json)
    {
        if(json==null || json.Length>4500000)throw new ArgumentException("Bounded current progress approval required.");var a=JsonSerializer.Deserialize<WorkProgressApproval>(json);
        if(a==null || Approve(t,a.Progress,a.Current,a.Tick)!=json)throw new ArgumentException("Current progress approval differs.");return a;
    }
}
