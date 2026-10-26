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
    private static readonly System.Reflection.FieldInfo Unload = AccessTools.Field(typeof(Pawn_InventoryTracker), "unloadEverything");
    private static readonly System.Reflection.FieldInfo NotForSale = AccessTools.Field(typeof(Pawn_InventoryTracker), "itemsNotForSale");
    private static readonly System.Reflection.FieldInfo Unpacked = AccessTools.Field(typeof(Pawn_InventoryTracker), "unpackedCaravanItems");
    private static bool Animal(Pawn pawn, ThingOwner stock) => stock != null && pawn != null && pawn.GetType() == typeof(Pawn) &&
        pawn.RaceProps.Animal && !pawn.RaceProps.Humanlike && !pawn.Dead && !pawn.Destroyed && !pawn.Spawned && pawn.IsWorldPawn() &&
        pawn.holdingOwner == stock && pawn.Faction?.IsPlayer != true && pawn.guest == null && !pawn.Downed && !pawn.InMentalState && pawn.CurJob == null &&
        (pawn.apparel?.WornApparel.Count ?? 0) == 0 &&
        (pawn.equipment?.AllEquipmentListForReading.Count ?? 0) == 0 && pawn.carryTracker?.CarriedThing == null;
    internal static bool PreservableAnimal(Pawn pawn, ThingOwner stock) => Animal(pawn, stock) && (pawn.inventory?.innerContainer.Count ?? 0) == 0;
    internal static bool PreservablePackAnimal(Pawn pawn, ThingOwner stock) => Animal(pawn, stock) &&
        pawn.inventory?.GetType() == typeof(Pawn_InventoryTracker) && pawn.inventory.pawn == pawn && pawn.inventory.innerContainer != null &&
        pawn.inventory.innerContainer.Count is > 0 and <= 10000 && pawn.inventory.innerContainer.All(t => t != null && t.def != null &&
            t.holdingOwner == pawn.inventory.innerContainer && StockExclusion(t) == null) && PackSettings(pawn, out _, out _, out _);
    private static bool PackSettings(Pawn p, out bool unload, out string[] noSale, out string[] unpacked)
    {
        unload = false; noSale = []; unpacked = [];
        if (Unload == null || Unload.IsStatic || Unload.FieldType != typeof(bool) || NotForSale == null || NotForSale.IsStatic ||
            NotForSale.FieldType != typeof(List<Thing>) || Unpacked == null || Unpacked.IsStatic || Unpacked.FieldType != typeof(List<Thing>) ||
            NotForSale.GetValue(p.inventory) is not List<Thing> sale || Unpacked.GetValue(p.inventory) is not List<Thing> unpack) return false;
        var held = p.inventory.innerContainer;
        var contents = new HashSet<Thing>(held);
        foreach (var list in new[] { sale, unpack })
            if (list.Count > held.Count || list.Any(t => t == null || t.holdingOwner != held || !contents.Contains(t)) || list.Distinct().Count() != list.Count) return false;
        unload = (bool)Unload.GetValue(p.inventory); noSale = sale.Select(t => t.GetUniqueLoadID()).ToArray(); unpacked = unpack.Select(t => t.GetUniqueLoadID()).ToArray();
        return true;
    }
    private static CityStockItem Item(Thing thing) => new(thing.GetUniqueLoadID(), thing.def.defName, thing.Stuff?.defName, thing.stackCount, thing.HitPoints,
        thing.TryGetQuality(out var quality) ? (int)quality : null);
    internal static string Capture(Settlement city, out int format)
    {
        format = 0;
        if (city == null || city.GetType() != typeof(Settlement) || city.HasMap || city.previouslyGeneratedInhabitants == null ||
            city.trader?.GetType() != typeof(Settlement_TraderTracker) ||
            city.trader.settlement != city || MaterialCache == null || MaterialCache.IsStatic || MaterialCache.FieldType != typeof(Material)) return null;
        // Goods/StockListForReading can generate stock. This accessor only returns the current container (or null).
        var stock = city.trader.GetDirectlyHeldThings();
        if (stock?.Count > 10000) return null;
        var goods = new List<CityStockItem>();
        var animals = new List<CityTradeAnimal>();
        var cargo = new List<CityAnimalCargo>();
        int total = 0;
        if (stock != null)
            foreach (var thing in stock)
            {
                if (++total > 10000) return null;
                if (thing is Pawn pawn && (PreservableAnimal(pawn, stock) || PreservablePackAnimal(pawn, stock)))
                {
                    int carried = pawn.inventory?.innerContainer.Count ?? 0;
                    if (carried > 10000 - total) return null;
                    total += carried;
                    var evidence = ResidentBaselineStore.Evidence(pawn);
                    animals.Add(new(evidence.Id, pawn.def.defName, evidence.Faction, evidence.Health, evidence.Gear, evidence.Status));
                    if (pawn.inventory?.innerContainer.Count > 0)
                    {
                        if (!PackSettings(pawn, out bool unload, out var noSale, out var unpacked)) return null;
                        cargo.Add(new(evidence.Id, pawn.inventory.innerContainer.Select(Item).ToArray(), unload, noSale, unpacked));
                    }
                    continue;
                }
                if (StockExclusion(thing) != null) return null;
                goods.Add(Item(thing));
            }
        if (cargo.Count > 0)
        {
            var native = AccessTools.Method(typeof(Settlement), nameof(WorldObject.SetFaction), [typeof(Faction)]);
            if (native?.DeclaringType != typeof(WorldObject) || Harmony.GetPatchInfo(native)?.Owners.Any(id => id != "honor.series.contracts.preview") == true || stock.Any(t => t.holdingOwner != stock)) return null;
            format = 2;
            return CityStockProof.CapturePackedAnimals(city.EverVisited, stock != null, city.trader.NextRestockTick, goods, animals, cargo);
        }
        if (animals.Count == 0) return CityStockProof.Capture(city.EverVisited, stock != null, city.trader.NextRestockTick, goods);
        format = 1;
        return CityStockProof.CaptureAnimals(city.EverVisited, stock != null, city.trader.NextRestockTick, goods, animals);
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
