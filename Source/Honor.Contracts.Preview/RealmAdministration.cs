using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

/// <summary>NPC owner for a sovereign player's vassal towns; mirrors player diplomacy without owning player pawns or bases.</summary>
public sealed class RealmAdministrationLedger : GameComponent
{
    public const string DefName = "HonorContracts_RealmAdministration";
    private Faction faction;
    private string nativePlayer;
    private long createdTick;
    private bool initialized, syncing, routing;
    public bool Suspended;
    public bool RestoreFailed { get; private set; }
    public RealmAdministrationLedger(Game game) { }
    public Faction OwnedFaction => !RestoreFailed && faction != null && faction.def.defName == DefName && !faction.IsPlayer &&
        nativePlayer == Faction.OfPlayer.GetUniqueLoadID() && Find.FactionManager.AllFactionsListForReading.Contains(faction) &&
        Find.FactionManager.AllFactionsListForReading.Count(f => f.def.defName == DefName) == 1 ? faction : null;
    public override void ExposeData()
    {
        Scribe_References.Look(ref faction, "honorRealmAdministration");
        Scribe_Values.Look(ref nativePlayer, "honorAdministrationPlayer");
        Scribe_Values.Look(ref createdTick, "honorAdministrationCreatedTick");
        Scribe_Values.Look(ref initialized, "honorAdministrationInitialized");
        Scribe_Values.Look(ref Suspended, "honorAdministrationSuspended");
    }
    public override void FinalizeInit()
    {
        if (faction == null && (nativePlayer != null || initialized) || faction != null &&
            (OwnedFaction == null || createdTick < 0 || createdTick > Find.TickManager.TicksGame))
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Administration identity/time mismatch; operations blocked."); }
    }
    public override void GameComponentTick() { if (Find.TickManager.TicksGame % 250 == 0) Synchronize(); }
    private void Suspend()
    {
        if (Suspended) return;
        Suspended = true; Messages.Message("AD_Suspended".Translate(), MessageTypeDefOf.NeutralEvent); Find.TickManager.Pause();
    }
    private void Validate()
    {
        var player = Faction.OfPlayer;
        if (OwnedFaction == null || faction.defeated || faction.Hidden || !initialized || faction.HostileTo(player) ||
            Current.Game.GetComponent<PlayerPoliticsLedger>().RestoreFailed || Current.Game.GetComponent<PlayerPoliticsLedger>().State.Suspended ||
            Find.GoodwillSituationManager.GetMaxGoodwill(faction) < faction.BaseGoodwillWith(player))
            throw new ArgumentException("Administration is unavailable or conflicts with player diplomacy.");
    }
    internal Faction TerritoryAuthority()
    {
        Synchronize(); Validate();
        if (Suspended) throw new ArgumentException("Administration sharing is suspended.");
        return faction;
    }
    public void CreateOrResume()
    {
        if (RestoreFailed || Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges) { Reject(); return; }
        try
        {
            var politics = Current.Game.GetComponent<PlayerPoliticsLedger>();
            if (politics.RestoreFailed || Current.Game.GetComponent<LiveCharterLedger>().RestoreFailed ||
                !Find.WorldObjects.AllWorldObjects.OfType<RimWorld.Planet.Settlement>().Any(s => !s.Destroyed && s.Faction == Faction.OfPlayer))
                throw new ArgumentException("A valid player colony is required.");
            if (faction == null)
            {
                if (politics.State.Mode != PlayerAllegianceMode.Independent || Find.FactionManager.AllFactionsListForReading.Any(f => f.def.defName == DefName))
                    throw new ArgumentException("Only an independent player may establish a unique administration.");
                nativePlayer = Faction.OfPlayer.GetUniqueLoadID(); createdTick = Find.TickManager.TicksGame;
                faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(DefDatabase<FactionDef>.GetNamed(DefName), hidden: true));
                faction.hidden = false; Find.FactionManager.Add(faction);
            }
            if (OwnedFaction == null || faction.defeated) throw new ArgumentException("Cannot replace or resurrect an administration.");
            if (!initialized)
            {
                if (Find.GoodwillSituationManager.GetMaxGoodwill(faction) < 100 || QuestUtility.IsGoodwillLockedByQuest(Faction.OfPlayer, faction))
                    throw new ArgumentException("Initial internal alliance is capped or quest-locked.");
                syncing = true;
                try { PlayerPoliticsLedger.Pair(Faction.OfPlayer, faction, 100, FactionRelationKind.Ally); initialized = true; }
                finally { syncing = false; }
            }
            Validate(); Suspended = false; Synchronize();
        }
        catch (ArgumentException) { if (faction != null) Suspend(); Reject(); }
    }
    public void Synchronize()
    {
        if (Current.ProgramState != ProgramState.Playing || syncing || routing || faction == null || RestoreFailed || Suspended) return;
        var towns = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        if (CharterProtectionLedger.PreserveOriginalPawns || towns.RestoreFailed || towns.HasPendingTownLosses || Current.Game.GetComponent<LeaderJoinLedger>().BlocksChanges) return;
        try
        {
            Validate(); var player = Faction.OfPlayer;
            var others = Find.FactionManager.AllFactionsListForReading.Where(f => f != player && f != faction && !f.IsPlayer).ToArray();
            var source = others.Select(other => {
                var p = player.RelationWith(other, true); var a = faction.RelationWith(other, true);
                if (p == null || a == null || other.RelationWith(faction, true) == null || !RuntimeCompat.IsDefined(p.kind) ||
                    !faction.CanChangeGoodwillFor(other, 0) && (a.baseGoodwill != p.baseGoodwill || a.kind != p.kind))
                    throw new ArgumentException("Cannot bypass an administration quest/permanent relation lock.");
                return new SharedRelation(other.GetUniqueLoadID(), p.baseGoodwill, p.kind switch {
                    FactionRelationKind.Ally => SharedRelationKind.Ally, FactionRelationKind.Hostile => SharedRelationKind.Hostile, _ => SharedRelationKind.Neutral });
            }).ToArray();
            var plan = SharedDiplomacyPlan.Build(faction.GetUniqueLoadID(), player.GetUniqueLoadID(), source);
            syncing = true;
            foreach (var r in plan)
            {
                if (Suspended) break;
                var other = others.Single(f => f.GetUniqueLoadID() == r.Other);
                PlayerPoliticsLedger.Pair(faction, other, r.BaseGoodwill, r.Kind switch {
                    SharedRelationKind.Ally => FactionRelationKind.Ally, SharedRelationKind.Hostile => FactionRelationKind.Hostile, _ => FactionRelationKind.Neutral });
            }
        }
        catch (ArgumentException) { Suspend(); }
        finally { syncing = false; }
    }
    internal bool Route(Faction actor, Faction other, int delta, bool message, bool letter, HistoryEventDef reason,
        RimWorld.Planet.GlobalTargetInfo? target, out bool result)
    {
        result = false;
        if (Current.ProgramState != ProgramState.Playing || routing || RestoreFailed || Suspended || !initialized ||
            faction == null || actor != faction && other != faction || actor == Faction.OfPlayer || other == Faction.OfPlayer) return false;
        var towns = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        if (CharterProtectionLedger.PreserveOriginalPawns || towns.RestoreFailed || towns.HasPendingTownLosses || Current.Game.GetComponent<LeaderJoinLedger>().BlocksChanges) return false;
        try { Validate(); } catch (ArgumentException) { Suspend(); return false; }
        if (!actor.CanChangeGoodwillFor(other, delta)) return true;
        routing = true;
        try { result = actor == faction ? Faction.OfPlayer.TryAffectGoodwillWith(other, delta, message, letter, reason, target)
            : actor.TryAffectGoodwillWith(Faction.OfPlayer, delta, message, letter, reason, target); }
        finally { routing = false; }
        return true;
    }
    private static void Reject() => Messages.Message("AD_Unavailable".Translate(), MessageTypeDefOf.RejectInput);
}

[HarmonyPatch(typeof(Faction), nameof(Faction.TryAffectGoodwillWith))]
internal static class AdministrationGoodwill
{
    [HarmonyPriority(Priority.First)]
    static bool Prefix(Faction __instance, Faction other, int goodwillChange, bool canSendMessage, bool canSendHostilityLetter,
        HistoryEventDef reason, RimWorld.Planet.GlobalTargetInfo? lookTarget, ref bool __result)
    {
        var ledger = Current.Game?.GetComponent<RealmAdministrationLedger>();
        if (ledger == null || !ledger.Route(__instance, other, goodwillChange, canSendMessage, canSendHostilityLetter, reason, lookTarget, out var result)) return true;
        __result = result; return false;
    }
    static void Postfix() => Current.Game?.GetComponent<RealmAdministrationLedger>()?.Synchronize();
}
