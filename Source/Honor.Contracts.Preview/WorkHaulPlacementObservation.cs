using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace Honor.Contracts.Preview;
/// <summary>Observe the native operation, including merge/split and callbacks. Never invoke placement from recovery.</summary>
[HarmonyPatch]
internal static class WorkHaulPlacementObservation
{
    private static bool Prepare()=>NativeTransferBodies.WorkObservationBodies;
    private static IEnumerable<MethodBase> TargetMethods()=>WorkProgressHooks.CarryMethods();
    private static void Prefix(Pawn_CarryTracker __instance,Action<Thing,int> placedAction,out WorkObservationState __state)
    {
        __state=null;
        try
        {
            var p=__instance?.pawn;
            if(__instance?.GetType()!=typeof(Pawn_CarryTracker)||p?.carryTracker!=__instance||p.jobs?.curDriver?.GetType()!=typeof(JobDriver_HaulToCell)||p.jobs.curJob?.def?.driverClass!=typeof(JobDriver_HaulToCell))return;
            foreach(var callback in placedAction?.GetInvocationList()??[])
                if(callback.Method.Module.Assembly!=typeof(Pawn).Assembly || callback.Target!=null&&callback.Target.GetType().Assembly!=typeof(Pawn).Assembly || Harmony.GetPatchInfo(callback.Method)?.Owners.Any()==true)return;
            var cargo=__instance.CarriedThing;if(cargo==null)return;
            foreach(var type in new[]{typeof(GenDrop),typeof(GenPlace)})
                if(AccessTools.GetDeclaredMethods(type).Any(m=>Harmony.GetPatchInfo(m)?.Owners.Any()==true))return;
            if(AccessTools.GetDeclaredMethods(typeof(ThingOwner)).Where(m=>m.Name=="TryDrop").Any(m=>Harmony.GetPatchInfo(m)?.Owners.Any()==true))return;
            foreach(var method in new[]{AccessTools.Method(cargo.GetType(),nameof(Thing.SplitOff),[typeof(int)]),AccessTools.Method(cargo.GetType(),nameof(Thing.TryAbsorbStack),[typeof(Thing),typeof(bool)])})
                if(method==null || method.Module.Assembly!=typeof(Pawn).Assembly || Harmony.GetPatchInfo(method)?.Owners.Any()==true)return;
            __state=WorkProgressHooks.Begin(p,"haul-place",[cargo]);
        }
        catch(Exception e){Log.Warning("[Honor.Contracts.Preview] Native haul placement observation unavailable: "+e.GetType().Name);}
    }
    private static void Postfix(bool __result,WorkObservationState __state)=>WorkProgressHooks.End(__state,__result);
    private static Exception Finalizer(Exception __exception,WorkObservationState __state){if(__exception!=null)WorkProgressHooks.Failed(__state);return __exception;}
}
