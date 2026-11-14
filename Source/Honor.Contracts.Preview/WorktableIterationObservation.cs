using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    private WorktableIteration CaptureWorktableIteration(TerritoryTransfer t,TerritoryTransferReference r,Pawn pawn,string kind)
    {
        if(kind!="recipe"||WorkProgress.Validate(t,r.WorkProgressV1).Version!=3||pawn.jobs.curJob.bill?.GetType()!=typeof(Bill_Production)||pawn.jobs.curJob.targetA.Thing is not Building_WorkTable table||r.LoadedMap.AssetJournal==null)return null;
        var map=LoadedMapPreservation.Restore(t.LoadedMap);var original=map.WorktablesV1?.SingleOrDefault(p=>p.Id==table.GetUniqueLoadID());
        if(original==null||!r.LoadedMap.Things.Any(p=>ReferenceEquals(p,table)))return null;
        var bill=(Bill_Production)pawn.jobs.curJob.bill;
        if(ResidentActiveWork.Raw(bill,typeof(Bill_Production),"repeatMode")!=BillRepeatModeDefOf.RepeatCount||bill.repeatCount<=0)return null;
        var s=r.WorkAssetResolutionV5==null?null:WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5);var entry=s?.Entries.SingleOrDefault(e=>e.Original==original.Id);
        var before=WorktableEvidence.Capture(table);var work=WorkProgress.Validate(t,r.WorkProgressV1);
        if(JsonSerializer.Serialize(before)!=JsonSerializer.Serialize(entry==null?original:WorktableIterations.Current(entry,work)))throw new ArgumentException("Original or last positively observed table configuration required before iteration.");
        var template=entry??new WorkAssetResolution(original.Id,original.Id,null,map.OwnedAssets.Single(p=>p.Id==original.Id),map.Things.Single(p=>p.Id==original.Id)){WorktableV6=original};
        if(!WorktableEvidence.Bills(table).SequenceEqual(OriginalWorktableBills(r.LoadedMap,template)))throw new ArgumentException("Same original native Bill references and order required.");
        return new(original.Id,bill.GetUniqueLoadID(),before,WorktableEvidence.IterationStable(table,bill),bill.repeatCount,s?.BaseCalls??r.LoadedMap.AssetJournal);
    }
    private WorktableIteration FinishWorktableIteration(TerritoryTransferReference r,WorkProgressEvent ev)
    {
        var start=ev.WorktableIterationV15;if(start==null)return null;
        var table=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==start.Table) as Building_WorkTable;
        if(table==null)throw new ArgumentException("Same original table missing after iteration.");
        var bill=WorktableEvidence.Bills(table).SingleOrDefault(b=>b.GetUniqueLoadID()==start.Bill) as Bill_Production;
        if(bill==null||!r.LoadedMap.WorkBills.Any(b=>ReferenceEquals(b,bill)))throw new ArgumentException("Same original Bill missing after iteration.");
        return start with{After=WorktableEvidence.Capture(table),AfterStable=WorktableEvidence.IterationStable(table,bill),AfterCount=bill.repeatCount};
    }
    private (string Document,List<WorkAssetReferenceV5> References) ObserveWorktableIteration(TerritoryTransfer t,TerritoryTransferReference r,string progress,string document,List<WorkAssetReferenceV5> refs,int sequence)
    {
        var work=WorkProgress.Validate(t,progress);var row=work.Events[sequence].WorktableIterationV15;if(row==null)return(document,refs);
        string next=WorkAssetResolutions.AppendIteration(t,progress,document,row.Calls,row.Table,sequence);
        var entry=WorkAssetResolutions.Validate(t,progress,next).Entries.Single(e=>e.Original==row.Table);var references=(refs??[]).ToList();var reference=references.SingleOrDefault(p=>p.Original==row.Table);
        if(reference==null){reference=new(){Original=row.Table,Actual=r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==row.Table),WorktableBillsV6=OriginalWorktableBills(r.LoadedMap,entry)};references.Add(reference);}
        if(ResolutionPhysical(reference.Actual,r.LoadedMap.Map)!=entry.Physical||ResolutionAsset(r,reference.Actual,entry.Asset.Repair!=null,entry.Returned?r.Target:r.From)!=entry.Asset)throw new ArgumentException("Same original table physical state and known owner required after iteration.");
        VerifyResolutionWorktable(reference,entry,reference.Actual,r.LoadedMap,work);return(next,references);
    }
}
