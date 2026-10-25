using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

internal sealed record CharterLossCandidate(string Charter, string Territory, string Baseline, string Owner, string Book, long Tick, string Label,
    Game Context, TownLossKind Kind = TownLossKind.Destroyed, string CapturingOwner = null, string ScopeRoot = null, int? ScopeOrdinal = null);

public sealed partial class LiveCharterLedger
{
    private bool LossChangesAvailable()
    {
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        return !RestoreFailed && !Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksApartFromTownLosses && !payments.RestoreFailed &&
            !payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared);
    }
    internal CharterLossCandidate ReviewNativeLoss(Settlement city)
    {
        if (!LossChangesAvailable() || city == null || city.GetType() != typeof(Settlement) || city.Destroyed || city.Faction == null || city.Faction.IsPlayer ||
            !Find.WorldObjects.AllWorldObjects.Contains(city)) return null;
        var binding = bindings.SingleOrDefault(b => !b.PlayerGroup && !b.FactionGroup && b.Settlement == city);
        if (binding == null) return null;
        var c = Book.Charters.SingleOrDefault(c => c.Phase == RecordedCharterPhase.Active && c.Terms.Subject == binding.Identity);
        if (c == null || Book.Charters.Any(other => Book.Phase(other, Find.TickManager.TicksGame) == RecordedCharterPhase.Offered &&
            (other.Terms.Liege == binding.Identity || other.Terms.Subject == binding.Identity))) return null;
        bool upper = Book.Charters.Any(other => other.Phase == RecordedCharterPhase.Active && other.Terms.Liege == binding.Identity);
        string scope = townLosses.ObservationScope(Book, c.Id, city.Faction.GetUniqueLoadID());
        if ((upper || scope != null) && (upper && !Book.PlayerManaged(c) && scope == null || townLosses.Records.Count >= TownSuccessionArchive.Capacity)) return null;
        var entity = Book.Entity(binding.Identity); var territories = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        if (entity.Player || territories.RestoreFailed || !territories.Ledger.VerifiedOwner(entity.Id, entity.NativeOwner, city.Faction.GetUniqueLoadID())) return null;
        return new(c.Id, entity.Id, entity.NativeOwner, city.Faction.GetUniqueLoadID(), Book.Export(), Find.TickManager.TicksGame, city.Label, Current.Game,
            ScopeRoot: scope, ScopeOrdinal: scope == null ? null : townLosses.NextScopeOrdinal(scope));
    }
    internal void ApplyNativeLoss(Settlement city, CharterLossCandidate before)
    {
        if (before == null || before.Context != Current.Game || before.Kind != TownLossKind.Destroyed || !LossChangesAvailable() || city == null || !city.Destroyed || Find.WorldObjects.AllWorldObjects.Contains(city) ||
            city.Faction?.GetUniqueLoadID() != before.Owner || "territory/" + city.GetUniqueLoadID() != before.Territory ||
            Binding(before.Territory).Settlement != city || Book.Export() != before.Book) return;
        CommitNativeLoss(before);
    }
    internal CharterLossCandidate ReviewHostileOwnershipLoss(Settlement city, Faction target)
    {
        if (UnionWarCouncil.HasExternalProvider) return null; // An authoritative provider needs its own confirmed outcome adapter.
        if (target == null || city?.Faction == null || city.Faction.Hidden || city.Faction.temporary || city.Faction.defeated ||
            !Find.FactionManager.AllFactionsListForReading.Contains(city.Faction) || target == city.Faction || target.IsPlayer || target.def == null || target.Hidden || target.temporary || target.defeated ||
            !Find.FactionManager.AllFactionsListForReading.Contains(target) || !city.Faction.HostileTo(target) || !target.HostileTo(city.Faction)) return null;
        var candidate = ReviewNativeLoss(city);
        return candidate == null ? null : candidate with { Kind = TownLossKind.HostileNativeOwnershipChange, CapturingOwner = target.GetUniqueLoadID() };
    }
    internal void ApplyHostileOwnershipLoss(Settlement city, CharterLossCandidate before)
    {
        if (before == null || before.Context != Current.Game || before.Kind != TownLossKind.HostileNativeOwnershipChange || !LossChangesAvailable() || UnionWarCouncil.HasExternalProvider ||
            city == null || city.Destroyed || !Find.WorldObjects.AllWorldObjects.Contains(city) || city.Faction?.GetUniqueLoadID() != before.CapturingOwner ||
            "territory/" + city.GetUniqueLoadID() != before.Territory || Binding(before.Territory).Settlement != city || Book.Export() != before.Book) return;
        var source = Find.FactionManager.AllFactionsListForReading.SingleOrDefault(f => f.GetUniqueLoadID() == before.Owner);
        if (source == null || !source.HostileTo(city.Faction) || !city.Faction.HostileTo(source)) return;
        CommitNativeLoss(before);
    }
    private void CommitNativeLoss(CharterLossCandidate before)
    {
        var replacement = CharterBook.Restore(Book.Export());
        bool upper = Book.Charters.Any(c => c.Phase == RecordedCharterPhase.Active && c.Terms.Liege == before.Territory);
        if (upper || before.ScopeRoot != null)
        {
            var proof = new TownLossProof(before.Charter, before.Territory, before.Baseline, before.Owner,
                ConsentLabel(before.Label, before.Territory), before.Tick, Find.TickManager.TicksGame, before.Book,
                Kind: before.Kind, CapturingOwner: before.CapturingOwner, ScopeRoot: before.ScopeRoot, ScopeOrdinal: before.ScopeOrdinal);
            replacement = townLosses.CloseForObservation(proof);
            townLosses = townLosses.Observe(proof, replacement);
        }
        else if (before.Kind == TownLossKind.Destroyed)
            replacement.CloseDestroyedTown(before.Charter, before.Territory, before.Baseline, before.Tick, Find.TickManager.TicksGame, true, upper);
        else replacement.CloseHostileTransferredTown(before.Charter, before.Territory, before.Baseline, before.Owner, before.CapturingOwner,
            before.Tick, Find.TickManager.TicksGame, true, upper);
        Book = replacement;
        Binding(before.Territory).LastKnownLabel = before.Label;
        Messages.Message((before.ScopeRoot != null ? "LC_Observed" : before.Kind == TownLossKind.HostileNativeOwnershipChange ? "HL_Observed" : upper ? "LS_Observed" : "CL_Ended").Translate(before.Label), MessageTypeDefOf.NeutralEvent);
    }
}

