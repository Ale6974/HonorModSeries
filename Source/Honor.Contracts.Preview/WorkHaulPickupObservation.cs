using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace Honor.Contracts.Preview;
/// <summary>Observe only the reviewed whole-item pickup path. Never call pickup during recovery.</summary>
[HarmonyPatch]
internal static class WorkHaulPickupObservation
{
    private static bool Prepare()=>NativeTransferBodies.WorkObservationBodies;
    private static MethodBase TargetMethod()=>WorkProgressHooks.PickupMethod();
    private static void Prefix(Pawn_CarryTracker __instance,Thing item,int count,out WorkObservationState __state)
    {
        __state=null;
        try
        {
            if(WorkProgressHooks.Depth!=0||Current.Game==null||Current.ProgramState!=ProgramState.Playing)return;
            var pawn=__instance?.pawn;
            if(__instance?.GetType()!=typeof(Pawn_CarryTracker)||pawn?.carryTracker!=__instance||pawn.jobs?.curDriver?.GetType()!=typeof(JobDriver_HaulToCell)||pawn.jobs.curJob?.def.driverClass!=typeof(JobDriver_HaulToCell)||__instance.innerContainer?.Count!=0||item==null||!item.Spawned||item.Map!=pawn.Map||item.Faction!=null||count<item.stackCount)return;
            if(item is IThingHolder||item.GetType()!=typeof(Thing)&&item.GetType()!=typeof(ThingWithComps))return;
            ResidentActiveWork.Known(typeof(Thing),nameof(Thing.SplitOff));
            ResidentActiveWork.Known(typeof(ThingWithComps),"get_AllComps");
            ResidentActiveWork.Known(typeof(ThingComp),"PostSplitOff");
            foreach(var comp in (item as ThingWithComps)?.AllComps??[])
            {
                if(comp?.GetType()!=typeof(CompQuality)&&comp?.GetType()!=typeof(CompForbiddable))return;
                var callback=AccessTools.Method(comp.GetType(),"PostSplitOff");
                if(callback==null||callback.Module.Assembly!=typeof(Thing).Assembly||Harmony.GetPatchInfo(callback)?.Owners.Any()==true)return;
            }
            foreach(var method in new[]{AccessTools.Method(item.GetType(),nameof(Thing.SplitOff),[typeof(int)]),AccessTools.Method(typeof(Thing),nameof(Thing.DeSpawn))})
                if(method==null||method.Module.Assembly!=typeof(Thing).Assembly||Harmony.GetPatchInfo(method)?.Owners.Any()==true)return;
            foreach(var type in new[]{typeof(ThingOwner),typeof(ThingOwner<Thing>)})
                if(AccessTools.GetDeclaredMethods(type).Where(m=>m.Name is "TryAdd" or "TryAddOrTransfer" or "CanAcceptAnyOf" or "Contains" or "NotifyAdded" or "NotifyAddedAndMergedWith").Any(m=>Harmony.GetPatchInfo(m)?.Owners.Any()==true))return;
            if(!Current.Game.GetComponent<TerritoryAdministrationLedger>().CanObserveStoragePickup(pawn,item))return;
            __state=WorkProgressHooks.Begin(pawn,"haul-pickup",[item]);
            if(__state!=null)__state.Ledger.StageStoragePickup(__state,pawn,item,count);
        }
        catch(Exception e){Log.Warning("[Honor.Contracts.Preview] Whole original pickup observation unavailable: "+e.GetType().Name);}
    }
    private static void Postfix(int __result,WorkObservationState __state)=>WorkProgressHooks.EndPickup(__state,__result);
    private static Exception Finalizer(Exception __exception,WorkObservationState __state){if(__exception!=null)WorkProgressHooks.Failed(__state);return __exception;}
}
