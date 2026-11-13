using System.Reflection;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
using Verse.AI;
namespace Honor.Contracts.Preview;

internal sealed record WorkObservationState(Game Game,TerritoryAdministrationLedger Ledger,string Transfer,int Sequence,WorkSurvey Before,string[] Inputs);
internal static class WorkProgressHooks
{
    [ThreadStatic] internal static int Depth;
    internal static WorkObservationState Begin(Pawn pawn,string kind,Thing[] inputs)
    {
        if(Depth!=0 || !NativeTransferBodies.WorkObservationBodies || Current.Game==null || Current.ProgramState!=ProgramState.Playing)return null;
        try
        {
            var result=Current.Game.GetComponent<TerritoryAdministrationLedger>().BeginWorkObservation(pawn,kind,inputs);
            if(result!=null)Depth++;return result;
        }
        catch(Exception e){Log.Warning("[Honor.Contracts.Preview] Native work observation not started: "+e.GetType().Name);return null;}
    }
    internal static void End(WorkObservationState state)
    {
        if(state==null)return;
        try{state.Ledger.EndWorkObservation(state);}
        catch(Exception e){Log.Warning("[Honor.Contracts.Preview] Original native progress start retained without a verified return: "+e.GetType().Name);}
        finally{Depth--;}
    }
    internal static void Failed(WorkObservationState state){if(state!=null)Depth--;}
    internal static MethodBase RecipeMethod()
    {
        if(!NativeTransferBodies.WorkObservationBodies)return null;
        var method=typeof(Toils_Recipe).Module.ResolveMethod(0x06010955);
        if(method?.DeclaringType?.FullName!="Verse.AI.Toils_Recipe+<>c__DisplayClass3_0" || method.Name!="<FinishRecipeAndStartStoringProduct>b__1")throw new ArgumentException("Reviewed recipe completion boundary changed.");
        return method;
    }
    internal static bool IsExactRecipeObserver(MethodBase method,Patches info)
    {
        if(method!=RecipeMethod() || info==null)return false;
        var prefix=AccessTools.Method(typeof(WorkRecipeObservation),"Prefix");var postfix=AccessTools.Method(typeof(WorkRecipeObservation),"Postfix");var finalizer=AccessTools.Method(typeof(WorkRecipeObservation),"Finalizer");
        return info.Transpilers.Count==0 && info.Prefixes.All(p=>p.owner=="honor.series.contracts.preview"&&p.PatchMethod==prefix) &&
            info.Postfixes.All(p=>p.owner=="honor.series.contracts.preview"&&p.PatchMethod==postfix) && info.Finalizers.All(p=>p.owner=="honor.series.contracts.preview"&&p.PatchMethod==finalizer);
    }
    internal static void RequireObservers()
    {
        foreach(var pair in new[]{(AccessTools.Method(typeof(Frame),nameof(Frame.CompleteConstruction)),typeof(WorkFrameObservation)),
            (AccessTools.Method(typeof(Pawn_JobTracker),nameof(Pawn_JobTracker.EndCurrentJob)),typeof(WorkJobObservation)),(RecipeMethod() as MethodInfo,typeof(WorkRecipeObservation))})
        {
            var info=Harmony.GetPatchInfo(pair.Item1);
            if(info==null || info.Transpilers.Count!=0 || info.Prefixes.Count!=1 || info.Postfixes.Count!=1 || info.Finalizers.Count!=1 ||
                info.Prefixes[0].owner!="honor.series.contracts.preview" || info.Postfixes[0].owner!="honor.series.contracts.preview" || info.Finalizers[0].owner!="honor.series.contracts.preview" ||
                info.Prefixes[0].PatchMethod!=AccessTools.Method(pair.Item2,"Prefix") || info.Postfixes[0].PatchMethod!=AccessTools.Method(pair.Item2,"Postfix") || info.Finalizers[0].PatchMethod!=AccessTools.Method(pair.Item2,"Finalizer"))
                throw new ArgumentException("Reviewed sole native progress observers required.");
        }
    }
}
[HarmonyPatch(typeof(Frame),nameof(Frame.CompleteConstruction))]
internal static class WorkFrameObservation
{
    private static bool Prepare()=>NativeTransferBodies.WorkObservationBodies;
    private static void Prefix(Frame __instance,Pawn worker,out WorkObservationState __state)
    {
        __state=null;
        try{if(__instance?.GetType()==typeof(Frame) && __instance.resourceContainer?.GetType()==typeof(ThingOwner<Thing>) && __instance.resourceContainer.Owner==__instance)
            __state=WorkProgressHooks.Begin(worker,"frame",new[]{(Thing)__instance}.Concat(__instance.resourceContainer.ToArray()).ToArray());}
        catch(Exception e){Log.Warning("[Honor.Contracts.Preview] Frame progress observation unavailable: "+e.GetType().Name);}
    }
    private static void Postfix(WorkObservationState __state)=>WorkProgressHooks.End(__state);
    private static Exception Finalizer(Exception __exception,WorkObservationState __state){if(__exception!=null)WorkProgressHooks.Failed(__state);return __exception;}
}
[HarmonyPatch(typeof(Pawn_JobTracker),nameof(Pawn_JobTracker.EndCurrentJob))]
internal static class WorkJobObservation
{
    private static bool Prepare()=>NativeTransferBodies.WorkObservationBodies;
    private static void Prefix(Pawn_JobTracker __instance,JobCondition condition,out WorkObservationState __state)
    {
        __state=null;if(condition!=JobCondition.Succeeded)return;
        try{var owner=AccessTools.Field(typeof(Pawn_JobTracker),"pawn").GetValue(__instance) as Pawn;__state=WorkProgressHooks.Begin(owner,"job-end",[]);}
        catch(Exception e){Log.Warning("[Honor.Contracts.Preview] Job completion observation unavailable: "+e.GetType().Name);}
    }
    private static void Postfix(WorkObservationState __state)=>WorkProgressHooks.End(__state);
    private static Exception Finalizer(Exception __exception,WorkObservationState __state){if(__exception!=null)WorkProgressHooks.Failed(__state);return __exception;}
}
[HarmonyPatch]
internal static class WorkRecipeObservation
{
    private static bool Prepare()=>NativeTransferBodies.WorkObservationBodies;
    private static MethodBase TargetMethod()=>WorkProgressHooks.RecipeMethod();
    private static void Prefix(object __instance,out WorkObservationState __state)
    {
        __state=null;
        try
        {
            var toil=AccessTools.Field(__instance.GetType(),"toil")?.GetValue(__instance) as Toil;var pawn=toil?.actor;var job=pawn?.jobs?.curJob;
            if(job?.def.driverClass!=typeof(JobDriver_DoBill))return;
            // CalculateIngredients is a MUTATOR (splits/consumes UFT). Never invoke it to observe.
            var inputs=(job.placedThings??[]).Select(p=>p.thing).ToList();
            if(job.targetB.Thing is UnfinishedThing uft){inputs.Add(uft);inputs.AddRange(uft.ingredients??[]);}
            __state=WorkProgressHooks.Begin(pawn,"recipe",inputs.Distinct().ToArray());
        }
        catch(Exception e){Log.Warning("[Honor.Contracts.Preview] Recipe observation unavailable before native operation: "+e.GetType().Name);}
    }
    private static void Postfix(WorkObservationState __state)=>WorkProgressHooks.End(__state);
    private static Exception Finalizer(Exception __exception,WorkObservationState __state){if(__exception!=null)WorkProgressHooks.Failed(__state);return __exception;}
}
