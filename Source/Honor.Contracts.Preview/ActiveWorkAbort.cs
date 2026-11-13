using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
public sealed partial class TerritoryAdministrationLedger
{
    private string UnstartedActiveWork(TerritoryTransfer t, TerritoryTransferReference r, long tick)
    {
        if (RestoreFailed || !NativeTransferBodies.WorkObservationBodies || Current.Game != ownerGame || Charters.RestoreFailed || Current.Game.GetComponent<PoliticalTransitionLedger>().Journal.Pending || !t.StandaloneActiveWorkV1 || t.Phase != TerritoryTransferPhase.Prepared || t.StockFormat != 25 || r.CustodyAgreementV1 != null || !r.CityCallV1 || r.CityCallStarted || r.CityCallReturned || r.Residents?.Profile != 16 || r.Residents.Started || !r.ResidentCareJournalRequired || !r.Residents.CareJournalV1 || r.From == null || r.From.IsPlayer || r.From.GetUniqueLoadID()!=t.FromOwner || r.City?.GetType()!=typeof(RimWorld.Planet.Settlement) || r.City.Destroyed || r.City.Faction != r.From || !r.City.HasMap || r.LoadedMap?.Map != r.City.Map || r.Residents.NativeMap != r.City.Map || !Find.WorldObjects.AllWorldObjects.Contains(r.City) || r.Residents.Pawns == null || r.Residents.Pawns.Count != r.Residents.Set().Residents.Count || r.Residents.Pawns.Where((p,i) => p?.GetType()!=typeof(Pawn) || p.GetUniqueLoadID() != r.Residents.Set().Residents[i].Id || p.Faction != r.From).Any()) throw new ArgumentException("Original positively unstarted own intent and surviving source custody required.");
        if (r.Residents.Surgery == null || r.Residents.Surgery.Count != r.Residents.Pawns.Count || r.Residents.Surgery.Any(s => s == null || s.LoadFailed || s.Detached?.Count != 0 || s.Set().Orders.Count != 0) || r.Residents.Prisoners?.Count != 0) throw new ArgumentException("Surgical/prisoner custody is not an untouched civilian intent.");
        var work=LoadedMapPreservation.Restore(t.LoadedMap).ActiveWorkV1;
        if (work==null || r.Residents.ActiveWorkJobs==null || r.Residents.ActiveWorkJobs.Count!=work.Sum(row => row.Jobs.Length)) throw new ArgumentException("Original job-reference slot count changed.");
        // Finished native jobs can be pooled. Never treat a recycled reference as the old job; abort only this untouched intent and retain its original raw work document.
        ResidentExecution.ValidateCensus(r.Residents,r.ResidentCensusProof,t.Territory,t.FromOwner,t.TargetOwner);
        return ActiveWorkAbort.Prepare(t,r.Residents.Census,r.ResidentCensusProof,r.Residents.AuthorityJournal.ToArray(),r.Residents.CareJournal.ToArray(),r.Residents.AreaRecords.Select(a => a.Journal).ToArray(),r.LoadedMap.AssetJournal,tick);
    }
    private bool KeepsUnstartedActiveWork(TerritoryTransfer t, TerritoryTransferReference r)
    { try { _=UnstartedActiveWork(t,r,t.PreparedTick); return true; } catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException) { return false; } }
    internal void DrawUnstartedActiveWork(Listing_Standard list, string id)
    {
        var t=Ledger.Transfer(id); var r=references.Single(r => r.Id==id);
        if (!KeepsUnstartedActiveWork(t,r) || !list.ButtonText("RE_AbortUnstartedWork".Translate())) return;
        string proof=UnstartedActiveWork(t,r,Find.TickManager.TicksGame); var game=Current.Game;
        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RE_AbortUnstartedWorkConfirm".Translate(),()=> {
            try {
                if (Current.Game!=game) throw new ArgumentException("Another game.");
                var current=Ledger.Transfer(id); string fresh=UnstartedActiveWork(current,r,Find.TickManager.TicksGame);
                // Rebuild current no-start evidence immediately; the reviewed original transfer must still be identical.
                if (JsonSerializer.Deserialize<ActiveWorkAbortState>(proof).Original != JsonSerializer.Deserialize<ActiveWorkAbortState>(fresh).Original) throw new ArgumentException("Original intent changed.");
                var next=TerritoryTransferLedger.Restore(Ledger.Export()); next.CancelUnstartedActiveWork(id,fresh,Find.TickManager.TicksGame);
                Ledger=next; ResidentExecution.ReleaseReferences(r.Residents); LoadedMapRecords.Release(r.LoadedMap);
                Messages.Message("RE_AbortUnstartedWorkDone".Translate(),MessageTypeDefOf.NeutralEvent);
            } catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Unstarted work intent retained: "+e.GetType().Name); Messages.Message("CT_Unavailable".Translate(),MessageTypeDefOf.RejectInput); }
        }));
    }
}
