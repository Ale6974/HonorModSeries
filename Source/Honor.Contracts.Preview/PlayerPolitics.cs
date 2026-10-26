using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

/// <summary>Player sovereignty is represented by allegiance; neither Pawn nor Settlement faction is changed.</summary>
public sealed class PlayerPoliticsLedger : GameComponent
{
    private string snapshot;
    private PlayerAllegiance allegiance;
    private bool synchronizing, routing;
    public bool RestoreFailed { get; private set; }
    public PlayerAllegianceState State => allegiance?.State ?? new();
    public PlayerPoliticsLedger(Game game) { }
    private LiveCharterLedger Charters => Current.Game.GetComponent<LiveCharterLedger>();
    internal PlayerAllegiance Model => allegiance ??= new(Faction.OfPlayer.GetUniqueLoadID());
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed && allegiance != null) snapshot = allegiance.Export();
        Scribe_Values.Look(ref snapshot, "honorPlayerAllegianceV1");
        if (Scribe.mode != LoadSaveMode.PostLoadInit || snapshot == null) return;
        try { allegiance = PlayerAllegiance.Restore(snapshot); }
        catch (Exception e) when (e is ArgumentException or System.Text.Json.JsonException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Player allegiance restore rejected; snapshot retained. " + e.GetType().Name); }
    }
    public override void FinalizeInit()
    {
        if (allegiance != null && (allegiance.NativePlayer != Faction.OfPlayer.GetUniqueLoadID() || allegiance.LastTick > Find.TickManager.TicksGame))
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Player allegiance identity/clock mismatch; sharing disabled."); }
    }
    public override void GameComponentTick()
    { if (Find.TickManager.TicksGame % 250 == 0) Synchronize(); }
    private static Faction Resolve(string id) => Find.FactionManager.AllFactionsListForReading.SingleOrDefault(f => f.GetUniqueLoadID() == id);
    public Faction RecordedAuthority => State.Authority == null ? null : Resolve(State.Authority);
    public Faction UnionCandidate => Current.Game.GetComponent<UnionPoliticsLedger>().RestoreFailed ? null : Current.Game.GetComponent<UnionPoliticsLedger>().Union;
    public Faction LiegeCandidate
    {
        get { var bond = Charters.RestoreFailed ? null : PlayerBond(); return bond == null ? null : Resolve(Charters.Book.RecordedPoliticalOwner(bond.Terms.Subject)); }
    }
    public string ReviewToken(Faction root) => root == null ? null : root.GetUniqueLoadID() + "/" +
        string.Join(";", Find.FactionManager.AllFactionsListForReading.Where(f => f != root && !f.IsPlayer && !IsAdministration(f))
            .OrderBy(f => f.GetUniqueLoadID(), StringComparer.Ordinal).Select(f => {
                var r = root.RelationWith(f, true); var p = Faction.OfPlayer.RelationWith(f, true);
                return f.GetUniqueLoadID() + ":" + r?.baseGoodwill + ":" + r?.kind + ":" + p?.baseGoodwill + ":" + p?.kind;
            }));
    private RecordedCharter PlayerBond() => Charters.Book.Charters.SingleOrDefault(c => c.Phase == RecordedCharterPhase.Active && Charters.Book.Entity(c.Terms.Subject).Player);
    private bool OldReleased() => State.Mode != PlayerAllegianceMode.Vassal || Charters.Book.Charter(State.Contract).Phase == RecordedCharterPhase.Released;
    private void Suspend()
    {
        if (RestoreFailed || State.Mode == PlayerAllegianceMode.Independent || State.Suspended) return;
        Model.Apply(new(AllegianceAction.Suspend, Find.TickManager.TicksGame));
        Messages.Message("PA_Suspended".Translate(), MessageTypeDefOf.NeutralEvent);
        Find.TickManager.Pause();
    }
    private Faction ValidateAuthority(bool allowSuspended = false, IEnumerable<TerritoryObservation> scopedObservations = null)
    {
        if (RestoreFailed || Charters.RestoreFailed || State.Mode == PlayerAllegianceMode.Independent || State.Suspended && !allowSuspended)
            throw new ArgumentException("No active shared authority.");
        var root = RecordedAuthority;
        if (root == null || root.IsPlayer || root.def.defName == RealmAdministrationLedger.DefName || root.Hidden || root.defeated || !root.HasGoodwill || root.HostileTo(Faction.OfPlayer))
            throw new ArgumentException("Recorded authority is missing or in internal conflict.");
        if (State.Mode == PlayerAllegianceMode.Vassal)
        {
            var bond = Charters.Book.Charter(State.Contract);
            Charters.Book.ValidateCurrent(bond.Id, scopedObservations ?? Charters.Observe());
            if (bond.Phase != RecordedCharterPhase.Active || !Charters.Book.Entity(bond.Terms.Subject).Player ||
                Charters.Book.RecordedPoliticalOwner(bond.Terms.Subject) != root.GetUniqueLoadID()) throw new ArgumentException("Source contract is no longer authoritative.");
        }
        else if (root != UnionCandidate) throw new ArgumentException("Union identity changed.");
        CheckProjection(root);
        return root;
    }
    internal Faction TerritoryAuthority() => State.Mode == PlayerAllegianceMode.Independent
        ? Current.Game.GetComponent<RealmAdministrationLedger>().TerritoryAuthority() : ValidateAuthority();
    // The caller verifies its single pending city/reference before supplying locally normalized observations.
    // Fresh diplomacy/caps/quest locks still apply; this does not reaffirm or alter suspended allegiance.
    internal Faction TerritoryRecoveryAuthority(IEnumerable<TerritoryObservation> scopedObservations) => State.Mode == PlayerAllegianceMode.Independent
        ? Current.Game.GetComponent<RealmAdministrationLedger>().TerritoryAuthority() : ValidateAuthority(true, scopedObservations);
    private static bool IsAdministration(Faction f) => f != null && f == Current.Game.GetComponent<RealmAdministrationLedger>().OwnedFaction;
    private static void CheckProjection(Faction root)
    {
        if (root.RelationWith(Faction.OfPlayer, true) == null || Faction.OfPlayer.RelationWith(root, true) == null)
            throw new ArgumentException("Missing internal reciprocal relation.");
        if (Find.GoodwillSituationManager.GetMaxGoodwill(root) < root.BaseGoodwillWith(Faction.OfPlayer))
            throw new ArgumentException("Internal relationship exceeds the player goodwill cap.");
        foreach (var other in Find.FactionManager.AllFactionsListForReading.Where(f => f != root && !f.IsPlayer && !IsAdministration(f)))
        {
            var relation = root.RelationWith(other, true);
            if (relation == null || Faction.OfPlayer.RelationWith(other, true) == null || other.RelationWith(Faction.OfPlayer, true) == null ||
                relation.baseGoodwill is < -100 or > 100 || !RuntimeCompat.IsDefined(relation.kind) ||
                other.HasGoodwill && Find.GoodwillSituationManager.GetMaxGoodwill(other) < relation.baseGoodwill ||
                QuestUtility.IsGoodwillLockedByQuest(Faction.OfPlayer, other) &&
                    (Faction.OfPlayer.BaseGoodwillWith(other) != relation.baseGoodwill || Faction.OfPlayer.RelationKindWith(other) != relation.kind))
                throw new ArgumentException("Missing relations or a player-specific goodwill cap conflicts with shared diplomacy.");
        }
    }
    private static void CheckAdmission(Faction root, UnionWarConsent wars = null)
    {
        CheckProjection(root);
        var conflicts = AdmissionConflicts(root);
        if (wars != null) UnionWarCouncil.ValidateAccepted(root, wars);
        if (conflicts.Any(f => wars == null || !wars.Targets.Any(t => t.Enemy == f.GetUniqueLoadID())))
            throw new ArgumentException("UA_WarBlocked".Translate(string.Join(", ", conflicts.Select(f => f.Name))));
        if (Find.GoodwillSituationManager.GetMaxGoodwill(root) < 100 ||
            QuestUtility.IsGoodwillLockedByQuest(Faction.OfPlayer, root) &&
                (Faction.OfPlayer.BaseGoodwillWith(root) != 100 || Faction.OfPlayer.RelationKindWith(root) != FactionRelationKind.Ally))
            throw new ArgumentException("Admission must not override an internal quest lock or player goodwill cap.");
    }
    internal static IReadOnlyList<Faction> AdmissionConflicts(Faction root)
    {
        if (root == null) throw new ArgumentException("No admission destination.");
        var others = Find.FactionManager.AllFactionsListForReading.Where(f => f != root && !f.IsPlayer && !IsAdministration(f)).ToArray();
        SharedRelation Observe(Faction owner, Faction other)
        {
            var r = owner.RelationWith(other, true) ?? throw new ArgumentException("Missing admission relation.");
            return new(other.GetUniqueLoadID(), r.baseGoodwill, r.kind switch {
                FactionRelationKind.Hostile => SharedRelationKind.Hostile, FactionRelationKind.Ally => SharedRelationKind.Ally,
                FactionRelationKind.Neutral => SharedRelationKind.Neutral, _ => throw new ArgumentException("Unknown native relation kind.") });
        }
        var ids = SharedDiplomacyPlan.AdmissionConflicts(Faction.OfPlayer.GetUniqueLoadID(), root.GetUniqueLoadID(),
            others.Select(f => Observe(Faction.OfPlayer, f)), others.Select(f => Observe(root, f)));
        return Array.AsReadOnly(ids.Select(id => others.Single(f => f.GetUniqueLoadID() == id)).ToArray());
    }
    internal static void Pair(Faction player, Faction other, int goodwill, FactionRelationKind kind)
    {
        var left = player.RelationWith(other, true); var right = other.RelationWith(player, true);
        if (left == null || right == null) throw new ArgumentException("Missing native reciprocal relation.");
        var previousLeft = left.kind; var previousRight = right.kind;
        left.baseGoodwill = right.baseGoodwill = goodwill; left.kind = right.kind = kind;
        if (previousLeft != kind) player.Notify_RelationKindChanged(other, previousLeft, false, "PA_Reason".Translate(), GlobalTargetInfo.Invalid, out _);
        if (previousRight != kind) other.Notify_RelationKindChanged(player, previousRight, false, "PA_Reason".Translate(), GlobalTargetInfo.Invalid, out _);
    }
    private void Execute(Action action)
    {
        if (RestoreFailed || Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges) { Rejected(); return; }
        try { action(); Synchronize(); }
        catch (ArgumentException) { Rejected(); }
    }
    internal AllegianceCommand PlanCommand(bool joinUnion, UnionWarConsent wars = null)
    {
        if (RestoreFailed || Charters.RestoreFailed) throw new ArgumentException("Charter/allegiance ledger unavailable.");
        if (State.Mode == PlayerAllegianceMode.Union) ValidateAuthority();
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (payments.RestoreFailed || payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared))
            throw new ArgumentException("Resolve all reserved physical transfers before changing allegiance.");
        if (!OldReleased()) throw new ArgumentException("Prior feudal tie must be mutually released.");
        Faction root; AllegianceCommand command;
        if (joinUnion)
        {
            root = UnionCandidate;
            if (PlayerBond() != null) throw new ArgumentException("An active player-subject contract blocks union entry.");
            command = new(AllegianceAction.JoinUnion, Find.TickManager.TicksGame, root?.GetUniqueLoadID(),
                Consent: true, NoActiveFeudalContract: true, OldContractReleased: true);
        }
        else
        {
            var bond = PlayerBond() ?? throw new ArgumentException("No mutually signed player bond.");
            root = Resolve(Charters.Book.RecordedPoliticalOwner(bond.Terms.Subject));
            var releasedSource = Current.Game.GetComponent<PoliticalTransitionLedger>().ReleasedMigrationSource(root);
            Charters.Book.ValidateCurrent(bond.Id, Charters.ObserveForMigration(releasedSource));
            command = new(AllegianceAction.BindLiege, Find.TickManager.TicksGame, root?.GetUniqueLoadID(), bond.Id,
                Consent: true, OldContractReleased: true, UnionDuesSettled: true);
        }
        if (wars != null && !UnionWarAdmission.MatchesAction(wars, command.Action)) throw new ArgumentException("War consent belongs to a different admission authority.");
        CheckTransitionAdmission(root, wars);
        AllegianceChangeSource.Validate(Charters.Book, State, command, payments.Ledger);
        var clone = PlayerAllegiance.Restore(Model.Export()); clone.Apply(command);
        return command;
    }
    internal void CheckTransitionAdmission(Faction root, UnionWarConsent wars = null)
    {
        if (root == null || root.IsPlayer || root.def.defName == RealmAdministrationLedger.DefName || root.Hidden || root.defeated || !root.HasGoodwill ||
            root.HostileTo(Faction.OfPlayer) || root.leader == null || root.leader.Dead || root.leader.Faction != root)
            throw new ArgumentException("No current authority admission.");
        if (wars != null && ((root.def.defName == UnionPoliticsLedger.UnionDefName) != (wars.AuthorityKind == AdmissionAuthorityKind.UnionCouncil)))
            throw new ArgumentException("Current native realm does not match the recorded admission authority.");
        CheckAdmission(root, wars);
    }
    internal Faction TransitionAuthority() => ValidateAuthority(true);
    internal void ApplyTransitionDiplomacy(Faction root)
    {
        if (State.Authority != root?.GetUniqueLoadID()) throw new ArgumentException("Recorded destination changed.");
        CheckTransitionAdmission(root);
        synchronizing = true;
        try
        {
            Pair(Faction.OfPlayer, root, 100, FactionRelationKind.Ally);
            if (State.Suspended) Model.Apply(new(AllegianceAction.Reaffirm, Find.TickManager.TicksGame, root.GetUniqueLoadID(), Consent: true));
        }
        finally { synchronizing = false; }
        Synchronize();
        if (State.Suspended) throw new ArgumentException("Shared diplomacy could not be applied.");
    }
    public void Notice() => Execute(() => Model.Apply(new(AllegianceAction.UnionExitNotice, Find.TickManager.TicksGame, NoticeTicks: PoliticalWorld.Day * 3)));
    public void Reaffirm() => Execute(() => {
        var root = ValidateAuthority(true);
        if (root.leader == null || root.leader.Dead || root.GoodwillWith(Faction.OfPlayer) < 0) throw new ArgumentException("No current admission consent.");
        Model.Apply(new(AllegianceAction.Reaffirm, Find.TickManager.TicksGame, root.GetUniqueLoadID(), Consent: true));
    });
    public void Synchronize()
    {
        if (Current.ProgramState != ProgramState.Playing || synchronizing || routing || RestoreFailed || State.Mode == PlayerAllegianceMode.Independent || State.Suspended) return;
        var transition = Current.Game.GetComponent<PoliticalTransitionLedger>();
        var towns = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        if (towns.RestoreFailed || towns.HasPendingTownLosses) return;
        if (transition.Journal.Pending && transition.Journal.Current.Wars != null &&
            (!transition.Executing || !transition.Journal.WarsReady || UnionWarCouncil.HasExternalProvider)) return;
        try
        {
            var root = ValidateAuthority(); var player = Faction.OfPlayer;
            bool pendingAdmission = Current.Game.GetComponent<PoliticalTransitionLedger>().Journal.Pending;
            if (pendingAdmission) CheckAdmission(root);
            var sources = Find.FactionManager.AllFactionsListForReading.Where(f => f != root && !f.IsPlayer && !IsAdministration(f)).ToArray();
            var plan = SharedDiplomacyPlan.Build(player.GetUniqueLoadID(), root.GetUniqueLoadID(), sources.Select(f => {
                var relation = root.RelationWith(f, true);
                return new SharedRelation(f.GetUniqueLoadID(), relation.baseGoodwill, relation.kind switch {
                    FactionRelationKind.Ally => SharedRelationKind.Ally, FactionRelationKind.Hostile => SharedRelationKind.Hostile, _ => SharedRelationKind.Neutral });
            }));
            synchronizing = true; var changed = new List<string>();
            foreach (var relation in plan)
            {
                if (State.Suspended) break;
                var other = Resolve(relation.Other); var kind = relation.Kind switch {
                    SharedRelationKind.Ally => FactionRelationKind.Ally, SharedRelationKind.Hostile => FactionRelationKind.Hostile, _ => FactionRelationKind.Neutral };
                if (pendingAdmission && player.RelationKindWith(other) == FactionRelationKind.Hostile && kind != FactionRelationKind.Hostile)
                    throw new ArgumentException("An admission observer introduced an unreviewed existing war; preserve it and suspend.");
                if (player.RelationKindWith(other) != kind) changed.Add(other.Name);
                Pair(player, other, relation.BaseGoodwill, kind);
            }
            if (changed.Count > 0)
            { Messages.Message("PA_RelationsChanged".Translate(string.Join(", ", changed)), MessageTypeDefOf.NeutralEvent); Find.TickManager.Pause(); }
        }
        catch (ArgumentException) { Suspend(); }
        finally { synchronizing = false; }
    }
    public bool RouteGoodwill(Faction actor, Faction other, int delta, bool sendMessage, bool sendLetter, HistoryEventDef reason,
        GlobalTargetInfo? target, out bool result)
    {
        result = false;
        if (Current.ProgramState != ProgramState.Playing || routing || RestoreFailed || State.Mode == PlayerAllegianceMode.Independent || State.Suspended ||
            actor != Faction.OfPlayer && other != Faction.OfPlayer) return false;
        var transition = Current.Game.GetComponent<PoliticalTransitionLedger>();
        var towns = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        if (towns.RestoreFailed || towns.HasPendingTownLosses) return false;
        if (transition.Journal.Pending && transition.Journal.Current.Wars != null &&
            (!transition.Executing || !transition.Journal.WarsReady || UnionWarCouncil.HasExternalProvider)) return false;
        if (IsAdministration(actor) || IsAdministration(other)) return false; // Native player/own-administration events are internal, never foreign-root events.
        Faction root;
        try { root = ValidateAuthority(); } catch (ArgumentException) { Suspend(); return false; }
        if (actor == root || other == root) return false; // Internal conflict remains vanilla; it may suspend shared diplomacy.
        if (!actor.CanChangeGoodwillFor(other, delta)) return true; // Preserve original player-pair quest and permission locks.
        var external = actor == Faction.OfPlayer ? other : actor;
        int before = root.BaseGoodwillWith(external);
        int adjusted = actor.CalculateAdjustedGoodwillChange(other, delta);
        routing = true;
        try { result = actor == Faction.OfPlayer ? root.TryAffectGoodwillWith(other, adjusted, false, sendLetter, reason, target)
            : actor.TryAffectGoodwillWith(root, adjusted, false, sendLetter, reason, target); }
        finally { routing = false; }
        int after = root.BaseGoodwillWith(external);
        if (result && before != after)
        {
            if (reason != null) Find.HistoryEventsManager.RecordEvent(new HistoryEvent(reason, external.Named(HistoryEventArgsNames.AffectedFaction),
                (after - before).Named(HistoryEventArgsNames.CustomGoodwill)));
            if (sendMessage) Messages.Message("PA_Goodwill".Translate(external.Name, before, after), MessageTypeDefOf.NeutralEvent);
        }
        return true;
    }
    private static void Rejected() => Messages.Message("PA_Rejected".Translate(), MessageTypeDefOf.RejectInput);
}

