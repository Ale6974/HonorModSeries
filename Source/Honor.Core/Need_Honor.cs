using Honor.Rules;
using RimWorld;
using Verse;

namespace Honor.Core;

public sealed class Need_Honor : Need
{
    public Need_Honor(Pawn pawn) : base(pawn) { threshPercents = [.2f, .4f, .6f, .8f]; }
    public int Tier => HonorRules.Tier(CurLevel);
    public override void SetInitialLevel() => CurLevel = .5f;
    public override void NeedInterval()
    {
        if (!IsFrozen) CurLevel = HonorRules.Decay(CurLevel, HonorMod.Settings?.DecayPerDay ?? .001f, 150);
    }
    public void ApplyEvent(float delta) => CurLevel = HonorRules.Event(CurLevel, delta, HonorApi.Sensitivity(pawn));
    public override string GetTipString() => base.GetTipString() + "\n" + "Honor_Tier".Translate(Tier + 1);
}

/// <summary>Stable extension surface. No separate reputation balance exists.</summary>
public static class HonorApi
{
    public static event Action<DuelRecord> DuelEnded;
    public static event Action<Pawn, Pawn> InsultOccurred;
    public static event Action<DuelRecord, Pawn> DishonorOccurred;
    public static readonly List<Func<Pawn, bool>> AdditionalParticipantValidators = [];
    public static int PendingDelayTicks;
    public static readonly List<Func<Pawn, float>> SensitivityModifiers = [];
    public static Need_Honor Need(Pawn pawn) => pawn?.needs?.TryGetNeed<Need_Honor>();
    public static void Change(Pawn pawn, float delta, string reason)
    {
        Need(pawn)?.ApplyEvent(delta);
        if (HonorMod.Settings.Verbose) Log.Message($"[Honor.Core] event={reason} pawn={pawn?.ThingID} delta={delta}");
    }
    public static float Sensitivity(Pawn pawn)
    {
        var traits = pawn?.story?.traits;
        float value = traits?.HasTrait(HonorDefOf.Honor_Obsessed) == true ? 1.75f :
            traits?.HasTrait(HonorDefOf.Honor_Honorable) == true ? 1.25f :
            traits?.HasTrait(HonorDefOf.Honor_Vile) == true ? .5f : 1;
        foreach (var modifier in SensitivityModifiers) value *= Math.Clamp(modifier(pawn), 0, 5);
        return value;
    }
    internal static void NotifyEnded(DuelRecord duel)
    {
        if (DuelEnded == null) return;
        foreach (Action<DuelRecord> handler in DuelEnded.GetInvocationList())
            try { handler(duel); } catch (Exception e) { Log.Error("[Honor.Core] Extension failed: " + e); }
    }
    internal static void NotifyInsult(Pawn offender, Pawn victim)
    {
        if (InsultOccurred == null) return;
        foreach (Action<Pawn, Pawn> handler in InsultOccurred.GetInvocationList())
            try { handler(offender, victim); } catch (Exception e) { Log.Error("[Honor.Core] Insult extension failed: " + e); }
    }
    internal static void NotifyDishonor(DuelRecord duel, Pawn pawn)
    {
        if (DishonorOccurred == null) return;
        foreach (Action<DuelRecord, Pawn> handler in DishonorOccurred.GetInvocationList())
            try { handler(duel, pawn); } catch (Exception e) { Log.Error("[Honor.Core] Dishonor extension failed: " + e); }
    }
}

/// <summary>Attach to a PreceptDef; only the challenger's ideology is consulted.</summary>
public sealed class DuelRuleExtension : DefModExtension
{
    public DuelMode mode;
}
