using System.Text.Json;
using Honor.Contracts.Rules;
internal static class ActiveWorkTests
{
    internal static int Run()
    {
        int n = 0; void Check(bool b) { n++; if (!b) throw new Exception("Original active-work custody regression."); }
        void Reject(Action f) { bool rejected = false; try { f(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        string hash = new('A',64);
        var haul = new ActiveWorkRow("a", ["Job_1","Job_2"], "{\"Type\":\"haul\",\"remaining\":71,\"queue\":[\"Job_2\"],\"dest\":7}");
        var craft = new ActiveWorkRow("b", ["Job_3"], "{\"Type\":\"production\",\"workLeft\":123,\"bill\":\"Bill_Craft_4\",\"physical\":[\"ingredient\"]}");
        _ = ActiveWork.Validate([haul,craft],["a","b"]);
        foreach (var wrong in new[] { haul with { Pawn="other" }, haul with { Jobs=[] }, haul with { Jobs=["Job_1","Job_1"] }, haul with { Jobs=null }, haul with { Saved="[]" } })
            Reject(() => ActiveWork.Validate([wrong],["a"]));
        Reject(() => ActiveWork.Validate([haul,craft with { Jobs=["Job_1"] }],["a","b"]));
        Reject(() => ActiveWork.Validate([craft,haul],["a","b"]));
        Reject(() => ActiveWork.Validate([], ["a"]));
        Check(ActiveWork.Proof(haul)!=ActiveWork.Proof(haul with { Jobs=["Job_2","Job_1"] }));
        Check(ActiveWork.Proof(haul)!=ActiveWork.Proof(haul with { Saved=haul.Saved.Replace("71","70") }));
        Check(ActiveWork.Bind(hash,haul)!=ActiveWork.Bind(hash,null));
        var positions = new ResidentMapBinding[] { new("a","map",1,0),new("b","map",2,0) };
        var set = ResidentMoveSet.Capture(positions.Select((p,i)=>new ResidentMoveEvidence(p.Id,ActiveWork.Bind(ResidentMapped.Bind(hash,p), i==0?haul:craft),3)));
        string roots = LoadedMapPreservation.Capture("town","map",20,20,hash,CityStockProof.Capture(true,false,-1,[]),0,
            [new("ingredient","Steel",null,20,100,4,0,null),new("table","TableStonecutter",null,1,100,3,0,null)]);
        Check(!roots.Contains("ActiveWorkV1")&&!roots.Contains("WorktablesV1") && JsonSerializer.Serialize(LoadedMapPreservation.Restore(roots))==roots);
        string mapped=LoadedMapPreservation.CaptureResidents(roots,set.Residents.Select(r=>new NativeMapResident(r.Id,r.Preserved)));
        string owned=LoadedMapPreservation.CaptureCivicAssets(mapped,[new("table","source",hash)],false,false);
        var table=new NativeWorktable("table",hash,["Bill_Craft_4"]);
        string scope=LoadedMapPreservation.CaptureActiveWork(owned,[haul,craft],[table]);
        var frameRoots=LoadedMapPreservation.Capture("town","map",20,20,hash,CityStockProof.Capture(true,false,-1,[]),0,[new("frame","WallFrame",null,1,100,5,0,null)]);
        var frameMapped=LoadedMapPreservation.CaptureResidents(frameRoots,set.Residents.Select(r=>new NativeMapResident(r.Id,r.Preserved)));
        var frameOwned=LoadedMapPreservation.CaptureCivicAssets(frameMapped,[new("frame","source",hash)],false,false);
        var frame=new NativeWorkFrame("frame",hash,["resource-1","resource-2"]);
        var frameScope=LoadedMapPreservation.CaptureActiveWork(frameOwned,[haul,craft],frames:[frame]);
        Check(LoadedMapPreservation.Format(frameScope)==25 && LoadedMapPreservation.Restore(frameScope).WorkFramesV1[0].Configuration==hash);
        Reject(()=>LoadedMapPreservation.CaptureActiveWork(frameOwned,[haul,craft],frames:[frame with{Resources=["resource-1","resource-1"]}]));
        Reject(()=>LoadedMapPreservation.CaptureActiveWork(frameOwned,[haul,craft],frames:[frame with{Resources=["frame"]}]));
        Reject(()=>LoadedMapPreservation.CaptureActiveWork(frameOwned,[haul,craft],frames:[frame with{Id="missing"}]));
        Check(frameScope!=LoadedMapPreservation.CaptureActiveWork(frameOwned,[haul,craft],frames:[frame with{Resources=["resource-2","resource-1"]}]));
        Check(LoadedMapPreservation.Format(scope)==25 && LoadedMapPreservation.Format(owned)==15);
        LoadedMapPreservation.Pair(scope,"town",LoadedMapPreservation.Restore(scope).Proof,25);
        Reject(()=>LoadedMapPreservation.Pair(scope,"town",LoadedMapPreservation.Restore(scope).Proof,24));
        Reject(()=>LoadedMapPreservation.CaptureActiveWork(scope,[haul,craft]));
        Reject(()=>LoadedMapPreservation.CaptureGroundRest(scope,[new("a","Job_1","{}")],false));
        Reject(()=>LoadedMapPreservation.CaptureRoyalMapped(scope));
        Reject(()=>LoadedMapPreservation.CaptureActiveWork(owned,[haul,craft],[table with { Id="missing" }]));
        Reject(()=>LoadedMapPreservation.CaptureActiveWork(owned,[haul,craft],[table with { Bills=["same","same"] }]));
        Reject(()=>LoadedMapPreservation.CaptureActiveWork(owned,[haul,craft],[]));
        Check(scope!=LoadedMapPreservation.CaptureActiveWork(owned,[haul,craft],[table with { Configuration=new string('B',64) }]));
        ResidentMapped.Pair(ResidentMapped.Prepare(positions),set,scope);
        string census=ResidentTransferCensus.Prepare("town","source","target",16,set); var c=ResidentTransferCensus.Restore(census);
        var calls=set.Residents.Select(r=>JsonSerializer.Serialize(new ResidentAuthorityState(r.Id))).ToArray();
        var care=set.Residents.Select(r=>ResidentCareJournal.Prepare(r.Id,"source","target",3).Export()).ToArray();
        var book=new CharterBook(); book.Register(new("player","player",true),0);book.Register(new("town","source",false),0);
        TerritoryObservation[] obs=[new("player","player",true,true,true),new("town","source",true,false,true)];
        book.Offer("charter",new("player","town",90,PoliticalWorld.Day,0,TributeTransport.SubjectDelivery,0),0,PoliticalWorld.Day,obs);
        book.Sign("charter",true,1,obs);book.Sign("charter",false,2,obs); string before=book.Export();
        var actual=new TerritoryTransferObservation("town","source",2,HasMap:true,Visited:true,HasInhabitants:true,StockProof:LoadedMapPreservation.Restore(scope).Proof,StockFormat:25,ResidentProof:set.Proof){LoadedMap=scope};
        var physical=new TerritoryTransferLedger();var t=physical.PrepareLoadedJournaled("work",book,"charter",actual,"target","target",null,TerritoryTransferPurpose.ActiveCharter,3);
        var assets=MapAssetTransfers.Prepare(scope,"source","target");
        var areas=set.Residents.Select(r=>ResidentAreas.Prepare(r.Id,[])).ToArray();
        Reject(()=>ActiveWorkAbort.Prepare(t,census,c.Proof,calls,care,areas,assets,3));
        physical.MarkStandaloneActiveWork(t.Id); t=physical.Transfer(t.Id);
        string abort=ActiveWorkAbort.Prepare(t,census,c.Proof,calls,care,areas,assets,4);
        var stopped=TerritoryTransferLedger.Restore(physical.Export()); stopped.CancelUnstartedActiveWork(t.Id,abort,4);stopped=TerritoryTransferLedger.Restore(stopped.Export());
        Check(stopped.Transfer(t.Id).Phase==TerritoryTransferPhase.Cancelled && stopped.Transfer(t.Id).UnstartedActiveWorkAbortV1==abort && stopped.Transfer(t.Id).LoadedMap==scope);
        Reject(()=>stopped.CancelUnstartedActiveWork(t.Id,abort,5));
        Reject(()=>ActiveWorkAbort.Prepare(t,census,c.Proof,calls,care,areas,MapAssetTransfers.Begin(assets,"table","source"),4));
        Reject(()=>physical.MarkStandaloneGivenWork(t.Id));
        Reject(()=>GroundRestAbort.Validate(t,abort));
        for(int i=0;i<calls.Length;i++)
        {
            calls[i]=ResidentTransferCensus.Begin(calls[i]);var medical=ResidentCareJournal.Restore(care[i]);medical.Begin("source",3);care[i]=medical.Export();
            Reject(()=>ResidentTransferCensus.RequireRecoverable(calls,care));string uncertain=calls[i];Reject(()=>ResidentTransferCensus.Begin(uncertain));
            Reject(()=>ActiveWorkAbort.Prepare(t,census,c.Proof,calls,care,areas,assets,4));
            calls[i]=ResidentTransferCensus.Returned(calls[i]);
            // Retain native return even when later care/output is still uncertain; never replay the authority call.
            Reject(()=>ResidentTransferCensus.RequireRecoverable(calls,care));
            medical.RecordReturn("target",1);medical.Consume("target",1);care[i]=medical.Export();
            ResidentTransferCensus.Check(census,c.Proof,"town","source","target",16,set,calls,care,true);
            if(i==0) Reject(()=>ResidentTransferCensus.Check(census,c.Proof,"town","source","target",16,set,calls,care,true,true));
            physical=TerritoryTransferLedger.Restore(physical.Export());
            Check(physical.Transfer(t.Id).LoadedMap==scope && JsonSerializer.Serialize(LoadedMapPreservation.Restore(scope).ActiveWorkV1)==JsonSerializer.Serialize(new[]{haul,craft}));
        }
        ResidentTransferCensus.Check(census,c.Proof,"town","source","target",16,set,calls,care,true,true);
        Reject(()=>ResidentTransferCensus.Check(census,c.Proof,"town","source","target",15,set,calls,care,true,true));
        assets=MapAssetTransfers.Returned(MapAssetTransfers.Begin(assets,"table","source"),"table","target");MapAssetTransfers.Pair(assets,scope,"source","target",true);
        physical.Confirm(t.Id,actual with{Owner="target"},4);
        physical=TerritoryTransferLedger.Restore(physical.Export());
        Check(physical.Transfer(t.Id).Phase==TerritoryTransferPhase.Applied && physical.Transfer(t.Id).LoadedMap==scope && book.Export()==before);
        return n;
    }
}
