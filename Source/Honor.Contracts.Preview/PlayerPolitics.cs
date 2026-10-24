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
    private PlayerAllegiance Model => allegiance ??= new(Faction.OfPlayer.GetUniqueLoadID());
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
                var r = root.RelationWith(f, true); return f.GetUniqueLoadID() + ":" + r?.baseGoodwill + ":" + r?.kind;
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
    private Faction ValidateAuthority(bool allowSuspended = false)
    {
        if (RestoreFailed || Charters.RestoreFailed || State.Mode == PlayerAllegianceMode.Independent || State.Suspended && !allowSuspended)
            throw new ArgumentException("No active shared authority.");
        var root = RecordedAuthority;
        if (root == null || root.IsPlayer || root.def.defName == RealmAdministrationLedger.DefName || root.Hidden || root.defeated || !root.HasGoodwill || root.HostileTo(Faction.OfPlayer))
            throw new ArgumentException("Recorded authority is missing or in internal conflict.");
        if (State.Mode == PlayerAllegianceMode.Vassal)
        {
            var bond = Charters.Book.Charter(State.Contract);
            Charters.Book.ValidateCurrent(bond.Id, Charters.Observe());
            if (bond.Phase != RecordedCharterPhase.Active || !Charters.Book.Entity(bond.Terms.Subject).Player ||
                Charters.Book.RecordedPoliticalOwner(bond.Terms.Subject) != root.GetUniqueLoadID()) throw new ArgumentException("Source contract is no longer authoritative.");
        }
        else if (root != UnionCandidate) throw new ArgumentException("Union identity changed.");
        CheckProjection(root);
        return root;
    }
    internal Faction TerritoryAuthority() => State.Mode == PlayerAllegianceMode.Independent
        ? Current.Game.GetComponent<RealmAdministrationLedger>().TerritoryAuthority() : ValidateAuthority();
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
                relation.baseGoodwill is < -100 or > 100 || !Enum.IsDefined(relation.kind) ||
                other.HasGoodwill && Find.GoodwillSituationManager.GetMaxGoodwill(other) < relation.baseGoodwill ||
                QuestUtility.IsGoodwillLockedByQuest(Faction.OfPlayer, other) &&
                    (Faction.OfPlayer.BaseGoodwillWith(other) != relation.baseGoodwill || Faction.OfPlayer.RelationKindWith(other) != relation.kind))
                throw new ArgumentException("Missing relations or a player-specific goodwill cap conflicts with shared diplomacy.");
        }
    }
    private static void CheckAdmission(Faction root)
    {
        CheckProjection(root);
        if (Find.GoodwillSituationManager.GetMaxGoodwill(root) < 100 ||
            QuestUtility.IsGoodwillLockedByQuest(Faction.OfPlayer, root) &&
                (Faction.OfPlayer.BaseGoodwillWith(root) != 100 || Faction.OfPlayer.RelationKindWith(root) != FactionRelationKind.Ally))
            throw new ArgumentException("Admission must not override an internal quest lock or player goodwill cap.");
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
        if (RestoreFailed) { Rejected(); return; }
        try { action(); Synchronize(); }
        catch (ArgumentException) { Rejected(); }
    }
    public void JoinUnion(string reviewedToken) => Execute(() => {
        if (Charters.RestoreFailed) throw new ArgumentException("Charter ledger unavailable.");
        var root = UnionCandidate;
        if (root == null || root.Hidden || !root.HasGoodwill || root.defeated || root.IsPlayer || root.HostileTo(Faction.OfPlayer) ||
            root.leader == null || root.leader.Dead || reviewedToken != ReviewToken(root) ||
            PlayerBond() != null || !OldReleased()) throw new ArgumentException("Union admission is unavailable.");
        CheckAdmission(root); Charters.EnsurePlayerIdentity();
        Model.Apply(new(AllegianceAction.JoinUnion, Find.TickManager.TicksGame, root.GetUniqueLoadID(),
            Consent: true, NoActiveFeudalContract: true, OldContractReleased: true));
        synchronizing = true;
        try { Pair(Faction.OfPlayer, root, 100, FactionRelationKind.Ally); }
        finally { synchronizing = false; }
    });
    public void Notice() => Execute(() => Model.Apply(new(AllegianceAction.UnionExitNotice, Find.TickManager.TicksGame, NoticeTicks: PoliticalWorld.Day * 3)));
    public void ApplyLiege(string reviewedToken) => Execute(() => {
        if (Charters.RestoreFailed) throw new ArgumentException("Charter ledger unavailable.");
        var bond = PlayerBond() ?? throw new ArgumentException("No active mutually signed player bond.");
        Charters.Book.ValidateCurrent(bond.Id, Charters.Observe());
        var root = Resolve(Charters.Book.RecordedPoliticalOwner(bond.Terms.Subject));
        if (root == null || root.IsPlayer || root.def.defName == RealmAdministrationLedger.DefName || root.Hidden || !root.HasGoodwill || root.defeated || reviewedToken != ReviewToken(root) || root.HostileTo(Faction.OfPlayer) || !OldReleased() ||
            Current.Game.GetComponent<TributePaymentLedger>().RestoreFailed) throw new ArgumentException("Invalid superior.");
        // No union levy system is connected yet. Existing physical transfer reservations must still be resolved.
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        bool settled = payments.Ledger.Receipts.All(r => r.Phase != TributeReceiptPhase.Prepared);
        CheckAdmission(root);
        Model.Apply(new(AllegianceAction.BindLiege, Find.TickManager.TicksGame, root.GetUniqueLoadID(), bond.Id,
            Consent: true, OldContractReleased: true, UnionDuesSettled: settled));
        synchronizing = true;
        try { Pair(Faction.OfPlayer, root, 100, FactionRelationKind.Ally); }
        finally { synchronizing = false; }
    });
    public void Reaffirm() => Execute(() => {
        var root = ValidateAuthority(true);
        if (root.leader == null || root.leader.Dead || root.GoodwillWith(Faction.OfPlayer) < 0) throw new ArgumentException("No current admission consent.");
        Model.Apply(new(AllegianceAction.Reaffirm, Find.TickManager.TicksGame, root.GetUniqueLoadID(), Consent: true));
    });
    public void Synchronize()
    {
        if (Current.ProgramState != ProgramState.Playing || synchronizing || routing || RestoreFailed || State.Mode == PlayerAllegianceMode.Independent || State.Suspended) return;
        try
        {
            var root = ValidateAuthority(); var player = Faction.OfPlayer;
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
    private Faction reviewed;
    private string reviewedToken;
    private bool reviewingUnion;
    private Vector2 scroll;
    private float height = 1000;
    public override Vector2 InitialSize => new(850, 760);
    public PlayerAllegianceWindow() { forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override void DoWindowContents(Rect rect)
    {
        var ledger = Current.Game.GetComponent<PlayerPoliticsLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, 12000));
        list.Label("PA_Warning".Translate());
        var administration = Current.Game.GetComponent<RealmAdministrationLedger>();
        list.Label("AD_Warning".Translate());
        if (administration.RestoreFailed) list.Label("HCP_RestoreFailed".Translate());
        else if (administration.OwnedFaction == null && ledger.State.Mode == PlayerAllegianceMode.Independent)
        { if (list.ButtonText("AD_Create".Translate())) administration.CreateOrResume(); }
        else if (administration.OwnedFaction != null)
        {
            list.Label("AD_Status".Translate(administration.OwnedFaction.Name));
            if (administration.Suspended)
            { list.Label("AD_Suspended".Translate()); if (list.ButtonText("AD_Resume".Translate())) administration.CreateOrResume(); }
        }
        if (list.ButtonText("FB_Open".Translate())) Find.WindowStack.Add(new FollowerTransfersWindow());
        if (ledger.RestoreFailed) list.Label("HCP_RestoreFailed".Translate());
        else
        {
            list.Label("PA_State".Translate(("PA_" + ledger.State.Mode).Translate(), ledger.RecordedAuthority?.Name ?? "PA_None".Translate()));
            if (ledger.State.Suspended)
            {
                list.Label("PA_Suspended".Translate());
                if (list.ButtonText("PA_Reaffirm".Translate())) ledger.Reaffirm();
            }
            if (ledger.UnionCandidate != null && list.ButtonText("PA_ReviewUnion".Translate()))
            { reviewed = ledger.UnionCandidate; reviewedToken = ledger.ReviewToken(reviewed); reviewingUnion = true; }
            if (ledger.State.Mode == PlayerAllegianceMode.Union && !ledger.State.UnionNoticeUntil.HasValue && list.ButtonText("PA_Notice".Translate())) ledger.Notice();
            if (ledger.State.UnionNoticeUntil.HasValue) list.Label("PA_NoticeUntil".Translate(
                ((ledger.State.UnionNoticeUntil.Value - Find.TickManager.TicksGame) / (double)PoliticalWorld.Day).ToString("F1")));
            if (ledger.LiegeCandidate != null && list.ButtonText("PA_ReviewLiege".Translate()))
            { reviewed = ledger.LiegeCandidate; reviewedToken = ledger.ReviewToken(reviewed); reviewingUnion = false; }
            if (reviewed != null)
            {
                list.Label("PA_Review".Translate(reviewed.Name));
                foreach (var faction in Find.FactionManager.AllFactionsListForReading.Where(f => !f.IsPlayer && !f.Hidden && f != reviewed && f != administration.OwnedFaction))
                {
                    var relation = reviewed.RelationWith(faction, true);
                    if (relation != null) list.Label("PA_Relation".Translate(faction.Name, relation.baseGoodwill,
                        ("PA_Kind" + relation.kind).Translate()));
                }
                if (list.ButtonText((reviewingUnion ? "PA_Join" : "PA_ApplyLiege").Translate()))
                { if (reviewingUnion) ledger.JoinUnion(reviewedToken); else ledger.ApplyLiege(reviewedToken); reviewed = null; }
            }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
