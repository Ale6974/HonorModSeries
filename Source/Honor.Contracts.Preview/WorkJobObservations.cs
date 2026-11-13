using System.Text.Json;
using Honor.Contracts.Rules;
using Verse;
using Verse.AI;
namespace Honor.Contracts.Preview;

/// <summary>Own descriptor, native references only. Native job tracker remains the sole deep-save owner.</summary>
public sealed class WorkJobObservationRecord : IExposable
{
    public Pawn Pawn;
    public Job Job;
    public JobDef Definition;
    public int StartTick;
    public string Descriptor,Grant;
    public void ExposeData()
    {
        if(Scribe.mode==LoadSaveMode.Saving && Job!=null)
        {
            try{var g=Grant==null?null:JsonSerializer.Deserialize<WorkEventGrantV4>(Grant);
                if(g!=null&&(Pawn?.jobs?.curJob!=Job || Job.GetUniqueLoadID()!=g.Job || Job.startTick!=StartTick || Job.def!=Definition))Job=null;}
            catch(JsonException){ } // Preserve an unreadable raw descriptor; validation blocks its use.
        }
        Scribe_References.Look(ref Pawn,"originalWorkActor");Scribe_References.Look(ref Job,"approvedNativeJob");Scribe_Defs.Look(ref Definition,"approvedJobDefinition");
        Scribe_Values.Look(ref StartTick,"approvedJobStartTick",-1);Scribe_Values.Look(ref Descriptor,"approvedWorkDescriptor");Scribe_Values.Look(ref Grant,"currentWorkObservationGrantV4");
    }
    internal bool Matches(Pawn pawn,Job job)=>ReferenceEquals(Pawn,pawn)&&ReferenceEquals(Job,job)&&job.def==Definition&&job.startTick==StartTick&&
        job.GetUniqueLoadID()==JsonSerializer.Deserialize<WorkEventGrantV4>(Grant).Job;
}
public sealed partial class TerritoryAdministrationLedger
{
    private static ActiveWorkRow[] CurrentObservedJobs(TerritoryTransferReference r)
    {
        var rows=new List<ActiveWorkRow>();
        foreach(var p in r.Residents.Pawns)
        {
            if(p?.jobs?.curJob==null)continue;
            try{var row=ResidentActiveWork.Capture(p);if(row!=null)rows.Add(row);}
            catch(ArgumentException){ } // No observation permission is invented for an unsupported current task.
        }
        return rows.OrderBy(p=>p.Pawn,StringComparer.Ordinal).ToArray();
    }
    private List<WorkJobObservationRecord> StageWorkObservations(TerritoryTransferReference r,CustodyAgreementReceipt consent)
    {
        if(consent.Witness.WorkProgressV1==null)return r.WorkJobObservationsV4;
        var t=Ledger.Transfer(r.Id);var a=WorkProgress.Approval(t,consent.Witness.WorkProgressV1);
        if(a.JobsV4==null)return r.WorkJobObservationsV4;
        RequireWorkJobIdentity();
        var staged=(r.WorkJobObservationsV4??[]).ToList();
        foreach(var row in a.JobsV4)
        {
            var pawn=r.Residents.Pawns.Single(p=>p.GetUniqueLoadID()==row.Pawn);var job=pawn.jobs.curJob;
            if(job==null || job.loadID<0 || job.startTick<0 || job.startTick>Find.TickManager.TicksGame || job.GetUniqueLoadID()!=row.Jobs[0] || JsonSerializer.Serialize(ResidentActiveWork.Capture(pawn))!=JsonSerializer.Serialize(row))throw new ArgumentException("Current approved native work changed before registration.");
            if(staged.Any(g=>g.Matches(pawn,job)))continue;
            var grant=WorkJobObservations.Grant(t,consent,row,Find.TickManager.TicksGame);
            staged.Add(new(){Pawn=pawn,Job=job,Definition=job.def,StartTick=job.startTick,Descriptor=JsonSerializer.Serialize(row),Grant=JsonSerializer.Serialize(grant)});
            if(staged.Count>256 || staged.Sum(g=>(long)g.Descriptor.Length+g.Grant.Length)>32000000)throw new ArgumentException("Current observation archive capacity; no old grant is replaced.");
        }
        return staged;
    }
    private void ValidateWorkObservations(TerritoryTransfer t,TerritoryTransferReference r)
    {
        var progress=WorkProgress.Validate(t,r.WorkProgressV1);
        if(r.WorkJobObservationsV4==null){if(progress.Events.Any(e=>e.GrantV4!=null))throw new ArgumentException("Native event lost its current observation archive.");return;}
        if(progress.Version!=3 || r.WorkJobObservationsV4.Count>256 || r.WorkJobObservationsV4.Any(g=>g==null||g.Grant==null||g.Grant.Length>2048||g.Descriptor==null||g.Descriptor.Length>300000) ||
            r.WorkJobObservationsV4.Select(g=>g.Grant).Distinct().Count()!=r.WorkJobObservationsV4.Count)throw new ArgumentException("Bounded explicit work observation archive required.");
        if(r.WorkJobObservationsV4.Sum(g=>(long)g.Descriptor.Length+g.Grant.Length)>32000000)throw new ArgumentException("Current observation archive capacity.");
        foreach(var record in r.WorkJobObservationsV4)
        {
            var g=JsonSerializer.Deserialize<WorkEventGrantV4>(record.Grant);var row=JsonSerializer.Deserialize<ActiveWorkRow>(record.Descriptor);
            if(row==null || g==null || JsonSerializer.Serialize(row)!=record.Descriptor || g.Tick>Find.TickManager.TicksGame || record.StartTick<0 || record.StartTick>g.Tick)throw new ArgumentException("Current native work descriptor/clock changed.");
            WorkJobObservations.Pair(t,g,custodyAgreements.Record(g.Agreement),row);
            if(t.Phase==TerritoryTransferPhase.Prepared && record.Pawn!=r.Residents.Pawns.SingleOrDefault(p=>p.GetUniqueLoadID()==g.Pawn))throw new ArgumentException("Current observation actor is not the original native Pawn.");
        }
        foreach(var e in progress.Events.Where(e=>e.GrantV4!=null))
            if(!r.WorkJobObservationsV4.Any(record=>JsonSerializer.Deserialize<WorkEventGrantV4>(record.Grant)==e.GrantV4))throw new ArgumentException("Native event lost its exact approved observation record.");
    }
    private static void ReleaseWorkObservationReferences(TerritoryTransferReference r)
    {foreach(var record in r.WorkJobObservationsV4??[]){record.Pawn=null;record.Job=null;}ReleaseAssetResolutionReferences(r);}
    private static void RequireWorkJobIdentity()
    {
        if(!NativeTransferBodies.WorkObservationBodies)throw new ArgumentException("Reviewed native work identity required.");
        ResidentActiveWork.Known(typeof(Job),"Clear");ResidentActiveWork.Known(typeof(Job),"ExposeData");ResidentActiveWork.Known(typeof(Job),"GetUniqueLoadID");
        ResidentActiveWork.Known(typeof(JobMaker),"MakeJob");ResidentActiveWork.Known(typeof(JobMaker),"ReturnToPool");
    }
}
