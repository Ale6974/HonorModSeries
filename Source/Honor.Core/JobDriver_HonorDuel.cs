using Honor.Rules;
using Verse;
using Verse.AI;

namespace Honor.Core;

public sealed class JobDriver_HonorDuel : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
        var wait = ToilMaker.MakeToil("HonorWaitForOpponent");
        wait.defaultCompleteMode = ToilCompleteMode.Never;
        wait.tickAction = () =>
        {
            var duel = Map.GetComponent<DuelMapComponent>().ActiveFor(pawn);
            if (duel == null) EndJobWith(JobCondition.Succeeded);
            else if (duel.Phase == DuelPhase.Fighting) ReadyForNextToil();
        };
        yield return wait;
        var fight = ToilMaker.MakeToil("HonorControlledCombat");
        fight.defaultCompleteMode = ToilCompleteMode.Never;
        fight.tickAction = () =>
        {
            var manager = Map.GetComponent<DuelMapComponent>();
            var duel = manager.ActiveFor(pawn);
            if (duel == null || manager.CheckEnd(duel)) { EndJobWith(JobCondition.Succeeded); return; }
            var target = duel.Other(pawn);
            if (pawn.stances.FullBodyBusy) return;
            if (duel.Ranged)
            {
                var verb = pawn.equipment.PrimaryEq.PrimaryVerb;
                if (!verb.CanHitTarget(target)) { manager.Finish(duel, null, "firing line blocked"); return; }
                verb.TryStartCastOn(target, false, false, true);
            }
            else if (pawn.CanReachImmediate(target, PathEndMode.Touch)) pawn.meleeVerbs.TryMeleeAttack(target);
            else if (!pawn.pather.Moving) pawn.pather.StartPath(target, PathEndMode.Touch);
        };
        yield return fight;
    }
}
