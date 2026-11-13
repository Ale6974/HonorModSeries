using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed class WorkAssetReferenceV5 : IExposable
{
    public string Original;
    public Thing Actual;
    // The native BillStack remains the only deep-save owner, including after load.
    public List<Bill> WorktableBillsV6;
    public List<Thing> StorageContentsV7;
    public void ExposeData(){Scribe_Values.Look(ref Original,"originalAssetId");Scribe_References.Look(ref Actual,"separateActualProperty");Scribe_Collections.Look(ref WorktableBillsV6,"actualWorktableBillsV6",LookMode.Reference);Scribe_Collections.Look(ref StorageContentsV7,"actualStorageContentsV7",LookMode.Reference);}
}
internal sealed record WorkSuccessorCandidate(WorkAssetReferenceV5 Reference,MapAsset Asset,NeutralMapThing Physical,NativeWorktable Worktable,NeutralMapStorage Storage,WorkTerrainAcceptance Terrain=null);
public sealed partial class TerritoryAdministrationLedger
{
    private static NeutralMapThing ResolutionPhysical(Thing thing,Map map)
    {
        if(thing==null || thing.Destroyed || !thing.Spawned || thing.Map!=map || thing is IThingHolder || thing.GetType()!=typeof(Building)&&thing.GetType()!=typeof(Thing)&&thing.GetType()!=typeof(ThingWithComps)&&thing.GetType()!=typeof(Building_Storage)&&!WorktableEvidence.Supported(thing))
            throw new ArgumentException("Same basic original or new building property required.");
        MapAssetRecords.VerifyHolderRead(thing);int? quality=thing.TryGetQuality(out var q)?(int)q:null;
        return new(thing.GetUniqueLoadID(),thing.def.defName,thing.Stuff?.defName,thing.stackCount,thing.HitPoints,map.cellIndices.CellToIndex(thing.Position),thing.Rotation.AsInt,quality);
    }
    private static MapAsset ResolutionAsset(TerritoryTransferReference r,Thing thing,bool repair,Faction expected)
    {
        if(thing?.Faction!=expected)throw new ArgumentException("Actual property owner lacks its independent call fact.");
        var peers=(r.LoadedMap.MapResidents??[]).Concat(r.LoadedMap.Visitors??[]).ToArray();
        return MapAssetRecords.Capture(thing,r.City,null,repair,ownedStorage:thing.GetType()==typeof(Building_Storage),reviewedPawns:peers,independentOwner:expected) with{Owner=r.From.GetUniqueLoadID()};
    }
    private WorkSuccessorCandidate ObserveFrameSuccessor(TerritoryTransfer t,TerritoryTransferReference r,WorkObservationState state,WorkProgressEvent ev)
    {
        if(ev.Kind!="frame" || WorkProgress.Validate(t,r.WorkProgressV1).Version!=3 || r.LoadedMap.AssetJournal==null)return null;
        string id=ev.Inputs[0];var map=LoadedMapPreservation.Restore(t.LoadedMap);var original=map.OwnedAssets?.SingleOrDefault(a=>a.Id==id);
        if(original==null)return null;
        var call=MapAssetTransfers.Restore(r.LoadedMap.AssetJournal).Calls.Single(c=>c.Id==id);if(call.Returned)return null;
        if(call.Started || r.CityCallStarted || r.City.Faction!=r.From)throw new ArgumentException("Only an original unstarted source frame may have a separate successor.");
        var frame=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==id) as Frame;
        var row=map.Things.Single(p=>p.Id==id);
        if(frame?.GetType()!=typeof(Frame)||!frame.Destroyed||frame.def.defName!=row.Def||frame.Faction!=r.From||map.WorkFramesV1?.Any(f=>f.Id==id)!=true)
            throw new ArgumentException("Original same naturally completed frame required.");
        var added=r.City.Map.listerThings.AllThings.Where(p=>!state.Before.Ids.Contains(p.GetUniqueLoadID())).ToArray();
        if(frame.def.entityDefToBuild is TerrainDef terrain)
        {
            if(added.Length!=0||map.WorkFramesV1.Single(f=>f.Id==id).TerrainV8!=terrain.defName)throw new ArgumentException("Original explicit terrain frame and zero added objects required.");
            return new(new(){Original=id},null,null,null,null,CaptureResolutionTerrain(r.LoadedMap.Map,row.Cell,terrain.defName));
        }
        if(added.Length!=1 || added[0].GetType()!=typeof(Building)&&added[0].GetType()!=typeof(Building_Storage)&&!WorktableEvidence.Supported(added[0]) || added[0].Faction!=r.From || frame.def.entityDefToBuild!=added[0].def ||
            added[0].Stuff?.defName!=row.Stuff || r.City.Map.cellIndices.CellToIndex(added[0].Position)!=row.Cell || added[0].Rotation.AsInt!=row.Rotation)
            throw new ArgumentException("Exactly one same-definition/location source building must be observed after native completion.");
        var thing=added[0];var physical=ResolutionPhysical(thing,r.City.Map);var asset=ResolutionAsset(r,thing,true,r.From);
        var table=thing as Building_WorkTable;var descriptor=table==null?null:WorktableEvidence.Capture(table);
        var storage=thing is Building_Storage shelf?StorageMapEvidence.CaptureOwned(shelf,r.City.Map):null;
        if(storage!=null&&(map.WorkFramesV1.Single(f=>f.Id==id).StorageV7 is not string originalSettings||StorageMapEvidence.Configuration(((Building_Storage)thing).settings,(Building_Storage)thing,null,null)!=originalSettings))throw new ArgumentException("Original frame storage settings were not preserved by native completion.");
        return new(new(){Original=id,Actual=thing,WorktableBillsV6=table==null?null:WorktableEvidence.Bills(table).ToList(),StorageContentsV7=storage==null?null:storage.Contents.Select(content=>r.City.Map.listerThings.AllThings.Single(p=>p.GetUniqueLoadID()==content)).ToList()},asset,physical,descriptor,storage);
    }
    internal static bool KnownResolutionOwner(TerritoryTransferReference r,Thing thing)
    {
        if(r.WorkAssetResolutionV5==null || r.WorkAssetReferencesV5==null)return false;
        var ledger=JsonSerializer.Deserialize<WorkAssetResolutionSave>(r.WorkAssetResolutionV5);
        foreach(var entry in ledger.Entries)
            if(entry.Actual==thing.GetUniqueLoadID() && r.WorkAssetReferencesV5.Any(p=>p.Original==entry.Original&&ReferenceEquals(p.Actual,thing)))
                return thing.Faction==(entry.Returned?r.Target:r.From);
        return false;
    }
    private void ValidateAssetResolutions(TerritoryTransfer t,TerritoryTransferReference r,bool current)
    {
        if(r.WorkAssetResolutionV5==null){if(r.WorkAssetReferencesV5!=null)throw new ArgumentException("Property references have no independent resolution ledger.");return;}
        var s=WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5,t.Phase==TerritoryTransferPhase.Applied);
        if(s.BaseCalls!=r.LoadedMap.AssetJournal || r.WorkAssetReferencesV5==null || r.WorkAssetReferencesV5.Any(p=>p==null) || !r.WorkAssetReferencesV5.Select(p=>p.Original).SequenceEqual(s.Entries.Select(e=>e.Original)))
            throw new ArgumentException("Original call prefix or separately owned actual references changed.");
        foreach(var entry in s.Entries)
        {
            if(entry.Started)WorkAssetResolutions.Approved(t,r.WorkProgressV1,entry,custodyAgreements.Record(entry.Approval));
            if(entry.TerrainV8!=null)
            {
                if(entry.TerrainV8.Accepted)WorkAssetResolutions.Approved(t,r.WorkProgressV1,entry,custodyAgreements.Record(entry.TerrainV8.Approval));
                if(current)VerifyResolutionTerrain(r,entry);
                continue;
            }
            if(current)
            {
                if(entry.Started&&!entry.Returned)throw new ArgumentException("Unknown actual-property call must never replay.");
                var thing=r.WorkAssetReferencesV5.Single(p=>p.Original==entry.Original).Actual;
                if(thing?.GetUniqueLoadID()!=entry.Actual || ResolutionPhysical(thing,r.City.Map)!=entry.Physical || ResolutionAsset(r,thing,entry.Asset.Repair!=null,entry.Returned?r.Target:r.From)!=entry.Asset)
                    throw new ArgumentException("Same actual property and its separate original preparation required.");
                if(!entry.Event.HasValue && !r.LoadedMap.Things.Any(p=>ReferenceEquals(p,thing)))throw new ArgumentException("Original remainder property lost its original reference.");
                VerifyResolutionWorktable(r,entry,thing);
                VerifyResolutionStorage(r.WorkAssetReferencesV5.Single(p=>p.Original==entry.Original),entry,thing,r.LoadedMap);
            }
        }
    }
    private bool ResumeAssetResolutions(TerritoryTransfer t,TerritoryTransferReference r)
    {
        var s=WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5);var calls=MapAssetTransfers.Restore(s.BaseCalls).Calls;
        var next=calls.FirstOrDefault(c=>!c.Returned&&!s.Entries.Any(e=>e.Original==c.Id&&WorkAssetResolutions.Resolved(e)));
        if(next==null){WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5,true);return false;}
        RequireCurrentCustody(r);var consent=custodyAgreements.Record(r.CustodyAgreementV1);
        var approval=WorkProgress.Approval(t,consent.Witness.WorkProgressV1);
        if(approval.ResolutionsV5!=r.WorkAssetResolutionV5)throw new ArgumentException("Fresh exact actual-property approval required.");
        var entry=s.Entries.SingleOrDefault(e=>e.Original==next.Id);var refs=r.WorkAssetReferencesV5.ToList();string staged=r.WorkAssetResolutionV5;
        if(entry?.TerrainV8!=null)
        {
            VerifyResolutionTerrain(r,entry);RequireCurrentCustody(r);
            r.WorkAssetResolutionV5=WorkAssetResolutions.AcceptTerrain(t,r.WorkProgressV1,staged,entry.Original,consent);
            Messages.Message("RE_WorkTerrainAccepted".Translate(),MessageTypeDefOf.NeutralEvent);return true;
        }
        if(entry==null)
        {
            var thing=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==next.Id);
            staged=WorkAssetResolutions.AddOriginal(t,r.WorkProgressV1,staged,next.Id);entry=WorkAssetResolutions.Validate(t,r.WorkProgressV1,staged).Entries.Last();
            if(thing==null||ResolutionPhysical(thing,r.City.Map)!=entry.Physical||ResolutionAsset(r,thing,entry.Asset.Repair!=null,r.From)!=entry.Asset)throw new ArgumentException("Remaining original property is unsupported or changed.");
            refs.Add(new(){Original=next.Id,Actual=thing,WorktableBillsV6=entry.WorktableV6==null?null:OriginalWorktableBills(r.LoadedMap,entry),StorageContentsV7=entry.StorageV7==null?null:entry.StorageV7.Contents.Select(id=>r.LoadedMap.Things.Single(p=>p!=null&&p.GetUniqueLoadID()==id)).ToList()});
        }
        var actual=refs.Single(p=>p.Original==entry.Original).Actual;
        VerifyResolutionWorktable(refs.Single(p=>p.Original==entry.Original),entry,actual,r.LoadedMap);
        VerifyResolutionStorage(refs.Single(p=>p.Original==entry.Original),entry,actual,r.LoadedMap);
        if(entry.Started&&!entry.Returned)throw new ArgumentException("Unknown actual-property call; original retained.");
        if(!entry.Returned)
        {
            string begun=WorkAssetResolutions.Begin(t,r.WorkProgressV1,staged,entry.Original,actual.Faction?.GetUniqueLoadID(),consent);
            RequireCurrentCustody(r);r.WorkAssetReferencesV5=refs;r.WorkAssetResolutionV5=begun;
            var game=Current.Game;actual.SetFaction(r.Target);
            if(Current.Game!=game||Current.ProgramState!=ProgramState.Playing)throw new ArgumentException("Changed game during separate property call.");
            r.WorkAssetResolutionV5=WorkAssetResolutions.Returned(t,r.WorkProgressV1,r.WorkAssetResolutionV5,entry.Original,actual.Faction?.GetUniqueLoadID());
        }
        if(ResolutionPhysical(actual,r.City.Map)!=entry.Physical||ResolutionAsset(r,actual,entry.Asset.Repair!=null,r.Target)!=entry.Asset)throw new ArgumentException("Actual property returned but independent preservation is unverified.");
        VerifyResolutionWorktable(r,entry,actual);
        VerifyResolutionStorage(r.WorkAssetReferencesV5.Single(p=>p.Original==entry.Original),entry,actual,r.LoadedMap);
        r.WorkAssetResolutionV5=WorkAssetResolutions.Verify(t,r.WorkProgressV1,r.WorkAssetResolutionV5,entry.Original);
        Messages.Message("RE_WorkSuccessorProgress".Translate(),MessageTypeDefOf.NeutralEvent);return true;
    }
    private static void ReleaseAssetResolutionReferences(TerritoryTransferReference r)
    {foreach(var reference in r.WorkAssetReferencesV5??[]){reference.Actual=null;reference.WorktableBillsV6?.Clear();reference.StorageContentsV7?.Clear();}}
    private static void VerifyResolutionWorktable(TerritoryTransferReference r,WorkAssetResolution entry,Thing actual)
        =>VerifyResolutionWorktable(r.WorkAssetReferencesV5.Single(p=>p.Original==entry.Original),entry,actual,r.LoadedMap);
    private static List<Bill> OriginalWorktableBills(LoadedMapRecord original,WorkAssetResolution entry)
    {
        if(original.WorkBills==null)throw new ArgumentException("Original bill reference record missing.");
        return entry.WorktableV6.Bills.Select(id=>original.WorkBills.Single(b=>b!=null&&b.GetUniqueLoadID()==id)).ToList();
    }
    private static void VerifyResolutionWorktable(WorkAssetReferenceV5 reference,WorkAssetResolution entry,Thing actual,LoadedMapRecord original)
    {
        if(entry.WorktableV6==null){if(WorktableEvidence.Supported(actual)||reference.WorktableBillsV6!=null)throw new ArgumentException("Worktable requires its separate scope.");return;}
        if(actual is not Building_WorkTable table || JsonSerializer.Serialize(WorktableEvidence.Capture(table))!=JsonSerializer.Serialize(entry.WorktableV6)||reference.WorktableBillsV6==null||!WorktableEvidence.Bills(table).SequenceEqual(reference.WorktableBillsV6))throw new ArgumentException("Actual bill references/configuration changed.");
        if(!entry.Event.HasValue&&!reference.WorktableBillsV6.SequenceEqual(OriginalWorktableBills(original,entry)))throw new ArgumentException("Original remainder table lost its original Bill references.");
    }
    private static void VerifyResolutionStorage(WorkAssetReferenceV5 reference,WorkAssetResolution entry,Thing actual,LoadedMapRecord original)
    {
        if(entry.StorageV7==null){if(actual?.GetType()==typeof(Building_Storage)||reference.StorageContentsV7!=null)throw new ArgumentException("Storage needs its separate original contents/settings scope.");return;}
        if(actual is not Building_Storage shelf||JsonSerializer.Serialize(StorageMapEvidence.CaptureOwned(shelf,original.Map))!=JsonSerializer.Serialize(entry.StorageV7)||reference.StorageContentsV7==null||
            !reference.StorageContentsV7.Select(p=>p?.GetUniqueLoadID()).SequenceEqual(entry.StorageV7.Contents))throw new ArgumentException("Original actual storage scope changed.");
        var current=shelf.slotGroup.HeldThings.OrderBy(p=>p.GetUniqueLoadID(),StringComparer.Ordinal).ToArray();
        if(!current.SequenceEqual(reference.StorageContentsV7)||!entry.Event.HasValue&&reference.StorageContentsV7.Any(p=>!original.Things.Contains(p)))throw new ArgumentException("Same original stored goods references required; never haul or recreate.");
    }
    private static WorkTerrainAcceptance CaptureResolutionTerrain(Map map,int cell,string expected)
    {
        if(!NativeTransferBodies.WorkObservationBodies||map==null||map.terrainGrid?.GetType()!=typeof(TerrainGrid)||map.roofGrid?.GetType()!=typeof(RoofGrid)||cell<0||cell>=map.cellIndices.NumGridCells)throw new ArgumentException("Original audited terrain map required.");
        if(ResidentActiveWork.Raw(map.terrainGrid,typeof(TerrainGrid),"map")!=map||ResidentActiveWork.Raw(map.roofGrid,typeof(RoofGrid),"map")!=map)throw new ArgumentException("Original native terrain/roof owners changed.");
        if(ResidentActiveWork.Raw(map.terrainGrid,typeof(TerrainGrid),"foundationGrid") is not TerrainDef[] foundation||ResidentActiveWork.Raw(map.terrainGrid,typeof(TerrainGrid),"tempGrid") is not TerrainDef[] temporary||
            ResidentActiveWork.Raw(map.terrainGrid,typeof(TerrainGrid),"colorGrid") is not ColorDef[] colors||foundation.Length!=map.cellIndices.NumGridCells||temporary.Length!=foundation.Length||colors.Length!=foundation.Length||foundation[cell]!=null||temporary[cell]!=null||colors[cell]!=null)throw new ArgumentException("Layered or painted terrain needs its own preservation scope.");
        ResidentActiveWork.Known(typeof(TerrainGrid),"TerrainAt");ResidentActiveWork.Known(typeof(TerrainGrid),"UnderTerrainAt");ResidentActiveWork.Known(typeof(RoofGrid),"RoofAt");
        var position=map.cellIndices.IndexToCell(cell);var def=map.terrainGrid.TerrainAt(position);var under=map.terrainGrid.UnderTerrainAt(position);var roof=map.roofGrid.RoofAt(position);
        if(def?.GetType()!=typeof(TerrainDef)||def.defName!=expected||def.isFoundation||def.temporary||def.bridge||DefDatabase<TerrainDef>.GetNamedSilentFail(expected)!=def||
            under!=null&&(under.GetType()!=typeof(TerrainDef)||DefDatabase<TerrainDef>.GetNamedSilentFail(under.defName)!=under)||roof!=null&&(roof.GetType()!=typeof(RoofDef)||DefDatabase<RoofDef>.GetNamedSilentFail(roof.defName)!=roof))throw new ArgumentException("Same registered standard terrain/under-terrain/roof required.");
        return new(cell,def.defName,under?.defName,roof?.defName);
    }
    private static void VerifyResolutionTerrain(TerritoryTransferReference r,WorkAssetResolution entry)
    {
        var reference=r.WorkAssetReferencesV5.Single(p=>p.Original==entry.Original);var clean=entry.TerrainV8 with{Accepted=false,Approval=null};
        if(reference.Actual!=null||reference.WorktableBillsV6!=null||reference.StorageContentsV7!=null||CaptureResolutionTerrain(r.LoadedMap.Map,clean.Cell,clean.Def)!=clean)throw new ArgumentException("Same positively observed terrain required; no native property call is inferred.");
    }
}
