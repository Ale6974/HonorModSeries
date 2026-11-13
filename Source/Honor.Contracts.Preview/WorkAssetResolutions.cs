using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed class WorkAssetReferenceV5 : IExposable
{
    public string Original;
    public Thing Actual;
    public void ExposeData(){Scribe_Values.Look(ref Original,"originalAssetId");Scribe_References.Look(ref Actual,"separateActualProperty");}
}
internal sealed record WorkSuccessorCandidate(WorkAssetReferenceV5 Reference,MapAsset Asset,NeutralMapThing Physical);
public sealed partial class TerritoryAdministrationLedger
{
    private static NeutralMapThing ResolutionPhysical(Thing thing,Map map)
    {
        if(thing==null || thing.Destroyed || !thing.Spawned || thing.Map!=map || thing is IThingHolder || thing.GetType()!=typeof(Building)&&thing.GetType()!=typeof(Thing)&&thing.GetType()!=typeof(ThingWithComps))
            throw new ArgumentException("Same basic original or new building property required.");
        MapAssetRecords.VerifyHolderRead(thing);int? quality=thing.TryGetQuality(out var q)?(int)q:null;
        return new(thing.GetUniqueLoadID(),thing.def.defName,thing.Stuff?.defName,thing.stackCount,thing.HitPoints,map.cellIndices.CellToIndex(thing.Position),thing.Rotation.AsInt,quality);
    }
    private static MapAsset ResolutionAsset(TerritoryTransferReference r,Thing thing,bool repair,Faction expected)
    {
        if(thing?.Faction!=expected)throw new ArgumentException("Actual property owner lacks its independent call fact.");
        var peers=(r.LoadedMap.MapResidents??[]).Concat(r.LoadedMap.Visitors??[]).ToArray();
        return MapAssetRecords.Capture(thing,r.City,null,repair,reviewedPawns:peers,independentOwner:expected) with{Owner=r.From.GetUniqueLoadID()};
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
        if(added.Length!=1 || added[0].GetType()!=typeof(Building) || added[0].Faction!=r.From || frame.def.entityDefToBuild!=added[0].def ||
            added[0].Stuff?.defName!=row.Stuff || r.City.Map.cellIndices.CellToIndex(added[0].Position)!=row.Cell || added[0].Rotation.AsInt!=row.Rotation)
            throw new ArgumentException("Exactly one same-definition/location source building must be observed after native completion.");
        var thing=added[0];var physical=ResolutionPhysical(thing,r.City.Map);var asset=ResolutionAsset(r,thing,true,r.From);
        return new(new(){Original=id,Actual=thing},asset,physical);
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
            if(current)
            {
                if(entry.Started&&!entry.Returned)throw new ArgumentException("Unknown actual-property call must never replay.");
                var thing=r.WorkAssetReferencesV5.Single(p=>p.Original==entry.Original).Actual;
                if(thing?.GetUniqueLoadID()!=entry.Actual || ResolutionPhysical(thing,r.City.Map)!=entry.Physical || ResolutionAsset(r,thing,entry.Asset.Repair!=null,entry.Returned?r.Target:r.From)!=entry.Asset)
                    throw new ArgumentException("Same actual property and its separate original preparation required.");
                if(!entry.Event.HasValue && !r.LoadedMap.Things.Any(p=>ReferenceEquals(p,thing)))throw new ArgumentException("Original remainder property lost its original reference.");
            }
        }
    }
    private bool ResumeAssetResolutions(TerritoryTransfer t,TerritoryTransferReference r)
    {
        var s=WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5);var calls=MapAssetTransfers.Restore(s.BaseCalls).Calls;
        var next=calls.FirstOrDefault(c=>!c.Returned&&!s.Entries.Any(e=>e.Original==c.Id&&e.Verified));
        if(next==null){WorkAssetResolutions.Validate(t,r.WorkProgressV1,r.WorkAssetResolutionV5,true);return false;}
        RequireCurrentCustody(r);var consent=custodyAgreements.Record(r.CustodyAgreementV1);
        var approval=WorkProgress.Approval(t,consent.Witness.WorkProgressV1);
        if(approval.ResolutionsV5!=r.WorkAssetResolutionV5)throw new ArgumentException("Fresh exact actual-property approval required.");
        var entry=s.Entries.SingleOrDefault(e=>e.Original==next.Id);var refs=r.WorkAssetReferencesV5.ToList();string staged=r.WorkAssetResolutionV5;
        if(entry==null)
        {
            var thing=r.LoadedMap.Things.SingleOrDefault(p=>p!=null&&p.GetUniqueLoadID()==next.Id);
            staged=WorkAssetResolutions.AddOriginal(t,r.WorkProgressV1,staged,next.Id);entry=WorkAssetResolutions.Validate(t,r.WorkProgressV1,staged).Entries.Last();
            if(thing==null||ResolutionPhysical(thing,r.City.Map)!=entry.Physical||ResolutionAsset(r,thing,entry.Asset.Repair!=null,r.From)!=entry.Asset)throw new ArgumentException("Remaining original property is unsupported or changed.");
            refs.Add(new(){Original=next.Id,Actual=thing});
        }
        var actual=refs.Single(p=>p.Original==entry.Original).Actual;
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
        r.WorkAssetResolutionV5=WorkAssetResolutions.Verify(t,r.WorkProgressV1,r.WorkAssetResolutionV5,entry.Original);
        Messages.Message("RE_WorkSuccessorProgress".Translate(),MessageTypeDefOf.NeutralEvent);return true;
    }
    private static void ReleaseAssetResolutionReferences(TerritoryTransferReference r)
    {foreach(var reference in r.WorkAssetReferencesV5??[])reference.Actual=null;}
}
