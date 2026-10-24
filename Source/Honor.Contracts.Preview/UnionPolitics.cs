using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public enum InitialPoliticsChoice { Unchosen, KeepExisting, Configured }
public sealed class UnionCityReference : IExposable
{
    public string AdmissionId;
    public Settlement City;
    public Faction OriginalFaction;
    public void ExposeData()
    {
        Scribe_Values.Look(ref AdmissionId, "admission"); Scribe_References.Look(ref City, "city");
        Scribe_References.Look(ref OriginalFaction, "originalFaction");
    }
}

/// <summary>Explicit initial-world configuration. Does not change player faction, loaded maps or chartered territories.</summary>
public sealed class UnionPoliticsLedger : GameComponent
{
    public const string UnionDefName = "HonorContracts_FreeCityUnion";
    private readonly Game game;
    private string snapshot;
    private List<UnionCityReference> cities = [];
    private Faction union;
    private InitialPoliticsChoice choice;
    private bool queued;
    public UnionAdmissionLedger Admissions { get; private set; } = new();
    public bool RestoreFailed { get; private set; }
    public Faction Union => union;
    public InitialPoliticsChoice Choice => choice;
    public bool ConfigurationAvailable => choice != InitialPoliticsChoice.Configured || Admissions.Admissions.All(a => a.Phase == UnionAdmissionPhase.Cancelled);
    public UnionPoliticsLedger(Game game) => this.game = game;
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) snapshot = Admissions.Export();
        Scribe_Values.Look(ref snapshot, "honorUnionAdmissionsV1");
        Scribe_Values.Look(ref choice, "honorInitialPolitics", InitialPoliticsChoice.Unchosen);
        Scribe_References.Look(ref union, "honorFreeCityUnion"); Scribe_Collections.Look(ref cities, "honorUnionCities", LookMode.Deep);
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        cities ??= [];
        try
        {
            if (snapshot != null) Admissions = UnionAdmissionLedger.Restore(snapshot);
            if (!Enum.IsDefined(choice) || cities.Any(c => c == null) || cities.Select(c => c.AdmissionId).Distinct(StringComparer.Ordinal).Count() != cities.Count ||
                cities.Count != Admissions.Admissions.Count || cities.Any(c => Admissions.Admissions.All(a => a.Id != c.AdmissionId)))
                throw new ArgumentException("Invalid union reference registry.");
        }
        catch (Exception e) when (e is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Union registry restore rejected; original records retained. " + e.GetType().Name); }
    }
    public override void FinalizeInit()
    {
        if (Admissions.LastTick > Find.TickManager.TicksGame || union != null &&
            (union.def.defName != UnionDefName || union.IsPlayer || !Find.FactionManager.AllFactionsListForReading.Contains(union)) ||
            choice == InitialPoliticsChoice.Configured && union == null ||
            Admissions.Admissions.Any(a => union == null || a.UnionOwner != union.GetUniqueLoadID()))
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Union identity/time mismatch; configuration blocked."); }
    }
    public override void StartedNewGame() => OfferSetup();
    public override void LoadedGame() => OfferSetup();
    private void OfferSetup()
    {
        if (queued || choice != InitialPoliticsChoice.Unchosen || RestoreFailed || Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges) return;
        queued = true;
        LongEventHandler.ExecuteWhenFinished(() => {
            if (Current.Game == game && choice == InitialPoliticsChoice.Unchosen && !RestoreFailed)
                Find.WindowStack.Add(new InitialPoliticsWindow());
        });
    }
    public void KeepExisting()
    {
        if (!RestoreFailed && ConfigurationAvailable && !Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges) choice = InitialPoliticsChoice.KeepExisting;
    }
    private Settlement[] Settlements() => Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => !s.Destroyed).ToArray();
    private UnionCandidate Observe(Settlement city, Settlement[] all)
    {
        var faction = city.Faction;
        string id = city.GetUniqueLoadID();
        bool registered = Current.Game.GetComponent<LiveCharterLedger>().Book.Entities.Any(e => e.Id == "territory/" + id);
        bool eligible = faction?.def != null && !faction.Hidden && !faction.temporary && !faction.defeated && faction.HasGoodwill &&
            faction.def.humanlikeFaction && faction.def.defName != RealmAdministrationLedger.DefName && !faction.def.permanentEnemy && !(faction.def.royalTitleTags?.Count > 0);
        return new(id, faction?.GetUniqueLoadID() ?? "missing-owner", all.Count(s => s.Faction == faction),
            faction?.IsPlayer == true, faction?.def?.defName == UnionDefName, city.HasMap,
            city.trader == null || city.EverVisited || city.previouslyGeneratedInhabitants?.Count > 0, registered,
            faction == null || faction.HostileTo(Faction.OfPlayer), eligible);
    }
    public IReadOnlyList<UnionCandidate> Plan(int percent)
    {
        if (RestoreFailed || !ConfigurationAvailable || Current.Game.GetComponent<LiveCharterLedger>().RestoreFailed)
            return Array.Empty<UnionCandidate>();
        var all = Settlements(); return UnionAdmissionRules.Plan(all.Select(s => Observe(s, all)), percent);
    }
    public string CityLabel(string id) => Settlements().FirstOrDefault(s => s.GetUniqueLoadID() == id)?.Label ?? "HP_Missing".Translate();
    private Faction EnsureUnion()
    {
        var existing = Find.FactionManager.AllFactionsListForReading.Where(f => f.def.defName == UnionDefName).ToArray();
        if (existing.Length > 1 || existing.Any(f => f.defeated || f.IsPlayer) || union != null && !existing.Contains(union))
            throw new ArgumentException("Duplicate, defeated or unregistered union faction.");
        if (existing.Length == 1) return union = existing[0];
        var def = DefDatabase<FactionDef>.GetNamed(UnionDefName);
        if (def.isPlayer || !def.humanlikeFaction) throw new ArgumentException("Union must be a nonplayer humanlike faction.");
        // The verified generator's hidden override prevents its automatic random settlement side effect.
        // Initial relations still use faction definitions; reveal before registration/recache.
        union = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def, hidden: true));
        union.hidden = false;
        Find.FactionManager.Add(union);
        return union;
    }
    public void Configure(IReadOnlyList<UnionCandidate> reviewed)
    {
        if (RestoreFailed || !ConfigurationAvailable || Current.Game.GetComponent<LiveCharterLedger>().RestoreFailed ||
            Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges) { Rejected(); return; }
        try
        {
            if (reviewed == null || reviewed.Count is < 1 or > 10 || reviewed.Select(c => c.SettlementId).Distinct(StringComparer.Ordinal).Count() != reviewed.Count ||
                reviewed.GroupBy(c => c.OwnerId, StringComparer.Ordinal).Any(g => g.Count() > 1)) throw new ArgumentException("Invalid reviewed selection.");
            var all = Settlements();
            foreach (var expected in reviewed)
            {
                var city = all.SingleOrDefault(s => s.GetUniqueLoadID() == expected.SettlementId) ?? throw new ArgumentException("City missing.");
                var current = Observe(city, all);
                if (current.OwnerId != expected.OwnerId || UnionAdmissionRules.Exclusion(current) != UnionExclusion.None)
                    throw new ArgumentException("The reviewed city is no longer eligible.");
            }
            var target = EnsureUnion();
            foreach (var expected in reviewed)
            {
                all = Settlements(); var city = all.Single(s => s.GetUniqueLoadID() == expected.SettlementId);
                var current = Observe(city, all);
                // Recheck actual counts after every preceding city. Never remove a source faction's last base.
                if (current.OwnerId != expected.OwnerId || UnionAdmissionRules.Exclusion(current) != UnionExclusion.None)
                    throw new ArgumentException("City changed during configuration.");
                string admissionId = Guid.NewGuid().ToString("N");
                Admissions.Prepare(admissionId, current, target.GetUniqueLoadID(), Find.TickManager.TicksGame);
                cities.Add(new() { AdmissionId = admissionId, City = city, OriginalFaction = city.Faction });
                choice = InitialPoliticsChoice.Configured;
                city.SetFaction(target);
                Admissions.Confirm(admissionId, city.GetUniqueLoadID(), city.Faction.GetUniqueLoadID(), city.HasMap,
                    city.Faction.IsPlayer, Find.TickManager.TicksGame);
            }
            Messages.Message("HP_Configured".Translate(), MessageTypeDefOf.NeutralEvent);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            Log.Warning("[Honor.Contracts.Preview] Initial union configuration incomplete; recorded changes retained. " + e.GetType().Name);
            Rejected();
        }
    }
    public void Recover(string admissionId)
    {
        if (RestoreFailed) { Rejected(); return; }
        try
        {
            var record = Admissions.Admission(admissionId); var reference = cities.Single(c => c.AdmissionId == admissionId);
            if (record.Phase != UnionAdmissionPhase.Prepared || reference.City == null || reference.City.Destroyed ||
                !Find.WorldObjects.AllWorldObjects.Contains(reference.City) || reference.City.Faction == null)
                throw new ArgumentException("No verifiable current city.");
            var city = reference.City;
            if (city.Faction.GetUniqueLoadID() == record.OriginalOwner)
                Admissions.CancelUnmoved(record.Id, city.GetUniqueLoadID(), city.Faction.GetUniqueLoadID(), Find.TickManager.TicksGame);
            else
                Admissions.Confirm(record.Id, city.GetUniqueLoadID(), city.Faction.GetUniqueLoadID(), city.HasMap,
                    city.Faction.IsPlayer, Find.TickManager.TicksGame);
        }
        catch (ArgumentException) { Rejected(); }
    }
    private static void Rejected() => Messages.Message("HP_Rejected".Translate(), MessageTypeDefOf.RejectInput);
}

