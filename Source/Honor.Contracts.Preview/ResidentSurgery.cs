using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

/// <summary>Only detached originals are deep-saved here. Attached bills remain owned/saved by the pawn.</summary>
public sealed class ResidentSurgeryRecord : IExposable
{
    public Pawn Pawn;
    public string Snapshot;
    public List<Bill> Detached = [];
    public bool LoadFailed { get; private set; }
    private List<bool> partLinksAtSave = [];
    public void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !LoadFailed) partLinksAtSave = Detached?.Select(b => b is Bill_Medical m && m.Part != null).ToList() ?? [];
        Scribe_References.Look(ref Pawn, "pawn"); Scribe_Values.Look(ref Snapshot, "ordersV1");
        Scribe_Collections.Look(ref partLinksAtSave, "partLinksAtSave", LookMode.Value);
        Scribe_Collections.Look(ref Detached, "detachedOriginalOrders", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            Detached ??= [];
            try { ResidentSurgery.BindLoadedParts(this, partLinksAtSave); }
            catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
            { LoadFailed = true; Log.Error("[Honor.Contracts.Preview] Detached surgical data could not be bound; originals and snapshot retained."); }
        }
    }
    public SurgeryOrderSet Set() => SurgeryOrderSet.Restore(Snapshot);
}

