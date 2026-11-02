using System.Reflection;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

internal static class ContainerMapEvidence
{
    internal static bool Supported(Thing t) => t?.GetType() == typeof(Building_Casket) || t?.GetType() == typeof(Building_Crate);
    private static object Field(Type type, string name, Type expected, object owner)
    {
        var f = AccessTools.Field(type, name);
        if (f == null || f.IsStatic || f.FieldType != expected) throw new ResidentMoveBlocked("RE_ContainerMapUnavailable");
        return f.GetValue(owner);
    }
    internal static (ThingOwner Owner, NeutralMapContainer Evidence) Capture(Thing thing)
    {
        if (!Supported(thing) || thing.Faction != null || thing.stackCount != 1) throw new ResidentMoveBlocked("RE_ContainerMapUnavailable");
        foreach (var method in new[] {
            AccessTools.Method(typeof(Building_Casket), nameof(Building_Casket.GetDirectlyHeldThings), []),
            AccessTools.Method(typeof(Building_Casket), nameof(Building_Casket.GetChildHolders), [typeof(List<IThingHolder>)]) })
            if (method == null || Harmony.GetPatchInfo(method)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_ContainerMapUnavailable");
        var casket = (Building_Casket)thing; var owner = casket.GetDirectlyHeldThings();
        if (owner?.GetType() != typeof(ThingOwner<Thing>) || owner.Owner != thing || owner.Count > 10000 || owner.Any(t => t == null || t.holdingOwner != owner) || owner.Distinct().Count() != owner.Count)
            throw new ResidentMoveBlocked("RE_ContainerMapUnavailable");
        if (casket.AllComps.Any(c => c == null || c.parent != thing) || casket.AllComps.Count(c => c.GetType() == typeof(CompHackable)) > 1 || casket.AllComps.Count(c => c.GetType() == typeof(CompGlower)) > 1) throw new ResidentMoveBlocked("RE_ContainerMapUnavailable");
        var comps = new List<object>();
        foreach (var comp in casket.AllComps)
        {
            if (comp.GetType() == typeof(CompHackable))
            {
                if (thing.GetType() != typeof(Building_Crate) || comp.parent != thing) throw new ResidentMoveBlocked("RE_ContainerMapUnavailable");
                string[] names = ["hackingStartedSignal", "hackingCompletedSignal", "progress", "defence", "lastUserSpeed", "lastHackTick", "lastUser", "progressLastLockout", "lockedOutUntilTick", "lockedOutPermanently", "sentLetter", "autohack", "hacked"];
                var fields = typeof(CompHackable).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (fields.Length != names.Length || fields.Any(f => !names.Contains(f.Name))) throw new ResidentMoveBlocked("RE_ContainerMapUnavailable");
                var values = new Dictionary<string, object>();
                foreach (var f in fields.OrderBy(f => f.Name, StringComparer.Ordinal))
                {
                    object value = f.GetValue(comp);
                    if (f.Name == "lastUser") { if (f.FieldType != typeof(Pawn) || value != null) throw new ResidentMoveBlocked("RE_ContainerMapUnavailable"); }
                    else if (f.FieldType == typeof(float)) { if (!RuntimeCompat.IsFinite((float)value)) throw new ResidentMoveBlocked("RE_ContainerMapUnavailable"); }
                    else if (f.FieldType == typeof(string)) { if (((string)value)?.Length > 10000) throw new ResidentMoveBlocked("RE_ContainerMapUnavailable"); }
                    else if (f.FieldType != typeof(int) && f.FieldType != typeof(bool)) throw new ResidentMoveBlocked("RE_ContainerMapUnavailable");
                    if (f.Name == "autohack" && (f.FieldType != typeof(bool) || (bool)value)) throw new ResidentMoveBlocked("RE_ContainerMapUnavailable");
                    values.Add(f.Name, value);
                }
                comps.Add(new { Type = "hack", Values = values });
            }
            else if (comp.GetType() == typeof(CompGlower))
            {
                object color = Field(typeof(CompGlower), "glowColorOverride", typeof(ColorInt?), comp);
                var channels = color == null ? null : new Dictionary<string, object>();
                if (color != null) foreach (string channel in new[] { "r", "g", "b", "a" }) channels.Add(channel, Field(typeof(ColorInt), channel, typeof(int), color));
                comps.Add(new { Type = "glower", On = Field(typeof(CompGlower), "glowOnInt", typeof(bool), comp), Color = channels });
            }
        }
        string config = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            Known = Field(typeof(Building_Casket), "contentsKnown", typeof(bool), casket), casket.openedSignal, Comps = comps
        }))));
        if (casket.openedSignal?.Length > 10000) throw new ResidentMoveBlocked("RE_ContainerMapUnavailable");
        return (owner, new(thing.GetUniqueLoadID(), thing.GetType() == typeof(Building_Crate) ? "crate" : "casket", config, owner.Count));
    }
}
