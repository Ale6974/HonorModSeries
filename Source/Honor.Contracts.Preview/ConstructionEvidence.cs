using System.Reflection;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
internal static class ConstructionEvidence
{
    internal static List<Thing> Resources(Frame frame)
    {
        if (!NativeTransferBodies.WorkObservationBodies || frame?.GetType() != typeof(Frame) || !frame.Spawned || frame.Destroyed || frame.resourceContainer?.GetType() != typeof(ThingOwner<Thing>) ||
            frame.resourceContainer.Owner != frame || frame.resourceContainer.Count > 10000 || frame.storageGroup != null || frame.storageSettings != null ||
            !RuntimeCompat.IsFinite(frame.workDone) || frame.workDone < 0 || frame.def?.entityDefToBuild == null)
            throw new ArgumentException("Original basic construction frame and resources required.");
        ResidentActiveWork.Known(typeof(Frame), "ExposeData"); ResidentActiveWork.Known(typeof(Frame), "GetDirectlyHeldThings"); ResidentActiveWork.Known(typeof(Frame), "GetChildHolders");
        foreach (var method in typeof(Frame).GetInterfaceMap(typeof(IThingHolder)).TargetMethods)
            if (method.Module.Assembly != typeof(Pawn).Assembly || Harmony.GetPatchInfo(method)?.Owners.Any() == true) throw new ArgumentException("Unknown construction holder dispatch.");
        var resources = frame.resourceContainer.ToList();
        if (resources.Distinct().Count() != resources.Count || resources.Any(t => t == null || t.Spawned || t.holdingOwner != frame.resourceContainer || t.Map != null))
            throw new ArgumentException("Original ordered frame resources changed.");
        return resources;
    }
    internal static NativeWorkFrame Capture(Frame frame)
    {
        var resources = Resources(frame);
        foreach (var comp in frame.AllComps.Where(c=>c?.GetType()==typeof(CompForbiddable))) ResidentActiveWork.Known(typeof(CompForbiddable),"PostExposeData");
        var def = frame.def.entityDefToBuild;
        if (def is ThingDef thingDef ? DefDatabase<ThingDef>.GetNamedSilentFail(def.defName) != thingDef : def is not TerrainDef terrain || DefDatabase<TerrainDef>.GetNamedSilentFail(def.defName) != terrain)
            throw new ArgumentException("Original construction definition is unregistered.");
        object color = frame.glowerColorOverride.HasValue ? typeof(ColorInt).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => new { f.Name, Value = f.GetValue(frame.glowerColorOverride.Value) }).ToArray() : null;
        string proof = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Definition = def.defName, Type = def.GetType().FullName, frame.workDone, Color = color,
            Forbidden=frame.AllComps.Where(c=>c.GetType()==typeof(CompForbiddable)).Select(c=>(bool)ResidentActiveWork.Raw(c,typeof(CompForbiddable),"forbiddenInt")).ToArray(),
            Resources = resources.Select(t => ResidentActiveWork.SimpleCargo(t, frame.resourceContainer, null)).ToArray() }))));
        return new(frame.GetUniqueLoadID(), proof, resources.Select(t => t.GetUniqueLoadID()).ToArray());
    }
}
