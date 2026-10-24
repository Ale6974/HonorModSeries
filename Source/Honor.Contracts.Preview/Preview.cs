using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed class ContractsPreviewMod : Mod
{
    public ContractsPreviewMod(ModContentPack content) : base(content) => new Harmony("honor.series.contracts.preview").PatchAll();
}

/// <summary>Opt-in isolated political simulation; never mutates native factions, inventories or combat.</summary>
public sealed class ContractsPreviewLedger : GameComponent
{
    private string snapshot;
    private int lastGameTick = -1;
    public PoliticalWorld World { get; private set; }
    public bool FollowGameTime;
    public bool RestoreFailed { get; private set; }
    public ContractsPreviewLedger(Game game) { }
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && World != null) snapshot = World.Export();
        Scribe_Values.Look(ref snapshot, "honorPoliticalPreviewV1");
        Scribe_Values.Look(ref FollowGameTime, "honorPreviewFollowGameTime");
        Scribe_Values.Look(ref lastGameTick, "honorPreviewLastGameTick", -1);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && snapshot != null)
        {
            try { World = PoliticalWorld.Restore(snapshot); }
            catch (Exception e) when (e is ArgumentException or System.Text.Json.JsonException or InvalidOperationException or OverflowException)
            {
                RestoreFailed = true; FollowGameTime = false;
                Log.Error("[Honor.Contracts.Preview] Model restore rejected; raw snapshot preserved. " + e.GetType().Name);
            }
        }
    }
    public void Create(long interval)
    {
        if (World != null || RestoreFailed) return;
        World = new PoliticalWorld(interval);
        foreach (var seed in new[] { ("player", 100), ("town", 10), ("enemy", 100), ("rival", 100) })
            World.Apply(new(WorldAction.AddDomain, 0, seed.Item1, "preview-" + seed.Item1,
                seed.Item1 == "town" ? "union" : null, Count: seed.Item2, Flag: seed.Item1 == "player"));
        lastGameTick = Find.TickManager.TicksGame;
    }
    public bool Execute(WorldCommand command)
    {
        if (World == null || RestoreFailed) return false;
        try { World.Apply(command); return true; }
        catch (ArgumentException)
        { Messages.Message("HCP_Rejected".Translate(), MessageTypeDefOf.RejectInput); return false; }
    }
    public override void GameComponentTick()
    {
        int current = Find.TickManager.TicksGame;
        if (lastGameTick < 0 || current < lastGameTick) lastGameTick = current;
        if (!FollowGameTime || World == null || RestoreFailed) { lastGameTick = current; return; }
        if (current - lastGameTick < 250) return;
        long until = World.Now + current - lastGameTick;
        lastGameTick = current;
        if (!Execute(new(WorldAction.Advance, until))) FollowGameTime = false;
    }
}

[HarmonyPatch(typeof(Settlement), nameof(Settlement.GetGizmos))]
internal static class PreviewSettlementCommand
{
    static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Settlement __instance)
    {
        foreach (var gizmo in __result) yield return gizmo;
        if (__instance.Faction != null)
            yield return new Command_Action { defaultLabel = "HCL_Open".Translate(), defaultDesc = "HCL_Warning".Translate(),
                action = () => Find.WindowStack.Add(new LiveChartersWindow(__instance)) };
        if (__instance.Faction != Faction.OfPlayer) yield break;
        yield return new Command_Action { defaultLabel = "HP_Open".Translate(), defaultDesc = "HP_Setup".Translate(),
            action = () => Find.WindowStack.Add(new InitialPoliticsWindow()) };
        yield return new Command_Action { defaultLabel = "HCP_Open".Translate(), defaultDesc = "HCP_Warning".Translate(),
            action = () => Find.WindowStack.Add(new ContractsPreviewWindow()) };
    }
}

