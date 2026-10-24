using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum TerritoryTransferPurpose { ActiveCharter, ReleasedToUnion }
public enum TerritoryTransferPhase { Prepared, Applied, Cancelled }
public sealed record TerritoryTransferObservation(string Territory, string Owner, int SourceSettlements,
    bool Exists = true, bool Player = false, bool HasMap = false, bool Visited = false,
    bool Eligible = true, bool TargetEligible = true, bool TargetPlayer = false, bool PendingDelivery = false);
public sealed record TerritoryTransfer(string Id, string Territory, string BaselineOwner, string FromOwner,
    string TargetOwner, string Charter, TerritoryTransferPurpose Purpose, long PreparedTick,
    TerritoryTransferPhase Phase = TerritoryTransferPhase.Prepared, long? ResolvedTick = null);
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
    public bool VerifiedOwner(string territory, string baseline, string actual) => !Pending(territory) && ExpectedOwner(territory, baseline) == actual;
    public TerritoryObservation ObserveIdentity(TerritoryObservation actual, string baseline)
    {
        ArgumentNullException.ThrowIfNull(actual);
        bool verified = actual.Exists && !actual.Player && VerifiedOwner(actual.Id, baseline, actual.NativeOwner);
        return actual with { NativeOwner = verified ? baseline : actual.NativeOwner, Eligible = actual.Eligible && verified };
    }
    public TerritoryTransfer Prepare(string id, CharterBook book, string charterId, TerritoryTransferObservation observed,
        string target, string verifiedAuthority, string verifiedUnion, TerritoryTransferPurpose purpose, long now)
    {
        ArgumentNullException.ThrowIfNull(book); ArgumentNullException.ThrowIfNull(observed);
        Key(id); Key(target); Key(observed.Territory); Key(observed.Owner); Time(now);
        Require(now >= book.LastTick, "Transfer cannot precede the current consent book clock.");
        var charter = book.Charter(charterId); var subject = book.Entity(charter.Terms.Subject);
        Require(!book.Charters.Any(c => c.Terms.Subject == subject.Id && book.Phase(c, now) == RecordedCharterPhase.Offered),
            "Resolve open proposals before changing the NPC authority that granted consent.");
        Require(Enum.IsDefined(purpose) && !subject.Player && book.Entity(charter.Terms.Liege).Player &&
            observed.Territory == subject.Id && charter.PlayerSignedTick.HasValue && charter.NpcSignedTick.HasValue &&
            now >= Math.Max(charter.PlayerSignedTick.Value, charter.NpcSignedTick.Value), "A mutually signed player-liege/NPC-city charter is required.");
        if (purpose == TerritoryTransferPurpose.ActiveCharter)
            Require(charter.Phase == RecordedCharterPhase.Active && target == verifiedAuthority, "Target must be the live player's shared authority.");
        else
            Require(charter.Phase == RecordedCharterPhase.Released && charter.PlayerReleaseTick.HasValue && charter.NpcReleaseTick.HasValue &&
                now >= Math.Max(charter.PlayerReleaseTick.Value, charter.NpcReleaseTick.Value) && target == verifiedUnion &&
                !book.Charters.Any(c => c.Phase == RecordedCharterPhase.Active && c.Terms.Subject == subject.Id), "Release must be complete with no competing active liege.");
        Require(target != book.Entity(charter.Terms.Liege).NativeOwner && observed.Owner != target &&
            observed.Exists && !observed.Player && !observed.HasMap && !observed.Visited && observed.Eligible &&
            observed.TargetEligible && !observed.TargetPlayer && !observed.PendingDelivery && observed.SourceSettlements >= 2,
            "Only eligible untouched nonplayer cities with a surviving source base can move.");
        Require(VerifiedOwner(subject.Id, subject.NativeOwner, observed.Owner) && transfers.Count < Capacity &&
            !transfers.Any(t => t.Id == id || t.Phase == TerritoryTransferPhase.Prepared), "Resolve prior transfers and verify current ownership first.");
        var result = new TerritoryTransfer(id, subject.Id, subject.NativeOwner, observed.Owner, target, charterId, purpose, now);
        transfers.Add(result); LastTick = now; return result;
    }
    public void Confirm(string id, TerritoryTransferObservation actual, long now)
    {
        ArgumentNullException.ThrowIfNull(actual); Time(now); var transfer = Transfer(id);
        Require(transfer.Phase == TerritoryTransferPhase.Prepared && actual.Territory == transfer.Territory && actual.Exists &&
            actual.Owner == transfer.TargetOwner && !actual.Player && !actual.HasMap && !actual.Visited,
            "Applied transfer needs actual intended nonplayer ownership and untouched-city proof.");
        transfers[transfers.IndexOf(transfer)] = transfer with { Phase = TerritoryTransferPhase.Applied, ResolvedTick = now }; LastTick = now;
    }
    public void CancelUnmoved(string id, TerritoryTransferObservation actual, long now)
    {
        ArgumentNullException.ThrowIfNull(actual); Time(now); var transfer = Transfer(id);
        Require(transfer.Phase == TerritoryTransferPhase.Prepared && actual.Territory == transfer.Territory && actual.Exists &&
            actual.Owner == transfer.FromOwner && !actual.Player, "Only verified unmoved ownership can cancel a transfer.");
        transfers[transfers.IndexOf(transfer)] = transfer with { Phase = TerritoryTransferPhase.Cancelled, ResolvedTick = now }; LastTick = now;
    }
    public string Export() => JsonSerializer.Serialize(new TerritoryTransfersSave(Version, LastTick, transfers.ToArray()));
    public static TerritoryTransferLedger Restore(string json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 4000000, "Invalid transfer snapshot.");
        var save = JsonSerializer.Deserialize<TerritoryTransfersSave>(json);
        Require(save != null && save.Version == Version && save.LastTick is >= 0 and <= int.MaxValue && save.Transfers != null && save.Transfers.Length <= Capacity,
            "Unsupported transfer snapshot.");
        var result = new TerritoryTransferLedger();
        foreach (var item in save.Transfers)
        {
            ArgumentNullException.ThrowIfNull(item);
            foreach (var key in new[] { item.Id, item.Territory, item.BaselineOwner, item.FromOwner, item.TargetOwner, item.Charter }) Key(key);
            Require(Enum.IsDefined(item.Purpose) && Enum.IsDefined(item.Phase) && item.FromOwner != item.TargetOwner &&
                item.PreparedTick >= result.LastTick && item.PreparedTick <= save.LastTick &&
                (item.Phase == TerritoryTransferPhase.Prepared ? !item.ResolvedTick.HasValue : item.ResolvedTick >= item.PreparedTick && item.ResolvedTick <= save.LastTick) &&
                !result.transfers.Any(t => t.Id == item.Id || t.Phase == TerritoryTransferPhase.Prepared) &&
                result.ExpectedOwner(item.Territory, item.BaselineOwner) == item.FromOwner, "Invalid transfer history or owner chain.");
            result.transfers.Add(item); result.LastTick = item.ResolvedTick ?? item.PreparedTick;
        }
        result.LastTick = save.LastTick; return result;
    }
}
