using System.Text.Json;
namespace Honor.Contracts.Rules;
public sealed record GroundRestAbortState(string Original, string Census, string CensusProof, string[] Calls, string[] Care, string[] Areas, string Assets, long Tick);
public static class GroundRestAbort
{
    public static TerritoryTransfer Original(TerritoryTransfer t) => t with { Phase = TerritoryTransferPhase.Prepared, ResolvedTick = null, UnstartedWorkAbortV1 = null };
    public static string Prepare(TerritoryTransfer t, string census, string proof, string[] calls, string[] care, string[] areas, string assets, long tick)
    {
        string json = JsonSerializer.Serialize(new GroundRestAbortState(JsonSerializer.Serialize(Original(t)),census,proof,calls,care,areas,assets,tick)); Validate(t,json); return json;
    }
    public static void Validate(TerritoryTransfer t, string json)
    {
        if (json == null || json.Length > 4000000 || !t.StandaloneWorkV1 || t.StockFormat != 24 || t.RoyalResidentsV1 != null || t.PreviousTransferV1 != null || t.ReleasedRoot != null || t.Purpose != TerritoryTransferPurpose.ActiveCharter || !t.CityCallJournalV1) throw new ArgumentException("New original unstarted given-work custody required.");
        var s = JsonSerializer.Deserialize<GroundRestAbortState>(json);
        if (s == null || s.Original != JsonSerializer.Serialize(Original(t)) || s.Tick < t.PreparedTick || s.Tick > int.MaxValue || t.ResolvedTick.HasValue && s.Tick != t.ResolvedTick) throw new ArgumentException("Unstarted abort belongs to another original intent/clock.");
        var census = ResidentTransferCensus.Restore(s.Census); var residents = ResidentMoveSet.Restore(census.Residents);
        if (residents.Proof != t.ResidentProof || census.Profile != 15 || s.Areas == null || s.Areas.Length != residents.Residents.Count || LoadedMapPreservation.Restore(t.LoadedMap).GivenGroundRestV1 != 1) throw new ArgumentException("Original work census is unpaired.");
        ResidentTransferCensus.Check(s.Census,s.CensusProof,t.Territory,t.FromOwner,t.TargetOwner,15,residents,s.Calls,s.Care,false);
        for (int i=0;i<s.Areas.Length;i++) { var area=ResidentAreas.Restore(s.Areas[i]); if (area.Id != residents.Residents[i].Id || area.Started || area.Returned || area.Cleared || area.Applying || area.Consumed) throw new ArgumentException("Area preservation already started."); }
        if (LoadedMapPreservation.Restore(t.LoadedMap).OwnedAssets == null) { if (s.Assets != null) throw new ArgumentException("Unexpected original asset calls."); }
        else { MapAssetTransfers.Pair(s.Assets,t.LoadedMap,t.FromOwner,t.TargetOwner); if (MapAssetTransfers.Started(s.Assets)) throw new ArgumentException("Original asset call already started."); }
        if (JsonSerializer.Serialize(s) != json) throw new ArgumentException("Canonical original no-start evidence required.");
    }
}
