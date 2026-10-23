using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Honor.Core;

public sealed class HonorMod : Mod
{
    public static HonorSettings Settings;
    public static bool PatchesReady;
    public HonorMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<HonorSettings>();
        try
        {
            new Harmony("honor.series.core").PatchAll();
            PatchesReady = true;
            Log.Message("[Honor.Core] Harmony patches installed; target 1.7.4985 / Harmony 3.");
        }
        catch (Exception e) { Log.Error("[Honor.Core] Disabled: patch installation failed: " + e); }
    }
    public override string SettingsCategory() => "Honor.Core";
    public override void DoSettingsWindowContents(Rect rect)
    {
        var ui = new Listing_Standard(); ui.Begin(rect);
        ui.CheckboxLabeled("Honor_Auto".Translate(), ref Settings.AutoChallenge);
        ui.CheckboxLabeled("Honor_Minors".Translate(), ref Settings.AllowMinors);
        ui.CheckboxLabeled("Honor_Slaves".Translate(), ref Settings.AllowSlaves);
        ui.CheckboxLabeled("Honor_Prisoners".Translate(), ref Settings.AllowPrisoners);
        ui.CheckboxLabeled("Honor_Ranged".Translate(), ref Settings.AllowLongRange);
        ui.CheckboxLabeled("Honor_Verbose".Translate(), ref Settings.Verbose);
        ui.Label("Honor_Decay".Translate(Settings.DecayPerDay.ToString("P2")));
        Settings.DecayPerDay = ui.Slider(Settings.DecayPerDay, 0, .01f);
        ui.Label("Honor_Pain".Translate(Settings.SurrenderPain.ToString("P0")));
        Settings.SurrenderPain = ui.Slider(Settings.SurrenderPain, .1f, .8f);
        ui.End();
    }
}

public sealed class HonorSettings : ModSettings
{
    public bool AutoChallenge = true, AllowMinors, AllowSlaves, AllowPrisoners, AllowLongRange, Verbose;
    public float DecayPerDay = .001f, SurrenderPain = .35f;
    public override void ExposeData()
    {
        Scribe_Values.Look(ref AutoChallenge, "autoChallenge", true);
        Scribe_Values.Look(ref AllowMinors, "allowMinors");
        Scribe_Values.Look(ref AllowSlaves, "allowSlaves");
        Scribe_Values.Look(ref AllowPrisoners, "allowPrisoners");
        Scribe_Values.Look(ref AllowLongRange, "allowLongRange");
        Scribe_Values.Look(ref Verbose, "verbose");
        Scribe_Values.Look(ref DecayPerDay, "decayPerDay", .001f);
        Scribe_Values.Look(ref SurrenderPain, "surrenderPain", .35f);
        DecayPerDay = Mathf.Clamp(DecayPerDay, 0, .01f);
        SurrenderPain = Mathf.Clamp(SurrenderPain, .1f, .8f);
    }
}

[DefOf]
public static class HonorDefOf
{
    public static NeedDef Honor_Need;
    public static JobDef Honor_Duel;
    public static ThoughtDef Honor_Respected, Honor_Dishonorable;
    public static TraitDef Honor_Obsessed, Honor_Honorable, Honor_Vile;
    static HonorDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(HonorDefOf));
}
