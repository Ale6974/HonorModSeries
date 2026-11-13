using Honor.Contracts.Rules;
using System.Text.Json;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    internal static WorkClinicalState ClinicalState(ResidentExecutionRecord r)
    {
        if(r?.AuthorityJournal==null || r.CareJournal==null || r.AreaRecords==null || r.AreaRecords.Any(a=>a?.Journal==null))
            throw new ArgumentException("Missing original clinical call/output/restoration journals.");
        return new(r.AuthorityJournal.ToArray(),r.CareJournal.ToArray(),r.AreaRecords.Select(a=>a.Journal).ToArray());
    }
    internal void RequireApprovedResident(TerritoryTransferReference r,string pawn,string proof)
    {
        var consent=custodyAgreements.Record(r.CustodyAgreementV1);var a=WorkProgress.Approval(Ledger.Transfer(r.Id),consent.Witness.WorkProgressV1);
        if(a.PendingV3?.SingleOrDefault(p=>p.Pawn==pawn)?.Proof!=proof)throw new ArgumentException("Exact separately approved current resident clinical proof required.");
    }
    internal void SealClinicalReturn(ResidentExecutionRecord record)
    {
        var matches=references.Where(r=>ReferenceEquals(r.Residents,record)).ToArray();if(matches.Length!=1)return;
        var r=matches[0];var t=Ledger.Transfer(r.Id);
        if(r.WorkProgressV1!=null || !t.StandaloneActiveWorkV1 || t.StockFormat!=25 || r.LoadedMap==null || r.CityCallStarted || r.CustodyAgreementV1!=null)return;
        try
        {
            WorkProgressHooks.RequireObservers();ResidentExecution.ValidateCensus(record,r.ResidentCensusProof,t.Territory,t.FromOwner,t.TargetOwner);
            var survey=WorkProgressSurvey.Capture(r);
            var roster=r.City.previouslyGeneratedInhabitants.ToList();
            r.WorkProgressV1=WorkProgress.PrepareClinical(t,record.Census,r.ResidentCensusProof,ClinicalState(record),r.LoadedMap.AssetJournal,Find.TickManager.TicksGame,survey.Proof,survey.Ids,roster.Select(p=>p.GetUniqueLoadID()).ToArray());
            r.WorkAssetVerifiedV2=[];
            r.WorkResidentStartsV3=[];
            r.WorkNativeRosterV3=roster;
        }
        catch(ArgumentException){Log.Warning("[Honor.Contracts.Preview] Partial clinical observation unavailable; original strict execution retained.");}
    }
}
internal static partial class ResidentExecution
{
    internal static ResidentMoveActual FreshClinical(Pawn p)
    {
        if(p?.jobs?.curJob==null && (p?.jobs==null || p.jobs.curDriver!=null || p.jobs.jobQueue.Count!=0 || p.pather==null || p.pather.Moving ||
            ResidentActiveWork.Raw(p.pather,typeof(Verse.AI.Pawn_PathFollower),"curPath")!=null || ResidentActiveWork.Raw(p.pather,typeof(Verse.AI.Pawn_PathFollower),"curPathRequest")!=null))
            throw new ArgumentException("Idle current resident has unresolved jobs or path state.");
        var area=ResidentAreaRecords.Capture(p);var surgery=ResidentSurgery.Capture(p);
        return ReadInternal(p,16,surgery,null,false,area,captureAreas:true);
    }
    internal static bool ResumeClinicalOriginal(TerritoryTransferReference r)
    {
        var record=r.Residents;var ledger=Current.Game.GetComponent<TerritoryAdministrationLedger>();var game=Current.Game;
        ledger.RequireCurrentCustody(r);
        ValidateCensus(record,r.ResidentCensusProof,"territory/"+r.City.GetUniqueLoadID(),r.From.GetUniqueLoadID(),r.Target.GetUniqueLoadID());
        var set=record.Set();
        for(int i=0;i<record.Pawns.Count;i++)
        {
            var p=record.Pawns[i];var call=ResidentTransferCensus.Call(record.AuthorityJournal[i]);var care=ResidentCareJournal.Restore(record.CareJournal[i]);var area=record.AreaRecords[i];
            if(record.Profile!=16 || area.Pawn!=p || record.Surgery[i].Pawn!=p || record.Surgery[i].Detached.Count!=0 || record.Prisoners.Count!=0 || p.Map!=record.NativeMap || p.Map!=r.City.Map)
                throw new ArgumentException("Same original civilian clinical references required.");
            ResidentAreaRecords.References(area,p.GetUniqueLoadID());
            if(call.Returned)
            {
                if(p.Faction!=r.Target || !care.State.Returned)throw new ArgumentException("Original normal return and observed output required.");
                if(!care.State.Consumed)
                {
                    int? current=p.playerSettings==null?null:(int)p.playerSettings.medCare;
                    if(care.MayRestore(r.Target.GetUniqueLoadID(),current))
                    {care.Consume(r.Target.GetUniqueLoadID(),current);record.CareJournal[i]=care.Export();p.playerSettings.medCare=(MedicalCareCategory)care.State.OriginalCare.Value;}
                    else {care.SealUnchanged(r.Target.GetUniqueLoadID(),current);record.CareJournal[i]=care.Export();}
                    return true;
                }
                if(!ResidentAreas.Restore(area.Journal).Consumed){ResidentAreaRecords.Restore(area);return true;}
                continue;
            }
            if(call.Started || care.State.Started || p.Faction!=r.From)throw new ArgumentException("Never replay uncertain resident authority.");
            var actual=FreshClinical(p);var original=set.Residents[i];
            if(actual.MedicalCare!=original.MedicalCare)throw new ArgumentException("Original medical policy changed; no reset permission.");
            ResidentAreas.CheckCurrent(area.Journal,ResidentAreaRecords.Entries(p));
            var areaStart=ResidentAreas.Begin(area.Journal,ResidentAreaRecords.Entries(p));care.Begin(r.From.GetUniqueLoadID(),actual.MedicalCare);
            var start=ResidentTransferCensus.Begin(record.AuthorityJournal[i]);ledger.RequireCurrentCustody(r);
            ledger.RequireApprovedResident(r,p.GetUniqueLoadID(),actual.Preserved);
            if(r.WorkResidentStartsV3.Any(raw=>JsonSerializer.Deserialize<WorkResidentStart>(raw).Pawn==p.GetUniqueLoadID()))throw new ArgumentException("Original current resident start already exists.");
            r.WorkResidentStartsV3.Add(JsonSerializer.Serialize(new WorkResidentStart(p.GetUniqueLoadID(),actual.Preserved,r.CustodyAgreementV1,Find.TickManager.TicksGame)));
            record.CareJournal[i]=care.Export();record.AuthorityJournal[i]=start;area.Journal=areaStart;
            void Returned()
            {
                record.AuthorityJournal[i]=ResidentTransferCensus.Returned(record.AuthorityJournal[i]);
                if(Current.Game!=game || Current.ProgramState!=ProgramState.Playing || p.Faction!=r.Target)throw new ArgumentException("Changed original resident return context.");
                care.RecordReturn(r.Target.GetUniqueLoadID(),p.playerSettings==null?null:(int)p.playerSettings.medCare);record.CareJournal[i]=care.Export();ResidentAreaRecords.Returned(area);
            }
            if(p.CurJob!=null)NativeWorkFactionCall.Execute(p,r.Target,Returned);else {p.SetFaction(r.Target);Returned();}
            return true;
        }
        ValidateCensus(record,r.ResidentCensusProof,"territory/"+r.City.GetUniqueLoadID(),r.From.GetUniqueLoadID(),r.Target.GetUniqueLoadID(),true);return false;
    }
}
