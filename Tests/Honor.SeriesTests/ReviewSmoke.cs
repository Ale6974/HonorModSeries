using Honor.Core;
using Honor.Diplomacy;
using Honor.Oaths;
using Honor.Rules;
using Honor.Society;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.SeriesTests;

public sealed class ReviewSmoke : GameComponent
{
    public ReviewSmoke(Game game) { }
    public override void LoadedGame()
    {
        if (!GenCommandLine.CommandLineArgPassed("honorSeriesReviewResume")) return;
        LongEventHandler.ExecuteWhenFinished(() => {
            try
            {
                var site = Find.WorldObjects.AllWorldObjects.OfType<HonorDelegation>().Single();
                Boot.Check(site.Resolved && site.LoanWeapon == null, "review saved resolved site and reclaimed loan");
                Boot.Check(site.Quest != null && site.Quest.State != QuestState.Ongoing, "review saved completed quest reference");
                Boot.Check(Current.Game.GetComponent<OathLedger>().Records.Where(r => r.Completed || r.Broken).All(r => r.ResolvedTick >= 0), "review saved oath resolution timestamps");
                Log.Message("[Honor.Tests] PASS: reviewed resolved-state save reload");
            }
            catch (Exception e) { Log.Error("[Honor.Tests] FAIL: review reload " + e); }
        });
    }
    public override void StartedNewGame()
    {
        if (!GenCommandLine.CommandLineArgPassed("honorSeriesReview")) return;
        LongEventHandler.ExecuteWhenFinished(Run);
    }
    private static void Run()
    {
        var failures = new List<string>();
        void Case(string label, Action action)
        {
            try { action(); Log.Message("[Honor.Tests] ASSERT PASS: review " + label); }
            catch (Exception e) { failures.Add(label + ": " + e.Message); }
        }
        void Check(bool result, string message) { if (!result) throw new Exception(message); }
        try
        {
            HonorMod.Settings.AutoChallenge = false;
            var map = Find.CurrentMap; var center = map.Center;
            foreach (var cell in GenRadial.RadialCellsAround(center, 20, true))
            {
                foreach (var thing in cell.GetThingList(map).ToList())
                {
                    if (thing is Pawn p) p.Position = new IntVec3(20, 0, 20);
                    else if (thing.def.category is ThingCategory.Building or ThingCategory.Plant) thing.DeSpawn();
                }
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil); map.roofGrid.SetRoof(cell, null);
            }
            Pawn Make()
            {
                Pawn p;
                do { p = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer); }
                while (p.ageTracker.AgeBiologicalYears < 18 || p.WorkTagIsDisabled(WorkTags.Violent));
                GenSpawn.Spawn(p, center, map); return p;
            }
            var a = Make(); var b = Make(); var c = Make();
            var manager = map.GetComponent<DuelMapComponent>(); var society = map.GetComponent<SocialLedger>();
            var oaths = Current.Game.GetComponent<OathLedger>();
            Case("apology revalidates distance", () => {
                manager.RegisterInsult(a, b); var d = manager.Duels.Last();
                Check(society.Offer(d, a), "offer failed"); b.Position = center + new IntVec3(18, 0, 0);
                bool accepted = society.Accept(d, b); b.Position = center;
                manager.Finish(d, null, "review cleanup");
                Check(!accepted, "accepted after recipient left speaking range");
            });
            Case("pending obligation released on map exit", () => {
                manager.RegisterInsult(a, b); var d = manager.Duels.Last();
                a.DeSpawn(); manager.MapComponentTick(); bool cancelled = d.Phase == DuelPhase.Cancelled;
                GenSpawn.Spawn(a, center, map); manager.Finish(d, null, "review cleanup");
                Check(cancelled, "despawned pawn retained pending obligation");
            });
            Case("all pending entries cleaned with automatic staging disabled", () => {
                var other = Make();
                manager.RegisterInsult(a, b); var first = manager.Duels.Last();
                manager.RegisterInsult(c, other); var second = manager.Duels.Last();
                c.DeSpawn(); manager.MapComponentTick(); bool cancelled = second.Phase == DuelPhase.Cancelled;
                GenSpawn.Spawn(c, center, map);
                manager.Finish(first, null, "review cleanup"); manager.Finish(second, null, "review cleanup");
                Check(cancelled, "earlier valid obligation prevented later cleanup");
            });
            Case("queued duel suppresses parallel vanilla social fighting", () => {
                manager.RegisterInsult(a, b); var d = manager.Duels.Last();
                bool fighting = false;
                for (int i = 0; i < 64; i++) fighting |= a.interactions.CheckSocialFightStart(InteractionDefOf.Insult, b);
                manager.Finish(d, null, "review cleanup");
                Check(!fighting, "queued duel also started vanilla social combat");
            });
            Case("late victory cannot fulfill expired oath", () => {
                var oath = new OathRecord { Pawn = a, Kind = OathKind.WinDuel, Deadline = Find.TickManager.TicksGame - 1 };
                oaths.Records.Add(oath); oaths.Victory(a);
                Check(oath.Broken && !oath.Completed, "victory after deadline fulfilled oath");
            });
            Case("completed peace deadline precedes later insult", () => {
                var oath = new OathRecord { Pawn = b, Kind = OathKind.KeepPeace, Deadline = Find.TickManager.TicksGame - 1 };
                oaths.Records.Add(oath); oaths.Insult(b);
                Check(oath.Completed && !oath.Broken, "post-deadline insult broke completed peace");
            });
            Case("title penalty counts from actual oath breach", () => {
                oaths.Records.Add(new OathRecord { Pawn = c, Kind = OathKind.KeepPeace, Completed = true });
                var oath = new OathRecord { Pawn = c, Kind = OathKind.KeepPeace, Deadline = Find.TickManager.TicksGame + 300000 };
                oaths.Records.Add(oath); oaths.Insult(c);
                Check(oath.ResolvedTick == Find.TickManager.TicksGame && !oaths.HasTitle(c), "breach timestamp/title suppression incorrect");
            });
            Case("dishonor does not grant ordinary victory honor", () => {
                manager.RegisterInsult(a, b); var d = manager.Duels.Last();
                HonorApi.Need(b).CurLevel = .8f; manager.Dishonor(d, b);
                float penalized = HonorApi.Need(b).CurLevel; manager.Finish(d, b, "review dishonor");
                Check(Math.Abs(HonorApi.Need(b).CurLevel - penalized) < .00001f, "dishonorable winner received positive victory reward");
            });
            var settlements = Find.WorldObjects.Settlements.Where(s => s.Faction != Faction.OfPlayer && s.Faction.HasGoodwill && !s.Faction.HostileTo(Faction.OfPlayer)).ToList();
            var site = settlements.Select(DiplomacyApi.Create).First(s => s != null);
            var arena = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, new IntVec3(100, 1, 100), site.def);
            Case("delegation arena clears non-building obstructions", () => {
                var chunk = ThingMaker.MakeThing(ThingDef.Named("ChunkGranite"));
                var cell = arena.Center + IntVec3.West;
                GenSpawn.Spawn(chunk, cell, arena);
                Check(!cell.Standable(arena), "chunk did not reproduce blocked standing cell");
                site.PrepareArena();
                Check(cell.Standable(arena) && !chunk.Spawned && site.Representative.Spawned, "generated obstacle remains or representative was removed");
            });
            var champion = Make(); champion.DeSpawn(); GenSpawn.Spawn(champion, arena.Center + IntVec3.West, arena);
            champion.equipment.DestroyAllEquipment();
            var knife = (ThingWithComps)ThingMaker.MakeThing(ThingDef.Named("MeleeWeapon_Knife"), ThingDefOf.Steel);
            champion.equipment.AddEquipment(knife);
            Check(site.Begin(champion), "review delegation setup failed");
            var loan = site.Representative.equipment.Primary;
            loan.holdingOwner.Remove(loan);
            Check(champion.inventory.innerContainer.TryAdd(loan), "review simulated looting failed");
            var delegation = arena.GetComponent<DuelMapComponent>(); var pending = delegation.Duels.Single();
            // A 0.2.0 combat save has Core's weapon reference, but no new LoanWeapon field.
            pending.WeaponRight = loan; site.LoanWeapon = null;
            Case("delegation resolves when challenger has left map", () => {
                champion.DeSpawn(); delegation.Finish(pending, null, "participant left map");
                bool resolved = site.Resolved; GenSpawn.Spawn(champion, center, map);
                Check(resolved, "site ignored duel-ended event after challenger despawned");
            });
            Case("generated matching weapon is reclaimed", () => {
                site.Resolve(false); Check(loan.Destroyed, "matching weapon remained as free loot");
                Check(!knife.Destroyed, "player-owned weapon was destroyed");
            });
            GameDataSaveLoader.SaveGame("HonorReviewResolved");
            Log.Message("[Honor.Tests] ASSERT PASS: review resolved-state save after loan cleanup");
            if (failures.Count > 0) throw new Exception(string.Join(" | ", failures));
            Log.Message("[Honor.Tests] PASS: release review edge cases");
        }
        catch (Exception e) { Log.Error("[Honor.Tests] FAIL: review " + e); }
    }
}
