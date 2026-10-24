using Honor.Core;
using Honor.Rules;
using Honor.Society;
using Honor.Oaths;
using Honor.Diplomacy;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace Honor.SeriesTests;

[StaticConstructorOnStartup]
internal static class Boot
{
    static Boot()
    {
        if (GenCommandLine.CommandLineArgPassed("honorSeriesLanguage")) LongEventHandler.ExecuteWhenFinished(Language);
        if (GenCommandLine.CommandLineArgPassed("honorSeriesResume")) LongEventHandler.ExecuteWhenFinished(() => GameDataSaveLoader.LoadGame("HonorSeries"));
        if (GenCommandLine.CommandLineArgPassed("honorSeriesReviewResume")) LongEventHandler.ExecuteWhenFinished(() => GameDataSaveLoader.LoadGame("HonorReviewResolved"));
    }
    internal static void Check(bool result, string message)
    { if (!result) throw new Exception(message); Log.Message("[Honor.Tests] ASSERT PASS: " + message); }
    internal static void Language()
    {
        try
        {
            bool ko = LanguageDatabase.activeLanguage.folderName.StartsWith("Korean");
            Check(ko == GenCommandLine.CommandLineArgPassed("honorExpectKorean"), "native game language");
            Check("HS_Offer".Translate().ToString() == (ko ? "사과 제안" : "Offer apology"), "Society keyed language");
            Check("HO_Oath".Translate().ToString() == (ko ? "맹세하기" : "Swear oath"), "Oaths keyed language");
            Check("HD_Champion".Translate().ToString() == (ko ? "정착지 대표로 결투" : "Represent the colony"), "Diplomacy keyed language");
            Check("HD_ChampionDesc".Translate().ToString().Contains(ko ? "대여" : "loan"), "loan policy localized");
            if (ModsConfig.RoyaltyActive) Check("HN_Proxy".Translate().ToString() == (ko ? "대리 결투자 지정" : "Appoint champion"), "Nobility keyed language");
            Check(DefDatabase<ThoughtDef>.GetNamed("HonorSociety_Rival").stages[0].label == (ko ? "거듭된 모욕: 숙적" : "repeated insults: rival"), "Society injected memory");
            Check(DefDatabase<ThoughtDef>.GetNamed("HonorOaths_Fulfilled").stages[0].description == (ko ? "목격자들 앞에서 한 약속을 지켰다." : "I kept my word before witnesses."), "Oaths injected memory");
            var errors = LanguageDatabase.activeLanguage.loadErrors.Concat(LanguageDatabase.activeLanguage.defInjections.SelectMany(d => d.loadErrors));
            Check(!errors.Any(e => e.Contains("Honor", StringComparison.OrdinalIgnoreCase)), "no Honor translation errors");
            if (ko) Check(LanguageDatabase.activeLanguage.defInjections.SelectMany(d => d.injections.Values).Where(i => i.DefName.StartsWith("Honor")).All(i => i.injected), "all Honor injections applied");
            Log.Message("[Honor.Tests] PASS: series localization");
        }
        catch (Exception e) { Log.Error("[Honor.Tests] FAIL: " + e); }
    }
}
public sealed class SeriesSmoke : GameComponent
{
    private Pawn a, b, proxy;
    private HonorDelegation site;
    private DuelRecord duel;
    private int stage, started;
    private int goodwillBefore;
    public SeriesSmoke(Game game) { }
    public override void StartedNewGame()
    {
        if (!GenCommandLine.CommandLineArgPassed("honorSeries")) return;
        LongEventHandler.ExecuteWhenFinished(() => { try { Setup(); } catch (Exception e) { Fail(e); } });
    }
    public override void LoadedGame()
    {
        if (!GenCommandLine.CommandLineArgPassed("honorSeriesResume")) return;
        LongEventHandler.ExecuteWhenFinished(() => {
            try
            {
                site = Find.WorldObjects.AllWorldObjects.OfType<HonorDelegation>().Single();
                Check(site.Quest != null && site.Representative != null && !site.Resolved, "saved quest/site/representative references");
                Check(Current.Game.GetComponent<DelegationLedger>().Busy(site.Faction), "saved faction cooldown");
                var home = Find.Maps.First(m => m != site.Map);
                Check(home.GetComponent<SocialLedger>().Records.Any(r => r.Insults >= 3), "saved Society record");
                Check(Current.Game.GetComponent<OathLedger>().Records.Any(r => !r.Completed && !r.Broken), "saved active oath");
                duel = site.Map.GetComponent<DuelMapComponent>().Duels.Single();
                goodwillBefore = site.Faction.GoodwillWith(Faction.OfPlayer);
                Check(duel.Active, "saved delegation duel");
                Check(Current.Game.GetComponent<OathLedger>().Records.Where(r => r.Completed || r.Broken).All(r => r.ResolvedTick >= 0), "oath timestamp migration/persistence");
                started = Find.TickManager.TicksGame; stage = 2; Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
            }
            catch (Exception e) { Fail(e); }
        });
    }
    private void Setup()
    {
        Check(HonorMod.PatchesReady, "Core patches ready"); HonorMod.Settings.AutoChallenge = false;
        var map = Find.CurrentMap; var center = map.Center;
        foreach (var c in GenRadial.RadialCellsAround(center, 22, true))
        {
            foreach (var t in c.GetThingList(map).ToList())
            { if (t is Pawn p) p.Position = new IntVec3(20, 0, 20); else if (t.def.category is ThingCategory.Building or ThingCategory.Plant or ThingCategory.Filth) t.DeSpawn(); }
            map.terrainGrid.SetTerrain(c, TerrainDefOf.Soil); map.roofGrid.SetRoof(c, null);
        }
        a = Pawn(); b = Pawn(); proxy = Pawn();
        GenSpawn.Spawn(a, center, map); GenSpawn.Spawn(b, center + IntVec3.West, map); GenSpawn.Spawn(proxy, center + IntVec3.East, map);
        var component = map.GetComponent<DuelMapComponent>(); var social = map.GetComponent<SocialLedger>();
        component.Arena = center;
        component.RegisterInsult(a, b); duel = component.Duels.Last();
        Check(!social.Accept(duel, b), "apology requires offer");
        Check(social.Offer(duel, a) && social.Accept(duel, b), "offer and victim acceptance");
        Check(duel.Phase == DuelPhase.Cancelled && Memory(b, "HonorSociety_Apology"), "apology cancels duel and records vanilla social memory");
        for (int i = 0; i < 3; i++) component.RegisterInsult(a, b);
        Check(Memory(a, "HonorSociety_Rival") && Memory(b, "HonorSociety_Rival"), "repeated insults make rivals");
        duel = component.Duels.Last();
        if (ModsConfig.RoyaltyActive)
        {
            var empire = Find.FactionManager.AllFactions.First(f => f.def.defName == "Empire");
            b.royalty.SetTitle(empire, DefDatabase<RoyalTitleDef>.GetNamed("Knight"), false, sendLetter: false);
            var type = GenTypes.GetTypeInAnyAssembly("Honor.Nobility.NobilityMod");
            Check((float)type.GetMethod("Multiplier").Invoke(null, [b]) > 1, "royal sensitivity");
            var mode = duel.Mode;
            Check((bool)type.GetMethod("Appoint").Invoke(null, [duel, b, proxy]), "royal proxy appointment");
            Check(duel.Challenger == proxy && duel.PrincipalChallenger == b && duel.Mode == mode, "proxy keeps principal and original rules");
        }
        var oaths = Current.Game.GetComponent<OathLedger>();
        Check(oaths.Swear(a, OathKind.KeepPeace), "witnessed peace oath"); component.RegisterInsult(a, b);
        Check(oaths.Records.Last().Broken && Memory(a, "HonorOaths_Broken"), "insult breaks peace oath");
        Check(oaths.Swear(b, OathKind.WinDuel), "victory oath");
        var family = Pawn(); GenSpawn.Spawn(family, center + new IntVec3(12, 0, 0), map);
        family.relations.AddDirectRelation(PawnRelationDefOf.Parent, b);
        var familyHonor = HonorApi.Need(family).CurLevel;
        component.Finish(duel, duel.Challenger, "extension event test");
        Check(oaths.HasTitle(b) && Memory(b, "HonorOaths_Fulfilled"), "proxy result fulfills principal oath");
        Check(HonorApi.Need(family).CurLevel > familyHonor && Memory(family, "HonorSociety_Witness"), "blood relative honor and witnessed social memory");
        var listener = Pawn(); GenSpawn.Spawn(listener, center + new IntVec3(25, 0, 0), map);
        var timedOath = new OathRecord { Pawn = family, Kind = OathKind.KeepPeace, Deadline = Find.TickManager.TicksGame - 1 };
        oaths.Records.Add(timedOath);
        var rumor = social.Records.Last(r => r.Victor != null); rumor.RumorAt = Math.Max(1, Find.TickManager.TicksGame);
        Check(oaths.Swear(proxy, OathKind.KeepPeace), "active oath for persistence");
        var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("HonorOaths_Swear")); job.count = (int)OathKind.KeepPeace;
        a.jobs.TryTakeOrderedJob(job); started = Find.TickManager.TicksGame; stage = 1;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
    }
    public override void GameComponentTick()
    {
        if (stage == 0) return;
        try
        {
            if (Find.TickManager.TicksGame - started > 20000) throw new Exception("series test timed out");
            if (stage == 1)
            {
                if (Current.Game.GetComponent<OathLedger>().Active(a) == null) return;
                Check(a.CurJobDef != DefDatabase<JobDef>.GetNamed("HonorOaths_Swear"), "ceremony job completed");
                Check(Find.CurrentMap.GetComponent<SocialLedger>().Records.All(r => r.Victor == null || r.RumorAt == 0), "rumor tick processed");
                Check(Find.CurrentMap.mapPawns.FreeColonistsSpawned.Any(p => Memory(p, "HonorSociety_Rumor")), "delayed rumor becomes vanilla social memory");
                Check(Current.Game.GetComponent<OathLedger>().Records.Any(r => r.Kind == OathKind.KeepPeace && r.Completed), "peace oath completes on deadline");
                var settlement = Find.WorldObjects.Settlements.First(s => s.Faction != Faction.OfPlayer && !s.Faction.HostileTo(Faction.OfPlayer));
                site = DiplomacyApi.Create(settlement); Check(site != null && site.Quest != null, "real world quest generated");
                Check(DiplomacyApi.Create(settlement) == null, "faction duplicate/cooldown blocked");
                var map = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, new IntVec3(100, 1, 100), site.def);
                Check(site.Representative?.Spawned == true, "delegation map and NPC generated");
                var champion = Pawn(); GenSpawn.Spawn(champion, map.Center + IntVec3.West, map);
                Check(site.Begin(champion), "foreign representative admitted through API");
                duel = map.GetComponent<DuelMapComponent>().Duels.Single(); duel.Mode = DuelMode.FirstBlood;
                goodwillBefore = site.Faction.GoodwillWith(Faction.OfPlayer);
                HonorMod.Settings.AutoChallenge = true;
                Check(map.GetComponent<DuelMapComponent>().TryBegin(duel), "delegation stages: " + duel.Reason);
                started = Find.TickManager.TicksGame; stage = 2;
            }
            if (stage == 2 && GenCommandLine.CommandLineArgPassed("honorSeriesCheckpoint") && duel.Phase == DuelPhase.Fighting)
            {
                GameDataSaveLoader.SaveGame("HonorSeries"); stage = 0;
                Log.Message("[Honor.Tests] CHECKPOINT PASS: series saved during delegation combat"); return;
            }
            if (stage == 2 && !duel.Active)
            {
                Check(duel.Winner != null && site.Resolved, "real representative combat resolved quest");
                Check(duel.Challenger.health.hediffSet.BleedRateTotal < .00001f && duel.Offender.health.hediffSet.BleedRateTotal < .00001f, "both fighters stabilized");
                Check(site.Quest.State != QuestState.Ongoing, "quest ended");
                Check(site.Representative.equipment.Primary == null && site.LoanWeapon == null, "delegation loan reclaimed after real combat");
                bool won = duel.Winner != site.Representative && !duel.DishonorApplied;
                Check(site.Faction.GoodwillWith(Faction.OfPlayer) == Math.Clamp(goodwillBefore + (won ? 10 : -5), -100, 100), "faction goodwill result");
                int silver = site.Map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t => t.stackCount);
                if (won) Check(silver >= 100, "silver reward spawned");
                site.Resolve(won);
                Check(site.Map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t => t.stackCount) == silver, "reward idempotence");
                stage = 0; Log.Message("[Honor.Tests] PASS: series gameplay and persistence");
            }
        }
        catch (Exception e) { Fail(e); }
    }
    private static Pawn Pawn()
    {
        Pawn p;
        do { p = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer); }
        while (p.ageTracker.AgeBiologicalYears < 18 || p.WorkTagIsDisabled(WorkTags.Violent) || p.health.HasHediffsNeedingTend());
        p.equipment.DestroyAllEquipment(); var weapon = (ThingWithComps)ThingMaker.MakeThing(ThingDef.Named("MeleeWeapon_Knife"), ThingDefOf.Steel);
        weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony); p.equipment.AddEquipment(weapon); return p;
    }
    private static bool Memory(Pawn p, string def) => p.needs.mood.thoughts.memories.Memories.Any(m => m.def.defName == def);
    private static void Check(bool result, string message) => Boot.Check(result, message);
    private void Fail(Exception e) { stage = 0; Log.Error("[Honor.Tests] FAIL: " + e); }
}
