using System.Text.Json;
using Honor.Contracts.Rules;
internal static class WorkProgressTests
{
    internal static int Run()
    {
        int n=0;void Check(bool b){n++;if(!b)throw new Exception("Native work progress provenance regression.");}
        void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected);}
        string hash=new('A',64),next=new('B',64);
        var book=new CharterBook();book.Register(new("p","player",true),0);book.Register(new("town","source",false),0);book.Register(new("child","third",false),0);
        TerritoryObservation[] obs=[new("p","player",true,true,true),new("town","source",true,false,true),new("child","third",true,false,true)];
        book.Offer("root",new("p","town",100,PoliticalWorld.Day,0,TributeTransport.SubjectDelivery,0),0,PoliticalWorld.Day,obs);book.Sign("root",true,1,obs);book.Sign("root",false,2,obs);
        book.OfferNpc("lower",new("town","child",30,PoliticalWorld.Day,0,TributeTransport.SubjectDelivery,0),new("source-ruler","third-ruler"),3,PoliticalWorld.Day,obs);
        book.SignNpc("lower","town","source-ruler",4,obs);book.SignNpc("lower","child","third-ruler",5,obs);
        var work=new ActiveWorkRow("worker",["Job_1"],"{\"Driver\":\"frame\",\"progress\":10}");
        var binding=new ResidentMapBinding("worker","map",1,0);var residents=ResidentMoveSet.Capture([new("worker",ActiveWork.Bind(ResidentMapped.Bind(hash,binding),work),3)]);
        string map=LoadedMapPreservation.Capture("town","map",20,20,hash,CityStockProof.Capture(true,false,-1,[]),0,[new("frame","WallFrame","Wood",1,100,2,0,null)]);
        map=LoadedMapPreservation.CaptureResidents(map,[new("worker",residents.Residents[0].Preserved)]);
        map=LoadedMapPreservation.CaptureCivicAssets(map,[new("frame","source",hash)],false,false);
        map=LoadedMapPreservation.CaptureActiveWork(map,[work],frames:[new("frame",hash,["wood"])]);
        var physical=new TerritoryTransferLedger();var actual=new TerritoryTransferObservation("town","source",2,HasMap:true,Visited:true,HasInhabitants:true,StockProof:LoadedMapPreservation.Restore(map).Proof,StockFormat:25,ResidentProof:residents.Proof){LoadedMap=map};
        var t=physical.PrepareLoadedJournaled("move",book,"root",actual,"target","target",null,TerritoryTransferPurpose.ActiveCharter,10);physical.MarkStandaloneActiveWork(t.Id);t=physical.Transfer(t.Id);
        string original=JsonSerializer.Serialize(t),beforeBook=book.Export();
        string census=ResidentTransferCensus.Prepare("town","source","target",16,residents);var c=ResidentTransferCensus.Restore(census);
        string[] calls=[JsonSerializer.Serialize(new ResidentAuthorityState("worker"))];var medical=ResidentCareJournal.Prepare("worker","source","target",3);string[] care=[medical.Export()];
        string[] areas=[ResidentAreas.Prepare("worker",[])];string assets=MapAssetTransfers.Prepare(map,"source","target");
        Reject(()=>WorkProgress.Prepare(t,census,c.Proof,calls,care,areas,assets,11,hash,["frame","wood","worker"]));
        calls[0]=ResidentTransferCensus.Returned(ResidentTransferCensus.Begin(calls[0]));medical.Begin("source",3);medical.RecordReturn("target",1);medical.Consume("target",1);care[0]=medical.Export();
        areas[0]=ResidentAreas.Complete(ResidentAreas.Returned(ResidentAreas.Begin(areas[0],[]),[]),[]);
        Reject(()=>WorkProgress.Prepare(t,census,c.Proof,calls,care,areas,assets,11,hash,["frame","wood","worker"]));
        assets=MapAssetTransfers.Returned(MapAssetTransfers.Begin(assets,"frame","source"),"frame","target");
        string progress=WorkProgress.Prepare(t,census,c.Proof,calls,care,areas,assets,11,hash,["frame","wood","worker"]);
        string pendingAssets=MapAssetTransfers.Prepare(map,"source","target");
        string early=WorkProgress.PrepareBeforeAssets(t,census,c.Proof,calls,care,areas,pendingAssets,11,hash,["frame","wood","worker"]);
        Check(WorkProgress.Validate(t,early).Version==2);
        WorkProgress.AssetAdvance(t,early,pendingAssets);WorkProgress.AssetAdvance(t,early,assets,true);
        Reject(()=>WorkProgress.AssetAdvance(t,early,pendingAssets,true));
        Reject(()=>WorkProgress.AssetAdvance(t,early,MapAssetTransfers.Begin(pendingAssets,"frame","source")));
        Reject(()=>WorkProgress.AssetAdvance(t,progress,pendingAssets));
        string later=WorkProgress.PrepareBeforeAssets(t,census,c.Proof,calls,care,areas,assets,12,hash,["frame","wood","worker"]);
        Reject(()=>WorkProgress.AssetAdvance(t,later,pendingAssets));
        Check(WorkProgress.Approval(t,WorkProgress.Approve(t,early,next,20)).Progress==early);
        Check(WorkProgress.Validate(t,progress).Seal.Objects.SequenceEqual(["frame","wood","worker"]));
        Reject(()=>WorkProgress.Validate(t with{TargetOwner="other"},progress));
        Reject(()=>WorkProgress.Validate(t with{StandaloneActiveWorkV1=false},progress));
        Reject(()=>WorkProgress.Prepare(t,census,c.Proof,calls,care,areas,assets,9,hash,["frame"]));
        string started=WorkProgress.Begin(t,progress,"frame","worker","Job_1",["frame","wood"],hash,12);
        Reject(()=>WorkProgress.Approve(t,started,next,13));
        Reject(()=>WorkProgress.Begin(t,started,"frame","worker","Job_1",["frame"],hash,13));
        Reject(()=>WorkProgress.Returned(t,started,0,["frame","wood"],["wall"],next,13));
        string returned=WorkProgress.ObserveReturn(t,started,0,12);
        Check(WorkProgress.Validate(t,returned).Events[0].BodyReturned==12 && WorkProgress.Validate(t,returned).Events[0].Returned==null);
        Reject(()=>WorkProgress.Approve(t,returned,next,13));
        Reject(()=>WorkProgress.ObserveReturn(t,returned,0,13));
        Reject(()=>WorkProgress.Returned(t,returned,0,["frame"],["wall"],next,13));
        Reject(()=>WorkProgress.Returned(t,returned,0,["frame","foreign"],["wall"],next,13));
        progress=WorkProgress.Returned(t,returned,0,["frame","wood"],["wall"],next,13);
        Check(WorkProgress.Validate(t,progress).Events[0].Removed.SequenceEqual(["frame","wood"]));
        var normal=WorkProgress.Validate(t,progress);Check(normal.Seal.Census==census && normal.Seal.Assets==assets && normal.Seal.Survey==hash);
        string approval=WorkProgress.Approve(t,progress,next,20);
        Check(approval!=WorkProgress.Approve(t,progress,hash,20));
        Reject(()=>WorkProgress.Approve(t,progress,next,12));
        Reject(()=>WorkProgress.Approval(t,approval.Replace("Job_1","Job_2")));
        string ended=WorkProgress.Begin(t,progress,"job-end","worker","Job_1",[],next,14);ended=WorkProgress.ObserveReturn(t,ended,1,14);
        Reject(()=>WorkProgress.Returned(t,ended,1,["wall"],[],hash,15));
        ended=WorkProgress.Returned(t,ended,1,[],[],hash,15);Check(WorkProgress.Validate(t,ended).Events.Length==2);
        NpcRealmDecisionEvidence Realm(string id)=>new(id,id+"-ruler",NpcDecisionRegime.Ordinary,true,true,true,true,true,true);
        var payments=new TributeLedger();string paid=payments.Export();
        var witness=new CustodyAgreement("current","move",book.Export(),paid,physical.Export(),hash,[new("child","third",false),new("p","player",true),new("town","source",false)],
            [obs[2],obs[0],obs[1]],[Realm("source"),Realm("target"),Realm("third")],20){WorkProgressV1=approval};
        Check(CustodyAgreements.Validate(witness)==t && CustodyAgreements.Scope(witness)!=CustodyAgreements.Scope(witness with{WorkProgressV1=null}));
        Reject(()=>CustodyAgreements.Validate(witness with{Parties=witness.Parties.Skip(1).ToArray()}));
        Reject(()=>CustodyAgreements.Validate(witness with{Realms=[Realm("source"),Realm("target"),Realm("third") with{Willing=false}]}));
        var agreements=new CustodyAgreements();var consent=agreements.Add(witness);agreements=CustodyAgreements.Restore(agreements.Export());
        var partialWitness=witness with{WorkProgressV1=WorkProgress.Approve(t,early,next,20)};
        var partialConsent=new CustodyAgreementReceipt(partialWitness,WorkProgress.Hash(partialWitness));
        var partialPhysical=TerritoryTransferLedger.Restore(physical.Export());
        Reject(()=>partialPhysical.ConfirmWorkProgress(t.Id,partialWitness.WorkProgressV1,partialConsent,"target",true,true,21));
        Reject(()=>partialPhysical.ConfirmWorkProgress(t.Id,partialWitness.WorkProgressV1,partialConsent,"target",true,true,21,pendingAssets));
        partialPhysical.ConfirmWorkProgress(t.Id,partialWitness.WorkProgressV1,partialConsent,"target",true,true,21,assets);
        Check(TerritoryTransferLedger.Restore(partialPhysical.Export()).Transfer(t.Id).Phase==TerritoryTransferPhase.Applied);
        agreements.RequireCurrent("current",t,book.Export(),payments.Export(),hash);
        Reject(()=>physical.ConfirmWorkProgress(t.Id,approval,consent,"target",true,false,21));
        Reject(()=>physical.ConfirmWorkProgress(t.Id,approval,consent,"source",true,true,21));
        Reject(()=>physical.ConfirmWorkProgress(t.Id,approval,consent with{Proof=next},"target",true,true,21));
        Reject(()=>physical.ConfirmWorkProgress(t.Id,approval,consent with{Witness=witness with{WorkProgressV1=null}},"target",true,true,21));
        physical=TerritoryTransferLedger.Restore(physical.Export());physical.ConfirmWorkProgress(t.Id,approval,consent,"target",true,true,21);physical=TerritoryTransferLedger.Restore(physical.Export());
        var applied=physical.Transfer(t.Id);Check(applied.Phase==TerritoryTransferPhase.Applied && applied.LoadedMap==t.LoadedMap && applied.ResidentProof==t.ResidentProof && applied.WorkCompletionV1!=null);
        Check(JsonSerializer.Serialize(WorkProgress.Original(applied))==original && book.Export()==beforeBook && payments.Export()==paid);
        agreements.ValidateHistory(book,physical,payments);
        Reject(()=>physical.ConfirmWorkProgress(t.Id,approval,consent,"target",true,true,22));
        return n;
    }
}
