using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;

/// <summary>Original basic worktable and its native-owned production bills. No Bill constructor or settings writer.</summary>
internal static class WorktableEvidence
{
    internal static bool Component(Type type) => type == typeof(CompAffectedByFacilities) || type == typeof(CompReportWorkSpeed) || type == typeof(CompForbiddable);
    internal static bool Supported(Thing table) => table?.GetType() == typeof(Building_WorkTable) || table?.GetType() == typeof(Building_WorkTable_HeatPush);
    private static object Raw(object value, Type type, string name) => ResidentActiveWork.Raw(value, type, name);
    internal static List<Bill> Bills(Building_WorkTable table)
    {
        if (!NativeTransferBodies.WorkObservationBodies || !Supported(table) || !table.Spawned || table.billStack?.GetType() != typeof(BillStack) ||
            table.billStack.billGiver != table || Raw(table.billStack, typeof(BillStack), "bills") is not List<Bill> bills || bills.Count > 15 ||
            bills.Any(b => b == null || b.billStack != table.billStack) || bills.Distinct().Count() != bills.Count)
            throw new ArgumentException("Original native worktable bill owner required.");
        ResidentActiveWork.Known(typeof(Building_WorkTable), "ExposeData"); ResidentActiveWork.Known(typeof(Building_WorkTable), "SpawnSetup");
        ResidentActiveWork.Known(typeof(BillStack), "ExposeData"); ResidentActiveWork.Known(typeof(Bill), "ExposeData"); ResidentActiveWork.Known(typeof(Bill), "GetUniqueLoadID");
        if (table.GetType() == typeof(Building_WorkTable_HeatPush)) ResidentActiveWork.Known(typeof(Building_WorkTable_HeatPush),"UsedThisTick");
        foreach (string field in new[] { "powerComp", "refuelableComp", "breakdownableComp", "moteEmitterComp" })
            if (Raw(table, typeof(Building_WorkTable), field) != null) throw new ArgumentException("Powered/fueled/complex table requires its own component protocol.");
        foreach (var comp in table.AllComps)
        {
            if (comp?.GetType() == typeof(CompAffectedByFacilities))
            {
                if (Raw(comp,typeof(CompAffectedByFacilities),"linkedFacilities") is not List<Thing> linked || linked.Count != 0) throw new ArgumentException("Linked facilities need their own rights/relationship scope.");
                ResidentActiveWork.Known(typeof(CompAffectedByFacilities),"PostSpawnSetup"); ResidentActiveWork.Known(typeof(CompAffectedByFacilities),"LinkToNearbyFacilities");
            }
            if (comp?.GetType() == typeof(CompForbiddable)) ResidentActiveWork.Known(typeof(CompForbiddable),"PostExposeData");
        }
        return bills;
    }
    internal static string Identity(Bill bill, Pawn pawn)
    {
        if (bill?.billStack?.billGiver is not Building_WorkTable table || table.Map != pawn.Map || Bills(table).Count(b => ReferenceEquals(b, bill)) != 1 ||
            pawn.jobs.curJob?.targetA.Thing != table && !ResidentActiveWork.Queue(pawn).Any(q => q.job.targetA.Thing == table && q.job.bill == bill))
            throw new ArgumentException("Original current/queued crafting bill and table required.");
        _ = Capture(table); return bill.GetUniqueLoadID();
    }
    private static string Filter(ThingFilter filter)
    {
        if (filter?.GetType() != typeof(ThingFilter) || Raw(filter, typeof(ThingFilter), "allowedDefs") is not HashSet<ThingDef> allowed || allowed.Count > 30000 ||
            allowed.Any(d => d == null || DefDatabase<ThingDef>.GetNamedSilentFail(d.defName) != d) ||
            Raw(filter, typeof(ThingFilter), "disallowedSpecialFilters") is not List<SpecialThingFilterDef> special || special.Count > 10000 ||
            special.Any(d => d == null || DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail(d.defName) != d) || special.Distinct().Count() != special.Count)
            throw new ArgumentException("Original bill ingredient filter required.");
        ResidentActiveWork.Known(typeof(ThingFilter), "ExposeData");
        var hp = (FloatRange)Raw(filter, typeof(ThingFilter), "allowedHitPointsPercents");
        var mental = (FloatRange)Raw(filter, typeof(ThingFilter), "allowedMentalBreakChance");
        var quality = (QualityRange)Raw(filter, typeof(ThingFilter), "allowedQualities");
        if (!RuntimeCompat.IsFinite(hp.min) || !RuntimeCompat.IsFinite(hp.max) || hp.min < 0 || hp.max > 1 || hp.min > hp.max ||
            !RuntimeCompat.IsFinite(mental.min) || !RuntimeCompat.IsFinite(mental.max) || mental.min < 0 || mental.max > 1 || mental.min > mental.max ||
            (int)quality.min < 0 || (int)quality.max > 6 || quality.min > quality.max) throw new ArgumentException("Original bill filter ranges invalid.");
        var root = (ThingCategoryDef)Raw(filter, typeof(ThingFilter), "overrideRootDef");
        if (root != null && DefDatabase<ThingCategoryDef>.GetNamedSilentFail(root.defName) != root) throw new ArgumentException("Original filter category changed.");
        return Hash(new { Allowed = allowed.Select(d => d.defName).OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            Disallowed = special.Select(d => d.defName).OrderBy(n => n, StringComparer.Ordinal).ToArray(), Hp = new[] { hp.min, hp.max }, Mental = new[] { mental.min, mental.max },
            Quality = new[] { (int)quality.min, (int)quality.max }, Root = root?.defName, Only = (bool)Raw(filter, typeof(ThingFilter), "onlySpecialFilters") });
    }
    private static object BillState(Bill bill, bool progress = false, bool omitIterationCount = false)
    {
        if (bill.GetType() != typeof(Bill_Production) && bill.GetType() != typeof(Bill_ProductionWithUft) || Raw(bill, typeof(Bill), "loadID") is not int id || id < 0 ||
            bill.recipe?.GetType() != typeof(RecipeDef) || DefDatabase<RecipeDef>.GetNamedSilentFail(bill.recipe.defName) != bill.recipe || bill.recipe.workerClass?.Assembly != typeof(Pawn).Assembly ||
            Raw(bill, typeof(Bill), "pawnRestriction") != null || bill.deleted || bill.precept != null || bill.xenogerm != null || (bool)Raw(bill, typeof(Bill), "slavesOnly") || (bool)Raw(bill, typeof(Bill), "mechsOnly") ||
            Raw(bill, typeof(Bill_Production), "storeGroup") != null || Raw(bill, typeof(Bill_Production), "includeGroup") != null ||
            !progress && bill.GetType() == typeof(Bill_ProductionWithUft) && Raw(bill, typeof(Bill_ProductionWithUft), "boundUftInt") != null)
            throw new ArgumentException("Basic production bills only; private labor/unfinished products require separate authority.");
        ResidentActiveWork.Known(typeof(Bill_Production), "ExposeData"); ResidentActiveWork.Known(typeof(Bill), "ValidateSettings");
        if (bill.GetType() == typeof(Bill_ProductionWithUft)) ResidentActiveWork.Known(typeof(Bill_ProductionWithUft), "ExposeData");
        var repeat = (BillRepeatModeDef)Raw(bill, typeof(Bill_Production), "repeatMode"); var store = (BillStoreModeDef)Raw(bill, typeof(Bill_Production), "storeMode");
        if (repeat == null || store == null || DefDatabase<BillRepeatModeDef>.GetNamedSilentFail(repeat.defName) != repeat || DefDatabase<BillStoreModeDef>.GetNamedSilentFail(store.defName) != store)
            throw new ArgumentException("Bill save defaults must already be explicit.");
        var filter = bill.ingredientFilter;
        if (bill.style != null && DefDatabase<ThingStyleDef>.GetNamedSilentFail(bill.style.defName) != bill.style) throw new ArgumentException("Unregistered original bill style.");
        _ = Filter(filter);
        // Bill.ExposeData otherwise removes invalid allowed defs. Require an already load-stable original filter.
        if (bill.recipe.fixedIngredientFilter != null)
        {
            _ = Filter(bill.recipe.fixedIngredientFilter);
            var fixedAllowed = (HashSet<ThingDef>)Raw(bill.recipe.fixedIngredientFilter, typeof(ThingFilter), "allowedDefs");
            if (((HashSet<ThingDef>)Raw(filter, typeof(ThingFilter), "allowedDefs")).Any(d => !fixedAllowed.Contains(d))) throw new ArgumentException("Bill saving would normalize its original ingredients.");
        }
        var fields = new[] { (typeof(Bill), "loadID recipe suspended ingredientSearchRadius allowedSkillRange pawnRestriction precept xenogerm slavesOnly mechsOnly nonMechsOnly style globalStyle graphicIndexOverride"),
            (typeof(Bill_Production), "repeatMode repeatCount storeMode playerCustomName targetCount pauseWhenSatisfied unpauseWhenYouHave includeEquipped includeTainted hpRange qualityRange limitToAllowedStuff paused") };
        object Value(object v)
        {
            if (v is IntRange ints) return new[] { ints.min, ints.max };
            if (v is FloatRange floats) { if (!RuntimeCompat.IsFinite(floats.min) || !RuntimeCompat.IsFinite(floats.max)) throw new ArgumentException("Nonfinite bill range."); return new[] { floats.min, floats.max }; }
            if (v is QualityRange q) return new[] { (int)q.min, (int)q.max };
            if (v is Def def) return new { Type = def.GetType().FullName, def.defName };
            if (v == null || v is int or bool or string) return v;
            if (v is float f && RuntimeCompat.IsFinite(f)) return f;
            throw new ArgumentException("Unknown original production bill field.");
        }
        return new { Type = bill.GetType().FullName, Id = bill.GetUniqueLoadID(), Worker = bill.recipe.workerClass.FullName, FixedFilter = bill.recipe.fixedIngredientFilter == null ? null : Filter(bill.recipe.fixedIngredientFilter),
            Fields = fields.SelectMany(pair => pair.Item2.Split(' ').Where(n=>!omitIterationCount||n!="repeatCount").Select(n => new { Name = n, Value = Value(Raw(bill, pair.Item1, n)) })).ToArray(), Filter = Filter(filter) };
    }
    internal static NativeWorktable Capture(Building_WorkTable table)
    {
        var bills = Bills(table);
        var states = bills.Select(b=>BillState(b)).ToArray();
        if (bills.Select(b => b.GetUniqueLoadID()).Distinct().Count() != bills.Count) throw new ArgumentException("Duplicate original production bill IDs.");
        return new(table.GetUniqueLoadID(), Hash(new { Class=table.GetType().FullName, Bills=states, Comps=table.AllComps.Select(c=>new { Type=c.GetType().FullName,
            Forbidden=c.GetType()==typeof(CompForbiddable)?(bool?)Raw(c,typeof(CompForbiddable),"forbiddenInt"):null }).ToArray() }), bills.Select(b => b.GetUniqueLoadID()).ToArray());
    }
    internal static NativeWorktable ObserveProgress(Building_WorkTable table)
    { var bills=Bills(table); return new(table.GetUniqueLoadID(),Hash(new {Class=table.GetType().FullName,Bills=bills.Select(b=>new {State=BillState(b,true),Bound=b.GetType()==typeof(Bill_ProductionWithUft)?(Raw(b,typeof(Bill_ProductionWithUft),"boundUftInt") as Thing)?.GetUniqueLoadID():null}).ToArray()}),bills.Select(b=>b.GetUniqueLoadID()).ToArray()); }
    internal static string IterationStable(Building_WorkTable table,Bill bill)
    {
        var bills=Bills(table);
        if(bill?.GetType()!=typeof(Bill_Production)||bills.Count(b=>ReferenceEquals(b,bill))!=1||Raw(bill,typeof(Bill_Production),"repeatMode")!=BillRepeatModeDefOf.RepeatCount)throw new ArgumentException("Same exact native repeat-count production bill required.");
        ResidentActiveWork.Known(typeof(Bill_Production),"Notify_IterationCompleted");
        return Hash(new{Class=table.GetType().FullName,Bills=bills.Select(b=>BillState(b,omitIterationCount:ReferenceEquals(b,bill))).ToArray(),Comps=table.AllComps.Select(c=>new{Type=c.GetType().FullName,Forbidden=c.GetType()==typeof(CompForbiddable)?(bool?)Raw(c,typeof(CompForbiddable),"forbiddenInt"):null}).ToArray()});
    }
    private static string Hash(object value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}
