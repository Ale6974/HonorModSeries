using System.Reflection;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
using Verse.AI;
namespace Honor.Contracts.Preview;

internal sealed record WorkObservationState(Game Game,TerritoryAdministrationLedger Ledger,string Transfer,int Sequence,WorkSurvey Before,string[] Inputs)
{
    // Same-call transient witness only. An interrupted native merge never replays after load.
    internal Thing MergeCargo { get; init; }
}
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
    internal static void End(WorkObservationState state,bool? placed=null)
    {
        if(state==null)return;
        try{state.Ledger.EndWorkObservation(state,placed);}
        catch(Exception e){Log.Warning("[Honor.Contracts.Preview] Original native progress start retained without a verified return: "+e.GetType().Name);}
        finally{Depth--;}
    }
    internal static void EndPickup(WorkObservationState state,int taken)
    {
        if(state==null)return;
        try{state.Ledger.EndPickupObservation(state,taken);}
        catch(Exception e){Log.Warning("[Honor.Contracts.Preview] Original pickup start retained without verified output: "+e.GetType().Name);}
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
        var pairs=new[]{(AccessTools.Method(typeof(Frame),nameof(Frame.CompleteConstruction)),typeof(WorkFrameObservation)),
            (AccessTools.Method(typeof(Pawn_JobTracker),nameof(Pawn_JobTracker.EndCurrentJob)),typeof(WorkJobObservation)),(RecipeMethod() as MethodInfo,typeof(WorkRecipeObservation))}.ToList();
        pairs.AddRange(CarryMethods().Select(m=>(m,typeof(WorkHaulPlacementObservation))));
        pairs.Add((PickupMethod() as MethodInfo,typeof(WorkHaulPickupObservation)));
        foreach(var pair in pairs)
        {
            var info=Harmony.GetPatchInfo(pair.Item1);
            if(info==null || info.Transpilers.Count!=0 || info.Prefixes.Count!=1 || info.Postfixes.Count!=1 || info.Finalizers.Count!=1 ||
                info.Prefixes[0].owner!="honor.series.contracts.preview" || info.Postfixes[0].owner!="honor.series.contracts.preview" || info.Finalizers[0].owner!="honor.series.contracts.preview" ||
                info.Prefixes[0].PatchMethod!=AccessTools.Method(pair.Item2,"Prefix") || info.Postfixes[0].PatchMethod!=AccessTools.Method(pair.Item2,"Postfix") || info.Finalizers[0].PatchMethod!=AccessTools.Method(pair.Item2,"Finalizer"))
                throw new ArgumentException("Reviewed sole native progress observers required.");
        }
    }
    internal static MethodInfo[] CarryMethods()
    {
        if(!NativeTransferBodies.WorkObservationBodies)return [];
        var rows=AccessTools.GetDeclaredMethods(typeof(Pawn_CarryTracker)).Where(m=>m.Name==nameof(Pawn_CarryTracker.TryDropCarriedThing)).OrderBy(m=>m.GetParameters().Length).ToArray();
        Type[][] expected=[[typeof(IntVec3),typeof(ThingPlaceMode),typeof(Thing).MakeByRefType(),typeof(Action<Thing,int>)],
            [typeof(IntVec3),typeof(int),typeof(ThingPlaceMode),typeof(Thing).MakeByRefType(),typeof(Action<Thing,int>)]];
        if(rows.Length!=2 || rows.Where((m,i)=>m.ReturnType!=typeof(bool)||!m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(expected[i])).Any())throw new ArgumentException("Reviewed native placement boundaries changed.");
        return rows;
    }
    internal static MethodInfo PickupMethod()
    {
        if(!NativeTransferBodies.WorkObservationBodies)return null;
        var method=AccessTools.Method(typeof(Pawn_CarryTracker),nameof(Pawn_CarryTracker.TryStartCarry),[typeof(Thing),typeof(int),typeof(bool)]);
        if(method?.DeclaringType!=typeof(Pawn_CarryTracker)||method.ReturnType!=typeof(int))throw new ArgumentException("Reviewed native whole pickup boundary changed.");return method;
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
