using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum TerritoryTransferPurpose { ActiveCharter, ReleasedToUnion }
public enum TerritoryTransferPhase { Prepared, Applied, Cancelled, Destroyed }
public sealed record TerritoryTransferDestruction(string Proof, string Owner, long LossTick, long ObservedTick);
public sealed record TerritoryTransferObservation(string Territory, string Owner, int SourceSettlements,
    bool Exists = true, bool Player = false, bool HasMap = false, bool Visited = false,
    bool Eligible = true, bool TargetEligible = true, bool TargetPlayer = false, bool PendingDelivery = false,
    bool HasInhabitants = false, string StockProof = null, int StockFormat = 0, string ResidentProof = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string LoadedMap { get; init; }
}
public sealed record TerritoryTransfer(string Id, string Territory, string BaselineOwner, string FromOwner,
    string TargetOwner, string Charter, TerritoryTransferPurpose Purpose, long PreparedTick,
    TerritoryTransferPhase Phase = TerritoryTransferPhase.Prepared, long? ResolvedTick = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string StockProof = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int StockFormat = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string ReleasedRoot = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string ResidentProof = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] TerritoryTransferDestruction Destruction = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool CityCallJournalV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string PreviousTransferV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string RoyalResidentsV1 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string RoyalRightsV2 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string LoadedMap { get; init; }
}
public sealed record TerritoryTransfersSave(int Version, long LastTick, TerritoryTransfer[] Transfers);

