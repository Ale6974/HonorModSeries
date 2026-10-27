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
public sealed record ProtectionGenerationReview(Game Context, string Request, Map Map, Faction Source, string[] Pawns, string Stamp);

/// <summary>Separate allied protection compact, native aid, or an explicit supported NPC refusal response. No automatic independence.</summary>
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
    private string nativeRequest;
    private Pawn[] generated;
    private bool generationObserved, generationAmbiguous;
    internal static readonly System.Reflection.MethodInfo GenerateRaid = AccessTools.Method(typeof(IncidentWorker_Raid), nameof(IncidentWorker_Raid.TryGenerateRaidInfo),
        [typeof(IncidentParms), typeof(List<Pawn>).MakeByRefType(), typeof(bool)]);
    internal static bool PreserveOriginalPawns => active != null && active.context == Current.Game && active.Executing;
    internal static void ObserveGenerated(IncidentWorker_Raid worker, IncidentParms parms, List<Pawn> pawns, bool debug, bool returned)
    {
        if (!PreserveOriginalPawns || worker != active.nativeWorker || parms != active.nativeParms || debug) return;
        if (active.generationObserved)
        {
            active.generationAmbiguous = true;
            if (active.Ledger.Requests.Any(r => r.Plan.Id == active.nativeRequest && r.Generation != null))
                active.Ledger = active.Ledger.MarkRepeatedGenerationReturn(active.nativeRequest);
            return;
        }
        active.generationObserved = true;
        active.generated = returned && pawns != null ? pawns.ToArray() : [];
        if (!returned || active.generated.Length == 0 || parms.target is not Map map) return;
        try
        {
            var plan = active.Ledger.Requests.Single(r => r.Plan.Id == active.nativeRequest).Plan;
            var original = new HashSet<string>(plan.BeforePawns, StringComparer.Ordinal);
            if (map.GetUniqueLoadID() != plan.Map || parms.faction?.GetUniqueLoadID() != plan.Source || active.generated.Any(p =>
                p == null || !p.Spawned || p.Map != map || p.Dead || p.Faction != parms.faction || original.Contains(p.GetUniqueLoadID()))) return;
            active.Ledger = active.Ledger.RecordGeneration(plan.Id, plan.Source, plan.Map,
                active.generated.Select(p => p.GetUniqueLoadID()).OrderBy(p => p, StringComparer.Ordinal).ToArray(), Find.TickManager.TicksGame);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            active.generationAmbiguous = true;
            Log.Warning("[Honor.Contracts.Preview] Exact aid generation receipt unavailable; native outcome remains uncertain. " + e.GetType().Name);
        }
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
        if (TryRefuse(fresh, worker, parms)) return;
        Ledger = Ledger.Begin(Charters.Book, fresh.Plan, NpcDecisionAuthority.Read(fresh.Source), Allied(fresh.Source), Find.TickManager.TicksGame);
        // Persist Applying before ANY native dispatch. An exception retains it; never replay or call inability a refusal.
        nativeParms = parms; nativeWorker = worker; nativeRequest = fresh.Plan.Id; generated = []; generationObserved = generationAmbiguous = false;
        active = this; Executing = true;
        string resultKey = null;
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
            resultKey = Ledger.Requests.Last().Outcome == CharterAidOutcome.ArrivalsObserved ? "CP_Arrived" : "CP_NoVerifiedAid";
        }
        finally { Executing = false; active = null; nativeParms = null; nativeWorker = null; nativeRequest = null; generated = null; }
        Messages.Message(resultKey.Translate(), MessageTypeDefOf.NeutralEvent);
    });
    private bool TryRefuse(ProtectionAidReview reviewed, IncidentWorker_RaidFriendly worker, IncidentParms parms)
    {
        bool Vile() => Honor.Core.HonorDefOf.Honor_Vile != null && reviewed.Representative.story?.traits?.HasTrait(Honor.Core.HonorDefOf.Honor_Vile) == true;
        if (!Vile()) return false;
        Unpatched(AccessTools.Method(typeof(TraitSet), nameof(TraitSet.HasTrait), [typeof(TraitDef)]));
        if (DefDatabase<TraitDef>.GetNamedSilentFail("Honor_Vile") != Honor.Core.HonorDefOf.Honor_Vile)
            throw new ArgumentException("Registered original response trait required.");
        // This mod-authored ordinary NPC policy refuses deployment only after positive native availability AND group-capability checks.
        // These checks do not generate or dispatch pawns. Any false/exception keeps inability separate from deliberate refusal.
        Unpatched(AccessTools.Method(typeof(IncidentParmsUtility), nameof(IncidentParmsUtility.GetDefaultPawnGroupMakerParms), [typeof(PawnGroupKindDef), typeof(IncidentParms), typeof(bool)]));
        Unpatched(AccessTools.Method(typeof(PawnGroupMaker), nameof(PawnGroupMaker.CanGenerateFrom), [typeof(PawnGroupMakerParms)]));
        Unpatched(AccessTools.Method(typeof(PawnGroupMaker), nameof(PawnGroupMaker.MinPointsToGenerateAnything), [typeof(FactionDef), typeof(PawnGroupMakerParms)]));
        Unpatched(AccessTools.Method(typeof(PawnGroupKindWorker_Normal), nameof(PawnGroupKindWorker_Normal.MinPointsToGenerateAnything), [typeof(PawnGroupMaker), typeof(FactionDef), typeof(PawnGroupMakerParms)]));
        Unpatched(AccessTools.Method(typeof(PawnGroupKindWorker_Normal), nameof(PawnGroupKindWorker_Normal.CanGenerateFrom), [typeof(PawnGroupMakerParms), typeof(PawnGroupMaker)]));
        Unpatched(AccessTools.Method(typeof(PawnGroupKindWorker), nameof(PawnGroupKindWorker.CanGenerateFrom), [typeof(PawnGroupMakerParms), typeof(PawnGroupMaker)]));
        Unpatched(AccessTools.Method(typeof(PawnGroupMakerUtility), "AnyOptions"));
        bool available;
        active = this; Executing = true;
        try
        {
            available = worker.FactionCanBeGroupSource(reviewed.Source, parms) && worker.CanFireNow(parms);
            if (available)
            {
                var group = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(PawnGroupKindDefOf.Combat, parms);
                available = reviewed.Source.def.pawnGroupMakers?.Any(m => m != null && m.GetType() == typeof(PawnGroupMaker) &&
                    m.kindDef == PawnGroupKindDefOf.Combat && !float.IsNaN(m.commonality) && !float.IsInfinity(m.commonality) && m.commonality > 0 && m.CanGenerateFrom(group)) == true;
            }
        }
        finally { Executing = false; active = null; }
        var fresh = ReviewAid(reviewed.Plan.Charter, reviewed.Map, reviewed.Plan.Id);
        if (fresh.Stamp != reviewed.Stamp || fresh.Source != reviewed.Source || fresh.Representative != reviewed.Representative || !Vile() ||
            parms.target != fresh.Map || parms.faction != fresh.Source || parms.points != StorytellerUtility.DefaultThreatPointsNow(fresh.Map) ||
            parms.raidStrategy != RaidStrategyDefOf.ImmediateAttackFriendly || parms.raidArrivalMode != PawnsArrivalModeDefOf.EdgeWalkIn)
            throw new ArgumentException("NPC response scope or current personality changed during capability review.");
        if (!available) return false;
        Ledger = Ledger.Refuse(Charters.Book, fresh.Plan, NpcDecisionAuthority.Read(fresh.Source), Allied(fresh.Source),
            Honor.Core.HonorDefOf.Honor_Vile.defName, 1, (decimal)parms.points, Find.TickManager.TicksGame);
        Find.LetterStack.ReceiveLetter("CP_RefusalTitle".Translate(), "CP_Refused".Translate(fresh.Source.Name, fresh.Representative.LabelShort), LetterDefOf.NegativeEvent);
        return true;
    }
    public ProtectionGenerationReview ReviewGeneration(string id)
    {
        if (context != Current.Game || Current.ProgramState != ProgramState.Playing || RestoreFailed || Charters.RestoreFailed ||
            Politics.RestoreFailed || Executing || active != null || context.GetComponent<PoliticalTransitionLedger>().Executing)
            throw new ArgumentException("Review the original saved generation outside native execution.");
        var row = Ledger.Requests.SingleOrDefault(r => r.Plan.Id == id) ?? throw new ArgumentException("Unknown aid request.");
        if (row.Generation == null) throw new ArgumentException("No original positive generation receipt; never infer it from a later army.");
        var maps = Find.Maps.Where(m => m != null && m.GetUniqueLoadID() == row.Plan.Map).Take(2).ToArray();
        var sources = Find.FactionManager.AllFactionsListForReading.Where(f => f != null && f.GetUniqueLoadID() == row.Plan.Source).Take(2).ToArray();
        if (maps.Length != 1 || sources.Length != 1 || sources[0].GetType() != typeof(Faction) || sources[0].IsPlayer)
            throw new ArgumentException("Exact recorded source and original loaded map required.");
        var map = maps[0]; var source = sources[0];
        var expected = new HashSet<string>(row.Generation.Pawns, StringComparer.Ordinal);
        var known = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Concat(map.mapPawns.AllPawns).Distinct()
            .Where(p => p != null && expected.Contains(p.GetUniqueLoadID())).ToArray();
        if (known.Length != expected.Count || known.Select(p => p.GetUniqueLoadID()).Distinct(StringComparer.Ordinal).Count() != expected.Count ||
            known.Any(p => !p.Spawned || p.Map != map || p.Dead || p.Faction != source))
            throw new ArgumentException("Every originally recorded reinforcement must retain its exact current source and map identity.");
        var ids = known.Select(p => p.GetUniqueLoadID()).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        Ledger.ReviewGenerationAcknowledgement(Charters.Book, id, source.GetUniqueLoadID(), map.GetUniqueLoadID(), ids, Find.TickManager.TicksGame);
        string stamp = JsonSerializer.Serialize(new { History = Ledger.Export(), Book = Charters.Book.Export(), Politics = Politics.State,
            Map = map.GetUniqueLoadID(), Source = source.GetUniqueLoadID(), Ids = ids, Tick = Find.TickManager.TicksGame });
        return new(context, id, map, source, ids, stamp);
    }
    public void AcknowledgeGeneration(ProtectionGenerationReview reviewed) => Execute(() => {
        if (reviewed?.Context != context) throw new ArgumentException("Review this game first.");
        var fresh = ReviewGeneration(reviewed.Request);
        if (fresh.Stamp != reviewed.Stamp || fresh.Map != reviewed.Map || fresh.Source != reviewed.Source || !Enumerable.SequenceEqual(fresh.Pawns, reviewed.Pawns, StringComparer.Ordinal))
            throw new ArgumentException("Recorded generation or current identities changed.");
        Ledger = Ledger.AcknowledgeGeneration(Charters.Book, fresh.Request, fresh.Source.GetUniqueLoadID(), fresh.Map.GetUniqueLoadID(), fresh.Pawns, Find.TickManager.TicksGame);
        Messages.Message("CP_GenerationSaved".Translate(), MessageTypeDefOf.NeutralEvent);
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
    private CharterProtection displayed;
    private IReadOnlyList<CharterAidReceipt> displayedRequests = [];
    public override Vector2 InitialSize => new(850, 720);
    public override void PreOpen() { base.PreOpen(); forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override void DoWindowContents(Rect rect)
    {
        if (context != Current.Game) { Widgets.Label(rect, "CP_Blocked".Translate()); return; }
        var ledger = context.GetComponent<CharterProtectionLedger>();
        // Models are replaced on commit; never clone every historical world-pawn ID on every GUI frame.
        if (displayed != ledger.Ledger) { displayedRequests = ledger.Ledger.Requests; displayed = ledger.Ledger; }
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
        foreach (var r in displayedRequests.Where(r => r.Plan.Charter == charter).Reverse())
        {
            list.Label("CP_Record".Translate((r.Plan.Tick / (double)PoliticalWorld.Day).ToString("0.##"), ("CP_" + r.Outcome).Translate(), r.Arrivals?.Length ?? 0));
            if (current && r.Outcome == CharterAidOutcome.IntentionallyRefused && list.ButtonText("LX_Review".Translate()))
            {
                try
                {
                    var transition = context.GetComponent<PoliticalTransitionLedger>();
                    var review = transition.ReviewLawfulIndependence(r.Plan.Id);
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("LX_Confirm".Translate(review.Admission.Destination.Name),
                        () => transition.StartLawfulIndependence(review)));
                }
                catch (ArgumentException) { Messages.Message("LX_Unavailable".Translate(), MessageTypeDefOf.RejectInput); }
            }
            if (r.Outcome == CharterAidOutcome.Applying && r.Generation != null && list.ButtonText("CP_ReviewGeneration".Translate()))
            {
                try { var review = ledger.ReviewGeneration(r.Plan.Id); Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CP_ConfirmGeneration".Translate(review.Pawns.Length), () => ledger.AcknowledgeGeneration(review))); }
                catch (ArgumentException) { Messages.Message("CP_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
            }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
