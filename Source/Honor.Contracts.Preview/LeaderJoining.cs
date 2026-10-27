using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

public sealed class LeaderJoinRecord : IExposable
{
    public string Id, Snapshot, ActorLabel, SourceLabel, DestinationLabel;
    public Pawn Actor, Successor, TargetLeader;
    public Faction SourceFaction, TargetFaction;
    public Settlement Source, Destination;
    public bool Finished;
    internal LeaderJoinPhase? DisplayPhase; // UI cache only; execution always restores/reviews Snapshot afresh.
    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id"); Scribe_Values.Look(ref Snapshot, "joinExecutionV1"); Scribe_Values.Look(ref Finished, "finished");
        Scribe_Values.Look(ref ActorLabel, "actorLabel"); Scribe_Values.Look(ref SourceLabel, "sourceLabel"); Scribe_Values.Look(ref DestinationLabel, "destinationLabel");
        Scribe_References.Look(ref Actor, "actor"); Scribe_References.Look(ref Successor, "successor"); Scribe_References.Look(ref TargetLeader, "targetLeader");
        Scribe_References.Look(ref SourceFaction, "sourceFaction"); Scribe_References.Look(ref TargetFaction, "targetFaction");
        Scribe_References.Look(ref Source, "source"); Scribe_References.Look(ref Destination, "destination");
    }
    public LeaderJoinExecution Journal() => LeaderJoinExecution.Restore(Snapshot);
}
public sealed record LeaderJoinReview(Game Context, LeaderJoinPlan Plan, Pawn Actor, Faction SourceFaction, Faction TargetFaction, Settlement Source, Settlement Destination, Pawn TargetLeader);

