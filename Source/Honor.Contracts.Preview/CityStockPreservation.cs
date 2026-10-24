using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

internal static class CityStockPreservation
{
    // Verified against installed 1.7.4985. Fail closed if this private presentation cache changes.
    private static readonly System.Reflection.FieldInfo MaterialCache = AccessTools.Field(typeof(Settlement), "cachedMat");
    internal static string Capture(Settlement city)
    {
        if (city == null || city.GetType() != typeof(Settlement) || city.HasMap || city.previouslyGeneratedInhabitants == null ||
            city.previouslyGeneratedInhabitants.Count > 0 || city.trader?.GetType() != typeof(Settlement_TraderTracker) ||
            city.trader.settlement != city || MaterialCache == null || MaterialCache.IsStatic || MaterialCache.FieldType != typeof(Material)) return null;
        // Goods/StockListForReading can generate stock. This accessor only returns the current container (or null).
        var stock = city.trader.GetDirectlyHeldThings();
        var goods = new List<CityStockItem>();
        if (stock != null)
            foreach (var thing in stock)
            {
                if (StockExclusion(thing) != null) return null;
                goods.Add(new(thing.GetUniqueLoadID(), thing.def.defName, thing.Stuff?.defName, thing.stackCount, thing.HitPoints,
                    thing.TryGetQuality(out var quality) ? (int)quality : null));
            }
        return CityStockProof.Capture(city.EverVisited, stock != null, city.trader.NextRestockTick, goods);
    }
    // Shared by the read-only audit and execution guard, without generating or enumerating nested stock.
    internal static string StockExclusion(Thing thing)
    {
        if (thing == null || thing.Destroyed) return "CA_Invalid";
        if (thing is Pawn pawn) return pawn.RaceProps.Humanlike ? "CA_People" : "CA_Animals";
        if (thing is Corpse) return "CA_Corpses";
        if (thing is MinifiedThing || thing is IThingHolder || thing is ThingWithComps c && c.AllComps.Any(comp => comp is IThingHolder)) return "CA_Containers";
        return thing.def.category == ThingCategory.Item ? null : "CA_Other";
    }
    internal static void InvalidateMaterial(Settlement city)
    {
        if (MaterialCache == null || MaterialCache.IsStatic || MaterialCache.FieldType != typeof(Material))
            throw new ArgumentException("Settlement presentation cache unavailable.");
        MaterialCache.SetValue(city, null); // Never destroy pooled materials or touch inventory/pawns.
    }
}