// Only a normally completed native change with reciprocal hostility before and after qualifies.
// It proves lost ownership, not a battle, conquest charter, automatic independence or the external actor's intentions.
[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.SetFaction), new Type[] { typeof(Faction) })]
internal static class NativeHostileTownOwnershipLoss
{
    static void Prefix(WorldObject __instance, Faction newFaction, out CharterLossCandidate __state)
    {
        __state = null;
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null || __instance is not Settlement city) return;
        try { __state = Current.Game.GetComponent<LiveCharterLedger>().ReviewHostileOwnershipLoss(city, newFaction); }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Native hostile ownership preflight unavailable; records retained. " + e.GetType().Name); }
    }
    static void Postfix(WorldObject __instance, CharterLossCandidate __state)
    {
        if (__state == null || Current.ProgramState != ProgramState.Playing || Current.Game == null || __instance is not Settlement city) return;
        try { Current.Game.GetComponent<LiveCharterLedger>().ApplyHostileOwnershipLoss(city, __state); }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Native hostile ownership closure unavailable; records retained. " + e.GetType().Name); }
    }
}

// Observe real, successfully completed native destruction only. Missing references/foreign occupation are not destruction proof.
[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Destroy), new Type[] { })]
internal static class NativeTownLoss
{
    static void Prefix(WorldObject __instance, out CharterLossCandidate __state)
    {
        __state = null;
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null || __instance is not Settlement city) return;
        try { __state = Current.Game.GetComponent<LiveCharterLedger>().ReviewNativeLoss(city); }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Native town loss preflight unavailable; contract records retained. " + e.GetType().Name); }
    }
    static void Postfix(WorldObject __instance, CharterLossCandidate __state)
    {
        if (__state == null || Current.ProgramState != ProgramState.Playing || Current.Game == null || __instance is not Settlement city) return;
        try { Current.Game.GetComponent<LiveCharterLedger>().ApplyNativeLoss(city, __state); }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Native town loss closure unavailable; contract records retained. " + e.GetType().Name); }
    }
}