[HarmonyPatch(typeof(Faction), nameof(Faction.TryAffectGoodwillWith))]
internal static class SharedPlayerGoodwill
{
    static bool Prefix(Faction __instance, Faction other, int goodwillChange, bool canSendMessage, bool canSendHostilityLetter,
        HistoryEventDef reason, GlobalTargetInfo? lookTarget, ref bool __result)
    {
        var ledger = Current.Game?.GetComponent<PlayerPoliticsLedger>();
        if (ledger == null || !ledger.RouteGoodwill(__instance, other, goodwillChange, canSendMessage, canSendHostilityLetter, reason, lookTarget, out var result)) return true;
        __result = result; return false;
    }
    static void Postfix() => Current.Game?.GetComponent<PlayerPoliticsLedger>()?.Synchronize();
}
[HarmonyPatch(typeof(Faction), nameof(Faction.SetRelationDirect))]
internal static class SharedPlayerDirectRelations
{ static void Postfix() => Current.Game?.GetComponent<PlayerPoliticsLedger>()?.Synchronize(); }
[HarmonyPatch(typeof(Faction), nameof(Faction.Notify_GoodwillSituationsChanged))]
internal static class SharedPlayerGoodwillSituations
{ static void Postfix() => Current.Game?.GetComponent<PlayerPoliticsLedger>()?.Synchronize(); }

