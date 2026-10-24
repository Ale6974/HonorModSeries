using Honor.Rules;
using RimWorld;
using Verse;

namespace Honor.Core;

public sealed class DuelRecord : IExposable
{
    public int Id, CreatedTick, StartedTick, EndedTick;
    public Pawn Challenger, Offender, Winner;
    public Pawn PrincipalChallenger, PrincipalOffender;
    public Pawn Beneficiary(Pawn combatant) => combatant == Challenger ? PrincipalChallenger ?? Challenger : PrincipalOffender ?? Offender;
    public bool Involves(Pawn p) => Contains(p) || p != null && (p == PrincipalChallenger || p == PrincipalOffender);
    public ThingWithComps WeaponLeft, WeaponRight;
    public DuelPhase Phase;
    public DuelMode Mode;
    public IntVec3 Left, Right;
    public bool Ranged, ChallengerDrafted, OffenderDrafted, RewardApplied, DishonorApplied;
    public float BleedLeft, BleedRight;
    public string Reason;
    public List<Apparel> ArmorLeft = [], ArmorRight = [];
    public bool Active => Phase is DuelPhase.Gathering or DuelPhase.Fighting;
    public bool Contains(Pawn pawn) => pawn != null && (pawn == Challenger || pawn == Offender);
    public Pawn Other(Pawn pawn) => pawn == Challenger ? Offender : Challenger;
    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id");
        Scribe_Values.Look(ref CreatedTick, "createdTick");
        Scribe_Values.Look(ref StartedTick, "startedTick");
        Scribe_Values.Look(ref EndedTick, "endedTick");
        Scribe_References.Look(ref Challenger, "challenger");
        Scribe_References.Look(ref Offender, "offender");
        Scribe_References.Look(ref Winner, "winner");
        Scribe_References.Look(ref PrincipalChallenger, "principalChallenger");
        Scribe_References.Look(ref PrincipalOffender, "principalOffender");
        Scribe_References.Look(ref WeaponLeft, "weaponLeft");
        Scribe_References.Look(ref WeaponRight, "weaponRight");
        Scribe_Values.Look(ref Phase, "phase");
        Scribe_Values.Look(ref Mode, "mode");
        Scribe_Values.Look(ref Left, "left");
        Scribe_Values.Look(ref Right, "right");
        Scribe_Values.Look(ref Ranged, "ranged");
        Scribe_Values.Look(ref ChallengerDrafted, "challengerDrafted");
        Scribe_Values.Look(ref OffenderDrafted, "offenderDrafted");
        Scribe_Values.Look(ref RewardApplied, "rewardApplied");
        Scribe_Values.Look(ref DishonorApplied, "dishonorApplied");
        Scribe_Values.Look(ref BleedLeft, "bleedLeft");
        Scribe_Values.Look(ref BleedRight, "bleedRight");
        Scribe_Values.Look(ref Reason, "reason");
        Scribe_Collections.Look(ref ArmorLeft, "armorLeft", LookMode.Reference);
        Scribe_Collections.Look(ref ArmorRight, "armorRight", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) { ArmorLeft ??= []; ArmorRight ??= []; }
    }
}
