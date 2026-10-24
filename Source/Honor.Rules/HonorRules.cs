namespace Honor.Rules;

public enum DuelMode { FirstBlood, Surrender, Death }
public enum DuelPhase { Pending, Gathering, Fighting, Completed, Cancelled }

/// <summary>Rules independent of the engine; values are normalized to 0..1.</summary>
public static class HonorRules
{
    public static int Tier(float honor) => Math.Min(4, (int)(RuntimeCompat.Clamp(honor, 0, 1) * 5));
    public static float Floor(float honor) => Tier(honor) * .2f;
    public static float Decay(float honor, float perDay, int ticks)
        => Math.Max(Floor(honor), honor - Math.Max(0, perDay) * Math.Max(0, ticks) / 60000f);
    public static float Event(float honor, float delta, float sensitivity = 1)
        => RuntimeCompat.Clamp(honor + delta * Math.Max(0, sensitivity), 0, 1);
    public static bool Eligible(bool adult, bool slave, bool prisoner, bool allowMinor, bool allowSlave, bool allowPrisoner)
        => (adult || allowMinor) && (!slave || allowSlave) && (!prisoner || allowPrisoner);
    public static bool Ends(DuelMode mode, bool dead, bool downed, bool newBleeding, float pain, float surrenderPain)
        => dead || (mode != DuelMode.Death && (downed || (mode == DuelMode.FirstBlood ? newBleeding : pain >= surrenderPain)));
    public static DuelMode Resolve(DuelMode? ideology, bool industrialOrHigher)
        => ideology ?? (industrialOrHigher ? DuelMode.FirstBlood : DuelMode.Surrender);
}
