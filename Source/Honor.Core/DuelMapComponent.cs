using Honor.Rules;
using RimWorld;
using Verse;
using Verse.AI;

namespace Honor.Core;

public sealed class DuelMapComponent : MapComponent
{
    public List<DuelRecord> Duels = [];
    public IntVec3 Arena = IntVec3.Invalid;
    private int nextId = 1;
    public DuelMapComponent(Map map) : base(map) { }
    public override void ExposeData()
    {
        Scribe_Values.Look(ref Arena, "honorArena", IntVec3.Invalid);
        Scribe_Values.Look(ref nextId, "honorNextId", 1);
        Scribe_Collections.Look(ref Duels, "honorDuels", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) Duels ??= [];
    }
    public static bool Eligible(Pawn p)
    {
        var s = HonorMod.Settings;
        return HonorMod.PatchesReady && p is { Spawned: true, Dead: false } && p.RaceProps.Humanlike &&
            (p.Faction == Faction.OfPlayer || p.IsPrisonerOfColony || HonorApi.AdditionalParticipantValidators.Any(v => v(p))) &&
            !p.WorkTagIsDisabled(WorkTags.Violent) && HonorRules.Eligible(p.ageTracker.AgeBiologicalYears >= 18,
            p.IsSlave, p.IsPrisoner, s.AllowMinors, s.AllowSlaves, s.AllowPrisoners);
    }
    public bool CanRegister(Pawn challenger, Pawn offender) => challenger != offender && Eligible(challenger) &&
        Eligible(offender) && challenger.Map == offender.Map && !challenger.HostileTo(offender) &&
        !Duels.Any(d => (d.Active || d.Phase == DuelPhase.Pending) && (d.Involves(challenger) || d.Involves(offender)));

