using System.Collections;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
namespace Honor.Contracts.Preview;

internal sealed record WorkSurvey(string Proof,string[] Ids);
/// <summary>Read-only current custody, never an original clinical snapshot or permission to refaction a resident/asset.</summary>
internal static class WorkProgressSurvey
{
    private static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static object Value(object v,int depth=0)
    {
        if(depth>8)throw new ArgumentException("Progress survey depth.");
        if(v==null || v is string or bool or int or long or uint or byte or short or ushort or sbyte or ulong)return v;
        if(v is float f && RuntimeCompat.IsFinite(f))return f;
        if(v is double d && !double.IsNaN(d) && !double.IsInfinity(d))return d;
        var type=v.GetType();if(type.IsEnum)return Convert.ToInt64(v);
        if(v is Def def)return new {Type=type.FullName,def.defName};
        if(v is Thing t)return t.GetUniqueLoadID();if(v is Job j)return j.GetUniqueLoadID();if(v is Bill b)return b.GetUniqueLoadID();if(v is Map m)return m.GetUniqueLoadID();
        if(v is Faction faction)return faction.GetUniqueLoadID();if(v is Area area)return area.GetUniqueLoadID();
        if(v is Name name && name.GetType().Assembly==typeof(Pawn).Assembly)return name.ToStringFull;
        if(v is Precept precept)return precept.GetUniqueLoadID();
        if(v is LocalTargetInfo target)return new {Thing=target.Thing?.GetUniqueLoadID(),Cell=new[]{target.Cell.x,target.Cell.y,target.Cell.z}};
        if(v is GlobalTargetInfo global && global.Equals(GlobalTargetInfo.Invalid))return "invalid";
        if(v is IDictionary dictionary){if(dictionary.Count>10000)throw new ArgumentException("Progress dictionary capacity.");var entries=new List<object>();foreach(DictionaryEntry e in dictionary)entries.Add(new {Key=Value(e.Key,depth+1),Value=Value(e.Value,depth+1)});return entries.OrderBy(e=>JsonSerializer.Serialize(e),StringComparer.Ordinal).ToArray();}
        if(v is IEnumerable list && type.Assembly==typeof(List<>).Assembly){var all=list.Cast<object>().Take(10001).ToArray();if(all.Length>10000)throw new ArgumentException("Progress collection capacity.");return all.Select(x=>Value(x,depth+1)).ToArray();}
        if(type.IsValueType || type.Assembly==typeof(Pawn).Assembly && (v is ThingComp || type.FullName is "Verse.AI.ThingCountTracker+PawnCount" or "Verse.ThingCountClass"))
            return new {Type=type.FullName,Fields=type.GetFields(Fields).Where(field=>!field.IsStatic && field.Name is not ("parent" or "props") && !field.GetCustomAttributes(false).Any(a=>a.GetType().Name=="UnsavedAttribute"))
                .OrderBy(field=>field.Name,StringComparer.Ordinal).Select(field=>new {field.Name,Value=Value(field.GetValue(v),depth+1)}).ToArray()};
        throw new ArgumentException("Unreviewed current progress field: "+type.FullName);
    }
    private static object SavedFields(object obj,Type type,string names) => names.Split(' ').Select(n=>new {Name=n,Value=Value(ResidentActiveWork.Raw(obj,type,n))}).ToArray();
    internal static WorkSurvey Capture(TerritoryTransferReference r)
    {
        if(!NativeTransferBodies.WorkObservationBodies || r.City?.HasMap!=true || r.LoadedMap?.Map!=r.City.Map || r.Residents?.Profile!=16 || r.Residents.NativeMap!=r.City.Map)
            throw new ArgumentException("Same original map and residents required for current progress survey.");
        LoadedMapRecords.RequireProgressBoundary(r.City);
        ResidentMapActors.RequireEmptyReservations(r.City.Map,typeof(Pawn).Module.ModuleVersionId,activeWork:true);
        var map=r.City.Map;var ids=new HashSet<string>(StringComparer.Ordinal);var rows=new List<object>();
        bool partial=r.WorkProgressV1==null || WorkProgress.Validate(Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger.Transfer(r.Id),r.WorkProgressV1).Version>=2;
        var assetOwners=partial&&r.LoadedMap.AssetJournal!=null?MapAssetTransfers.Restore(r.LoadedMap.AssetJournal).Calls.ToDictionary(c=>c.Id,c=>MapAssetTransfers.ExpectedOwner(r.LoadedMap.AssetJournal,c.Id)):null;
        void Thing(Thing t,string owner,int slot,int depth=0)
        {
            if(depth>8 || t==null || t.Destroyed || t.GetType().Assembly!=typeof(Pawn).Assembly || !ids.Add(t.GetUniqueLoadID()) || ids.Count>10000 ||
                t.Faction!=null && t.Faction!=r.Target && !(assetOwners?.TryGetValue(t.GetUniqueLoadID(),out var expected)==true && t.Faction.GetUniqueLoadID()==expected && r.LoadedMap.Things.Contains(t)))throw new ArgumentException("Current objects have missing, duplicate or foreign rights.");
            if(t is Pawn)throw new ArgumentException("Current actors must retain original identity.");
            var type=t.GetType();
            if(type!=typeof(Thing) && type!=typeof(ThingWithComps) && type!=typeof(Building) && type!=typeof(Building_Storage) && type!=typeof(Plant) && type!=typeof(Filth) &&
                type!=typeof(Frame) && type!=typeof(MinifiedThing) && type!=typeof(UnfinishedThing) && !WorktableEvidence.Supported(t) && !ContainerMapEvidence.Supported(t))
                throw new ArgumentException("Current native object needs a separate reviewed state adapter.");
            var comps=(t as ThingWithComps)?.AllComps??[];
            if(comps.Any(c=>c==null || c.parent!=t || c.GetType().Assembly!=typeof(Pawn).Assembly || c is IThingHolder))throw new ArgumentException("Unreviewed current holder component.");
            rows.Add(new {Id=t.GetUniqueLoadID(),Type=t.GetType().FullName,Def=t.def.defName,Stuff=t.Stuff?.defName,t.stackCount,HP=t.HitPoints,Faction=t.Faction?.GetUniqueLoadID(),
                Owner=owner,Slot=slot,Cell=t.Spawned?map.cellIndices.CellToIndex(t.Position):-1,Rotation=t.Rotation.AsInt,Comps=comps.Select(c=>Value(c)).ToArray(),
                Frame=t is Frame frame?new {frame.workDone}:null});
            if(t is Building_WorkTable table){_ = WorktableEvidence.ObserveProgress(table);rows.Add(new {Table=table.GetUniqueLoadID(),Bills=WorktableEvidence.ObserveProgress(table)});}
            if(t is UnfinishedThing uft)
            {
                var ingredients=ResidentActiveWork.Raw(uft,typeof(UnfinishedThing),"ingredients") as List<Thing>??throw new ArgumentException("Original current unfinished ingredients unavailable.");
                rows.Add(new {Unfinished=uft.GetUniqueLoadID(),Fields=SavedFields(uft,typeof(UnfinishedThing),"creatorInt creatorName boundBillInt recipeInt workLeft")});
                for(int i=0;i<ingredients.Count;i++){if(ingredients[i]?.Spawned!=false || ingredients[i].holdingOwner!=null)throw new ArgumentException("Unfinished ingredient owner changed.");Thing(ingredients[i],uft.GetUniqueLoadID(),i,depth+1);}return;
            }
            if(t is not IThingHolder holder)return;
            if(t.GetType()!=typeof(Frame) && t.GetType()!=typeof(MinifiedThing) && !ContainerMapEvidence.Supported(t))throw new ArgumentException("Unreviewed current native holder.");
            foreach(var method in t.GetType().GetInterfaceMap(typeof(IThingHolder)).TargetMethods)if(Harmony.GetPatchInfo(method)?.Owners.Any()==true)throw new ArgumentException("Patched current holder dispatch.");
            var container=holder.GetDirectlyHeldThings();if(container?.GetType()!=typeof(ThingOwner<Thing>) || container.Owner!=holder || container.Count>10000)throw new ArgumentException("Current original holder unavailable.");
            for(int i=0;i<container.Count;i++){if(container[i]?.holdingOwner!=container || container[i].Spawned)throw new ArgumentException("Current holder slot changed.");Thing(container[i],t.GetUniqueLoadID(),i,depth+1);}
        }
        var roster=ResidentMapActors.SpawnedRoster(map);
        var original=r.LoadedMap.MapResidents??[];var visitors=r.LoadedMap.Visitors??[];
        if(roster.Count!=original.Count+visitors.Count || roster.Any(p=>!original.Contains(p)&&!visitors.Contains(p)))throw new ArgumentException("Original mapped people changed.");
        foreach(var p in r.Residents.Pawns)
        {
            int index=r.Residents.Pawns.IndexOf(p);var expectedFaction=ResidentTransferCensus.Call(r.Residents.AuthorityJournal[index]).Returned?r.Target:r.From;
            if(p==null || p.Destroyed || p.Dead || p.Faction!=expectedFaction || p.HostFaction!=null || p.IsPrisoner || p.IsSlave || p.InMentalState || p.GetLord()!=null || (p.royalty?.AllTitlesInEffectForReading.Count??0)!=0 || !ids.Add(p.GetUniqueLoadID()))throw new ArgumentException("Original current civilian identity/rights changed.");
            ResidentMapActors.RequireHolders(p);
            var baseline=ResidentBaselineStore.Evidence(p);
            var job=p.jobs?.curJob;var driver=p.jobs?.curDriver;
            if(job!=null && (job.GetType()!=typeof(Job)||driver?.GetType().Assembly!=typeof(Pawn).Assembly||driver.job!=job||driver.pawn!=p))throw new ArgumentException("Current native job owner changed.");
            rows.Add(new {Pawn=p.GetUniqueLoadID(),baseline,Genes=ResidentGenes.Proof(p),Ideology=ResidentIdeology.Proof(p),
                PendingClinical=r.WorkProgressV1!=null&&WorkProgress.Validate(Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger.Transfer(r.Id),r.WorkProgressV1).Version==3&&!ResidentTransferCensus.Call(r.Residents.AuthorityJournal[index]).Started?ResidentExecution.FreshClinical(p):null,
                Needs=p.needs.AllNeeds.Select(n=>new {Def=n.def.defName,Level=n.CurLevel}).ToArray(),Relations=p.relations.DirectRelations.Select(x=>new {Def=x.def.defName,Pawn=x.otherPawn.GetUniqueLoadID()}).ToArray(),
                Medical=p.playerSettings==null?null:(int?)p.playerSettings.medCare,Areas=ResidentAreaRecords.Entries(p),Surgery=ResidentSurgery.Capture(p).Set().Export(),
                Cell=p.Spawned?map.cellIndices.CellToIndex(p.Position):-1,Job=job==null?null:SavedFields(job,typeof(Job),ResidentGroundRest.JobFields),
                Driver=driver==null?null:new {Type=driver.GetType().FullName,Fields=SavedFields(driver,typeof(JobDriver),ResidentGroundRest.DriverFields)},
                Work=driver?.GetType()==typeof(JobDriver_DoBill)?SavedFields(driver,typeof(JobDriver_DoBill),"workLeft billStartTick ticksSpentDoingRecipeWork"):
                    driver?.GetType()==typeof(JobDriver_HaulToCell)?SavedFields(driver,typeof(JobDriver_HaulToCell),"forbiddenInitially"):null,
                Path=SavedFields(p.pather,typeof(Pawn_PathFollower),"moving nextCell nextCellCostLeft nextCellCostTotal peMode cellsUntilClamor lastEnteredCellTick lastMovedTick debugDisabled curPathJobIsStale"),
                Queue=p.jobs==null?null:ResidentActiveWork.Queue(p).Select(q=>new {Job=SavedFields(q.job,typeof(Job),ResidentGroundRest.JobFields),Tag=Value(q.tag)}).ToArray()});
            foreach(var tracker in new IThingHolder[]{p.inventory,p.equipment,p.apparel,p.carryTracker}.Where(x=>x!=null))
            {var container=tracker.GetDirectlyHeldThings();if(container==null || container.Owner!=tracker)throw new ArgumentException("Current personal holder changed.");for(int i=0;i<container.Count;i++){if(container[i].holdingOwner!=container || container[i].Spawned)throw new ArgumentException("Current gear/cargo owner changed.");Thing(container[i],p.GetUniqueLoadID()+"/"+tracker.GetType().Name,i);}}
        }
        foreach(var p in visitors){rows.Add(ResidentMapVisitors.Capture(p));if(!ids.Add(p.GetUniqueLoadID()))throw new ArgumentException("Duplicate bystander.");}
        foreach(var t in map.listerThings.AllThings.Where(t=>t is not Pawn).OrderBy(t=>t.GetUniqueLoadID(),StringComparer.Ordinal))Thing(t,map.GetUniqueLoadID(),-1);
        var grid=map.AllCells.Select(c=>new {Terrain=map.terrainGrid.TerrainAt(c)?.defName,Under=map.terrainGrid.UnderTerrainAt(c)?.defName,Roof=map.roofGrid.RoofAt(c)?.defName}).ToArray();
        var reservations=new {Regular=ResidentActiveWork.ReservationState(map),Physical=ResidentActiveWork.PhysicalState(map),Enroute=ResidentActiveWork.EnrouteState(map),Destination=ResidentActiveWork.DestinationState(map)};
        string proof=WorkProgress.Hash(new {Map=map.GetUniqueLoadID(),Size=new[]{map.Size.x,map.Size.z},Grid=grid,Reservations=reservations,Rows=rows,AssetCalls=partial?r.LoadedMap.AssetJournal:null,
            Clinical=r.WorkProgressV1!=null&&WorkProgress.Validate(Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger.Transfer(r.Id),r.WorkProgressV1).Version==3?TerritoryAdministrationLedger.ClinicalState(r.Residents):null});return new(proof,ids.OrderBy(id=>id,StringComparer.Ordinal).ToArray());
    }
}
