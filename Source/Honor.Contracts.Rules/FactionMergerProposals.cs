using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record MergerTown(string Territory, string Charter);
public sealed record FactionMergerScope(string Source, string Target, string Player, string Representative, string SourceLabel, string TargetLabel,
    string RepresentativeLabel, string Book, string Transfers, IReadOnlyList<MergerTown> Towns, IReadOnlyList<string> NativeWorldObjects, IReadOnlyList<string> NativePawns, string Diplomacy,
    int Goodwill, bool MutualAlly, bool SameExternalWars);
public enum FactionMergerResponse { Accepted, Relationship, InternalAlliance, ExternalWars }
public enum FactionMergerPhase { NpcAccepted, NpcDeclined, PlayerConfirmed, PlayerDeclined }
public sealed record FactionMergerProposal(string Id, FactionMergerScope Scope, long OfferedTick, long ExpiresTick, FactionMergerResponse Response,
    FactionMergerPhase Phase, long? PlayerTick = null);
public sealed record FactionMergerProposalSave(int Version, long LastTick, FactionMergerProposal[] Proposals);

/// <summary>Separate political merger consent only. It grants no city/pawn/leader transfer or last-base exception.</summary>
public sealed class FactionMergerProposals
{
    public const int Capacity = 128;
    private readonly List<FactionMergerProposal> proposals = [];
    public long LastTick { get; private set; }
    public IReadOnlyList<FactionMergerProposal> Proposals => proposals.AsReadOnly();
    private static bool Key(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 120 && !id.Any(char.IsControl);
    private static bool Label(string label) => !string.IsNullOrWhiteSpace(label) && label.Length <= 200;
    private void Time(long now) { if (now < LastTick || now > int.MaxValue) throw new ArgumentException("Invalid merger proposal clock."); }
    public static void ValidateScope(FactionMergerScope s)
    {
        if (s == null || !Key(s.Source) || !Key(s.Target) || !Key(s.Player) || !Key(s.Representative) ||
            s.Source == s.Target || s.Source == s.Player || s.Target == s.Player || !Label(s.SourceLabel) || !Label(s.TargetLabel) || !Label(s.RepresentativeLabel) ||
            !CityStockProof.Valid(s.Diplomacy) || s.Goodwill is < -100 or > 100 || s.Towns == null || s.Towns.Count is < 1 or > 128 ||
            s.Towns.Any(t => t == null || !Key(t.Territory) || !Key(t.Charter)) || s.Towns.Select(t => t.Territory).Distinct(StringComparer.Ordinal).Count() != s.Towns.Count ||
            s.Towns.Select(t => t.Charter).Distinct(StringComparer.Ordinal).Count() != s.Towns.Count ||
            s.NativePawns == null || s.NativePawns.Count is < 1 or > 4000 || s.NativePawns.Any(id => !Key(id)) ||
            s.NativePawns.Distinct(StringComparer.Ordinal).Count() != s.NativePawns.Count || !s.NativePawns.Contains(s.Representative))
            throw new ArgumentException("Invalid whole-faction merger proposal scope.");
        if (s.NativeWorldObjects == null || s.NativeWorldObjects.Count is < 1 or > 4000 || s.NativeWorldObjects.Any(id => !Key(id)) ||
            s.NativeWorldObjects.Distinct(StringComparer.Ordinal).Count() != s.NativeWorldObjects.Count || s.Towns.Any(t => !t.Territory.StartsWith("territory/", StringComparison.Ordinal)) ||
            s.Towns.Any(t => !s.NativeWorldObjects.Contains(t.Territory.Substring("territory/".Length))))
            throw new ArgumentException("Whole-faction world scope must include every actual source town.");
        var book = CharterBook.Restore(s.Book); var transfers = TerritoryTransferLedger.Restore(s.Transfers);
        if (transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) || book.Entities.Count(e => e.Player && e.NativeOwner == s.Player) != 1)
            throw new ArgumentException("Player identity and resolved prior physical journals required.");
        foreach (var town in s.Towns)
        {
            var c = book.Charter(town.Charter); var entity = book.Entity(town.Territory);
            if (c.Phase != RecordedCharterPhase.Active || c.PlayerReleaseTick.HasValue || c.NpcReleaseTick.HasValue || !book.PlayerManaged(c) || c.Terms.Subject != town.Territory || entity.Player ||
                !transfers.VerifiedOwner(entity.Id, entity.NativeOwner, s.Source))
                throw new ArgumentException("Each actual source town needs its own current managed charter and verified original ownership.");
        }
    }
    public static FactionMergerResponse Assess(FactionMergerScope scope)
    {
        ValidateScope(scope);
        return scope.Goodwill < 75 ? FactionMergerResponse.Relationship : !scope.MutualAlly ? FactionMergerResponse.InternalAlliance :
            !scope.SameExternalWars ? FactionMergerResponse.ExternalWars : FactionMergerResponse.Accepted;
    }
    public bool HasOpen(long now) => proposals.Any(p => p.Response == FactionMergerResponse.Accepted && p.OfferedTick <= now && now <= p.ExpiresTick && (!p.PlayerTick.HasValue || p.PlayerTick > now));
    private static void CurrentOffers(FactionMergerScope scope, long now)
    {
        var book = CharterBook.Restore(scope.Book);
        if (book.Charters.Any(c => book.Phase(c, now) == RecordedCharterPhase.Offered && scope.Towns.Any(t => t.Territory == c.Terms.Subject || t.Territory == c.Terms.Liege)))
            throw new ArgumentException("Resolve live touching city proposals before whole-faction merger consent.");
    }
    public void Offer(string id, FactionMergerScope scope, long now)
    {
        Time(now); var response = Assess(scope); CurrentOffers(scope, now); var book = CharterBook.Restore(scope.Book); var transfers = TerritoryTransferLedger.Restore(scope.Transfers);
        if (!Key(id) || proposals.Count >= Capacity || proposals.Any(p => p.Id == id) || HasOpen(now) || now < book.LastTick || now < transfers.LastTick ||
            now > int.MaxValue - PoliticalWorld.Day * 3) throw new ArgumentException("Resolve another offer, clock or proposal capacity first.");
        var row = new FactionMergerProposal(id, Freeze(scope), now, now + PoliticalWorld.Day * 3,
            response, response == FactionMergerResponse.Accepted ? FactionMergerPhase.NpcAccepted : FactionMergerPhase.NpcDeclined);
        var copy = Restore(Export()); copy.proposals.Add(row); copy.LastTick = now;
        if (copy.Export().Length > 32000000) throw new ArgumentException("Merger proposal archive capacity.");
        proposals.Add(row); LastTick = now;
    }
    public void Confirm(string id, FactionMergerScope actual, long now)
    {
        Time(now); var row = proposals.SingleOrDefault(p => p.Id == id); var response = Assess(actual); CurrentOffers(actual, now);
        if (row == null || row.Phase != FactionMergerPhase.NpcAccepted || now > row.ExpiresTick || response != FactionMergerResponse.Accepted ||
            JsonSerializer.Serialize(row.Scope) != JsonSerializer.Serialize(actual)) throw new ArgumentException("Re-review the exact current separate merger scope and representative.");
        proposals[proposals.IndexOf(row)] = row with { Phase = FactionMergerPhase.PlayerConfirmed, PlayerTick = now }; LastTick = now;
    }
    public void Decline(string id, long now)
    {
        Time(now); var row = proposals.SingleOrDefault(p => p.Id == id);
        if (row?.Phase != FactionMergerPhase.NpcAccepted) throw new ArgumentException("Only an unanswered NPC merger proposal may be declined.");
        proposals[proposals.IndexOf(row)] = row with { Phase = FactionMergerPhase.PlayerDeclined, PlayerTick = now }; LastTick = now;
    }
    private static FactionMergerScope Freeze(FactionMergerScope s) => s with { Towns = Array.AsReadOnly(s.Towns.ToArray()), NativeWorldObjects = Array.AsReadOnly(s.NativeWorldObjects.ToArray()), NativePawns = Array.AsReadOnly(s.NativePawns.ToArray()) };
    public string Export() => JsonSerializer.Serialize(new FactionMergerProposalSave(1, LastTick, proposals.ToArray()));
    public static FactionMergerProposals Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32000000) throw new ArgumentException("Invalid merger proposal archive size.");
        var save = JsonSerializer.Deserialize<FactionMergerProposalSave>(json);
        if (save == null || save.Version != 1 || save.LastTick is < 0 or > int.MaxValue || save.Proposals == null || save.Proposals.Length > Capacity)
            throw new ArgumentException("Invalid merger proposal archive.");
        var result = new FactionMergerProposals(); long last = 0;
        foreach (var p in save.Proposals)
        {
            if (p == null || !Key(p.Id) || result.proposals.Any(old => old.Id == p.Id) || !RuntimeCompat.IsDefined(p.Phase) || !RuntimeCompat.IsDefined(p.Response))
                throw new ArgumentException("Invalid merger proposal identity/phase.");
            var response = Assess(p.Scope); CurrentOffers(p.Scope, p.OfferedTick); var book = CharterBook.Restore(p.Scope.Book); var transfers = TerritoryTransferLedger.Restore(p.Scope.Transfers);
            bool answered = p.Phase is FactionMergerPhase.PlayerConfirmed or FactionMergerPhase.PlayerDeclined;
            if (response != p.Response || (response == FactionMergerResponse.Accepted) != (p.Phase != FactionMergerPhase.NpcDeclined) ||
                p.OfferedTick < last || p.OfferedTick < book.LastTick || p.OfferedTick < transfers.LastTick || p.OfferedTick > save.LastTick ||
                p.OfferedTick > int.MaxValue - PoliticalWorld.Day * 3 || p.ExpiresTick != p.OfferedTick + PoliticalWorld.Day * 3 ||
                answered != p.PlayerTick.HasValue || p.PlayerTick.HasValue && (p.PlayerTick < p.OfferedTick || p.PlayerTick > save.LastTick ||
                    p.Phase == FactionMergerPhase.PlayerConfirmed && p.PlayerTick > p.ExpiresTick) || result.HasOpen(p.OfferedTick))
                throw new ArgumentException("Invalid merger proposal response, scope or clock history.");
            result.proposals.Add(p with { Scope = Freeze(p.Scope) }); last = p.OfferedTick;
        }
        result.LastTick = save.LastTick; return result;
    }
}
