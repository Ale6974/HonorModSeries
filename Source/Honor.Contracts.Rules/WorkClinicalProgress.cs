using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record WorkClinicalState(string[] Calls,string[] Care,string[] Areas);
public sealed record WorkResidentStart(string Pawn,string CurrentProof,string Agreement,long Tick);
public static partial class WorkProgress
{
    public static string PrepareClinical(TerritoryTransfer t,string census,string proof,WorkClinicalState state,string assets,long tick,string survey,string[] objects,string[] roster=null)
    {
        roster??=ResidentMoveSet.Restore(ResidentTransferCensus.Restore(census).Residents).Residents.Select(p=>p.Id).ToArray();
        string json=JsonSerializer.Serialize(new WorkProgressSave(3,new(Hash(Original(t)),census,proof,state.Calls,state.Care,state.Areas,assets,tick,survey,objects),[]){RosterV3=roster});
        Validate(t,json);return json;
    }
    public static void ClinicalAdvance(TerritoryTransfer t,string progress,WorkClinicalState current,bool complete=false)
    {
        if(current==null)throw new ArgumentException("Current original clinical journals required.");
        var s=Validate(t,progress);var c=ResidentTransferCensus.Restore(s.Seal.Census);var set=ResidentMoveSet.Restore(c.Residents);
        ResidentTransferCensus.Check(s.Seal.Census,s.Seal.CensusProof,t.Territory,t.FromOwner,t.TargetOwner,16,set,current.Calls,current.Care,true,complete);
        ResidentTransferCensus.RequireRecoverable(current.Calls,current.Care);
        if(current.Areas?.Length!=s.Seal.Areas.Length)throw new ArgumentException("Original area coverage changed.");
        for(int i=0;i<current.Calls.Length;i++)
        {
            var old=ResidentTransferCensus.Call(s.Seal.Calls[i]);var now=ResidentTransferCensus.Call(current.Calls[i]);
            var med=ResidentCareJournal.Restore(s.Seal.Care[i]).State;var next=ResidentCareJournal.Restore(current.Care[i]).State;
            var area=ResidentAreas.Restore(s.Seal.Areas[i]);var a=ResidentAreas.Restore(current.Areas[i]);
            if(old.Started&&!now.Started || old.Returned&&!now.Returned || med.Returned&&(!next.Returned||next.ReturnedCare!=med.ReturnedCare) || med.Consumed&&!next.Consumed ||
                area.Id!=a.Id || !area.Original.SequenceEqual(a.Original) || area.Started&&!a.Started || area.Returned&&(!a.Returned||area.Cleared!=a.Cleared) ||
                area.Applying&&!a.Applying || area.Consumed&&!a.Consumed || a.Started!=now.Started || a.Started&&!a.Returned || complete&&!a.Consumed)
                throw new ArgumentException("Original clinical output was changed or regressed.");
        }
    }
}
