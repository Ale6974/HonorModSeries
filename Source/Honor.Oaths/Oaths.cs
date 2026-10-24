using HarmonyLib;
using Honor.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace Honor.Oaths;

public enum OathKind { WinDuel, KeepPeace }
public sealed class OathRecord : IExposable
{
    public Pawn Pawn;
    public OathKind Kind;
    public int Deadline;
    public int ResolvedTick = -1;
    public bool Completed, Broken;
    public void ExposeData()
    {
        Scribe_References.Look(ref Pawn, "pawn"); Scribe_Values.Look(ref Kind, "kind");
        Scribe_Values.Look(ref Deadline, "deadline"); Scribe_Values.Look(ref Completed, "completed"); Scribe_Values.Look(ref Broken, "broken");
        Scribe_Values.Look(ref ResolvedTick, "resolvedTick", -1);
    }
}
public sealed class OathsMod : Mod
{
    public OathsMod(ModContentPack content) : base(content)
    {
        new Harmony("honor.series.oaths").PatchAll();
        HonorApi.InsultOccurred += (p, _) => Current.Game?.GetComponent<OathLedger>().Insult(p);
        HonorApi.DuelEnded += d => {
            if (d.Winner != null && !d.DishonorApplied) Current.Game.GetComponent<OathLedger>().Victory(d.Beneficiary(d.Winner));
        };
        HonorApi.DishonorOccurred += (_, p) => Current.Game?.GetComponent<OathLedger>().Betray(p);
    }
}
public sealed class OathLedger : GameComponent
{
    public List<OathRecord> Records = [];
    public OathLedger(Game game) { }
    public override void LoadedGame()
    {
        // Migrate old records after the complete game clock and deep collections have loaded.
        int now = Math.Max(0, Find.TickManager.TicksGame);
        foreach (var r in Records.Where(r => (r.Completed || r.Broken) && r.ResolvedTick < 0))
            r.ResolvedTick = RuntimeCompat.Clamp(r.Deadline, 0, now);
    }
    public OathRecord Active(Pawn p)
    {
        var oath = Records.FirstOrDefault(r => r.Pawn == p && !r.Completed && !r.Broken);
        if (oath != null && Find.TickManager.TicksGame >= oath.Deadline)
        { Resolve(oath, oath.Kind == OathKind.KeepPeace); return null; }
        return oath;
    }
    public bool HasTitle(Pawn p) => Records.Any(r => r.Pawn == p && r.Completed) &&
        !Records.Any(r => r.Pawn == p && r.Broken && r.ResolvedTick > Find.TickManager.TicksGame - 300000);
    public bool Swear(Pawn p, OathKind kind)
    {
        if (!RuntimeCompat.IsDefined(kind) || p?.Map == null || !DuelMapComponent.Eligible(p) || p.Downed || p.Drafted ||
            !p.Awake() || p.InMentalState || Active(p) != null ||
            p.Map.mapPawns.FreeColonistsSpawned.Count(w => w != p && !w.Downed && w.Awake() &&
                w.Position.InHorDistOf(p.Position, 10) && GenSight.LineOfSight(w.Position, p.Position, p.Map)) < 2) return false;
        Records.Add(new OathRecord { Pawn = p, Kind = kind, Deadline = Find.TickManager.TicksGame + 300000 });
        Messages.Message("HO_Sworn".Translate(p.LabelShort), p, MessageTypeDefOf.PositiveEvent); return true;
    }
    public void Victory(Pawn p)
    { var oath = Active(p); if (oath?.Kind == OathKind.WinDuel) Resolve(oath, true); }
    public void Insult(Pawn p)
    { var oath = Active(p); if (oath?.Kind == OathKind.KeepPeace) Resolve(oath, false); }
    public void Betray(Pawn p)
    { var oath = Active(p); if (oath != null) Resolve(oath, false); }
    public void Resolve(OathRecord oath, bool success)
    {
        if (oath.Completed || oath.Broken) return;
        oath.Completed = success; oath.Broken = !success;
        oath.ResolvedTick = Find.TickManager.TicksGame;
        if (oath.Pawn == null || oath.Pawn.Dead) return;
        HonorApi.Change(oath.Pawn, success ? .06f : -.1f, success ? "oath fulfilled" : "oath broken");
        oath.Pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed(success ? "HonorOaths_Fulfilled" : "HonorOaths_Broken"));
        Messages.Message((success ? "HO_Complete" : "HO_Broken").Translate(oath.Pawn.LabelShort), oath.Pawn, MessageTypeDefOf.NeutralEvent);
    }
    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % 250 != 0) return;
        foreach (var r in Records.Where(r => !r.Completed && !r.Broken))
        {
            if (r.Pawn == null || r.Pawn.Dead) { Resolve(r, false); continue; }
            if (Find.TickManager.TicksGame >= r.Deadline) Resolve(r, r.Kind == OathKind.KeepPeace);
        }
    }
    public override void ExposeData()
    { Scribe_Collections.Look(ref Records, "honorOaths", LookMode.Deep); if (Scribe.mode == LoadSaveMode.PostLoadInit) Records ??= []; }
}
public sealed class JobDriver_SwearOath : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
    protected override IEnumerable<Toil> MakeNewToils()
    {
        yield return Toils_General.Wait(600);
        yield return Toils_General.Do(() => {
            if (!Current.Game.GetComponent<OathLedger>().Swear(pawn, (OathKind)job.count))
                Messages.Message("HO_Witnesses".Translate(), pawn, MessageTypeDefOf.RejectInput);
        });
    }
}
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
internal static class OathCommands
{
    static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        foreach (var g in __result) yield return g;
        var p = __instance; if (p.Map == null || p.Faction != Faction.OfPlayer || !DuelMapComponent.Eligible(p)) yield break;
        var ledger = Current.Game.GetComponent<OathLedger>();
        yield return new Command_Action { defaultLabel = ledger.HasTitle(p) ? "HO_Title".Translate() : "HO_Oath".Translate(),
            defaultDesc = "HO_Desc".Translate(), action = () => {
                if (ledger.Active(p) is { } oath) {
                    Messages.Message("HO_Status".Translate(((oath.Deadline - Find.TickManager.TicksGame) / 60000f).ToString("0.0")), p, MessageTypeDefOf.NeutralEvent); return;
                }
                Find.WindowStack.Add(new FloatMenu(RuntimeCompat.EnumValues<OathKind>().Select(kind => new FloatMenuOption(
                    (kind == OathKind.WinDuel ? "HO_Win" : "HO_Peace").Translate(), () => {
                        var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("HonorOaths_Swear")); job.count = (int)kind; job.playerForced = true;
                        p.jobs.TryTakeOrderedJob(job);
                    })).ToList()));
            } };
    }
}