public sealed class PlayerAllegianceWindow : Window
{
    private bool proposeWars;
    private PoliticalChangeReview review;
    private Vector2 scroll;
    private float height = 1000;
    public override Vector2 InitialSize => new(850, 760);
    public PlayerAllegianceWindow() { forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    private void Review(Func<PoliticalChangeReview> action)
    {
        try { review = action(); }
        catch (UnionWarRejected rejected) { review = null; Messages.Message(("WU_Decision_" + rejected.Reason).Translate(), MessageTypeDefOf.RejectInput); }
        catch (UnionWarProviderUnavailable) { review = null; Messages.Message("WU_Provider".Translate(), MessageTypeDefOf.RejectInput); }
        catch (FeudalWarAuthorityUnavailable) { review = null; Messages.Message("WF_Authority".Translate(), MessageTypeDefOf.RejectInput); }
        catch (ArgumentException) { review = null; Messages.Message("PS_Rejected".Translate(), MessageTypeDefOf.RejectInput); }
    }
    public override void DoWindowContents(Rect rect)
    {
        var ledger = Current.Game.GetComponent<PlayerPoliticsLedger>();
        var transition = Current.Game.GetComponent<PoliticalTransitionLedger>();
        var territories = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        var administration = Current.Game.GetComponent<RealmAdministrationLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, Mathf.Max(12000, height + 1000)));
        list.Label("PA_Warning".Translate()); list.Label("PS_Warning".Translate()); list.Label("WM_Warning".Translate());
        list.Label("AD_Warning".Translate());
        if (Current.Game.GetComponent<LiveCharterLedger>().UnresolvedTownLosses) list.Label("LS_Freeze".Translate());
        if (list.ButtonText("CA_OpenAll".Translate())) territories.OpenPreservationAudit();
        if (list.ButtonText("NS_Open".Translate())) Find.WindowStack.Add(new NpcSubchartersWindow());
        if (administration.RestoreFailed) list.Label("HCP_RestoreFailed".Translate());
        else if (!transition.BlocksChanges && administration.OwnedFaction == null && ledger.State.Mode == PlayerAllegianceMode.Independent)
        { if (list.ButtonText("AD_Create".Translate())) administration.CreateOrResume(); }
        else if (administration.OwnedFaction != null)
        {
            list.Label("AD_Status".Translate(administration.OwnedFaction.Name));
            if (administration.Suspended)
            { list.Label("AD_Suspended".Translate()); if (!transition.BlocksChanges && list.ButtonText("AD_Resume".Translate())) administration.CreateOrResume(); }
        }
        if (!transition.BlocksChanges && list.ButtonText("FB_Open".Translate())) Find.WindowStack.Add(new FollowerTransfersWindow());
        if (ledger.RestoreFailed || transition.RestoreFailed || territories.RestoreFailed || Current.Game.GetComponent<LiveCharterLedger>().RestoreFailed)
            list.Label("HCP_RestoreFailed".Translate());
        else
        {
            list.Label("PA_State".Translate(("PA_" + ledger.State.Mode).Translate(), ledger.RecordedAuthority?.Name ?? "PA_None".Translate()));
            if (!transition.BlocksChanges && ledger.UnionCandidate == null && ledger.State.Mode != PlayerAllegianceMode.Union)
            {
                list.Label("UA_PrepareInfo".Translate());
                if (list.ButtonText("UA_Prepare".Translate())) { Current.Game.GetComponent<UnionPoliticsLedger>().PrepareForPlayer(); review = null; }
            }
            if (transition.Journal.Pending)
            {
                if (transition.Journal.Current.Wars != null)
                    list.Label("WU_Progress".Translate(transition.Journal.Current.WarResults.Count(r => r.Step == UnionWarStep.Verified), transition.Journal.Current.WarResults.Length));
                list.Label("PS_Pending".Translate());
                list.Label(("PS_" + transition.Journal.Current.Phase).Translate());
                if (transition.Journal.LostCharters.Any()) list.Label("PS_LossProgress".Translate(transition.Journal.LostCharters.Count(), transition.Journal.Current.Followers.Count));
                territories.DrawPendingTownLosses(list);
                foreach (var pending in territories.Ledger.Transfers.Where(t => t.Phase == TerritoryTransferPhase.Prepared).ToArray())
                {
                    list.Label("FB_Pending".Translate(territories.FollowerLabel(pending.Charter)));
                    if (list.ButtonText("CT_Recover".Translate())) { territories.Recover(pending.Id); review = null; }
                }
                if (transition.Journal.Current.Phase == AllegianceTransitionPhase.Prepared && transition.Journal.IsBefore(ledger.Model))
                { if (list.ButtonText("PS_Cancel".Translate())) { transition.CancelUnchanged(); review = null; } }
                else if (list.ButtonText("PS_ReviewResume".Translate())) Review(transition.ReviewRemaining);
            }
            else
            {
                if (ledger.State.Mode != PlayerAllegianceMode.Union || ledger.LiegeCandidate != null)
                {
                    bool previousWars = proposeWars;
                    list.CheckboxLabeled("WU_Propose".Translate(), ref proposeWars);
                    list.Label("WU_Policy".Translate());
                    list.Label("WF_Policy".Translate());
                    if (previousWars != proposeWars) review = null;
                }
                if (ledger.State.Suspended)
                {
                    list.Label("PA_Suspended".Translate());
                    if (list.ButtonText("PA_Reaffirm".Translate())) ledger.Reaffirm();
                }
                if (ledger.UnionCandidate != null && ledger.State.Mode != PlayerAllegianceMode.Union &&
                    list.ButtonText((ledger.State.Mode == PlayerAllegianceMode.Vassal ? "PS_ReturnUnion" : "PA_ReviewUnion").Translate()))
                    Review(() => transition.Review(true, proposeWars));
                if (ledger.UnionCandidate != null && ledger.State.Mode != PlayerAllegianceMode.Union)
                {
                    try
                    {
                        var conflicts = PlayerPoliticsLedger.AdmissionConflicts(ledger.UnionCandidate);
                        if (conflicts.Count > 0 && review?.Wars == null) list.Label("UA_WarBlocked".Translate(string.Join(", ", conflicts.Select(f => f.Name))));
                    }
                    catch (ArgumentException) { list.Label("PA_Rejected".Translate()); }
                }
                if (ledger.State.Mode == PlayerAllegianceMode.Union && !ledger.State.UnionNoticeUntil.HasValue && list.ButtonText("PA_Notice".Translate())) ledger.Notice();
                if (ledger.State.UnionNoticeUntil.HasValue) list.Label("PA_NoticeUntil".Translate(
                    ((ledger.State.UnionNoticeUntil.Value - Find.TickManager.TicksGame) / (double)PoliticalWorld.Day).ToString("F1")));
                if (ledger.LiegeCandidate != null && list.ButtonText("PA_ReviewLiege".Translate())) Review(() => transition.Review(false, proposeWars));
            }
            if (review != null)
            {
                if (review.Wars != null)
                {
                    if (review.Wars.AuthorityKind == AdmissionAuthorityKind.FeudalLeader)
                        list.Label("WF_Accepted".Translate(review.Destination.Name, review.Destination.leader?.LabelShortCap ?? "HCL_Missing".Translate()));
                    else list.Label("WU_Votes".Translate(review.Wars.ApprovedWeight, review.Wars.TotalWeight));
                    foreach (var enemy in review.Wars.Targets) list.Label("WU_Enemy".Translate(UnionWarCouncil.Name(enemy.Enemy)));
                    if (review.Wars.AuthorityKind == AdmissionAuthorityKind.UnionCouncil)
                        list.Label((review.Wars.Founding ? "WU_Founding" : "WU_Council").Translate());
                }
                list.Label("PA_Review".Translate(review.Destination.Name));
                foreach (var faction in Find.FactionManager.AllFactionsListForReading.Where(f => !f.IsPlayer && !f.Hidden && f != review.Destination && f != administration.OwnedFaction))
                {
                    var relation = review.Destination.RelationWith(faction, true);
                    if (review.Wars?.Targets.Any(t => t.Enemy == faction.GetUniqueLoadID()) == true) list.Label("WU_PlannedHostility".Translate(faction.Name));
                    else if (relation != null) list.Label("PA_Relation".Translate(faction.Name, relation.baseGoodwill, ("PA_Kind" + relation.kind).Translate()));
                }
                list.Label("FB_Plan".Translate(review.Destination.Name, review.Followers.Moves.Count, review.Followers.Entries.Count));
                list.Label("RE_Limits".Translate());
                foreach (var entry in review.Followers.Entries)
                    list.Label("FB_Entry".Translate(territories.FollowerLabel(entry.Charter), territories.OwnerLabel(entry.Observation.Owner),
                        (review.Followers.Moves.Contains(entry.Charter) ? "FB_Move" : "FB_Already").Translate()));
                list.Label("PS_DebtsRetained".Translate());
                if (list.ButtonText((review.Command == null ? "PS_Resume" : "PS_Confirm").Translate()))
                { if (review.Command == null) transition.Resume(review); else transition.Start(review); review = null; }
            }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