/// <summary>Explicit ownership execution journal, separate from consent and the immutable original identity.</summary>
public sealed class TerritoryTransferLedger
{
    public const int Version = 1, Capacity = 2000;
    private readonly List<TerritoryTransfer> transfers = [];
    public IReadOnlyList<TerritoryTransfer> Transfers => transfers.AsReadOnly();
    public long LastTick { get; private set; }
    private static void Require(bool value, string reason) { if (!value) throw new ArgumentException(reason); }
    private static void Key(string value) => Require(!string.IsNullOrWhiteSpace(value) && value.Length <= 120 && !value.Any(char.IsControl), "Invalid transfer identity.");
    private void Time(long now) => Require(now >= LastTick && now <= int.MaxValue, "Invalid transfer clock.");
    public TerritoryTransfer Transfer(string id) => transfers.SingleOrDefault(t => t.Id == id) ?? throw new ArgumentException("Unknown territory transfer.");
    public bool Pending(string territory) => transfers.Any(t => t.Territory == territory && t.Phase == TerritoryTransferPhase.Prepared);
    public string ExpectedOwner(string territory, string baseline)
    {
        Key(territory); Key(baseline);
        Require(transfers.All(t => t.Territory != territory || t.BaselineOwner == baseline), "Baseline mismatch.");
        return transfers.LastOrDefault(t => t.Territory == territory && t.Phase == TerritoryTransferPhase.Applied)?.TargetOwner ?? baseline;
    }
    public bool VerifiedOwner(string territory, string baseline, string actual) => !Pending(territory) &&
        !transfers.Any(t => t.Territory == territory && t.Phase == TerritoryTransferPhase.Destroyed) && ExpectedOwner(territory, baseline) == actual;
    public TerritoryObservation ObserveIdentity(TerritoryObservation actual, string baseline)
    {
        RuntimeCompat.NotNull(actual);
        bool verified = actual.Exists && !actual.Player && VerifiedOwner(actual.Id, baseline, actual.NativeOwner);
        return actual with { NativeOwner = verified ? baseline : actual.NativeOwner, Eligible = actual.Eligible && verified };
    }
    public TerritoryTransfer Prepare(string id, CharterBook book, string charterId, TerritoryTransferObservation observed,
        string target, string verifiedAuthority, string verifiedUnion, TerritoryTransferPurpose purpose, long now, string verifiedAdministration = null, string releasedRoot = null)
        => PrepareCore(id, book, charterId, observed, target, verifiedAuthority, verifiedUnion, purpose, now, verifiedAdministration, releasedRoot, false);
    public TerritoryTransfer PrepareLoaded(string id, CharterBook book, string charterId, TerritoryTransferObservation observed,
        string target, string verifiedAuthority, string verifiedUnion, TerritoryTransferPurpose purpose, long now, string verifiedAdministration = null, string releasedRoot = null)
        => PrepareCore(id, book, charterId, observed, target, verifiedAuthority, verifiedUnion, purpose, now, verifiedAdministration, releasedRoot, true);
    private TerritoryTransfer PrepareCore(string id, CharterBook book, string charterId, TerritoryTransferObservation observed,
        string target, string verifiedAuthority, string verifiedUnion, TerritoryTransferPurpose purpose, long now, string verifiedAdministration, string releasedRoot, bool loaded, string previous = null, string royalResidents = null, string privateRights = null)
    {
        RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(observed);
        Require(privateRights == null || RoyalRights.Proof(privateRights) == royalResidents, "Unpaired private royal evidence.");
        Require(observed.StockFormat != 22 || privateRights != null, "Royal mapped custody requires fresh separate private rights.");
        Require(loaded ? observed.HasMap && observed.LoadedMap != null && observed.StockFormat is 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 : observed.LoadedMap == null && !observed.HasMap, "Loaded custody requires a separately scoped preparation.");
        Require(observed.LoadedMap == null || LoadedMapPreservation.Restore(observed.LoadedMap).OwnedAssets?.Any(a => a.Owner != observed.Owner) != true, "Original property rights must belong to the reviewed source faction.");
        Require(observed.StockFormat is not (10 or 15 or 17 or 19 or 21 or 22 or 23) || observed.HasInhabitants && CityStockProof.Valid(observed.ResidentProof), "Mapped residents require their original clinical census.");
        Key(id); Key(target); Key(observed.Territory); Key(observed.Owner); Time(now);
        Require(now >= book.LastTick, "Transfer cannot precede the current consent book clock.");
        var charter = book.Charter(charterId); var subject = book.Entity(charter.Terms.Subject);
        Require(!book.Charters.Any(c => c.Terms.Subject == subject.Id && book.Phase(c, now) == RecordedCharterPhase.Offered),
            "Resolve open proposals before changing the NPC authority that granted consent.");
        bool subtree = releasedRoot != null && SubtreeRelease.Authorizes(book, releasedRoot, charterId);
        Require(releasedRoot == null || subtree && purpose == TerritoryTransferPurpose.ReleasedToUnion, "Invalid released subtree scope.");
        Require(RuntimeCompat.IsDefined(purpose) && !subject.Player && (book.PlayerManaged(charter) || subtree || previous != null) &&
            observed.Territory == subject.Id && charter.PlayerSignedTick.HasValue && charter.NpcSignedTick.HasValue &&
            now >= Math.Max(charter.PlayerSignedTick.Value, charter.NpcSignedTick.Value), "A mutually signed NPC charter below the player is required.");
        if (purpose == TerritoryTransferPurpose.ActiveCharter)
            Require(charter.Phase == RecordedCharterPhase.Active && target == verifiedAuthority, "Target must be the live player's shared authority.");
        else
            Require(subtree ? target == verifiedUnion : charter.Phase == RecordedCharterPhase.Released && charter.PlayerReleaseTick.HasValue && charter.NpcReleaseTick.HasValue &&
                now >= Math.Max(charter.PlayerReleaseTick.Value, charter.NpcReleaseTick.Value) && target == verifiedUnion &&
                !book.Charters.Any(c => c.Phase == RecordedCharterPhase.Active && c.Terms.Subject == subject.Id), "Release must be complete with no competing active liege.");
        Require(!book.Entities.Any(e => e.Player && e.NativeOwner == target) && observed.Owner != target &&
            observed.Exists && !observed.Player && Preserved(observed, observed.StockProof, observed.ResidentProof) && observed.Eligible &&
            observed.TargetEligible && !observed.TargetPlayer && !observed.PendingDelivery &&
            (observed.SourceSettlements >= 2 || observed.Owner == verifiedAdministration && observed.SourceSettlements == 1),
            "Only unloaded nonplayer cities with verified stock/resident preservation and a surviving source base can move.");
        Require(VerifiedOwner(subject.Id, subject.NativeOwner, observed.Owner) && transfers.Count < Capacity &&
            !transfers.Any(t => t.Id == id || t.Phase == TerritoryTransferPhase.Prepared), "Resolve prior transfers and verify current ownership first.");
        if (previous != null)
        {
            var prior = Transfer(previous);
            Require(prior.Phase == TerritoryTransferPhase.Applied && prior.Territory == subject.Id && transfers.Last(t => t.Territory == subject.Id).Id == previous && prior.TargetOwner == observed.Owner &&
                charter.NpcAuthority != null && charter.Phase == RecordedCharterPhase.Active && !book.PlayerManaged(charter), "Positive original handover and current foreign contract required.");
        }
        var result = new TerritoryTransfer(id, subject.Id, subject.NativeOwner, observed.Owner, target, charterId, purpose, now,
            StockProof: observed.StockProof, StockFormat: observed.StockFormat, ReleasedRoot: releasedRoot, ResidentProof: observed.ResidentProof) { LoadedMap = observed.LoadedMap, CityCallJournalV1 = loaded, PreviousTransferV1 = previous, RoyalResidentsV1 = royalResidents, RoyalRightsV2 = privateRights };
        if (JsonSerializer.Serialize(new TerritoryTransfersSave(Version, now, transfers.Append(result).ToArray())).Length > 4000000)
            throw new ArgumentException("Original physical journal capacity; retain existing history unchanged.");
        transfers.Add(result); LastTick = now; return result;
    }
    public TerritoryTransfer PrepareJournaled(string id, CharterBook book, string charterId, TerritoryTransferObservation observed,
        string target, string verifiedAuthority, string verifiedUnion, TerritoryTransferPurpose purpose, long now, string verifiedAdministration = null, string releasedRoot = null)
    {
        var prepared = Prepare(id, book, charterId, observed, target, verifiedAuthority, verifiedUnion, purpose, now, verifiedAdministration, releasedRoot);
        var paired = prepared with { CityCallJournalV1 = true };
        transfers[transfers.Count - 1] = paired;
        return paired;
    }
    public TerritoryTransfer PrepareLoadedJournaled(string id, CharterBook book, string charterId, TerritoryTransferObservation observed,
        string target, string verifiedAuthority, string verifiedUnion, TerritoryTransferPurpose purpose, long now, string verifiedAdministration = null, string releasedRoot = null)
    {
        var prepared = PrepareLoaded(id, book, charterId, observed, target, verifiedAuthority, verifiedUnion, purpose, now, verifiedAdministration, releasedRoot);
        var paired = prepared with { CityCallJournalV1 = true }; transfers[transfers.Count - 1] = paired; return paired;
    }
    // Preparation is not execution permission: the native adapter must separately stage current scoped consent before calling native setters.
    public TerritoryTransfer PrepareForeignContinuation(string id, string previous, CharterBook book, string charter, TerritoryTransferObservation observed, string target, long now, string administration = null, string royalResidents = null)
    {
        Require(royalResidents == null || CityStockProof.Valid(royalResidents), "Invalid royal continuation proof.");
        var t = PrepareCore(id, book, charter, observed, target, target, null, TerritoryTransferPurpose.ActiveCharter, now, administration, null, observed.HasMap, previous, royalResidents);
        var paired = t with { CityCallJournalV1 = true }; transfers[transfers.Count - 1] = paired; return paired;
    }
    public TerritoryTransfer PrepareRoyalJournaled(string id, CharterBook book, string charter, TerritoryTransferObservation observed, string target, string royalResidents, long now, string administration = null)
    {
        Require(CityStockProof.Valid(royalResidents), "Exact original royal civilian evidence required.");
        var t = PrepareCore(id, book, charter, observed, target, target, null, TerritoryTransferPurpose.ActiveCharter, now, administration, null, observed.HasMap, royalResidents: royalResidents);
        var paired = t with { CityCallJournalV1 = true }; transfers[transfers.Count - 1] = paired; return paired;
    }
    public TerritoryTransfer PreparePrivateRoyalContinuation(string id, string previous, CharterBook book, string charter, TerritoryTransferObservation observed, string target, string rights, long now, string administration = null)
    {
        var t = PrepareCore(id, book, charter, observed, target, target, null, TerritoryTransferPurpose.ActiveCharter, now, administration, null, observed.HasMap, previous, RoyalRights.Proof(rights), rights);
        var paired = t with { RoyalRightsV2 = rights, CityCallJournalV1 = true }; transfers[transfers.Count - 1] = paired; return paired;
    }
    public TerritoryTransfer PreparePrivateRoyalJournaled(string id, CharterBook book, string charter, TerritoryTransferObservation observed, string target, string rights, long now, string administration = null)
    {
        var t = PrepareCore(id, book, charter, observed, target, target, null, TerritoryTransferPurpose.ActiveCharter, now, administration, null, observed.HasMap, royalResidents: RoyalRights.Proof(rights), privateRights: rights);
        var paired = t with { RoyalRightsV2 = rights, CityCallJournalV1 = true }; transfers[transfers.Count - 1] = paired; return paired;
    }
    public void Confirm(string id, TerritoryTransferObservation actual, long now)
    {
        RuntimeCompat.NotNull(actual); Time(now); var transfer = Transfer(id);
        Require(transfer.Phase == TerritoryTransferPhase.Prepared && actual.Territory == transfer.Territory && actual.Exists &&
            actual.Owner == transfer.TargetOwner && !actual.Player && actual.LoadedMap == transfer.LoadedMap && actual.StockFormat == transfer.StockFormat && Preserved(actual, transfer.StockProof, transfer.ResidentProof),
            "Applied transfer needs actual intended nonplayer ownership and matching preserved stock evidence.");
        transfers[transfers.IndexOf(transfer)] = transfer with { Phase = TerritoryTransferPhase.Applied, ResolvedTick = now }; LastTick = now;
    }
    public void CancelUnmoved(string id, TerritoryTransferObservation actual, long now)
    {
        RuntimeCompat.NotNull(actual); Time(now); var transfer = Transfer(id);
        Require(transfer.RoyalResidentsV1 == null && transfer.Phase == TerritoryTransferPhase.Prepared && actual.Territory == transfer.Territory && actual.Exists &&
            actual.Owner == transfer.FromOwner && !actual.Player && actual.LoadedMap == transfer.LoadedMap && actual.StockFormat == transfer.StockFormat && Preserved(actual, transfer.StockProof, transfer.ResidentProof),
            "Only verified unmoved ownership with unchanged stock can cancel a transfer.");
        transfers[transfers.IndexOf(transfer)] = transfer with { Phase = TerritoryTransferPhase.Cancelled, ResolvedTick = now }; LastTick = now;
    }
    public string Export() => JsonSerializer.Serialize(new TerritoryTransfersSave(Version, LastTick, transfers.ToArray()));
    internal void ResolveDestroyed(string id, TerritoryTransferDestruction loss, long now)
    {
        RuntimeCompat.NotNull(loss); Time(now); var t = Transfer(id);
        Require(t.RoyalResidentsV1 == null && t.Phase == TerritoryTransferPhase.Prepared && t.Purpose == TerritoryTransferPurpose.ActiveCharter && t.ReleasedRoot == null &&
            t.ResidentProof == null && t.StockFormat == 0 && CityStockProof.Valid(loss.Proof) && (loss.Owner == t.FromOwner || loss.Owner == t.TargetOwner) &&
            loss.LossTick >= t.PreparedTick && loss.ObservedTick >= loss.LossTick && now >= loss.ObservedTick, "Exact observed empty-city destruction required.");
        transfers[transfers.IndexOf(t)] = t with { Phase = TerritoryTransferPhase.Destroyed, ResolvedTick = now, Destruction = loss }; LastTick = now;
    }
    private static bool Preserved(TerritoryTransferObservation actual, string proof, string residents) => MapPreserved(actual, proof) &&
        (residents == null ? !actual.HasInhabitants && actual.ResidentProof == null : actual.HasInhabitants && CityStockProof.Valid(residents) && actual.ResidentProof == residents) &&
        (proof == null ? !actual.Visited : CityStockProof.Valid(proof) && actual.StockProof == proof);
    private static bool MapPreserved(TerritoryTransferObservation actual, string proof)
    {
        if (actual.LoadedMap == null) return actual.StockFormat is 0 or 1 or 2 && (actual.StockFormat == 0 || proof != null) && !actual.HasMap;
        if (!actual.HasMap || actual.StockFormat is not (3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23)) return false;
        LoadedMapPreservation.Pair(actual.LoadedMap, actual.Territory, proof, actual.StockFormat); return true;
    }
    public static TerritoryTransferLedger Restore(string json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 4000000, "Invalid transfer snapshot.");
        var save = JsonSerializer.Deserialize<TerritoryTransfersSave>(json);
        Require(save != null && save.Version == Version && save.LastTick is >= 0 and <= int.MaxValue && save.Transfers != null && save.Transfers.Length <= Capacity,
            "Unsupported transfer snapshot.");
        var result = new TerritoryTransferLedger();
        foreach (var item in save.Transfers)
        {
            RuntimeCompat.NotNull(item);
            foreach (var key in new[] { item.Id, item.Territory, item.BaselineOwner, item.FromOwner, item.TargetOwner, item.Charter }) Key(key);
            if (item.ReleasedRoot != null) { Key(item.ReleasedRoot); Require(item.Purpose == TerritoryTransferPurpose.ReleasedToUnion, "Subtree scope is union-only."); }
            Require(item.ResidentProof == null || CityStockProof.Valid(item.ResidentProof), "Invalid resident preservation proof.");
            Require(item.StockFormat is not (10 or 15 or 17 or 19 or 21 or 22 or 23) || CityStockProof.Valid(item.ResidentProof), "Mapped city history requires its original resident proof.");
            if (item.LoadedMap != null)
            {
                LoadedMapPreservation.Pair(item.LoadedMap, item.Territory, item.StockProof, item.StockFormat);
                Require(LoadedMapPreservation.Restore(item.LoadedMap).OwnedAssets?.Any(a => a.Owner != item.FromOwner) != true, "Original asset owner changed.");
            }
            Require((item.LoadedMap == null ? item.StockFormat is 0 or 1 or 2 : item.StockFormat is 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 && item.CityCallJournalV1) && (item.StockFormat == 0 || item.StockProof != null) &&
                (item.StockProof == null || CityStockProof.Valid(item.StockProof)) &&
                RuntimeCompat.IsDefined(item.Purpose) && RuntimeCompat.IsDefined(item.Phase) && item.FromOwner != item.TargetOwner &&
                item.PreparedTick >= result.LastTick && item.PreparedTick <= save.LastTick &&
                (item.Phase == TerritoryTransferPhase.Prepared ? !item.ResolvedTick.HasValue : item.ResolvedTick >= item.PreparedTick && item.ResolvedTick <= save.LastTick) &&
                !result.transfers.Any(t => t.Id == item.Id || t.Phase == TerritoryTransferPhase.Prepared) &&
                !result.transfers.Any(t => t.Territory == item.Territory && t.Phase == TerritoryTransferPhase.Destroyed) &&
                result.ExpectedOwner(item.Territory, item.BaselineOwner) == item.FromOwner, "Invalid transfer history or owner chain.");
            Require(item.Phase == TerritoryTransferPhase.Destroyed ? item.Destruction != null && item.Purpose == TerritoryTransferPurpose.ActiveCharter &&
                item.ReleasedRoot == null && item.ResidentProof == null && item.StockFormat == 0 && CityStockProof.Valid(item.Destruction.Proof) &&
                (item.Destruction.Owner == item.FromOwner || item.Destruction.Owner == item.TargetOwner) && item.Destruction.LossTick >= item.PreparedTick &&
                item.Destruction.ObservedTick >= item.Destruction.LossTick && item.ResolvedTick >= item.Destruction.ObservedTick : item.Destruction == null,
                "Invalid destruction terminal state.");
            Require(item.StockFormat != 22 || item.RoyalRightsV2 != null, "Mapped royal history lacks private rights.");
            if (item.RoyalRightsV2 != null && RoyalRights.Proof(item.RoyalRightsV2) != item.RoyalResidentsV1) throw new ArgumentException("Unpaired private royal rights.");
            Require(item.RoyalResidentsV1 == null || CityStockProof.Valid(item.RoyalResidentsV1) && item.Phase is TerritoryTransferPhase.Prepared or TerritoryTransferPhase.Applied && item.CityCallJournalV1 && item.Purpose == TerritoryTransferPurpose.ActiveCharter && item.ReleasedRoot == null, "Invalid royal custody provenance.");
            if (item.PreviousTransferV1 != null)
            {
                var prior = result.Transfer(item.PreviousTransferV1);
                Require(prior.Phase == TerritoryTransferPhase.Applied && prior.Territory == item.Territory && result.transfers.Last(t => t.Territory == item.Territory).Id == item.PreviousTransferV1 && prior.TargetOwner == item.FromOwner &&
                    prior.ResolvedTick <= item.PreparedTick && item.CityCallJournalV1 && item.Purpose == TerritoryTransferPurpose.ActiveCharter && item.ReleasedRoot == null,
                    "Invalid original continuation provenance.");
            }
            result.transfers.Add(item); result.LastTick = item.ResolvedTick ?? item.PreparedTick;
        }
        result.LastTick = save.LastTick; return result;
    }
}
