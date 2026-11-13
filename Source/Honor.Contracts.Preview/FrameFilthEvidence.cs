using Honor.Contracts.Rules;
using HarmonyLib;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
/// <summary>Observe the exact neutral native filth that an original terrain frame will remove. No destruction or cleanup calls.</summary>
internal static class FrameFilthEvidence
{
    private static void RequireReads()
    {
        ResidentActiveWork.Known(typeof(Thing),"GetUniqueLoadID");
        foreach(string property in new[]{nameof(Thing.Spawned),nameof(Thing.Destroyed),nameof(Thing.Map),nameof(Thing.Faction),nameof(Thing.Position),nameof(Thing.HitPoints),nameof(Thing.Rotation),nameof(Thing.Stuff)})
        {
            var getter=AccessTools.PropertyGetter(typeof(Thing),property);
            if(!NativeTransferBodies.WorkObservationBodies||getter?.DeclaringType?.Assembly!=typeof(Thing).Assembly||Harmony.GetPatchInfo(getter)?.Owners.Any()==true)throw new ArgumentException("Original unpatched filth read boundary required.");
        }
    }
    private static List<Thing> HomeRows(Map map)
    {
        if(map.listerFilthInHomeArea?.GetType()!=typeof(ListerFilthInHomeArea)||ResidentActiveWork.Raw(map.listerFilthInHomeArea,typeof(ListerFilthInHomeArea),"map")!=map||
            ResidentActiveWork.Raw(map.listerFilthInHomeArea,typeof(ListerFilthInHomeArea),"filthInHomeArea") is not List<Thing> rows)throw new ArgumentException("Original native filth registry owner required.");
        ResidentActiveWork.Known(typeof(ListerFilthInHomeArea),"Notify_FilthDespawned");return rows;
    }
    private static WorkFrameFilth Capture(Filth filth,Map map)
    {
        RequireReads();
        if(!NativeTransferBodies.WorkObservationBodies||filth?.GetType()!=typeof(Filth)||!filth.Spawned||filth.Destroyed||filth.Map!=map||filth.Faction!=null||filth.def?.GetType()!=typeof(ThingDef)||filth.def.thingClass!=typeof(Filth)||
            DefDatabase<ThingDef>.GetNamedSilentFail(filth.def.defName)!=filth.def||filth.questTags?.Count>0||filth.thickness is <1 or >5||filth.sources?.Count>16||filth.sources?.Any(s=>s==null||s.Length>2048)==true||filth.drawInstances?.Count>512)
            throw new ArgumentException("Original bounded neutral native filth required.");
        if(filth.holdingOwner!=null&&!ReferenceEquals(filth.holdingOwner,map.GetDirectlyHeldThings()))throw new ArgumentException("Original map filth owner required.");
        ResidentActiveWork.Known(typeof(Filth),"ExposeData");ResidentActiveWork.Known(typeof(Filth),"DeSpawn");ResidentActiveWork.Known(typeof(Thing),"Destroy");ResidentActiveWork.Known(typeof(FilthMaker),"RemoveAllFilth");
        ResidentActiveWork.Known(typeof(Thing),"GetUniqueLoadID");
        ResidentActiveWork.Known(typeof(Thing),"DeSpawn");int homeCount=HomeRows(map).Count(row=>ReferenceEquals(row,filth));
        if(homeCount>1)throw new ArgumentException("Original native filth registry membership is duplicated.");
        if(filth.drawInstances?.Any(d=>!RuntimeCompat.IsFinite(d.rotation)||!RuntimeCompat.IsFinite(d.drawPos.x)||!RuntimeCompat.IsFinite(d.drawPos.y)||!RuntimeCompat.IsFinite(d.drawPos.z))==true)throw new ArgumentException("Original finite filth draw data required.");
        var stuff=filth.Stuff;
        if(stuff!=null&&(stuff.GetType()!=typeof(ThingDef)||DefDatabase<ThingDef>.GetNamedSilentFail(stuff.defName)!=stuff))throw new ArgumentException("Original registered filth stuff required.");
        string proof=WorkProgress.Hash(new{filth.thickness,Sources=filth.sources?.ToArray(),GrowTick=(int)ResidentActiveWork.Raw(filth,typeof(Filth),"growTick"),
            Disappear=(int)ResidentActiveWork.Raw(filth,typeof(Filth),"disappearAfterTicks"),Draw=filth.drawInstances?.Select(d=>new{d.rotation,X=d.drawPos.x,Y=d.drawPos.y,Z=d.drawPos.z}).ToArray(),
            filth.stackCount,HP=filth.HitPoints,Rotation=filth.Rotation.AsInt,HomeCount=homeCount,Stuff=stuff?.defName,filth.overrideGraphicIndex,filth.debugRotLocked,
            Position=new[]{filth.Position.x,filth.Position.y,filth.Position.z},Quest=filth.questTags?.ToArray(),TickDelta=(int)ResidentActiveWork.Raw(filth,typeof(Thing),"tickDelta"),
            MapState=(sbyte)ResidentActiveWork.Raw(filth,typeof(Thing),"mapIndexOrState"),SpawnedTick=(int)ResidentActiveWork.Raw(filth,typeof(Thing),"spawnedTick"),
            DespawnedTick=(int)ResidentActiveWork.Raw(filth,typeof(Thing),"despawnedTick"),Revealed=(bool)ResidentActiveWork.Raw(filth,typeof(Thing),"beenRevealed")});
        return new(filth.GetUniqueLoadID(),filth.def.defName,map.cellIndices.CellToIndex(filth.Position),proof);
    }
    internal static WorkFrameFilth[] Original(Frame frame)
    {
        if(frame.def.entityDefToBuild is not TerrainDef)return null;
        RequireReads();
        var rows=frame.Map.listerThings.AllThings.OfType<Filth>().Where(f=>f.Position==frame.Position).Select(f=>Capture(f,frame.Map)).OrderBy(f=>f.Id,StringComparer.Ordinal).ToArray();
        if(rows.Length==0)return null;WorkFrameFilthRules.Check(rows);return rows;
    }
    internal static WorkFrameFilth[] Current(TerritoryTransfer t,TerritoryTransferReference r,string kind,Thing[] inputs)
    {
        if(kind!="frame"||inputs.Length==0)return null;
        var original=LoadedMapPreservation.Restore(t.LoadedMap).WorkFramesV1?.SingleOrDefault(frame=>frame.Id==inputs[0].GetUniqueLoadID())?.FilthV10;
        if(original==null)return null;
        RequireReads();
        if(inputs[0] is not Frame frame||!r.LoadedMap.Things.Any(p=>ReferenceEquals(p,frame))||frame.Map!=r.LoadedMap.Map)throw new ArgumentException("Same original terrain frame reference required.");
        var filth=frame.Map.listerThings.AllThings.OfType<Filth>().Where(f=>f.Position==frame.Position).ToArray();
        if(filth.Any(f=>!r.LoadedMap.Things.Any(p=>ReferenceEquals(p,f))))throw new ArgumentException("Current filth must retain the original actual references.");
        var rows=filth.Select(f=>Capture(f,frame.Map)).OrderBy(row=>row.Id,StringComparer.Ordinal).ToArray();WorkFrameFilthRules.Pair(original,rows);return rows;
    }
    internal static void After(TerritoryTransferReference r,WorkProgressEvent observed)
    {
        if(observed.FilthV10==null)return;
        RequireReads();
        var rows=HomeRows(r.LoadedMap.Map);
        foreach(var input in observed.FilthV10)
        {
            var original=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==input.Id);
            if(original?.GetType()!=typeof(Filth)||!original.Destroyed||original.Spawned||rows.Any(row=>ReferenceEquals(row,original)))throw new ArgumentException("Native body returned but original filth removal/registry output remains unverified.");
        }
    }
}
