using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

internal sealed record CharterLossCandidate(string Charter, string Territory, string Baseline, string Owner, string Book, long Tick, string Label);

public sealed partial class LiveCharterLedger
{
    private bool LossChangesAvailable()
    {
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        return !RestoreFailed && !Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges && !payments.RestoreFailed &&
            !payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared);
    }
    internal CharterLossCandidate ReviewNativeLoss(Settlement city)
    {
        if (!LossChangesAvailable() || city == null || city.GetType() != typeof(Settlement) || city.Destroyed || city.Faction == null || city.Faction.IsPlayer ||
            !Find.WorldObjects.AllWorldObjects.Contains(city)) return null;
        var binding = bindings.SingleOrDefault(b => !b.PlayerGroup && !b.FactionGroup && b.Settlement == city);
        if (binding == null) return null;
        var c = Book.Charters.SingleOrDefault(c => c.Phase == RecordedCharterPhase.Active && c.Terms.Subject == binding.Identity);
        if (c == null || Book.Charters.Any(other => other.Phase == RecordedCharterPhase.Active && other.Terms.Liege == binding.Identity ||
            Book.Phase(other, Find.TickManager.TicksGame) == RecordedCharterPhase.Offered && (other.Terms.Liege == binding.Identity || other.Terms.Subject == binding.Identity))) return null;
        var entity = Book.Entity(binding.Identity); var territories = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        if (entity.Player || territories.RestoreFailed || !territories.Ledger.VerifiedOwner(entity.Id, entity.NativeOwner, city.Faction.GetUniqueLoadID())) return null;
        return new(c.Id, entity.Id, entity.NativeOwner, city.Faction.GetUniqueLoadID(), Book.Export(), Find.TickManager.TicksGame, city.Label);
    }
    internal void ApplyNativeLoss(Settlement city, CharterLossCandidate before)
    {
        if (before == null || !LossChangesAvailable() || city == null || !city.Destroyed || Find.WorldObjects.AllWorldObjects.Contains(city) ||
            city.Faction?.GetUniqueLoadID() != before.Owner || "territory/" + city.GetUniqueLoadID() != before.Territory ||
            Binding(before.Territory).Settlement != city || Book.Export() != before.Book) return;
        var replacement = CharterBook.Restore(Book.Export());
        replacement.CloseDestroyedLeaf(before.Charter, before.Territory, before.Baseline, before.Tick, Find.TickManager.TicksGame, true);
        Book = replacement;
        Binding(before.Territory).LastKnownLabel = before.Label;
        Messages.Message("CL_Ended".Translate(before.Label), MessageTypeDefOf.NeutralEvent);
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