public sealed class ContractsPreviewWindow : Window
{
    private Vector2 scroll;
    private float contentHeight = 1200;
    private int intervalDays = 1;
    public override Vector2 InitialSize => new(850, 760);
    public ContractsPreviewWindow() { forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override void DoWindowContents(Rect rect)
    {
        var ledger = Current.Game.GetComponent<ContractsPreviewLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, contentHeight));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, 3000));
        list.Label("HCP_Warning".Translate()); list.Gap();
        if (ledger.RestoreFailed) list.Label("HCP_RestoreFailed".Translate());
        else if (ledger.World == null)
        {
            list.Label("HCP_Interval".Translate(intervalDays));
            intervalDays = (int)list.Slider(intervalDays, 1, 10);
            if (list.ButtonText("HCP_Create".Translate())) ledger.Create(intervalDays * PoliticalWorld.Day);
        }
        else
        {
            var world = ledger.World;
            list.Label("HCP_Time".Translate((world.Now / (double)PoliticalWorld.Day).ToString("F2"),
                (world.TurnTicks / (double)PoliticalWorld.Day).ToString("F2")));
            list.CheckboxLabeled("HCP_FollowTime".Translate(), ref ledger.FollowGameTime);
            if (list.ButtonText("HCP_Day".Translate())) ledger.Execute(new(WorldAction.Advance, world.Now + PoliticalWorld.Day));
            foreach (var d in world.Domains)
                list.Label("HCP_Domain".Translate(Name(d.Id), Group(world, d.Id), d.Available, d.Troops,
                    d.Honor.ToString("P0"), d.Prosperity.ToString("P0")));
            list.Gap();
            var bond = world.Contracts.SingleOrDefault(c => c.Active && c.Subject == "town");
            if (bond == null)
            {
                if (list.ButtonText("HCP_Bind".Translate())) ledger.Execute(new(WorldAction.Bind, world.Now,
                    "bond-" + Guid.NewGuid().ToString("N"), "town", "player", Value: 100,
                    Interval: PoliticalWorld.Day * 5, Grace: PoliticalWorld.Day, Rate: .1m));
            }
            else
            {
                list.Label("HCP_Bond".Translate(bond.TributeValue.ToString("F0"), bond.DueValue.ToString("F0"), bond.Trust.ToString("P0")));
                foreach (var bill in world.Assessments.Where(b => b.Contract == bond.Id && !b.Paid && b.Due <= world.Now).ToArray())
                {
                    list.Label("HCP_Bill".Translate((bill.Due / (double)PoliticalWorld.Day).ToString("F1"),
                        bill.DeliveryValue.ToString("F0"), ("HCP_" + bill.State(world.Now)).Translate()));
                    if (list.ButtonText("HCP_Pay".Translate())) ledger.Execute(new(WorldAction.DeliverTribute,
                        world.Now, bill.Id, "town", Evidence: Guid.NewGuid().ToString("N"), Value: bill.DeliveryValue));
                }
                if (bond.ProtectionBreached && !bond.CureUntil.HasValue && list.ButtonText("HCP_Notice".Translate()))
                    ledger.Execute(new(WorldAction.DemandCure, world.Now, Actor: "town", Interval: PoliticalWorld.Day * 3));
                if (bond.CureUntil.HasValue)
                {
                    list.Label("HCP_CureUntil".Translate((bond.CureUntil.Value / (double)PoliticalWorld.Day).ToString("F2")));
                    if (world.Now > bond.CureUntil && list.ButtonText("HCP_Secede".Translate()))
                        ledger.Execute(new(WorldAction.Secede, world.Now, Actor: "town"));
                }
                if (bond.ProtectionBreached && list.ButtonText("HCP_Remedy".Translate()))
                    ledger.Execute(new(WorldAction.CureProtection, world.Now, Actor: "town", Target: bond.Liege,
                        Evidence: Guid.NewGuid().ToString("N"), Flag: true));
                if (list.ButtonText("HCP_Release".Translate())) ledger.Execute(new(WorldAction.Release,
                    world.Now, Actor: "town", Target: bond.Liege, Flag: true));
            }
            var attack = world.Attacks.SingleOrDefault(a => a.State == AttackState.AwaitingRelief);
            if (attack == null && list.ButtonText("HCP_Attack".Translate())) ledger.Execute(new(WorldAction.OpenAttack,
                world.Now, "attack-" + Guid.NewGuid().ToString("N"), "enemy", "town", Count: 50, Interval: PoliticalWorld.Day * 3));
            if (attack != null)
            {
                list.Label("HCP_AttackInfo".Translate(attack.Strength, attack.ReliefTroops,
                    (attack.Deadline / (double)PoliticalWorld.Day).ToString("F2")));
                if (list.ButtonText("HCP_Relief".Translate())) ledger.Execute(new(WorldAction.SendRelief, world.Now, attack.Id, "player", Count: 50));
            }
            foreach (var result in world.Attacks.Where(a => a.State != AttackState.AwaitingRelief).TakeLast(5))
                list.Label("HCP_Result".Translate(("HCP_" + result.State).Translate()));
        }
        contentHeight = list.CurHeight + 20;
        list.End(); Widgets.EndScrollView();
    }
    private static string Name(string id) => ("HCP_Name_" + id).Translate();
    private static string Group(PoliticalWorld world, string id) => world.DiplomaticGroup(id) == "free-city-union"
        ? "HCP_Union".Translate() : Name(world.Root(id));
}
