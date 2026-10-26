using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed record ProtectionAgreementReview(Game Context, string Charter, string Stamp);
public sealed record ProtectionAidReview(Game Context, CharterAidPlan Plan, Map Map, Faction Source, Pawn Representative, string Stamp);

/// <summary>Separate explicit allied protection compact, then a journalled real native friendly-aid request. Never adjudicates refusal or independence.</summary>
public sealed class CharterProtectionLedger : GameComponent
{
    private readonly Game context;
    private string snapshot;
    public CharterProtection Ledger { get; private set; } = new();
    public bool RestoreFailed { get; private set; }
    public bool Executing { get; private set; }
    private static CharterProtectionLedger active;
    private IncidentParms nativeParms;
    private IncidentWorker_RaidFriendly nativeWorker;
    private Pawn[] generated;
    private bool generationObserved, generationAmbiguous;
    internal static readonly System.Reflection.MethodInfo GenerateRaid = AccessTools.Method(typeof(IncidentWorker_Raid), nameof(IncidentWorker_Raid.TryGenerateRaidInfo),
        [typeof(IncidentParms), typeof(List<Pawn>).MakeByRefType(), typeof(bool)]);
    internal static bool PreserveOriginalPawns => active != null && active.context == Current.Game && active.Executing;
    internal static void ObserveGenerated(IncidentWorker_Raid worker, IncidentParms parms, List<Pawn> pawns, bool debug, bool returned)
    {
        if (!PreserveOriginalPawns || worker != active.nativeWorker || parms != active.nativeParms || debug) return;
        if (active.generationObserved) { active.generationAmbiguous = true; return; }
        active.generationObserved = true;
        active.generated = returned && pawns != null ? pawns.ToArray() : [];
    }
    public CharterProtectionLedger(Game game) { context = game; }
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) snapshot = Ledger.Export();
        Scribe_Values.Look(ref snapshot, "honorCharterProtectionAidV1");
        if (Scribe.mode != LoadSaveMode.PostLoadInit || snapshot == null) return;
        try { Ledger = CharterProtection.Restore(snapshot); }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Protection history rejected; raw retained. " + e.GetType().Name); }
    }
    public override void FinalizeInit()
    {
        try
        {
            var charters = context.GetComponent<LiveCharterLedger>();
            if (RestoreFailed || charters.RestoreFailed || Ledger.LastTick > Find.TickManager.TicksGame) throw new ArgumentException("Unavailable protection history.");
            Ledger.ValidateBook(charters.Book);
        }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Protection book/clock mismatch; raw retained. " + e.GetType().Name); }
    }
    private LiveCharterLedger Charters => context.GetComponent<LiveCharterLedger>();
    private PlayerPoliticsLedger Politics => context.GetComponent<PlayerPoliticsLedger>();
    private static string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
    private static bool Allied(Faction f) => f.RelationWith(Faction.OfPlayer, true)?.kind == FactionRelationKind.Ally &&
        Faction.OfPlayer.RelationWith(f, true)?.kind == FactionRelationKind.Ally;
    private Faction Ready(string id)
    {
        if (context != Current.Game || Current.ProgramState != ProgramState.Playing || RestoreFailed || Executing || active != null ||
            Charters.RestoreFailed || Politics.RestoreFailed || context.GetComponent<PoliticalTransitionLedger>().BlocksChanges ||
            context.GetComponent<TributePaymentLedger>().RestoreFailed || context.GetComponent<TributePaymentLedger>().Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) ||
            context.GetComponent<TributePaymentLedger>().Ledger.LastTick > Find.TickManager.TicksGame ||
            Politics.State.Mode != PlayerAllegianceMode.Vassal || Politics.State.Suspended || Politics.State.Contract != id)
            throw new ArgumentException("Review the current active shared-realm player charter first.");
        Ledger.ValidateBook(Charters.Book); Charters.Book.ValidateCurrent(id, Charters.Observe());
        var source = Politics.TransitionAuthority();
        if (source != Charters.CounterpartyFaction(id) || !Allied(source) || source.deactivated) throw new ArgumentException("Actual allied liege must still match the contract.");
        _ = NpcDecisionAuthority.Read(source); return source;
    }
    private string Stamp(Faction source) => JsonSerializer.Serialize(new { Book = Charters.Book.Export(), Politics = Politics.Model.Export(),
        Protection = Ledger.Export(), Payments = context.GetComponent<TributePaymentLedger>().Ledger.Export(), Source = source.GetUniqueLoadID(), Representative = source.leader.GetUniqueLoadID(),
        Goodwill = source.GoodwillWith(Faction.OfPlayer), Reciprocal = Faction.OfPlayer.GoodwillWith(source), Relations = Politics.ReviewToken(source) });
    public ProtectionAgreementReview ReviewAgreement(string id)
    {
        var source = Ready(id);
        var charter = Charters.Book.Charter(id);
        var history = TributePerformanceReview.ForPair(Charters.Book, context.GetComponent<TributePaymentLedger>().Ledger,
            charter.Terms.Liege, charter.Terms.Subject, Find.TickManager.TicksGame);
        if (!CharterOffers.AssessHistory(new(CharterOfferDecision.Accepted, 75, 0, 0), history).Accepted)
            throw new ArgumentException("New NPC protection consent waits for the same pair's actual overdue or incomplete tribute history.");
        _ = Ledger.Agree(Charters.Book, id, NpcDecisionAuthority.Read(source), source.GoodwillWith(Faction.OfPlayer), Allied(source), Find.TickManager.TicksGame);
        return new(context, id, Stamp(source));
    }
    public void Agree(ProtectionAgreementReview reviewed) => Execute(() => {
        if (reviewed?.Context != context) throw new ArgumentException("Review this game first.");
        var fresh = ReviewAgreement(reviewed.Charter); if (fresh.Stamp != reviewed.Stamp) throw new ArgumentException("Agreement review changed.");
        var source = Ready(reviewed.Charter);
        Ledger = Ledger.Agree(Charters.Book, reviewed.Charter, NpcDecisionAuthority.Read(source), source.GoodwillWith(Faction.OfPlayer), Allied(source), Find.TickManager.TicksGame);
        Messages.Message("CP_Agreed".Translate(), MessageTypeDefOf.NeutralEvent);
    });
    private static void Unpatched(System.Reflection.MethodInfo method, bool own = false)
    {
        if (method == null || Harmony.GetPatchInfo(method)?.Owners.Any(o => !own || o != "honor.series.contracts.preview") == true)
            throw new ArgumentException("Unsupported native friendly-aid handler.");
    }
    private static IncidentWorker_RaidFriendly Worker()
    {
        var def = IncidentDefOf.RaidFriendly;
        if (def?.GetType() != typeof(IncidentDef) || def.Worker?.GetType() != typeof(IncidentWorker_RaidFriendly) || def.Worker.def != def ||
            PawnGroupKindDefOf.Combat?.Worker?.GetType() != typeof(PawnGroupKindWorker_Normal) ||
            RaidStrategyDefOf.ImmediateAttackFriendly?.Worker?.GetType() != typeof(RaidStrategyWorker_ImmediateAttackFriendly))
            throw new ArgumentException("Supported native friendly aid unavailable.");
        Unpatched(AccessTools.Method(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute), [typeof(IncidentParms)]));
        Unpatched(AccessTools.Method(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow), [typeof(IncidentParms)]));
        Unpatched(AccessTools.Method(typeof(IncidentWorker_Raid), "TryExecuteWorker", [typeof(IncidentParms)]));
        Unpatched(AccessTools.Method(typeof(IncidentWorker_RaidFriendly), "CanFireNowSub", [typeof(IncidentParms)]));
        Unpatched(AccessTools.Method(typeof(IncidentWorker_RaidFriendly), "TryResolveRaidFaction", [typeof(IncidentParms)]));
        Unpatched(AccessTools.Method(typeof(IncidentWorker_RaidFriendly), nameof(IncidentWorker_RaidFriendly.FactionCanBeGroupSource), [typeof(Faction), typeof(IncidentParms), typeof(bool)]));
        Unpatched(AccessTools.Method(typeof(IncidentWorker_PawnsArrive), nameof(IncidentWorker_RaidFriendly.FactionCanBeGroupSource), [typeof(Faction), typeof(IncidentParms), typeof(bool)]));
        Unpatched(AccessTools.Method(typeof(IncidentWorker_RaidFriendly), nameof(IncidentWorker_RaidFriendly.ResolveRaidStrategy), [typeof(IncidentParms), typeof(PawnGroupKindDef)]));
        Unpatched(AccessTools.Method(typeof(PawnGroupKindWorker_Normal), "GeneratePawns", [typeof(PawnGroupMakerParms), typeof(PawnGroupMaker), typeof(List<Pawn>), typeof(bool)]));
        Unpatched(GenerateRaid, true);
        Unpatched(LeaderJoinLedger.Candidates, true); Unpatched(LeaderJoinLedger.Generate, true);
        if (Harmony.GetPatchInfo(GenerateRaid)?.Owners.Contains("honor.series.contracts.preview") != true ||
            Harmony.GetPatchInfo(LeaderJoinLedger.Candidates)?.Owners.Contains("honor.series.contracts.preview") != true ||
            Harmony.GetPatchInfo(LeaderJoinLedger.Generate)?.Owners.Contains("honor.series.contracts.preview") != true)
            throw new ArgumentException("Native aid must not redress existing world pawns.");
        return (IncidentWorker_RaidFriendly)def.Worker;
    }
    private static Pawn[] Threats(Map map, Faction source)
    {
        var threats = map.attackTargetsCache.TargetsHostileToColony.OfType<Pawn>().Where(p => GenHostility.IsActiveThreatToPlayer(p)).OrderBy(p => p.GetUniqueLoadID(), StringComparer.Ordinal).ToArray();
        float power = threats.Sum(p => p.kindDef.combatPower);
        if (threats.Length == 0 || float.IsNaN(power) || float.IsInfinity(power) || power <= 120 ||
            threats.Any(p => p.Faction == null || !source.HostileTo(p.Faction) || !p.Faction.HostileTo(source) || !Find.FactionManager.AllFactionsListForReading.Contains(p.Faction)))
            throw new ArgumentException("Review an actual active shared-enemy attack above native friendly-aid threshold.");
        return threats;
    }
    public ProtectionAidReview ReviewAid(string charter, Map map, string id = null)
    {
        var source = Ready(charter); _ = Worker();
        if (map == null || !Find.Maps.Contains(map) || !map.IsPlayerHome || map.Parent?.Faction != Faction.OfPlayer)
            throw new ArgumentException("An actual loaded player home settlement is required.");
        var threats = Threats(map, source);
        var all = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Concat(map.mapPawns.AllPawns).Distinct().Select(p => p.GetUniqueLoadID()).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var decision = NpcDecisionAuthority.Read(source);
        var plan = new CharterAidPlan(id ?? Guid.NewGuid().ToString("N"), charter, source.GetUniqueLoadID(), source.leader.GetUniqueLoadID(), map.GetUniqueLoadID(),
            threats.Select(p => p.GetUniqueLoadID()).ToArray(), all, Find.TickManager.TicksGame, Hash(Charters.Book.Export()), Hash(Politics.Model.Export()), decision);
        Ledger.Review(Charters.Book, plan, decision, Allied(source), Find.TickManager.TicksGame);
        float points = StorytellerUtility.DefaultThreatPointsNow(map);
        if (float.IsNaN(points) || float.IsInfinity(points) || points <= 0 || points > 100000) throw new ArgumentException("Native threat points unavailable.");
        string stamp = JsonSerializer.Serialize(new { State = Stamp(source), Plan = plan, Points = points,
            Threats = threats.Select(p => new { Id = p.GetUniqueLoadID(), Faction = p.Faction.GetUniqueLoadID(), p.kindDef.combatPower }) });
        return new(context, plan, map, source, source.leader, stamp);
    }
    public void Request(ProtectionAidReview reviewed) => Execute(() => {
        if (reviewed?.Context != context) throw new ArgumentException("Review this game first.");
        var fresh = ReviewAid(reviewed.Plan.Charter, reviewed.Map, reviewed.Plan.Id);
        if (fresh.Stamp != reviewed.Stamp || fresh.Source != reviewed.Source || fresh.Representative != reviewed.Representative)
            throw new ArgumentException("Defense request changed.");
        var worker = Worker();
        var parms = new IncidentParms { target = fresh.Map, faction = fresh.Source, points = StorytellerUtility.DefaultThreatPointsNow(fresh.Map),
            pawnGroupKind = PawnGroupKindDefOf.Combat, raidStrategy = RaidStrategyDefOf.ImmediateAttackFriendly, raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn };
        Ledger = Ledger.Begin(Charters.Book, fresh.Plan, NpcDecisionAuthority.Read(fresh.Source), Allied(fresh.Source), Find.TickManager.TicksGame);
        // Persist Applying before ANY native dispatch. An exception retains it; never replay or call inability a refusal.
        nativeParms = parms; nativeWorker = worker; generated = []; generationObserved = generationAmbiguous = false;
        active = this; Executing = true;
        try
        {
            // An explicit faction must not bypass the native candidate/availability policy.
            bool returned = worker.FactionCanBeGroupSource(fresh.Source, parms) && worker.CanFireNow(parms) && worker.TryExecute(parms);
            if (context != Current.Game || !Find.Maps.Contains(fresh.Map) || fresh.Source.leader != fresh.Representative || !Allied(fresh.Source) ||
                Hash(Charters.Book.Export()) != fresh.Plan.BookProof || Hash(Politics.Model.Export()) != fresh.Plan.PoliticsProof)
                throw new ArgumentException("Native aid returned across changed authority or game context; retain uncertain intent.");
            var old = new HashSet<string>(fresh.Plan.BeforePawns, StringComparer.Ordinal);
            if (generationAmbiguous || generated.Any(p => p == null || old.Contains(p.GetUniqueLoadID()) || p.Faction != fresh.Source))
                throw new ArgumentException("Native raid output differs from the exact new reinforcement identities.");
            string[] arrivals = generated.Where(p => p.Spawned && p.Map == fresh.Map && !p.Dead && p.Faction == fresh.Source)
                .Select(p => p.GetUniqueLoadID()).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Ledger = Ledger.Returned(fresh.Plan.Id, returned, arrivals, Find.TickManager.TicksGame);
            Messages.Message((Ledger.Requests.Last().Outcome == CharterAidOutcome.ArrivalsObserved ? "CP_Arrived" : "CP_NoVerifiedAid").Translate(), MessageTypeDefOf.NeutralEvent);
        }
        finally { Executing = false; active = null; nativeParms = null; nativeWorker = null; generated = null; }
    });
    private static void Execute(Action action)
    {
        try { action(); }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Protection request not advanced; original intent retained. " + e.GetType().Name); Messages.Message("CP_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
}

