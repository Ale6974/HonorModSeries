using Honor.Core;
using Honor.Rules;
using RimWorld;
using Verse;
using Verse.AI;

namespace Honor.InGame;

[StaticConstructorOnStartup]
internal static class SmokeBoot
{
    static SmokeBoot()
    {
        if (GenCommandLine.CommandLineArgPassed("honorLanguageSmoke"))
            LongEventHandler.ExecuteWhenFinished(LocalizationSmoke.Run);
        if (GenCommandLine.CommandLineArgPassed("honorSmokeResume"))
            LongEventHandler.ExecuteWhenFinished(() => GameDataSaveLoader.LoadGame("HonorActive"));
    }
}

/// <summary>Real engine integration test. Opt in with -honorSmoke -quicktest and isolated save data.</summary>
public sealed class SmokeComponent : GameComponent
{
    private int stage, startTick;
    private Pawn a, b;
    private DuelRecord duel;
    public SmokeComponent(Game game) { }
    public override void LoadedGame()
    {
        if (!GenCommandLine.CommandLineArgPassed("honorSmokeResume")) return;
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            try
            {
                duel = Find.CurrentMap.GetComponent<DuelMapComponent>().Duels.Single();
                a = duel.Offender; b = duel.Challenger;
                Check(a != null && b != null && HonorApi.Need(a) != null, "Scribe pawn and need references");
                Check(duel.Phase == DuelPhase.Fighting, "Scribe active duel phase");
                Check(a.CurJobDef == HonorDefOf.Honor_Duel && b.CurJobDef == HonorDefOf.Honor_Duel, "Scribe duel jobs");
                startTick = Find.TickManager.TicksGame; stage = 2;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                Log.Message("[Honor.Tests] RESUME PASS: continuing saved combat.");
            }
            catch (Exception e) { Fail(e); }
        });
    }
    public override void StartedNewGame()
    {
        if (!GenCommandLine.CommandLineArgPassed("honorSmoke")) return;
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            try
            {
                Check(HonorMod.PatchesReady, "Harmony ready");
                var map = Find.CurrentMap;
                var center = map.Center;
                foreach (var cell in GenRadial.RadialCellsAround(center, 18, true))
                {
                    foreach (var thing in cell.GetThingList(map).ToList())
                    {
                        if (thing is Pawn p) { p.Position = new IntVec3(20, 0, 20); continue; }
                        if (thing.def.category is ThingCategory.Building or ThingCategory.Plant or ThingCategory.Filth) thing.DeSpawn();
                    }
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                    map.roofGrid.SetRoof(cell, null);
                }
                a = MakePawn(); b = MakePawn();
                GenSpawn.Spawn(a, center + IntVec3.West, map); GenSpawn.Spawn(b, center, map);
                map.GetComponent<DuelMapComponent>().Arena = center;
                HonorMod.Settings.AutoChallenge = true;
                Check(a.interactions.TryInteractWith(b, InteractionDefOf.Insult), "real insult succeeds");
                duel = map.GetComponent<DuelMapComponent>().Duels.Single();
                Check(duel.Challenger == b && duel.Offender == a, "insult direction");
                Check(a.MentalStateDef != MentalStateDefOf.SocialFighting && b.MentalStateDef != MentalStateDefOf.SocialFighting, "no parallel social fight");
                duel.Mode = GenCommandLine.CommandLineArgPassed("honorSmokeDeath") ? DuelMode.Death :
                    GenCommandLine.CommandLineArgPassed("honorSmokeSurrender") ? DuelMode.Surrender : DuelMode.FirstBlood;
                bool staged = map.GetComponent<DuelMapComponent>().TryBegin(duel);
                Check(staged, "duel stages immediately in prepared arena: " + duel.Reason);
                startTick = Find.TickManager.TicksGame; stage = 1;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                Log.Message("[Honor.Tests] SETUP PASS; waiting for real combat.");
            }
            catch (Exception e) { Fail(e); }
        });
    }
    private static Pawn MakePawn()
    {
        Pawn p;
        do { p = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer); }
        while (p.ageTracker.AgeBiologicalYears < 18 || p.WorkTagIsDisabled(WorkTags.Violent) || p.health.HasHediffsNeedingTend());
        p.equipment.DestroyAllEquipment();
        bool ranged = GenCommandLine.CommandLineArgPassed("honorSmokeRanged");
        var knife = (ThingWithComps)ThingMaker.MakeThing(ThingDef.Named(ranged ? "Gun_Revolver" : "MeleeWeapon_Knife"), ranged ? null : ThingDefOf.Steel);
        knife.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);
        p.equipment.AddEquipment(knife);
        p.apparel.DestroyAll();
        p.apparel.Wear((Apparel)ThingMaker.MakeThing(ThingDef.Named("Apparel_BasicShirt"), ThingDefOf.Cloth));
        p.apparel.Wear((Apparel)ThingMaker.MakeThing(ThingDef.Named("Apparel_FlakVest")));
        return p;
    }
    public override void GameComponentTick()
    {
        if (stage == 0 || stage == 99) return;
        try
        {
            // Quicktest scenario colonists and wildlife must not contaminate the controlled arena test.
            foreach (var other in a.MapHeld.mapPawns.AllPawnsSpawned.Where(p => p != a && p != b &&
                p.Position.InHorDistOf(a.MapHeld.GetComponent<DuelMapComponent>().Arena, 20)).ToList())
                other.Position = new IntVec3(20, 0, 20);
            if (Find.TickManager.TicksGame - startTick > 16000) throw new Exception("Duel timeout: " + duel.Phase + " " + duel.Reason);
            if (duel.Phase == DuelPhase.Cancelled) throw new Exception("Duel cancelled: " + duel.Reason);
            if (stage == 1 && duel.Phase == DuelPhase.Fighting)
            {
                Check(a.apparel.WornApparel.All(x => !DuelMapComponent.IsArmor(x)), "armor removed A");
                Check(b.apparel.WornApparel.All(x => !DuelMapComponent.IsArmor(x)), "armor removed B");
                Check(a.apparel.WornApparel.Any(x => x.def.defName == "Apparel_BasicShirt"), "ordinary clothing retained");
                if (GenCommandLine.CommandLineArgPassed("honorSmokeCheckpoint"))
                {
                    GameDataSaveLoader.SaveGame("HonorActive"); stage = 99;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    Log.Message("[Honor.Tests] CHECKPOINT PASS: active duel saved."); return;
                }
                stage = 2;
            }
            if (duel.Phase != DuelPhase.Completed) return;
            Check(duel.Winner != null && duel.RewardApplied, "winner and honor applied");
            Check(duel.Mode == DuelMode.Death ? a.Dead != b.Dead : !a.Dead && !b.Dead, "survival / death rule");
            Check((a.Dead || !a.health.HasHediffsNeedingTend()) && (b.Dead || !b.health.HasHediffsNeedingTend()), "immediate tending");
            Check(a.CurJobDef != HonorDefOf.Honor_Duel && b.CurJobDef != HonorDefOf.Honor_Duel, "combat jobs ended");
            Check(duel.Winner.apparel.WornApparel.Any(x => x.def.defName == "Apparel_FlakVest"), "armor restored to survivor");
            var old = HonorApi.Need(duel.Winner).CurLevel;
            a.Map.GetComponent<DuelMapComponent>().Finish(duel, duel.Winner, "duplicate result test");
            Check(HonorApi.Need(duel.Winner).CurLevel == old, "idempotent result");
            GameDataSaveLoader.SaveGame("HonorSmoke");
            stage = 99;
            Log.Message($"[Honor.Tests] PASS: real insult -> gathering -> combat -> {duel.Mode} -> tending -> honor; save requested.");
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        }
        catch (Exception e) { Fail(e); }
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Log.Message("[Honor.Tests] ASSERT PASS: " + label);
    }
    private void Fail(Exception e)
    {
        stage = 99; Log.Error("[Honor.Tests] FAIL: " + e);
        if (Find.TickManager != null) Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
    }
}
