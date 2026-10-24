using HarmonyLib;
using Honor.Core;
using Honor.Rules;
using RimWorld;
using Verse;

namespace Honor.Society;

public sealed class SocietyMod : Mod
{
    public SocietyMod(ModContentPack content) : base(content)
    {
        new Harmony("honor.series.society").PatchAll();
        HonorApi.PendingDelayTicks = Math.Max(HonorApi.PendingDelayTicks, 600);
        HonorApi.InsultOccurred += (a, b) => b.Map?.GetComponent<SocialLedger>().Insult(a, b);
        HonorApi.DuelEnded += d => (d.Winner?.Map ?? d.Challenger?.Map)?.GetComponent<SocialLedger>().DuelEnded(d);
        HonorApi.DishonorOccurred += (d, p) => Family(p, -.02f);
    }
    internal static void Memory(Pawn observer, Pawn subject, string def) => observer?.needs?.mood?.thoughts?.memories?
        .TryGainMemory(DefDatabase<ThoughtDef>.GetNamed(def), subject);
    internal static void Family(Pawn subject, float delta)
    {
        foreach (var relative in subject.relations.FamilyByBlood.Where(p => !p.Dead)) HonorApi.Change(relative, delta, "family honor");
    }
}
public sealed class SocialRecord : IExposable
{
    public Pawn Offender, Victim;
    public int Insults, LastInsult, RumorAt;
    public bool ApologyOffered;
    public Pawn Victor;
    public List<Pawn> Informed = [];
    public void ExposeData()
    {
        Scribe_References.Look(ref Offender, "offender"); Scribe_References.Look(ref Victim, "victim");
        Scribe_References.Look(ref Victor, "victor"); Scribe_Values.Look(ref Insults, "insults");
        Scribe_Values.Look(ref LastInsult, "lastInsult"); Scribe_Values.Look(ref RumorAt, "rumorAt");
        Scribe_Values.Look(ref ApologyOffered, "apologyOffered"); Scribe_Collections.Look(ref Informed, "informed", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) Informed ??= [];
    }
}
public sealed class SocialLedger : MapComponent
{
    public List<SocialRecord> Records = [];
    public SocialLedger(Map map) : base(map) { }
    public SocialRecord Pair(Pawn offender, Pawn victim) => Records.FirstOrDefault(r => r.Offender == offender && r.Victim == victim);
    private bool CanDiscuss(Pawn a, Pawn b) => a is { Spawned: true, Dead: false } && b is { Spawned: true, Dead: false } &&
        a.Map == map && b.Map == map && !a.Downed && !b.Downed && a.Awake() && b.Awake() &&
        a.Position.InHorDistOf(b.Position, 12);
    public void Insult(Pawn offender, Pawn victim)
    {
        if (offender == null || victim == null || offender.Faction != Faction.OfPlayer || victim.Faction != Faction.OfPlayer) return;
        var r = Pair(offender, victim);
        if (r == null) { r = new SocialRecord { Offender = offender, Victim = victim }; Records.Add(r); }
        int tick = Find.TickManager.TicksGame;
        if (tick - r.LastInsult > 900000) r.Insults = 0;
        r.LastInsult = tick; r.ApologyOffered = false;
        if (++r.Insults >= 3)
        { SocietyMod.Memory(victim, offender, "HonorSociety_Rival"); SocietyMod.Memory(offender, victim, "HonorSociety_Rival"); }
    }
    public bool Offer(DuelRecord duel, Pawn offender)
    {
        var victim = duel.PrincipalChallenger ?? duel.Challenger;
        if (duel.Phase != DuelPhase.Pending || (duel.PrincipalOffender ?? duel.Offender) != offender ||
            !CanDiscuss(offender, victim)) return false;
        var r = Pair(offender, victim);
        if (r == null) return false;
        r.ApologyOffered = true;
        Messages.Message("HS_Offered".Translate(offender.LabelShort, victim.LabelShort), victim, MessageTypeDefOf.NeutralEvent); return true;
    }
    public bool Accept(DuelRecord duel, Pawn victim)
    {
        var offender = duel.PrincipalOffender ?? duel.Offender;
        var r = Pair(offender, victim);
        if (r?.ApologyOffered != true || !CanDiscuss(offender, victim) || !map.GetComponent<DuelMapComponent>().AcceptApology(duel, victim)) return false;
        r.ApologyOffered = false; r.Insults = Math.Max(0, r.Insults - 1);
        SocietyMod.Memory(victim, offender, "HonorSociety_Apology");
        HonorApi.Change(offender, -.01f, "admitted insult"); return true;
    }
    public void DuelEnded(DuelRecord d)
    {
        if (d.Winner == null || d.DishonorApplied) return;
        var winner = d.Beneficiary(d.Winner);
        SocietyMod.Family(winner, .015f);
        var record = new SocialRecord { Victor = winner, RumorAt = Find.TickManager.TicksGame + 60000 };
        foreach (var witness in map.mapPawns.FreeColonistsSpawned.Where(p => !d.Involves(p) &&
            p.Position.InHorDistOf(d.Winner.Position, 20) && GenSight.LineOfSight(p.Position, d.Winner.Position, map)))
        { SocietyMod.Memory(witness, winner, "HonorSociety_Witness"); record.Informed.Add(witness); }
        Records.Add(record);
    }
    public override void MapComponentTick()
    {
        if (Find.TickManager.TicksGame % 250 != 0) return;
        foreach (var r in Records.Where(r => r.Victor != null && r.RumorAt > 0 && r.RumorAt <= Find.TickManager.TicksGame))
        {
            foreach (var listener in map.mapPawns.FreeColonistsSpawned.Where(p => p != r.Victor && !r.Informed.Contains(p)))
                SocietyMod.Memory(listener, r.Victor, "HonorSociety_Rumor");
            r.RumorAt = 0;
        }
        Records.RemoveAll(r => r.Victor != null && r.RumorAt == 0 || r.Victor == null && Find.TickManager.TicksGame - r.LastInsult > 1800000);
    }
    public override void ExposeData()
    { Scribe_Collections.Look(ref Records, "honorSocietyRecords", LookMode.Deep); if (Scribe.mode == LoadSaveMode.PostLoadInit) Records ??= []; }
}
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
internal static class SocialCommands
{
    static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        foreach (var g in __result) yield return g;
        var p = __instance; if (p.Map == null || p.Faction != Faction.OfPlayer) yield break;
        var duel = p.Map.GetComponent<DuelMapComponent>().Duels.FirstOrDefault(d => d.Phase == DuelPhase.Pending && d.Involves(p));
        if (duel == null) yield break;
        var ledger = p.Map.GetComponent<SocialLedger>();
        bool victim = (duel.PrincipalChallenger ?? duel.Challenger) == p;
        if (!victim && (duel.PrincipalOffender ?? duel.Offender) != p) yield break;
        yield return new Command_Action { defaultLabel = (victim ? "HS_Accept" : "HS_Offer").Translate(),
            defaultDesc = "HS_Desc".Translate(), action = () => {
                if (!(victim ? ledger.Accept(duel, p) : ledger.Offer(duel, p)))
                    Messages.Message("HS_NotReady".Translate(), MessageTypeDefOf.RejectInput);
            } };
    }
}
