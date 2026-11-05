using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;
using Verse;
using Verse.AI.Group;

namespace Honor.Contracts.Preview;

/// <summary>Original reference custody for new visitor-only map transfers; never deep-save native people or their gear.</summary>
public sealed class MapVisitorReference : IExposable
{
    public Pawn Pawn;
    public Lord Lord;
    public List<Thing> Inventory, Equipment, Apparel;
    public void ExposeData()
    {
        Scribe_References.Look(ref Pawn, "originalVisitor");
        Scribe_References.Look(ref Lord, "originalVisitorLord");
        Scribe_Collections.Look(ref Inventory, "originalVisitorInventory", LookMode.Reference);
        Scribe_Collections.Look(ref Equipment, "originalVisitorEquipment", LookMode.Reference);
        Scribe_Collections.Look(ref Apparel, "originalVisitorApparel", LookMode.Reference);
    }
}

/// <summary>Read-only bystanders, never participants in affiliation, medical or area writes.</summary>
internal static class ResidentMapVisitors
{
    private static List<Thing> Ordered(IEnumerable<Thing> things) => (things ?? []).OrderBy(t => t.GetUniqueLoadID(), StringComparer.Ordinal).ToList();
    internal static MapVisitorReference References(Pawn p)
    {
        _ = Capture(p); // Validate known native holder dispatch before reading any contents.
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in new (IThingHolder Tracker, Type Container)[] { (p.inventory, typeof(ThingOwner<Thing>)), (p.equipment, typeof(ThingOwner<ThingWithComps>)), (p.apparel, typeof(ThingOwner<RimWorld.Apparel>)) }.Where(t => t.Tracker != null))
        {
            var container = pair.Tracker.GetDirectlyHeldThings();
            if (container?.GetType() != pair.Container || !ReferenceEquals(container.Owner, pair.Tracker) || container.Count > 256 ||
                container.Any(t => t == null || t.Spawned || t.Map != null || t.holdingOwner != container || !ids.Add(t.GetUniqueLoadID())))
                throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        }
        if (ids.Count > 256) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        return new() { Pawn = p, Lord = p.GetLord(), Inventory = Ordered(p.inventory?.innerContainer.Cast<Thing>()),
            Equipment = Ordered(p.equipment?.AllEquipmentListForReading.Cast<Thing>()), Apparel = Ordered(p.apparel?.WornApparel.Cast<Thing>()) };
    }
    internal static void VerifyReferences(MapVisitorReference original, Pawn p)
    {
        if (original == null || !ReferenceEquals(original.Pawn, p)) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        var fresh = References(p);
        if (!ReferenceEquals(original.Lord, fresh.Lord) || original.Inventory == null || original.Equipment == null || original.Apparel == null ||
            !original.Inventory.SequenceEqual(fresh.Inventory) || !original.Equipment.SequenceEqual(fresh.Equipment) || !original.Apparel.SequenceEqual(fresh.Apparel))
            throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
    }
    internal static NativeMapResident Capture(Pawn p)
    {
        if (p?.GetType() != typeof(Pawn) || !p.Spawned || p.Map == null || !p.RaceProps.Humanlike || p.Dead || p.Destroyed || p.IsPrisoner || p.IsSlave || p.carryTracker?.CarriedThing != null ||
            p.mechanitor != null || p.creepjoiner != null || p.IsMutant || p.relations?.DirectRelations == null || p.needs?.AllNeeds == null ||
            p.relations.DirectRelations.Count > 1000 || p.needs.AllNeeds.Count > 128 ||
            (p.royalty?.AllTitlesInEffectForReading.Count ?? 0) != 0 ||
            p.AllComps.Any(c => c == null || c.GetType().Assembly != typeof(Pawn).Assembly || c is IThingHolder) ||
            p.needs.AllNeeds.Any(n => n == null || !RuntimeCompat.IsFinite(n.CurLevel) || n.GetType().Assembly != typeof(Pawn).Assembly && n.GetType().Assembly.GetName().Name != "Honor.Core") ||
            p.relations.DirectRelations.Any(r => r?.def == null || r.otherPawn == null) ||
            (p.needs.mood?.thoughts?.memories?.Memories.Count ?? 0) > 1000 ||
            p.needs.mood?.thoughts?.memories?.Memories.Any(m => m?.def == null || m.GetType().Assembly != typeof(Pawn).Assembly) == true ||
            p.health?.hediffSet?.hediffs == null || p.health.hediffSet.hediffs.Any(h => h == null || h.GetType().Assembly != typeof(Pawn).Assembly))
            throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        ResidentMapActors.RequireHolders(p);
        var gear = (p.inventory?.innerContainer.Cast<Thing>() ?? []).Concat(p.equipment?.AllEquipmentListForReading.Cast<Thing>() ?? []).Concat(p.apparel?.WornApparel.Cast<Thing>() ?? []);
        if (gear.Any(t => t == null || t.GetType().Assembly != typeof(Pawn).Assembly || t is IThingHolder || t is ThingWithComps c && c.AllComps.Any(x => x == null || x is IThingHolder || x.GetType().Assembly != typeof(Pawn).Assembly))) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        var baseline = ResidentBaselineStore.Evidence(p);
        var lord = p.GetLord();
        if (lord != null && (lord.GetType() != typeof(Lord) || lord.LordJob?.GetType().Assembly != typeof(Pawn).Assembly || lord.ownedPawns.Any(x => x == null))) throw new ResidentMoveBlocked("RE_MapResidentUnavailable");
        var settings = p.playerSettings;
        string proof = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            baseline, Position = ResidentMapActors.Binding(p), Name = p.Name?.ToStringFull, Age = p.ageTracker?.AgeBiologicalTicks,
            Host = p.HostFaction?.GetUniqueLoadID(), Job = p.CurJob?.GetUniqueLoadID(), p.Drafted,
            Relations = p.relations.DirectRelations.Select(r => new { Def = r.def.defName, Other = r.otherPawn.GetUniqueLoadID() }).OrderBy(r => r.Def, StringComparer.Ordinal).ThenBy(r => r.Other, StringComparer.Ordinal).ToArray(),
            Needs = p.needs.AllNeeds.Select(n => new { Def = n.def.defName, Level = n.CurLevel }).OrderBy(n => n.Def, StringComparer.Ordinal).ToArray(),
            Memories = p.needs.mood?.thoughts?.memories?.Memories.Select(m => new { Def = m.def.defName, Stage = m.CurStageIndex, Other = m.otherPawn?.GetUniqueLoadID(), m.age, m.moodPowerFactor, m.moodOffset, m.permanent, m.durationTicksOverride }).ToArray(),
            Settings = settings == null ? null : new { settings.joinTick, settings.followDrafted, settings.followFieldwork, settings.animalsReleased, settings.hostilityResponse, settings.selfTend, settings.displayOrder, settings.animalForage, settings.animalDig, Care = (int)settings.medCare },
            Areas = settings == null ? null : ResidentAreaRecords.Entries(p),
            Surgery = ResidentSurgery.Proof(p, null), Faith = ResidentIdeology.Proof(p), Genes = ResidentGenes.Proof(p),
            Lord = lord?.GetUniqueLoadID(), LordFaction = lord?.faction?.GetUniqueLoadID(), Members = lord?.ownedPawns.Select(x => x.GetUniqueLoadID()).OrderBy(id => id, StringComparer.Ordinal).ToArray()
        }))));
        return new(p.GetUniqueLoadID(), proof);
    }
}