    public void RegisterInsult(Pawn offender, Pawn recipient)
    {
        HonorApi.Change(recipient, -.04f, "insult");
        HonorApi.NotifyInsult(offender, recipient);
        if (!CanRegister(recipient, offender)) return;
        var d = new DuelRecord { Id = nextId++, Challenger = recipient, Offender = offender,
            CreatedTick = Find.TickManager.TicksGame, Mode = ResolveMode(recipient), Phase = DuelPhase.Pending };
        Duels.Add(d);
        Messages.Message("Honor_Challenge".Translate(recipient.LabelShort, offender.LabelShort), recipient, MessageTypeDefOf.NeutralEvent);
        Trace(d, "mandatory challenge queued");
    }
    private static DuelMode ResolveMode(Pawn challenger)
    {
        // Conflicting custom precepts resolve deterministically by defName; never use the offender's ideology.
        var ext = challenger.Ideo?.PreceptsListForReading.OrderBy(p => p.def.defName)
            .Select(p => p.def.GetModExtension<DuelRuleExtension>()).FirstOrDefault(e => e != null);
        return HonorRules.Resolve(ext?.mode, (challenger.Faction?.def.techLevel ?? TechLevel.Industrial) >= TechLevel.Industrial);
    }
    public DuelRecord ActiveFor(Pawn p) => Duels.FirstOrDefault(d => d.Active && d.Contains(p));
    public bool AcceptApology(DuelRecord d, Pawn victim)
    {
        if (d.Phase != DuelPhase.Pending || (d.PrincipalChallenger ?? d.Challenger) != victim) return false;
        HonorApi.Change(victim, .04f, "apology accepted");
        Finish(d, null, "apology accepted"); return true;
    }
    public bool AssignProxy(DuelRecord d, Pawn principal, Pawn proxy)
    {
        if (d.Phase != DuelPhase.Pending || !Eligible(proxy) || proxy.Map != map || d.Involves(proxy) ||
            Duels.Any(x => x != d && (x.Active || x.Phase == DuelPhase.Pending) && x.Involves(proxy))) return false;
        if ((d.PrincipalChallenger ?? d.Challenger) == principal)
        { d.PrincipalChallenger = principal; d.Challenger = proxy; return true; }
        if ((d.PrincipalOffender ?? d.Offender) == principal)
        { d.PrincipalOffender = principal; d.Offender = proxy; return true; }
        return false;
    }
    public DuelRecord RecentFor(Pawn p) => Duels.LastOrDefault(d => d.Contains(p) &&
        (d.Active || (d.EndedTick > 0 && Find.TickManager.TicksGame - d.EndedTick < 600)));
    public override void MapComponentTick()
    {
        int tick = Find.TickManager.TicksGame;
        foreach (var d in Duels.Where(x => x.Active).ToList())
        {
            if (d.Challenger == null || d.Offender == null || !d.Challenger.Spawned || !d.Offender.Spawned ||
                d.Challenger.Map != map || d.Offender.Map != map)
            { Finish(d, null, "participant left map"); continue; }
            if (CheckEnd(d)) continue;
            if (tick - d.StartedTick > 15000 || d.Challenger.InMentalState || d.Offender.InMentalState ||
                !Eligible(d.Challenger) || !Eligible(d.Offender) || !JobsIntact(d) || !EquipmentIntact(d))
            { Finish(d, null, "interrupted or equipment changed"); continue; }
            if (tick % 30 == 0 && !ArenaClear(d)) { Finish(d, null, "arena unsafe"); continue; }
            if (d.Phase == DuelPhase.Gathering && d.Challenger.Position == d.Left && d.Offender.Position == d.Right)
            {
                if (!StripArmor(d.Challenger, d.ArmorLeft) || !StripArmor(d.Offender, d.ArmorRight))
                { Finish(d, null, "could not remove armor"); continue; }
                d.Phase = DuelPhase.Fighting; Trace(d, "combat started");
            }
        }
        if (tick % 60 != 0) return;
        foreach (var d in Duels.Where(x => x.Phase == DuelPhase.Pending).ToList())
        {
            if (d.Challenger == null || d.Offender == null || d.Challenger.Dead || d.Offender.Dead)
            { Finish(d, null, "participant unavailable"); continue; }
            if (!HonorMod.Settings.AutoChallenge || Duels.Any(x => x.Active)) break;
            if (tick - d.CreatedTick < HonorApi.PendingDelayTicks) continue;
            TryBegin(d);
        }
        // Bound history, retain pending obligations and projectile grace windows.
        Duels.RemoveAll(d => !d.Active && d.Phase != DuelPhase.Pending && tick - d.EndedTick > 60000);
    }
    private bool JobsIntact(DuelRecord d) =>
        (d.Challenger.CurJobDef == HonorDefOf.Honor_Duel || (d.Mode == DuelMode.Death && d.Challenger.Downed)) &&
        (d.Offender.CurJobDef == HonorDefOf.Honor_Duel || (d.Mode == DuelMode.Death && d.Offender.Downed));
    private static bool EquipmentIntact(DuelRecord d) =>
        ((d.Mode == DuelMode.Death && d.Challenger.Downed) || d.Challenger.equipment.Primary == d.WeaponLeft) &&
        ((d.Mode == DuelMode.Death && d.Offender.Downed) || d.Offender.equipment.Primary == d.WeaponRight);
    public bool TryBegin(DuelRecord d)
    {
        if (d.Phase != DuelPhase.Pending || Duels.Any(x => x.Active)) return Block(d, "another duel is active");
        if (!Arena.IsValid) return Block(d, "no arena selected");
        if (!Eligible(d.Challenger) || !Eligible(d.Offender)) return Block(d, "participant eligibility changed");
        if (!Ready(d.Challenger)) return Block(d, "challenger not awake, undrafted, healthy or capable");
        if (!Ready(d.Offender)) return Block(d, "offender not awake, undrafted, healthy or capable");
        if (!WeaponsMatch(d.Challenger, d.Offender, out bool ranged)) return Block(d, "allowed weapons do not match");
        d.Ranged = ranged;
        d.WeaponLeft = d.Challenger.equipment.Primary; d.WeaponRight = d.Offender.equipment.Primary;
        d.Left = Arena + new IntVec3(ranged ? -5 : -1, 0, 0);
        d.Right = Arena + new IntVec3(ranged ? 5 : 1, 0, 0);
        if (!ArenaClear(d)) return Block(d, "arena blocked, occupied, burning or threatened");
        if (!GenSight.LineOfSight(d.Left, d.Right, map)) return Block(d, "firing line blocked");
        if (!d.Challenger.CanReach(d.Left, PathEndMode.OnCell, Danger.None) ||
            !d.Offender.CanReach(d.Right, PathEndMode.OnCell, Danger.None)) return Block(d, "no safe path to arena");
        d.BleedLeft = d.Challenger.health.hediffSet.BleedRateTotal;
        d.BleedRight = d.Offender.health.hediffSet.BleedRateTotal;
        d.ChallengerDrafted = d.Challenger.Drafted; d.OffenderDrafted = d.Offender.Drafted;
        d.StartedTick = Find.TickManager.TicksGame; d.Phase = DuelPhase.Gathering; d.Reason = null;
        StartJob(d.Challenger, d.Offender, d.Left); StartJob(d.Offender, d.Challenger, d.Right);
        Trace(d, "gathering"); return true;
    }
    private static bool Block(DuelRecord d, string reason) { d.Reason = reason; return false; }
    private static bool Ready(Pawn p) => !p.Downed && p.Awake() && !p.InMentalState &&
        !p.Drafted && !p.health.HasHediffsNeedingTend() && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
    private static void StartJob(Pawn p, Pawn opponent, IntVec3 cell)
    {
        var job = JobMaker.MakeJob(HonorDefOf.Honor_Duel, opponent, cell);
        job.playerForced = true; job.expiryInterval = 15000; job.checkOverrideOnExpire = false;
        p.jobs.StartJob(job, JobCondition.InterruptForced);
    }
    public static bool WeaponsMatch(Pawn a, Pawn b, out bool ranged)
    {
        var wa = a.equipment?.Primary; var wb = b.equipment?.Primary;
        ranged = wa?.def.IsRangedWeapon == true;
        if (wa == null || wb == null || wa.def != wb.def || wa.Stuff != wb.Stuff) return false;
        wa.TryGetQuality(out var qa); wb.TryGetQuality(out var qb);
        if (qa != qb || Math.Abs(wa.HitPoints / (float)wa.MaxHitPoints - wb.HitPoints / (float)wb.MaxHitPoints) > .1f) return false;
        bool basic = wa.def.defName is "MeleeWeapon_LongSword" or "MeleeWeapon_Knife" or "Gun_Revolver";
        // Explosive / beam / modded verbs have unbounded collateral paths; MVP permits only vanilla bullets.
        if (!basic && !(HonorMod.Settings.AllowLongRange && ranged)) return false;
        if (ranged && (wa.def.Verbs == null || wa.def.Verbs.Any(v => v.defaultProjectile == null ||
            v.defaultProjectile.projectile == null || v.defaultProjectile.projectile.explosionRadius > 0 ||
            v.defaultProjectile.projectile.damageDef != DamageDefOf.Bullet))) return false;
        return true;
    }
    private bool ArenaClear(DuelRecord d)
    {
        int radius = d.Ranged ? 7 : 4;
        foreach (var c in GenRadial.RadialCellsAround(Arena, radius, true))
        {
            if (!c.InBounds(map) || !c.Standable(map) || c.GetDoor(map) != null || c.GetThingList(map).Any(t => t is Fire)) return false;
            if (c.GetThingList(map).OfType<Pawn>().Any(p => !d.Contains(p))) return false;
        }
        return !map.mapPawns.AllPawnsSpawned.Any(p => !d.Contains(p) && !p.Downed && p.Awake() &&
            p.Position.InHorDistOf(Arena, radius + 20) && GenSight.LineOfSight(p.Position, Arena, map) &&
            (p.HostileTo(d.Challenger) || p.CurJobDef == JobDefOf.AttackStatic));
    }
    private static bool StripArmor(Pawn p, List<Apparel> removed)
    {
        foreach (var apparel in p.apparel.WornApparel.Where(IsArmor).ToList())
        {
            if (!p.apparel.TryDrop(apparel, out var dropped, p.Position, false)) return false;
            removed.Add(dropped);
        }
        return true;
    }
    public static bool IsArmor(Apparel a) => a.def.apparel.tags?.Contains("Armor") == true ||
        a.def.thingCategories?.Any(c => c.defName is "ApparelArmor" or "ArmorHeadgear") == true;
    public bool CheckEnd(DuelRecord d)
    {
        if (!d.Active) return true;
        bool left = Lost(d, d.Challenger, d.BleedLeft), right = Lost(d, d.Offender, d.BleedRight);
        if (!left && !right) return false;
        Finish(d, left == right ? null : left ? d.Offender : d.Challenger, "duel resolved"); return true;
    }
    private static bool Lost(DuelRecord d, Pawn p, float bleed) => HonorRules.Ends(d.Mode, p.Dead, p.Downed,
        p.health.hediffSet.BleedRateTotal > bleed + .00001f, p.health.hediffSet.PainTotal, HonorMod.Settings.SurrenderPain);
    public void Finish(DuelRecord d, Pawn winner, string reason)
    {
        if (d.Phase is DuelPhase.Completed or DuelPhase.Cancelled) return;
        bool hadStarted = d.Active;
        // Persist terminal phase BEFORE stopping jobs, tending or notifying extensions (all can re-enter).
        d.Phase = winner == null ? DuelPhase.Cancelled : DuelPhase.Completed;
        d.Winner = winner; d.EndedTick = Find.TickManager.TicksGame; d.Reason = reason;
        foreach (var p in new[] { d.Challenger, d.Offender })
        {
            if (p == null) continue;
            if (p.CurJobDef == HonorDefOf.Honor_Duel) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            p.stances?.CancelBusyStanceHard();
            if (hadStarted && !p.Dead) EmergencyTend(p);
        }
        Restore(d.Challenger, d.ArmorLeft); Restore(d.Offender, d.ArmorRight);
        if (winner != null && !d.RewardApplied)
        {
            d.RewardApplied = true;
            HonorApi.Change(d.Beneficiary(winner), .08f, "duel victory"); HonorApi.Change(d.Beneficiary(d.Other(winner)), -.04f, "duel defeat");
            if (d.Beneficiary(winner) != winner) HonorApi.Change(winner, .02f, "proxy victory");
            d.Other(winner)?.needs?.mood?.thoughts?.memories?.TryGainMemory(HonorDefOf.Honor_Respected, winner);
        }
        Trace(d, reason);
        Messages.Message("Honor_Result".Translate(d.Id, winner?.LabelShort ?? "Honor_NoWinner".Translate().ToString()), MessageTypeDefOf.NeutralEvent);
        HonorApi.NotifyEnded(d);
    }
    public void Dishonor(DuelRecord d, Pawn killer)
    {
        if (d.DishonorApplied) return;
        d.DishonorApplied = true;
        HonorApi.Change(killer, -.3f, "dishonorable killing");
        foreach (var witness in map.mapPawns.FreeColonistsSpawned.Where(p => p != killer &&
            p.Position.InHorDistOf(killer.Position, 20) && GenSight.LineOfSight(p.Position, killer.Position, map)))
            witness.needs?.mood?.thoughts?.memories?.TryGainMemory(HonorDefOf.Honor_Dishonorable, killer);
        Trace(d, "dishonorable killing by " + killer.ThingID);
        HonorApi.NotifyDishonor(d, killer);
    }
    private void EmergencyTend(Pawn patient)
    {
        var doctor = map.mapPawns.FreeColonistsSpawned.Where(p => p != patient && !p.Downed && !p.InMentalState &&
            !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && p.Position.InHorDistOf(patient.Position, 15) &&
            p.CanReach(patient, PathEndMode.Touch, Danger.None)).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine).Level ?? 0).FirstOrDefault();
        // Immediate field stabilization; no medicine spawned or consumed. This does not heal injuries.
        for (int i = 0; i < 100 && patient.health.HasHediffsNeedingTend(); i++) TendUtility.DoTend(doctor, patient, null);
    }
    private static void Restore(Pawn p, List<Apparel> items)
    {
        if (p is not { Dead: false, Spawned: true } || p.apparel == null) return;
        foreach (var a in items.Where(a => a is { Destroyed: false, Spawned: true }).ToList())
            if (a.Position.InHorDistOf(p.Position, 12)) { a.DeSpawn(); p.apparel.Wear(a, false, false); }
        items.Clear();
    }
    private static void Trace(DuelRecord d, string text) => Log.Message($"[Honor.Core] duel={d.Id} phase={d.Phase} mode={d.Mode} {text}");
}
