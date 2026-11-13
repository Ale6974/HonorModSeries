namespace Honor.Contracts.Rules;
public sealed record WorkEventGrantV4(string Pawn,string Job,string Agreement,string Receipt,string Work,long Tick);
/// <summary>Permission to observe a native job, never to start, replay or reconstruct it.</summary>
public static class WorkJobObservations
{
    private static bool Key(string s)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=120&&!s.Any(char.IsControl);
    public static void Check(WorkEventGrantV4 g,string pawn,string job,long earliest,long now)
    {
        if(g==null||g.Pawn!=pawn||g.Job!=job||!Key(pawn)||!Key(job)||!Key(g.Agreement)||!CityStockProof.Valid(g.Receipt)||!CityStockProof.Valid(g.Work)||g.Tick<earliest||g.Tick>now||now>int.MaxValue)
            throw new ArgumentException("Exact approved current work observation required.");
    }
    public static WorkEventGrantV4 Grant(TerritoryTransfer t,CustodyAgreementReceipt consent,ActiveWorkRow work,long tick)
    {
        if(consent==null||consent.Proof!=WorkProgress.Hash(consent.Witness)||CustodyAgreements.Validate(consent.Witness)!=WorkProgress.Original(t))throw new ArgumentException("Original transfer/current work consent mismatch.");
        var a=WorkProgress.Approval(t,consent.Witness.WorkProgressV1);string proof=ActiveWork.Proof(work);
        if(a.JobsV4?.SingleOrDefault(r=>r.Pawn==work.Pawn) is not ActiveWorkRow approved||ActiveWork.Proof(approved)!=proof||tick<a.Tick)throw new ArgumentException("Current work was not explicitly approved.");
        var g=new WorkEventGrantV4(work.Pawn,work.Jobs[0],consent.Witness.Id,consent.Proof,proof,tick);Check(g,work.Pawn,work.Jobs[0],WorkProgress.Validate(t,a.Progress).Seal.Tick,tick);return g;
    }
    public static void Pair(TerritoryTransfer t,WorkEventGrantV4 grant,CustodyAgreementReceipt consent,ActiveWorkRow row)
    {
        if(grant==null)throw new ArgumentException("Current work observation descriptor required.");
        if(Grant(t,consent,row,grant.Tick)!=grant)throw new ArgumentException("Work observation lost its exact approved descriptor.");
    }
}