// A fixed political name follows the current game language; IDs and saved faction identity do not change.
[HarmonyPatch(typeof(Faction), nameof(Faction.Name), MethodType.Getter)]
internal static class UnionFactionName
{
    static bool Prefix(Faction __instance, ref string __result)
    {
        if (__instance.def?.defName == RealmAdministrationLedger.DefName) { __result = "AD_Name".Translate(); return false; }
        if (__instance.def?.defName != UnionPoliticsLedger.UnionDefName) return true;
        __result = "HP_UnionName".Translate(); return false;
    }
}

public sealed class InitialPoliticsWindow : Window
{
    private int percent = 10;
    private IReadOnlyList<UnionCandidate> plan;
    private Vector2 scroll;
    private float height = 1000;
    public override Vector2 InitialSize => new(850, 760);
    public InitialPoliticsWindow() { forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override void DoWindowContents(Rect rect)
    {
        var ledger = Current.Game.GetComponent<UnionPoliticsLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, 10000));
        list.Label("HP_Setup".Translate()); list.Gap();
        if (ledger.RestoreFailed) list.Label("HCP_RestoreFailed".Translate());
        else
        {
            if (ledger.ConfigurationAvailable)
            {
                if (list.ButtonText("HP_Keep".Translate())) { ledger.KeepExisting(); Close(); }
                list.Label("HP_Ratio".Translate(percent));
                int selected = Mathf.RoundToInt(list.Slider(percent, 0, 30));
                if (selected != percent) { percent = selected; plan = null; }
                if (list.ButtonText("HP_Preview".Translate())) plan = ledger.Plan(percent);
                if (plan != null)
                {
                    list.Label("HP_Plan".Translate(plan.Count));
                    foreach (var city in plan) list.Label(ledger.CityLabel(city.SettlementId));
                    if (plan.Count > 0 && list.ButtonText("HP_Apply".Translate())) { ledger.Configure(plan); plan = null; }
                }
            }
            else list.Label("HP_Done".Translate());
            foreach (var record in ledger.Admissions.Admissions)
            {
                list.Label("HP_Record".Translate(ledger.CityLabel(record.SettlementId), ("HP_" + record.Phase).Translate()));
                if (record.Phase == UnionAdmissionPhase.Prepared && list.ButtonText("HP_Recover".Translate())) ledger.Recover(record.Id);
            }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
