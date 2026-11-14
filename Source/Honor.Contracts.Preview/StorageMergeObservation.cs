using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    private static NeutralMapThing CarriedMergePhysical(Pawn pawn,Thing thing)
    {
        ResidentActiveWork.Known(typeof(Pawn_CarryTracker),"get_CarriedThing");
        ResidentActiveWork.Known(typeof(ThingWithComps),"get_AllComps");
        foreach(var name in new[]{"get_Destroyed","get_Spawned","get_Faction","get_Stuff","get_HitPoints","get_Rotation"})ResidentActiveWork.Known(typeof(Thing),name);
        if(thing==null||thing.Destroyed||thing.Spawned||thing.Faction!=null||thing is IThingHolder||thing.GetType()!=typeof(Thing)&&thing.GetType()!=typeof(ThingWithComps)||pawn.carryTracker?.CarriedThing!=thing||thing.holdingOwner!=pawn.carryTracker.innerContainer)throw new ArgumentException("Same basic neutral whole carried input required.");
        MapAssetRecords.VerifyHolderRead(thing);int? quality=thing.TryGetQuality(out var q)?(int)q:null;
        return new(thing.GetUniqueLoadID(),thing.def.defName,thing.Stuff?.defName,thing.stackCount,thing.HitPoints,-1,thing.Rotation.AsInt,quality);
    }
    internal WorkObservationState StageStorageMerge(WorkObservationState state,Pawn pawn,IntVec3 cell,ThingPlaceMode mode,int count)
    {
        if(mode!=ThingPlaceMode.Direct||state.Game!=Current.Game||state.MergeCargo!=null)return state;
        var r=references.Single(p=>p.Id==state.Transfer);var t=Ledger.Transfer(r.Id);var work=WorkProgress.Validate(t,r.WorkProgressV1);var ev=work.Events.Last();
        if(work.Version!=3||ev.Sequence!=state.Sequence||ev.BodyReturned.HasValue||ev.Actor!=pawn.GetUniqueLoadID()||pawn.Map!=r.LoadedMap.Map||r.LoadedMap.AssetJournal==null||!cell.InBounds(pawn.Map))return state;
        ResidentActiveWork.Known(typeof(Pawn_CarryTracker),"get_CarriedThing");
        var cargo=pawn.carryTracker.CarriedThing;if(cargo==null||cargo.def.category!=ThingCategory.Item||count!=cargo.stackCount||ev.Inputs.Length!=1||ev.Inputs[0]!=cargo.GetUniqueLoadID())return state;
        var carried=CarriedMergePhysical(pawn,cargo);var map=LoadedMapPreservation.Restore(t.LoadedMap);int index=pawn.Map.cellIndices.CellToIndex(cell);
        var scope=map.Storage?.SingleOrDefault(s=>s.Cells.Contains(index));if(scope==null)return state;
        var saved=r.WorkAssetResolutionV5==null?null:WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5);var entry=saved?.Entries.SingleOrDefault(e=>e.Original==scope.Id);
        if(entry?.StorageSplitsV19!=null)return StageStorageContinuationPlace(state,t,r,pawn,cargo,index,count,entry,work);
        if(entry==null?ev.StorageOriginsV12?.Stores.Any(p=>p.Id==scope.Id)!=true:entry.StorageMergeEventsV17==null)return state;
        var shelf=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==scope.Id) as Building_Storage;
        if(shelf==null||JsonSerializer.Serialize(StorageMapEvidence.CaptureOwned(shelf,pawn.Map))!=JsonSerializer.Serialize(scope))throw new ArgumentException("Same original ungrouped merge shelf scope required.");
        if(entry!=null)VerifyResolutionStorage(r.WorkAssetReferencesV5.Single(p=>p.Original==scope.Id),entry,shelf,r.LoadedMap,work);
        var before=StackPhysical(r,scope.Contents,shelf);
        var targets=before.Where(p=>p.Cell==index&&p.Def==carried.Def&&p.Stuff==carried.Stuff&&p.Quality==carried.Quality).ToArray();
        if(targets.Length!=1||targets[0].HitPoints!=carried.HitPoints||(long)targets[0].Count+carried.Count>cargo.def.stackLimit)return state;
        var actualTarget=r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==targets[0].Id);
        if(actualTarget.GetType()!=cargo.GetType()||actualTarget.def!=cargo.def||actualTarget.Stuff!=cargo.Stuff||(cargo as ThingWithComps)?.AllComps.Count>0||(actualTarget as ThingWithComps)?.AllComps.Count>0)return state;
        foreach(var name in new[]{nameof(Thing.CanStackWith),nameof(Thing.TryAbsorbStack)})
        {
            var method=AccessTools.Method(actualTarget.GetType(),name);
            if(method==null||method.Module.Assembly!=typeof(Thing).Assembly||Harmony.GetPatchInfo(method)?.Owners.Any()==true)throw new ArgumentException("Known unpatched original merge dispatch required.");
        }
        ResidentActiveWork.Known(typeof(ThingUtility),"TryAbsorbStackNumToTake");
        ResidentActiveWork.Known(typeof(Thing),nameof(Thing.CanStackWith));
        ResidentActiveWork.Known(typeof(Thing),nameof(Thing.TryAbsorbStack));
        var start=new WorkStorageMerge(scope.Id,targets[0].Id,before,carried,saved?.BaseCalls??r.LoadedMap.AssetJournal);
        r.WorkProgressV1=WorkProgress.BindStorageMerge(t,r.WorkProgressV1,start);return state with{MergeCargo=cargo};
    }
    private static WorkStorageMerge FinishStorageMerge(TerritoryTransferReference r,WorkProgressEvent ev,WorkObservationState state,bool? placed)
    {
        var start=ev.StorageMergeV17;if(start==null)return null;
        if(state.MergeCargo==null||state.MergeCargo.GetUniqueLoadID()!=start.Cargo.Id||!placed.HasValue)throw new ArgumentException("Same-call actual merge input witness required.");
        if(placed.Value)
        {
            if(!state.MergeCargo.Destroyed||state.MergeCargo.Spawned||state.MergeCargo.holdingOwner!=null||state.MergeCargo.Faction!=null)throw new ArgumentException("Whole original cargo must have actual destruction output after absorption.");
        }
        else
        {
            var pawn=r.Residents.Pawns.Single(p=>p!=null&&p.GetUniqueLoadID()==ev.Actor);
            if(CarriedMergePhysical(pawn,state.MergeCargo)!=start.Cargo)throw new ArgumentException("Failed placement changed its original carried input.");
        }
        var shelf=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==start.Shelf) as Building_Storage;
        if(shelf==null)throw new ArgumentException("Original merge shelf disappeared.");
        return start with{After=StackPhysical(r,start.Before.Select(p=>p.Id).ToArray(),shelf)};
    }
    private (string Document,List<WorkAssetReferenceV5> References) ObserveStorageMerge(TerritoryTransfer t,TerritoryTransferReference r,string progress,string document,List<WorkAssetReferenceV5> refs,int sequence)
    {
        var work=WorkProgress.Validate(t,progress);var ev=work.Events[sequence];var row=ev.StorageMergeV17;if(row==null||ev.PlacementResultV4!=true)return(document,refs);
        string next=WorkAssetResolutions.AppendMerge(t,progress,document,sequence);var entry=WorkAssetResolutions.Validate(t,progress,next).Entries.Single(p=>p.Original==row.Shelf);var references=(refs??[]).ToList();var reference=references.SingleOrDefault(p=>p.Original==row.Shelf);
        if(reference==null){reference=new(){Original=row.Shelf,Actual=r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==row.Shelf),StorageContentsV7=entry.StorageV7.Contents.Select(id=>r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==id)).ToList()};references.Add(reference);}
        if(ResolutionPhysical(reference.Actual,r.LoadedMap.Map)!=entry.Physical||ResolutionAsset(r,reference.Actual,entry.Asset.Repair!=null,entry.Returned?r.Target:r.From)!=entry.Asset)throw new ArgumentException("Original merge shelf physical state and known owner required.");
        VerifyResolutionStorage(reference,entry,reference.Actual,r.LoadedMap,work);return(next,references);
    }
}