[HarmonyPatch]
internal static class ProtectionAidGenerationReceipt
{
    static bool Prepare() => CharterProtectionLedger.GenerateRaid?.ReturnType == typeof(bool);
    static System.Reflection.MethodBase TargetMethod() => CharterProtectionLedger.GenerateRaid;
    static void Postfix(IncidentWorker_Raid __instance, IncidentParms __0, List<Pawn> __1, bool __2, bool __result)
        => CharterProtectionLedger.ObserveGenerated(__instance, __0, __1, __2, __result);
}

public sealed class CharterProtectionWindow(string charter) : Window
{
    private readonly Game context = Current.Game;
    private Vector2 scroll;
    private float height = 1000;
    public override Vector2 InitialSize => new(850, 720);
    public override void PreOpen() { base.PreOpen(); forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override void DoWindowContents(Rect rect)
    {
        if (context != Current.Game) { Widgets.Label(rect, "CP_Blocked".Translate()); return; }
        var ledger = context.GetComponent<CharterProtectionLedger>();
        bool current = context.GetComponent<LiveCharterLedger>().Book.Charter(charter).Phase == RecordedCharterPhase.Active;
        var area = new Rect(0, 0, rect.width, rect.height - 45); Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, 10000));
        list.Label("CP_Terms".Translate()); list.Label("CP_Limits".Translate());
        if (current && !ledger.RestoreFailed && !ledger.Ledger.Compacts.Any(c => c.Charter == charter) && list.ButtonText("CP_Agree".Translate()))
        {
            try { var review = ledger.ReviewAgreement(charter); Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CP_Terms".Translate(), () => ledger.Agree(review))); }
            catch (ArgumentException) { Messages.Message("CP_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
        }
        if (ledger.RestoreFailed) list.Label("CP_Blocked".Translate());
        else if (ledger.Ledger.Compacts.Any(c => c.Charter == charter))
        {
            list.Label("CP_Agreed".Translate());
            if (ledger.Ledger.Pending) list.Label("CP_Uncertain".Translate());
            else if (current) foreach (var map in Find.Maps.Where(m => m.IsPlayerHome).ToArray())
                if (list.ButtonText("CP_Request".Translate(map.Parent.LabelCap)))
                {
                    try { var review = ledger.ReviewAid(charter, map); Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CP_Confirm".Translate(), () => ledger.Request(review))); }
                    catch (ArgumentException) { Messages.Message("CP_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
                }
        }
        foreach (var r in ledger.Ledger.Requests.Where(r => r.Plan.Charter == charter).Reverse())
            list.Label("CP_Record".Translate((r.Plan.Tick / (double)PoliticalWorld.Day).ToString("0.##"), ("CP_" + r.Outcome).Translate(), r.Arrivals?.Length ?? 0));
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