internal static class ResidentSurgery
{
    private static string Digest(object data) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))));
    internal static SurgeryOrder Describe(Pawn pawn, Bill bill, bool allowMissingPart = false)
    {
        if (pawn?.health?.surgeryBills == null || bill == null || bill.GetType() != typeof(Bill_Medical) || bill.deleted || bill.recipe == null ||
            bill.billStack != pawn.health.surgeryBills || bill.billStack.billGiver != pawn || bill.ingredientFilter == null || bill.ingredientFilter == bill.recipe.fixedIngredientFilter ||
            bill.ingredientFilter.GetType() != typeof(ThingFilter) || bill.recipe.workerClass == null || bill.recipe.workerClass.Assembly != typeof(Pawn).Assembly ||
            bill.PawnRestriction != null || bill.precept != null || bill.xenogerm != null ||
            !RuntimeCompat.IsFinite(bill.ingredientSearchRadius) || bill.ingredientSearchRadius < 0 ||
            bill.allowedSkillRange.min < 0 || bill.allowedSkillRange.max > 20 || bill.allowedSkillRange.min > bill.allowedSkillRange.max)
            throw new ArgumentException("Unsupported surgical order.");
        var medical = (Bill_Medical)bill;
        if (medical.iterationCompleted || medical.consumedMedicine == null || medical.consumedMedicine.Count != 0 || medical.uniqueRequiredIngredients?.Count > 0)
            throw new ArgumentException("Started/resource-bound surgery is not movable.");
        var part = medical.Part;
        if (part == null && bill.recipe.targetsBodyPart && !allowMissingPart) throw new ArgumentException("Missing surgical body part.");
        if (part != null && (part.body != pawn.RaceProps.body || part.Index < 0 || part.Index >= pawn.RaceProps.body.AllParts.Count ||
            pawn.RaceProps.body.AllParts[part.Index] != part)) throw new ArgumentException("Surgery body binding mismatch.");
        var allowed = bill.ingredientFilter.AllowedThingDefs.OrderBy(d => d.defName, StringComparer.Ordinal).ToArray();
        if (bill.recipe.fixedIngredientFilter != null && allowed.Any(d => !bill.recipe.fixedIngredientFilter.Allows(d)))
            throw new ArgumentException("Surgical filter would be normalized on save.");
        var hp = bill.ingredientFilter.AllowedHitPointsPercents; var quality = bill.ingredientFilter.AllowedQualityLevels;
        var mental = bill.ingredientFilter.AllowedMentalBreakChance;
        if (!RuntimeCompat.IsFinite(hp.min) || !RuntimeCompat.IsFinite(hp.max) || hp.min < 0 || hp.max > 1 || hp.min > hp.max ||
            !RuntimeCompat.IsFinite(mental.min) || !RuntimeCompat.IsFinite(mental.max) || mental.min < 0 || mental.max > 1 || mental.min > mental.max ||
            (int)quality.min < 0 || (int)quality.max > 6 || quality.min > quality.max)
            throw new ArgumentException("Invalid surgical filter ranges.");
        string configuration = Digest(new { bill.suspended, bill.ingredientSearchRadius, Min = bill.allowedSkillRange.min, Max = bill.allowedSkillRange.max,
            bill.SlavesOnly, bill.MechsOnly, bill.NonMechsOnly, Style = bill.style?.defName, bill.globalStyle, bill.graphicIndexOverride,
            Allowed = allowed.Select(d => d.defName).ToArray(), HpMin = hp.min, HpMax = hp.max, QualityMin = (int)quality.min, QualityMax = (int)quality.max,
            MentalMin = mental.min, MentalMax = mental.max, bill.ingredientFilter.OnlySpecialFilters,
            Special = DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.OrderBy(d => d.defName, StringComparer.Ordinal)
                .Select(d => new { Def = d.defName, Allowed = bill.ingredientFilter.Allows(d) }).ToArray() });
        string id = bill.GetUniqueLoadID();
        if (!int.TryParse(id.Substring(id.LastIndexOf('_') + 1), out int loadId) || loadId < 0) throw new ArgumentException("Unassigned surgical identity.");
        return new(id, bill.recipe.defName, pawn.RaceProps.body.defName, part?.Index ?? -1, part?.def.defName, configuration);
    }
    internal static ResidentSurgeryRecord Capture(Pawn pawn)
    {
        var set = SurgeryOrderSet.Capture(pawn.health.surgeryBills.Bills.Select(b => Describe(pawn, b)));
        return new() { Pawn = pawn, Snapshot = set.Export() };
    }
    // Restore the unsaved billStack link before observation. No surgery parameter is changed here.
    internal static void BindDetached(ResidentSurgeryRecord record)
    {
        if (record?.Pawn?.health?.surgeryBills == null || record.Detached == null || record.LoadFailed) throw new ArgumentException("Missing surgical custody reference.");
        foreach (var bill in record.Detached)
        {
            if (bill == null || bill.billStack != null && bill.billStack != record.Pawn.health.surgeryBills) throw new ArgumentException("Foreign surgical holder.");
            bill.billStack = record.Pawn.health.surgeryBills;
        }
    }
    internal static void BindLoadedParts(ResidentSurgeryRecord record, IReadOnlyList<bool> partLinks)
    {
        BindDetached(record);
        if (partLinks == null || partLinks.Count != record.Detached.Count) throw new ArgumentException("Missing surgical link decode evidence.");
        if (record.Detached.Count == 0) return;
        var set = record.Set(); var expected = set.Orders;
        if (record.Detached.Count != expected.Count || record.Pawn.health.surgeryBills.Count != 0) throw new ArgumentException("Ambiguous surgical save ownership.");
        // Validate the complete native data before filling a body-part link absent from native load handling.
        for (int i = 0; i < expected.Count; i++)
        {
            var actual = Describe(record.Pawn, record.Detached[i], true); var wanted = expected[i];
            if (set.DecodePartLink(actual, partLinks[i]) != wanted) throw new ArgumentException("Detached surgical priority changed.");
            if (wanted.PartIndex >= 0 && (wanted.PartIndex >= record.Pawn.RaceProps.body.AllParts.Count ||
                record.Pawn.RaceProps.body.AllParts[wanted.PartIndex].def.defName != wanted.PartDef || record.Pawn.RaceProps.body.defName != wanted.Body))
                throw new ArgumentException("Saved surgical anatomy changed.");
        }
        for (int i = 0; i < expected.Count; i++)
            if (expected[i].PartIndex >= 0 && ((Bill_Medical)record.Detached[i]).Part == null)
                ((Bill_Medical)record.Detached[i]).Part = record.Pawn.RaceProps.body.AllParts[expected[i].PartIndex];
    }
    internal static SurgeryCustodyAction Review(ResidentSurgeryRecord record, bool atTarget)
    {
        BindDetached(record);
        return record.Set().Review(record.Pawn.health.surgeryBills.Bills.Select(b => Describe(record.Pawn, b)),
            record.Detached.Select(b => Describe(record.Pawn, b)), atTarget);
    }
    internal static string Proof(Pawn pawn, ResidentSurgeryRecord record)
    {
        if (record == null) return Capture(pawn).Set().Proof;
        if (record.Pawn != pawn) throw new ArgumentException("Wrong surgery resident.");
        BindDetached(record); var attached = pawn.health.surgeryBills.Bills.Select(b => Describe(pawn, b)).ToArray();
        var detached = record.Detached.Select(b => Describe(pawn, b)).ToArray();
        // Validate one exact complete holder, without granting faction-change authority.
        record.Set().Review(attached, detached, false);
        return SurgeryOrderSet.Capture(detached.Length == 0 ? attached : detached).Proof;
    }
    internal static void Detach(ResidentSurgeryRecord record)
    {
        if (Review(record, false) != SurgeryCustodyAction.Detach) return;
        var originals = record.Pawn.health.surgeryBills.Bills.ToList(); // Allocate before touching the native list.
        record.Detached = originals;
        record.Pawn.health.surgeryBills.Bills.Clear(); // No deletion callbacks; originals remain owned by this execution record.
        if (Review(record, false) != SurgeryCustodyAction.ChangeFaction) throw new ArgumentException("Detachment verification failed.");
    }
    internal static void Restore(ResidentSurgeryRecord record)
    {
        var action = Review(record, true);
        if (action == SurgeryCustodyAction.Verify) return;
        if (action != SurgeryCustodyAction.Restore) throw new ArgumentException("Surgical restoration not ready.");
        var bills = record.Pawn.health.surgeryBills;
        bills.Bills.Capacity = Math.Max(bills.Bills.Capacity, record.Detached.Count); // Allocate before transferring ownership.
        // BindDetached has restored the same unsaved billStack links. Copy the original list without callbacks or new IDs.
        bills.Bills.AddRange(record.Detached);
        record.Detached.Clear();
        record.Set().VerifyAttached(bills.Bills.Select(b => Describe(record.Pawn, b)), record.Detached.Select(b => Describe(record.Pawn, b)));
    }
}
