using Honor.Rules;
int assertions = 0;
void Check(bool condition, string name) { assertions++; if (!condition) throw new Exception(name); }
for (int i = 0; i <= 10000; i++)
{
    float value = i / 10000f;
    float result = HonorRules.Decay(value, .001f, 60000 * 1000);
    Check(HonorRules.Tier(result) == HonorRules.Tier(value), $"tier floor at {value}");
    Check(result <= value, "decay never increases honor");
}
Check(HonorRules.Tier(HonorRules.Event(.61f, -.1f)) < HonorRules.Tier(.61f), "event demotion");
Check(HonorRules.Event(.95f, .2f) == 1, "upper clamp");
Check(HonorRules.Event(.05f, -.2f) == 0, "lower clamp");
Check(HonorRules.Resolve(DuelMode.Death, true) == DuelMode.Death, "ideology priority");
Check(HonorRules.Resolve(null, true) == DuelMode.FirstBlood, "industrial fallback");
Check(HonorRules.Resolve(null, false) == DuelMode.Surrender, "preindustrial fallback");
for (int mask = 0; mask < 64; mask++)
{
    bool adult = (mask & 1) != 0, slave = (mask & 2) != 0, prisoner = (mask & 4) != 0;
    bool minors = (mask & 8) != 0, slaves = (mask & 16) != 0, prisoners = (mask & 32) != 0;
    Check(HonorRules.Eligible(adult, slave, prisoner, minors, slaves, prisoners) ==
        ((adult || minors) && (!slave || slaves) && (!prisoner || prisoners)), "eligibility matrix");
}
Check(HonorRules.Ends(DuelMode.FirstBlood, false, false, true, 0, .35f), "first blood");
Check(!HonorRules.Ends(DuelMode.Surrender, false, false, true, .1f, .35f), "surrender ignores blood");
Check(HonorRules.Ends(DuelMode.Surrender, false, false, false, .4f, .35f), "pain surrender");
Check(HonorRules.Ends(DuelMode.Surrender, false, true, false, .1f, .35f), "downed surrender");
Check(!HonorRules.Ends(DuelMode.Death, false, true, true, 1, .35f), "death duel continues");
foreach (var mode in Enum.GetValues<DuelMode>()) Check(HonorRules.Ends(mode, true, true, true, 1, .35f), "death ends all");
Console.WriteLine($"PASS: {assertions} assertions (tier boundaries, event demotion, eligibility, ideology priority, endings).");