/// <summary>One saved leader personally joins the player's nonplayer administration; native succession and explicit forward recovery.</summary>
public sealed class LeaderJoinLedger : GameComponent
{
    internal static readonly MethodInfo Candidates = AccessTools.Method(typeof(PawnGenerator), "GetValidCandidatesToRedress", [typeof(PawnGenerationRequest)]);
    internal static readonly MethodInfo Generate = AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), [typeof(PawnGenerationRequest)]);
    private List<LeaderJoinRecord> records = [];
    private LeaderJoinRecord activeNativeCall;
    private readonly Game context;
    internal static LeaderJoinLedger ActiveNative => LeaderNativeCallScope<LeaderJoinLedger>.Owner;
    public bool RestoreFailed { get; private set; }
    public bool BlocksChanges => RestoreFailed || records == null || records.Any(r => r == null || !r.Finished);
    internal bool ReservesResident(Pawn p) => records?.Any(r => r != null && (RestoreFailed || !r.Finished) && (r.Actor == p || r.Successor == p)) == true;
    private LiveCharterLedger Charters => Current.Game.GetComponent<LiveCharterLedger>();
    private TerritoryAdministrationLedger Territories => Current.Game.GetComponent<TerritoryAdministrationLedger>();
    public LeaderJoinLedger(Game game) { context = game; }
    public override void ExposeData()
    {
        Scribe_Collections.Look(ref records, "honorPersonalLeaderJoinsV1", LookMode.Deep);
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        records ??= [];
        try
        {
            if (records.Count > 64 || records.Any(r => r == null || string.IsNullOrWhiteSpace(r.Id) || r.Id.Length > 120 || r.Snapshot == null) ||
                records.Sum(r => (long)r.Snapshot.Length) > 32000000 || records.Select(r => r.Id).Distinct().Count() != records.Count || records.Count(r => !r.Finished) > 1)
                throw new ArgumentException("Invalid personal joining archive.");
            foreach (var r in records)
            {
                var state = r.Journal().State;
                r.DisplayPhase = state.Phase;
                if (r.Finished != (state.Phase is LeaderJoinPhase.Completed or LeaderJoinPhase.Cancelled)) throw new ArgumentException("Joining phase mismatch.");
                if (!r.Finished) References(r); else ClearActors(r);
            }
        }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Personal leader joining archive unavailable; original references retained. " + e.GetType().Name); }
    }
    public override void FinalizeInit()
    {
        if (RestoreFailed) return;
        try
        {
            foreach (var r in records)
            {
                var s = r.Journal().State;
                if (s.Plan.Tick > Find.TickManager.TicksGame || s.NativeReturn?.Tick > Find.TickManager.TicksGame || s.SuccessionReturn?.Tick > Find.TickManager.TicksGame || s.Succession?.Tick > Find.TickManager.TicksGame || s.ObservedTick > Find.TickManager.TicksGame || s.CareCheckTick > Find.TickManager.TicksGame || s.FinishedTick > Find.TickManager.TicksGame)
                    throw new ArgumentException("Future joining evidence.");
                if (!r.Finished) References(r);
            }
        }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Personal join clock/reference mismatch; changes blocked. " + e.GetType().Name); }
    }
    private static void ClearActors(LeaderJoinRecord r)
    { r.Actor = null; r.Successor = null; r.TargetLeader = null; r.Source = null; r.Destination = null; r.SourceFaction = null; r.TargetFaction = null; }
    private static void NoPatches(MethodInfo method, bool own = false)
    {
        if (method == null || Harmony.GetPatchInfo(method)?.Owners.Any(id => !own || id != "honor.series.contracts.preview") == true)
            throw new ArgumentException("Unverified native succession/faction callbacks.");
    }
    private static void NativeApi()
    {
        NoPatches(AccessTools.Method(typeof(Pawn), nameof(Pawn.SetFaction), [typeof(Faction), typeof(Pawn)]));
        NoPatches(AccessTools.Method(typeof(Faction), nameof(Faction.Notify_LeaderLost), []));
        NoPatches(AccessTools.Method(typeof(Faction), nameof(Faction.TryGenerateNewLeader), []));
        NoPatches(Generate, true);
        NoPatches(Candidates, true);
        if (Candidates.ReturnType != typeof(IEnumerable<Pawn>) || Harmony.GetPatchInfo(Candidates)?.Owners.Contains("honor.series.contracts.preview") != true ||
            Generate.ReturnType != typeof(Pawn) || Harmony.GetPatchInfo(Generate)?.Owners.Contains("honor.series.contracts.preview") != true)
            throw new ArgumentException("Scoped no-redress successor hook unavailable.");
        if (Find.Scenario != null)
        {
            NoPatches(AccessTools.Method(typeof(Scenario), nameof(Scenario.Notify_PawnGenerated), [typeof(Pawn), typeof(PawnGenerationContext), typeof(bool)]));
            foreach (var part in Find.Scenario.AllParts)
            {
                if (part == null || part.GetType().Assembly != typeof(Pawn).Assembly) throw new ArgumentException("Unverified scenario generation callbacks.");
                NoPatches(AccessTools.Method(part.GetType(), nameof(ScenPart.Notify_PawnGenerated), [typeof(Pawn), typeof(PawnGenerationContext), typeof(bool)]));
            }
        }
        ResidentExecution.EnsureRetention();
    }
    private void Ready(bool recovering)
    {
        if (Current.ProgramState != ProgramState.Playing || Current.Game != context || ActiveNative != null) throw new ArgumentException("Review this current playing game outside a native mutation callback.");
        var game = Current.Game; var political = game.GetComponent<PoliticalTransitionLedger>(); var payments = game.GetComponent<TributePaymentLedger>();
        if (RestoreFailed || !recovering && BlocksChanges || Charters.RestoreFailed || Charters.UnresolvedTownLosses || Territories.RestoreFailed || Territories.HasPendingTownLosses ||
            Territories.Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) || political.RestoreFailed || political.Journal.Pending ||
            game.GetComponent<SubtreeExitLedger>().BlocksChanges || game.GetComponent<LeaderRelocationLedger>().BlocksChanges ||
            payments.RestoreFailed || payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) || game.GetComponent<PlayerPoliticsLedger>().RestoreFailed ||
            game.GetComponent<PlayerPoliticsLedger>().State.Mode != PlayerAllegianceMode.Independent || game.GetComponent<RealmAdministrationLedger>().RestoreFailed ||
            game.GetComponent<UnionPoliticsLedger>().RestoreFailed || UnionWarCouncil.HasExternalProvider)
            throw new ArgumentException("Resolve other pending political, city and physical payment operations.");
        Territories.ValidatePoliticalLosses(political.Journal); NativeApi();
    }
    private static Settlement[] Cities() => Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => !s.Destroyed).ToArray();
    // Include retained registered historical town references: absence from the live world list is not proof of no saved association.
    private Settlement[] KnownTowns() => Find.WorldObjects.AllWorldObjects.OfType<Settlement>()
        .Concat(Charters.Book.Entities.Select(e => Charters.Binding(e.Id).Settlement)).Where(s => s != null).Distinct().ToArray();
    private void OtherResidency(Pawn actor, Settlement source, Settlement destination, bool unassigned)
    {
        var towns = KnownTowns();
        if (unassigned && towns.Any(s => s.previouslyGeneratedInhabitants == null) ||
            towns.Any(s => s != destination && (unassigned || s != source) && s.previouslyGeneratedInhabitants?.Contains(actor) == true))
            throw new ArgumentException("Outside or unverified saved leader assignment; never infer unassigned residence from missing town references.");
    }
    private static void Town(Settlement s, Faction owner)
    {
        if (s == null || s.GetType() != typeof(Settlement) || s.Destroyed || s.HasMap || s.questTags?.Count > 0 || s.Faction != owner || !Cities().Contains(s) ||
            s.previouslyGeneratedInhabitants == null || s.previouslyGeneratedInhabitants.Count > 128 || s.previouslyGeneratedInhabitants.Any(p => p == null) ||
            s.previouslyGeneratedInhabitants.Distinct().Count() != s.previouslyGeneratedInhabitants.Count)
            throw new ArgumentException("Unsupported source/destination saved roster.");
    }
    private static string[] Roster(Settlement s) => s.previouslyGeneratedInhabitants.Select(p => p.GetUniqueLoadID()).ToArray();
    private static string Stock(Settlement s) => CityStockPreservation.Capture(s, out _) ?? throw new ArgumentException("Unsupported exact original town stock.");
    private static void SourceAuthority(Faction source, Faction target)
    {
        if (source == null || source.GetType() != typeof(Faction) || source.def.GetType() != typeof(FactionDef) || source.IsPlayer || source.Hidden || source.temporary || source.defeated || !source.HasGoodwill || source.def.permanentEnemy ||
            source.def.defName == UnionPoliticsLedger.UnionDefName || source.def.defName == RealmAdministrationLedger.DefName || source.def.royalTitleTags?.Count > 0 ||
            !Find.FactionManager.AllFactionsListForReading.Contains(source) || Find.QuestManager == null || Find.QuestManager.IsReservedByAnyQuest(source) ||
            Find.QuestManager.IsReservedByAnyQuest(target) || Cities().Count(s => s.Faction == source) < 2 ||
            source.GoodwillWith(Faction.OfPlayer) < 75 || source.HostileTo(Faction.OfPlayer) || Faction.OfPlayer.HostileTo(source) ||
            source.RelationKindWith(target) != FactionRelationKind.Ally || target.RelationKindWith(source) != FactionRelationKind.Ally)
            throw new ArgumentException("Separate personal joining rule requires a surviving ordinary source and reciprocal alliance.");
    }
    private static void SuccessionAvailable(Faction source, Pawn actor)
    {
        if (source.def.generateNewLeaderFromMapMembersOnly)
        {
            var candidates = Find.Maps.SelectMany(m => m.mapPawns.AllPawns).Where(p => p != actor && !p.Destroyed && p.HomeFaction == source).ToArray();
            if (candidates.Length == 0 || candidates.Any(p => p.Dead || p.Downed || !p.Awake() || p.InMentalState || p.IsPrisoner || p.IsSlave || p.HostFaction != null ||
                p.Faction != source || p.HasExtraHomeFaction() || p.HasExtraMiniFaction() || Find.QuestManager.IsReservedByAnyQuest(p) || Find.FactionManager.AllFactionsListForReading.Any(f => f != source && f.leader == p)))
                throw new ArgumentException("No verified native map successor.");
        }
        else
        {
            var kinds = LeaderKinds(source);
            if (source.ideos?.PrimaryIdeo == null || kinds.Length == 0 || kinds.Any(k => k == null || k.GetType() != typeof(PawnKindDef) || k.race?.race?.Humanlike != true ||
                k.race.comps?.Any(c => c == null || c.GetType().Assembly != typeof(Pawn).Assembly || c.compClass == null || c.compClass.Assembly != typeof(Pawn).Assembly) == true))
                throw new ArgumentException("No verified native leader-generation route.");
        }
    }
    private static PawnKindDef[] LeaderKinds(Faction source)
    {
        if (source.def.pawnGroupMakers == null || source.def.pawnGroupMakers.Any(g => g == null || g.kindDef == PawnGroupKindDefOf.Combat && (g.options == null || g.options.Any(o => o == null || o.kind == null))))
            throw new ArgumentException("Unsupported source leader kind definitions.");
        return source.def.pawnGroupMakers.Where(g => g.kindDef == PawnGroupKindDefOf.Combat).SelectMany(g => g.options).Where(o => o.kind.factionLeader).Select(o => o.kind)
            .Concat(source.def.fixedLeaderKinds ?? []).ToArray();
    }
    public LeaderJoinReview Review(string charter, Settlement destination)
    {
        Ready(false); if (records.Count >= 64) throw new ArgumentException("Personal joining archive full.");
        var c = Charters.Book.Charter(charter); var binding = Charters.Binding(c.Terms.Subject); var sourceCity = binding.Settlement;
        if (c.Phase != RecordedCharterPhase.Active || !Charters.Book.PlayerManaged(c) || binding.PlayerGroup || binding.FactionGroup || sourceCity == null)
            throw new ArgumentException("Active source town charter required.");
        Charters.Book.ValidateCurrent(charter, Charters.Observe());
        var source = sourceCity.Faction; var target = Current.Game.GetComponent<RealmAdministrationLedger>().TerritoryAuthority(); SourceAuthority(source, target);
        Town(sourceCity, source); Town(destination, target); var actor = source.leader;
        var actual = ResidentExecution.ReadRetainedLeader(actor, LeaderPreservationProfiles.Current);
        bool unassigned = !sourceCity.previouslyGeneratedInhabitants.Contains(actor);
        if (actor.health.surgeryBills.Count != 0 || sourceCity == destination || Find.QuestManager.IsReservedByAnyQuest(actor) || target.leader == null || target.leader == actor ||
            target.leader.Dead || target.leader.Faction != target || Find.QuestManager.IsReservedByAnyQuest(target.leader) ||
            destination.previouslyGeneratedInhabitants.Count >= 128 || destination.previouslyGeneratedInhabitants.Contains(actor)) throw new ArgumentException("Unsupported personal actor/office/surgery/residency.");
        OtherResidency(actor, sourceCity, destination, unassigned);
        SuccessionAvailable(source, actor);
        var plan = new LeaderJoinPlan(charter, actor.GetUniqueLoadID(), source.GetUniqueLoadID(), target.GetUniqueLoadID(), sourceCity.GetUniqueLoadID(), destination.GetUniqueLoadID(),
            target.leader.GetUniqueLoadID(), Charters.Book.Export(), Territories.Ledger.Export(), new(actual.Id, actual.Preserved, actual.MedicalCare), Stock(sourceCity), Stock(destination),
            Roster(sourceCity), Roster(destination), Find.TickManager.TicksGame, true, SeparateSuccession: true, UnassignedSource: unassigned) { ResidentProfile = LeaderPreservationProfiles.Current };
        LeaderJoinExecution.Prepare(plan); return new(Current.Game, plan, actor, source, target, sourceCity, destination, target.leader);
    }
    private static void References(LeaderJoinRecord r)
    {
        var s = r.Journal().State; var p = s.Plan;
        if (r.Actor == null || r.Actor.GetUniqueLoadID() != p.Leader || r.Source == null || r.Source.GetUniqueLoadID() != p.SourceCity ||
            r.Destination == null || r.Destination.GetUniqueLoadID() != p.DestinationCity || r.SourceFaction?.GetUniqueLoadID() != p.SourceFaction ||
            r.TargetFaction?.GetUniqueLoadID() != p.TargetFaction || r.TargetLeader?.GetUniqueLoadID() != p.TargetLeader ||
            (s.Successor ?? s.Succession?.Successor) != null && r.Successor?.GetUniqueLoadID() != (s.Successor ?? s.Succession?.Successor)) throw new ArgumentException("Personal join reference identities changed.");
    }
    private LeaderJoinAction Review(LeaderJoinRecord r)
    {
        Ready(true); References(r); var j = r.Journal(); var p = j.State.Plan;
        if (r.Finished || Current.Game.GetComponent<RealmAdministrationLedger>().TerritoryAuthority() != r.TargetFaction) throw new ArgumentException("Joining authority changed.");
        SourceAuthority(r.SourceFaction, r.TargetFaction); Town(r.Source, r.SourceFaction); Town(r.Destination, r.TargetFaction);
        if (r.TargetFaction.leader != r.TargetLeader || r.TargetLeader.Dead || Find.QuestManager.IsReservedByAnyQuest(r.Actor) || Find.QuestManager.IsReservedByAnyQuest(r.TargetLeader) || r.Actor.health.surgeryBills.Count != 0)
            throw new ArgumentException("Unexpected office, medical orders or outside residency.");
        OtherResidency(r.Actor, r.Source, r.Destination, p.UnassignedSource);
        if (j.State.Phase == LeaderJoinPhase.Prepared) { ResidentExecution.ReadRetainedLeader(r.Actor, p.EffectiveResidentProfile); SuccessionAvailable(r.SourceFaction, r.Actor); }
        bool officeRequired = r.Actor.Faction == r.TargetFaction || p.SeparateSuccession && j.State.Phase != LeaderJoinPhase.Prepared;
        if (officeRequired && (r.SourceFaction.leader == null || r.SourceFaction.leader == r.Actor || r.SourceFaction.leader.Dead || r.SourceFaction.leader.Downed ||
            !r.SourceFaction.leader.Awake() || r.SourceFaction.leader.InMentalState || r.SourceFaction.leader.IsPrisoner || r.SourceFaction.leader.IsSlave || r.SourceFaction.leader.HostFaction != null || r.SourceFaction.leader.Faction != r.SourceFaction ||
            !PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Contains(r.SourceFaction.leader) || Find.FactionManager.AllFactionsListForReading.Any(f => f != r.SourceFaction && f.leader == r.Actor)))
            throw new ArgumentException("No actual valid surviving source successor or office separation.");
        if (officeRequired && Find.FactionManager.AllFactionsListForReading.Any(f => f != r.SourceFaction && f.leader == r.SourceFaction.leader))
            throw new ArgumentException("Source successor holds an unreviewed other faction office.");
        return j.Review(ResidentExecution.ReadJoiningLeader(r.Actor, p.EffectiveResidentProfile), r.SourceFaction.leader?.GetUniqueLoadID(), r.TargetFaction.leader?.GetUniqueLoadID(), Charters.Book.Export(), Territories.Ledger.Export(),
            Stock(r.Source), Stock(r.Destination), Roster(r.Source), Roster(r.Destination), Find.TickManager.TicksGame);
    }
    private static void Change(LeaderJoinRecord r, Action<LeaderJoinExecution> action) { var j = r.Journal(); action(j); r.Snapshot = j.Export(); r.DisplayPhase = j.State.Phase; }
    private void ScopedNative(LeaderJoinRecord r, LeaderNativeCall purpose, Action call)
    {
        LeaderNativeCallScope<LeaderJoinLedger>.Run(this, purpose, () => {
            activeNativeCall = r;
            try { call(); }
            finally { activeNativeCall = null; }
        });
    }
    public void Start(LeaderJoinReview reviewed)
    {
        try
        {
            if (reviewed?.Context != Current.Game) throw new ArgumentException("Review the current game.");
            var fresh = Review(reviewed.Plan.Charter, reviewed.Destination);
            if (JsonSerializer.Serialize(fresh.Plan) != JsonSerializer.Serialize(reviewed.Plan) || fresh.Actor != reviewed.Actor || fresh.Source != reviewed.Source || fresh.TargetLeader != reviewed.TargetLeader)
                throw new ArgumentException("Separate personal joining consent changed.");
            var r = new LeaderJoinRecord { Id = Guid.NewGuid().ToString("N"), Snapshot = LeaderJoinExecution.Prepare(fresh.Plan).Export(), Actor = fresh.Actor, Source = fresh.Source, Destination = fresh.Destination,
                SourceFaction = fresh.SourceFaction, TargetFaction = fresh.TargetFaction, TargetLeader = fresh.TargetLeader, DisplayPhase = LeaderJoinPhase.Prepared, ActorLabel = fresh.Actor.LabelShort,
                SourceLabel = fresh.Plan.UnassignedSource ? fresh.SourceFaction.Name : fresh.Source.Label, DestinationLabel = fresh.Destination.Label };
            if (records.Sum(old => (long)old.Snapshot.Length) + r.Snapshot.Length > 32000000) throw new ArgumentException("Joining archive capacity.");
            records.Add(r); Finish(r);
        }
        catch (Exception e) { Interrupted(e); }
    }
    private void Finish(LeaderJoinRecord r)
    {
        var action = Review(r); Find.WorldPawns.gc.CancelGCPass();
        if (action == LeaderJoinAction.ChangeOffice)
        {
            Change(r, j => j.BeginSuccession(Find.TickManager.TicksGame));
            ScopedNative(r, LeaderNativeCall.Succession, () => {
                r.SourceFaction.Notify_LeaderLost();
                Change(r, j => j.RecordSuccessionReturn(r.SourceFaction.leader?.GetUniqueLoadID(), Find.TickManager.TicksGame));
            });
            action = Review(r);
        }
        if (action == LeaderJoinAction.ObserveSuccession)
        {
            r.Successor = r.SourceFaction.leader;
            Change(r, j => j.ObserveSuccession(action, r.Successor.GetUniqueLoadID(), Find.TickManager.TicksGame)); action = Review(r);
        }
        if (action == LeaderJoinAction.ChangeFaction)
        {
            bool separate = r.Journal().State.Plan.SeparateSuccession;
            if (separate && PawnUtility.IsFactionLeader(r.Actor)) throw new ArgumentException("Recorded resignation must prevent a second native succession.");
            Change(r, j => j.Begin(Find.TickManager.TicksGame));
            void Affiliate()
            {
                r.Actor.SetFaction(r.TargetFaction);
                Change(r, j => j.RecordReturn(r.SourceFaction.leader?.GetUniqueLoadID(), r.Actor.playerSettings == null ? null : (int)r.Actor.playerSettings.medCare, Find.TickManager.TicksGame));
            }
            ScopedNative(r, separate ? LeaderNativeCall.Affiliation : LeaderNativeCall.LegacyCombined, Affiliate);
            action = Review(r);
        }
        if (action == LeaderJoinAction.Observe)
        {
            var actor = ResidentExecution.ReadJoiningLeader(r.Actor, r.Journal().State.Plan.EffectiveResidentProfile); r.Successor = r.SourceFaction.leader;
            Change(r, j => j.Observe(action, r.Successor.GetUniqueLoadID(), actor.MedicalCare, Find.TickManager.TicksGame)); action = Review(r);
        }
        if (action == LeaderJoinAction.RestoreCare)
        { Change(r, j => j.SealCare(action, Find.TickManager.TicksGame)); r.Actor.playerSettings.medCare = (MedicalCareCategory)r.Journal().State.Plan.Actor.MedicalCare.Value; action = Review(r); }
        if (!r.Journal().State.CareCheckTick.HasValue)
        { Change(r, j => j.SealCare(action, Find.TickManager.TicksGame)); action = Review(r); }
        if (action == LeaderJoinAction.AddDestination)
        { r.Destination.previouslyGeneratedInhabitants.Add(r.Actor); action = Review(r); }
        if (action == LeaderJoinAction.RemoveSource)
        { if (!r.Source.previouslyGeneratedInhabitants.Remove(r.Actor)) throw new ArgumentException("Source residency disappeared."); action = Review(r); }
        Change(r, j => j.Finish(action, Find.TickManager.TicksGame)); r.Finished = true; ClearActors(r);
        Messages.Message("LJ_Completed".Translate(r.ActorLabel, r.DestinationLabel), MessageTypeDefOf.NeutralEvent);
    }
    public void Recover(string id, bool cancel, Game context)
    {
        try
        {
            if (context != Current.Game) throw new ArgumentException("Review the current game.");
            var r = records.Single(x => x.Id == id); var action = Review(r);
            if (!cancel) Finish(r);
            else { Change(r, j => j.Cancel(action, Find.TickManager.TicksGame)); r.Finished = true; ClearActors(r); }
        }
        catch (Exception e) { Interrupted(e); }
    }
    internal bool ExcludeSuccessionRedress(PawnGenerationRequest request) => Current.Game == context && LeaderNativeCallScope<LeaderJoinLedger>.AllowsSuccession && activeNativeCall != null && request.Faction == activeNativeCall.SourceFaction && LeaderKinds(activeNativeCall.SourceFaction).Contains(request.KindDef);
    private static void Interrupted(Exception e)
    { Log.Warning("[Honor.Contracts.Preview] Personal leader join incomplete/unavailable; intent and actors retained. " + e.GetType().Name); Messages.Message("LJ_Unavailable".Translate(), MessageTypeDefOf.RejectInput); Find.TickManager.Pause(); }
    internal void Draw(Listing_Standard list, string charter)
    {
        list.Label("LJ_Limits".Translate());
        if (RestoreFailed) { list.Label("HCP_RestoreFailed".Translate()); return; }
        foreach (var r in records.Where(r => !r.Finished).ToArray())
        {
            list.Label("LJ_Pending".Translate(r.ActorLabel, r.SourceLabel, r.DestinationLabel)); var context = Current.Game;
            var stage = r.DisplayPhase ??= r.Journal().State.Phase;
            list.Label("LJ_Stage".Translate(("LJ_" + stage).Translate()));
            if (list.ButtonText("LJ_Recover".Translate())) Recover(r.Id, false, context);
            if (stage == LeaderJoinPhase.Prepared && list.ButtonText("LJ_Cancel".Translate())) Recover(r.Id, true, context);
        }
        if (BlocksChanges || charter == null) return;
        if (list.ButtonText("LJ_Review".Translate()))
        {
            var options = new List<FloatMenuOption>();
            foreach (var city in Cities())
            {
                try { var review = Review(charter, city); options.Add(new("LJ_Proposal".Translate(review.Actor.LabelShort, city.Label), () => Find.WindowStack.Add(
                    Dialog_MessageBox.CreateConfirmation((review.Plan.UnassignedSource ? "LJ_UnassignedConfirm" : "LJ_Confirm").Translate(review.Actor.LabelShort, review.Source.Label, city.Label), () => Start(review))))); }
                catch (Exception) { }
            }
            if (options.Count == 0) Messages.Message("LJ_Unavailable".Translate(), MessageTypeDefOf.RejectInput); else Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}

[HarmonyPatch]
internal static class LeaderSuccessorNoRedress
{
    static bool Prepare() => LeaderJoinLedger.Candidates?.ReturnType == typeof(IEnumerable<Pawn>);
    static MethodBase TargetMethod() => LeaderJoinLedger.Candidates;
    static void Postfix(PawnGenerationRequest __0, ref IEnumerable<Pawn> __result)
    {
        if (CharterProtectionLedger.PreserveOriginalPawns) { __result = Enumerable.Empty<Pawn>(); return; }
        if (LeaderJoinLedger.ActiveNative?.ExcludeSuccessionRedress(__0) == true) { __result = Enumerable.Empty<Pawn>(); return; }
        // A GC pin alone does not prevent native redress from changing a pending original pawn's health/gear.
        // Leave entry/world generation alone; only filter exact execution-reserved references in a playing game.
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null || __result == null) return;
        var game = Current.Game; var join = game.GetComponent<LeaderJoinLedger>();
        var towns = game.GetComponent<TerritoryAdministrationLedger>(); var relocation = game.GetComponent<LeaderRelocationLedger>();
        if (join?.BlocksChanges != true && relocation?.BlocksChanges != true && towns?.Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) != true && towns?.RestoreFailed != true) return;
        __result = __result.Where(p => Current.Game == game && join?.ReservesResident(p) != true && towns?.ReservesResident(p) != true && relocation?.ReservesResident(p) != true);
    }
}

[HarmonyPatch]
internal static class LeaderSuccessorNoNewRelations
{
    static bool Prepare() => LeaderJoinLedger.Generate?.ReturnType == typeof(Pawn);
    static MethodBase TargetMethod() => LeaderJoinLedger.Generate;
    static void Prefix(ref PawnGenerationRequest __0)
    {
        if (CharterProtectionLedger.PreserveOriginalPawns || LeaderJoinLedger.ActiveNative?.ExcludeSuccessionRedress(__0) == true) __0.CanGeneratePawnRelations = false;
    }
}
