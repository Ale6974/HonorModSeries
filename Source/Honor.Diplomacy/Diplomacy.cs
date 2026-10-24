using HarmonyLib;
using Honor.Core;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;
using Verse.AI;

namespace Honor.Diplomacy;

public sealed class DiplomacyMod : Mod
{
    public DiplomacyMod(ModContentPack content) : base(content)
    {
        new Harmony("honor.series.diplomacy").PatchAll();
        HonorApi.AdditionalParticipantValidators.Add(p => p.Map?.Parent is HonorDelegation site && !site.Resolved && site.Representative == p);
        HonorApi.DuelEnded += d => {
            if (d.Challenger?.Map?.Parent is HonorDelegation site && d.Contains(site.Representative))
                site.Resolve(d.Winner != null && d.Winner != site.Representative && !d.DishonorApplied);
        };
    }
}
public static class DiplomacyApi
{
    public static HonorDelegation Create(Settlement settlement)
    {
        if (settlement?.Faction == null || !settlement.Faction.HasGoodwill || settlement.Faction == Faction.OfPlayer || settlement.Faction.HostileTo(Faction.OfPlayer) ||
            Current.Game.GetComponent<DelegationLedger>().Busy(settlement.Faction) ||
            Find.WorldObjects.AllWorldObjects.OfType<HonorDelegation>().Any(s => s.Faction == settlement.Faction &&
                (!s.Resolved || Find.TickManager.TicksGame < s.Deadline))) return null;
        if (!TileFinder.TryFindNewSiteTile(out var tile, settlement.Tile, minDist: 2, maxDist: 8)) return null;
        var slate = new Slate(); slate.Set("honorFaction", settlement.Faction); slate.Set("honorTile", tile);
        var quest = QuestUtility.GenerateQuestAndMakeAvailable(DefDatabase<QuestScriptDef>.GetNamed("HonorDiplomacy_Delegation"), slate);
        quest.name = "HD_Quest".Translate(settlement.Faction.Name);
        quest.description = "HD_QuestDesc".Translate(settlement.Faction.Name);
        var site = Find.WorldObjects.AllWorldObjects.OfType<HonorDelegation>().Single(s => s.Quest == quest);
        Current.Game.GetComponent<DelegationLedger>().Reserve(settlement.Faction, site.Deadline);
        QuestUtility.SendLetterQuestAvailable(quest); return site;
    }
}
public sealed class DelegationCooldown : IExposable
{
    public Faction Faction; public int Until;
    public void ExposeData() { Scribe_References.Look(ref Faction, "faction"); Scribe_Values.Look(ref Until, "until"); }
}
public sealed class DelegationLedger : GameComponent
{
    public List<DelegationCooldown> Cooldowns = [];
    public DelegationLedger(Game game) { }
    public bool Busy(Faction faction) => Cooldowns.Any(c => c.Faction == faction && c.Until > Find.TickManager.TicksGame);
    public void Reserve(Faction faction, int until) { Cooldowns.RemoveAll(c => c.Faction == faction); Cooldowns.Add(new() { Faction = faction, Until = until }); }
    public override void ExposeData() { Scribe_Collections.Look(ref Cooldowns, "honorDelegationCooldowns", LookMode.Deep); if (Scribe.mode == LoadSaveMode.PostLoadInit) Cooldowns ??= []; }
}
public sealed class QuestNode_HonorDelegation : QuestNode
{
    protected override bool TestRunInt(Slate slate) => true;
    protected override void RunInt()
    {
        var site = (HonorDelegation)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("HonorDiplomacy_Delegation"));
        site.Tile = QuestGen.slate.Get<PlanetTile>("honorTile"); site.SetFaction(QuestGen.slate.Get<Faction>("honorFaction"));
        site.Deadline = Find.TickManager.TicksGame + 600000; site.Quest = QuestGen.quest;
        Find.WorldObjects.Add(site); QuestGen.quest.AddPart(new QuestPart_HonorDelegation { Site = site });
    }
}
public sealed class QuestPart_HonorDelegation : QuestPart
{
    public HonorDelegation Site;
    public override IEnumerable<GlobalTargetInfo> QuestLookTargets { get { if (Site != null) yield return Site; } }
    public override IEnumerable<Faction> InvolvedFactions { get { if (Site?.Faction != null) yield return Site.Faction; } }
    public override void ExposeData() { base.ExposeData(); Scribe_References.Look(ref Site, "honorSite"); }
}
public sealed class HonorDelegation : MapParent
{
    public Pawn Representative;
    public Quest Quest;
    public int Deadline;
    public bool Resolved;
    public override AcceptanceReport CanBeSettled => false;
    public override void ExposeData()
    {
        base.ExposeData(); Scribe_References.Look(ref Representative, "honorRepresentative"); Scribe_References.Look(ref Quest, "honorQuest");
        Scribe_Values.Look(ref Deadline, "honorDeadline"); Scribe_Values.Look(ref Resolved, "honorResolved");
    }
    public override void PostMapGenerate()
    {
        base.PostMapGenerate(); var center = Map.Center;
        foreach (var cell in GenRadial.RadialCellsAround(center, 8, true).Where(c => c.InBounds(Map)))
        {
            foreach (var thing in cell.GetThingList(Map).ToList())
                if (thing.def.category is ThingCategory.Building or ThingCategory.Plant) thing.DeSpawn();
            Map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil); Map.roofGrid.SetRoof(cell, null);
        }
        Map.GetComponent<DuelMapComponent>().Arena = center;
        for (int i = 0; i < 30; i++)
        {
            var candidate = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction);
            if (candidate.ageTracker.AgeBiologicalYears < 18 || candidate.WorkTagIsDisabled(WorkTags.Violent) || candidate.health.HasHediffsNeedingTend())
            { candidate.Destroy(); continue; }
            Representative = candidate; break;
        }
        if (Representative == null) { Resolve(false); return; }
        GenSpawn.Spawn(Representative, center + new IntVec3(2, 0, 0), Map);
        Representative.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 600000), JobCondition.InterruptForced);
    }
    public bool Begin(Pawn champion)
    {
        if (Resolved || champion?.Map != Map || champion.Faction != Faction.OfPlayer || !DuelMapComponent.Eligible(champion) ||
            Representative == null || Representative.Dead || Representative.Downed || champion.equipment.Primary == null) return false;
        var component = Map.GetComponent<DuelMapComponent>();
        if (!component.CanRegister(champion, Representative)) return false;
        var original = champion.equipment.Primary;
        var copy = (ThingWithComps)ThingMaker.MakeThing(original.def, original.Stuff);
        original.TryGetQuality(out var quality); copy.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Colony);
        copy.HitPoints = original.HitPoints;
        Representative.equipment.DestroyAllEquipment(); Representative.equipment.AddEquipment(copy);
        if (!DuelMapComponent.WeaponsMatch(champion, Representative, out _)) return false;
        component.RegisterInsult(Representative, champion); return true;
    }
    public void Resolve(bool victory)
    {
        if (Resolved) return; Resolved = true; forceRemoveWorldObjectWhenMapRemoved = true;
        Faction?.TryAffectGoodwillWith(Faction.OfPlayer, victory ? 10 : -5);
        if (victory && Map != null)
        {
            var silver = ThingMaker.MakeThing(ThingDefOf.Silver); silver.stackCount = 100;
            GenPlace.TryPlaceThing(silver, Map.Center, Map, ThingPlaceMode.Near);
        }
        Quest?.End(victory ? QuestEndOutcome.Success : QuestEndOutcome.Fail);
        Messages.Message((victory ? "HD_Won" : "HD_Lost").Translate(), MessageTypeDefOf.NeutralEvent);
        if (!HasMap) Destroy();
    }
    protected override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (!Resolved && Find.TickManager.TicksGame >= Deadline)
        {
            if (Map != null)
                foreach (var duel in Map.GetComponent<DuelMapComponent>().Duels.Where(d => d.Active || d.Phase == Honor.Rules.DuelPhase.Pending).ToList())
                    Map.GetComponent<DuelMapComponent>().Finish(duel, null, "delegation expired");
            Resolve(false);
        }
    }
    public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
    { alsoRemoveWorldObject = Resolved; return Resolved && !Map.mapPawns.AnyPawnBlockingMapRemoval; }
}
[HarmonyPatch(typeof(Settlement), nameof(Settlement.GetGizmos))]
internal static class SettlementCommands
{
    static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Settlement __instance)
    {
        foreach (var g in __result) yield return g;
        var s = __instance;
        if (s.Faction == null || !s.Faction.HasGoodwill || s.Faction == Faction.OfPlayer || s.Faction.HostileTo(Faction.OfPlayer)) yield break;
        yield return new Command_Action { defaultLabel = "HD_Request".Translate(), defaultDesc = "HD_RequestDesc".Translate(), action = () => {
            if (DiplomacyApi.Create(s) == null) Messages.Message("HD_Unavailable".Translate(), MessageTypeDefOf.RejectInput);
        } };
    }
}
[HarmonyPatch(typeof(Faction), nameof(Faction.Notify_MemberTookDamage))]
internal static class ConsensualDuelGoodwill
{
    static bool Prefix(Pawn member, DamageInfo dinfo)
    {
        if (member.Map?.Parent is not HonorDelegation site || member != site.Representative || dinfo.Instigator is not Pawn attacker) return true;
        var duel = member.Map.GetComponent<DuelMapComponent>().RecentFor(member);
        // Only the agreed opponent's duel strike is exempt. Other assaults retain vanilla penalties.
        return duel == null || duel.Other(member) != attacker ||
            !(duel.Phase == Honor.Rules.DuelPhase.Fighting || duel.EndedTick == Find.TickManager.TicksGame);
    }
}
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
internal static class ChampionCommands
{
    static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        foreach (var g in __result) yield return g;
        if (__instance.Map?.Parent is not HonorDelegation site || site.Resolved || __instance.Faction != Faction.OfPlayer) yield break;
        yield return new Command_Action { defaultLabel = "HD_Champion".Translate(), defaultDesc = "HD_ChampionDesc".Translate(), action = () => {
            if (!site.Begin(__instance)) Messages.Message("HD_NotReady".Translate(), MessageTypeDefOf.RejectInput);
        } };
    }
}
