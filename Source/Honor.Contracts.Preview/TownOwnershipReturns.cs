using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

internal sealed record NativeOwnershipReturn(Game Context, Settlement City, Faction From, Faction To, string Root,
    string Book, string Losses, long Tick, long Epoch);

public sealed partial class LiveCharterLedger
{
    private string ownershipReturnSnapshot;
    private TownOwnershipReturns ownershipReturns = new();
    private long ownershipAttemptEpoch;
    private static bool ExactReturnFaction(Faction faction) => faction != null && faction.GetType() == typeof(Faction) &&
        Find.FactionManager.AllFactionsListForReading.Count(f => f != null && f.GetUniqueLoadID() == faction.GetUniqueLoadID()) == 1 &&
        Find.FactionManager.AllFactionsListForReading.Contains(faction);
    private void ValidateOwnershipReturnClock()
    {
        if (!RestoreFailed && ownershipReturns.Records.Any(r => r.ObservedTick > Find.TickManager.TicksGame || r.InvalidatedTick > Find.TickManager.TicksGame))
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Ownership-return clock is invalid; original data retained."); }
    }
    private void ExposeOwnershipReturns()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) ownershipReturnSnapshot = ownershipReturns.Export();
        Scribe_Values.Look(ref ownershipReturnSnapshot, "honorTownOwnershipReturnsV1");
    }
    private void ValidateOwnershipReturns()
    {
        if (ownershipReturnSnapshot != null) ownershipReturns = TownOwnershipReturns.Restore(ownershipReturnSnapshot);
        ownershipReturns.ValidatePair(townLosses, Book);
    }
    internal NativeOwnershipReturn BeginOwnershipReturn(Settlement city, Faction target)
    {
        ownershipAttemptEpoch = checked(ownershipAttemptEpoch + 1);
        if (RestoreFailed || city == null || city.GetType() != typeof(Settlement)) return null;
        string territory = "territory/" + city.GetUniqueLoadID();
        // An attempted later ownership call retires the old receipt BEFORE native code. An exception
        // cannot silently make a previous positive observation reusable. This is not proof the call succeeded.
        foreach (var r in ownershipReturns.Records.Where(r => r.Territory == territory && r.InvalidatedTick == null).ToArray())
            if (townLosses.Record(r.Root).Resolution == null)
                ownershipReturns = ownershipReturns.Invalidate(r.Root, Find.TickManager.TicksGame, target?.GetUniqueLoadID());
        if (!LossChangesAvailable() || UnionWarCouncil.HasExternalProvider || city.Destroyed || !Find.WorldObjects.AllWorldObjects.Contains(city) ||
            city.Faction == null || target == null || target == city.Faction || target.IsPlayer || city.Faction.IsPlayer || target.def == null || city.Faction.def == null ||
            target.Hidden || city.Faction.Hidden || target.temporary || city.Faction.temporary || target.defeated || city.Faction.defeated ||
            !ExactReturnFaction(target) || !ExactReturnFaction(city.Faction) ||
            !target.HostileTo(city.Faction) || !city.Faction.HostileTo(target)) return null;
        var p = townLosses.Records.SingleOrDefault(p => p.Territory == territory && p.Resolution == null &&
            p.Kind == TownLossKind.HostileNativeOwnershipChange && p.ScopeRoot == null && p.PriorScopes == null);
        if (p == null || p.OwnerAtLoss != p.BaselineOwner || p.CapturingOwner != city.Faction.GetUniqueLoadID() || p.OwnerAtLoss != target.GetUniqueLoadID() ||
            townLosses.ScopeLosses(p.Root).Length != 1 || ownershipReturns.Records.Any(r => r.Root == p.Root) ||
            ownershipReturns.Records.Count >= TownOwnershipReturns.Capacity || Binding(territory).Settlement != city) return null;
        return new(Current.Game, city, city.Faction, target, p.Root, Book.Export(), townLosses.Export(), Find.TickManager.TicksGame, ownershipAttemptEpoch);
    }
    internal void ObserveOwnershipReturn(Settlement city, NativeOwnershipReturn before)
    {
        if (before == null || before.Context != Current.Game || before.City != city || before.Epoch != ownershipAttemptEpoch || !LossChangesAvailable() ||
            UnionWarCouncil.HasExternalProvider || city == null || city.Destroyed || !Find.WorldObjects.AllWorldObjects.Contains(city) || city.Faction != before.To ||
            Book.Export() != before.Book || townLosses.Export() != before.Losses || !ExactReturnFaction(before.From) ||
            !ExactReturnFaction(before.To) || before.From.Hidden || before.To.Hidden || before.From.temporary || before.To.temporary ||
            before.From.defeated || before.To.defeated || !before.From.HostileTo(before.To) || !before.To.HostileTo(before.From)) return;
        var p = townLosses.Record(before.Root);
        if (Binding(p.Territory).Settlement != city || p.Territory != "territory/" + city.GetUniqueLoadID()) return;
        ownershipReturns = ownershipReturns.Observe(new(p.Root, p.Territory, before.From.GetUniqueLoadID(), before.To.GetUniqueLoadID(),
            TownOwnershipReturns.LossDigest(p), before.Tick, Find.TickManager.TicksGame), townLosses, Book);
        Messages.Message("OR_Observed".Translate(city.Label), MessageTypeDefOf.NeutralEvent);
    }
}

[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.SetFaction), new Type[] { typeof(Faction) })]
internal static class NativeTownOwnershipReturn
{
    static void Prefix(WorldObject __instance, Faction newFaction, out NativeOwnershipReturn __state)
    {
        __state = null;
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null || __instance is not Settlement city) return;
        try { __state = Current.Game.GetComponent<LiveCharterLedger>().BeginOwnershipReturn(city, newFaction); }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Ownership-return preflight unavailable; no return authority. " + e.GetType().Name); }
    }
    static void Postfix(WorldObject __instance, NativeOwnershipReturn __state)
    {
        if (__state == null || Current.ProgramState != ProgramState.Playing || Current.Game == null || __instance is not Settlement city) return;
        try { Current.Game.GetComponent<LiveCharterLedger>().ObserveOwnershipReturn(city, __state); }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Ownership return unavailable; original loss retained. " + e.GetType().Name); }
    }
}
