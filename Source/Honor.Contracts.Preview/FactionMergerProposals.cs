using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed record LiveMergerProposalReview(Game Context, string Charter, FactionMergerScope Scope);

/// <summary>Persistent separate whole-faction political proposal and signatures. No native merger executor exists here.</summary>
public sealed class FactionMergerProposalLedger : GameComponent
{
    private string snapshot;
    public FactionMergerProposals Ledger { get; private set; } = new();
    public bool RestoreFailed { get; private set; }
    public FactionMergerProposalLedger(Game game) { }
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) snapshot = Ledger.Export();
        Scribe_Values.Look(ref snapshot, "honorFactionMergerProposalsV1");
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        try { if (snapshot != null) Ledger = FactionMergerProposals.Restore(snapshot); }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Merger proposal archive unavailable; raw data retained. " + e.GetType().Name); }
    }
    public override void FinalizeInit()
    {
        if (RestoreFailed) return;
        if (Ledger.LastTick > Find.TickManager.TicksGame || Ledger.Proposals.Any(p => p.Scope.Player != Faction.OfPlayer.GetUniqueLoadID()))
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Merger proposal identity/clock mismatch; proposals blocked."); }
    }
    private static string Label(string text, string fallback) => string.IsNullOrWhiteSpace(text) ? fallback : text.Length > 200 ? text.Substring(0, 200) : text;
    public LiveMergerProposalReview Review(string charter)
    {
        var game = Current.Game; var charters = game.GetComponent<LiveCharterLedger>(); var towns = game.GetComponent<TerritoryAdministrationLedger>();
        var politics = game.GetComponent<PlayerPoliticsLedger>(); var payments = game.GetComponent<TributePaymentLedger>();
        if (RestoreFailed || charters.RestoreFailed || towns.RestoreFailed || politics.RestoreFailed || payments.RestoreFailed ||
            game.GetComponent<PoliticalTransitionLedger>().BlocksChanges || game.GetComponent<UnionPoliticsLedger>().RestoreFailed ||
            politics.State.Mode != PlayerAllegianceMode.Independent || politics.State.Suspended || UnionWarCouncil.HasExternalProvider ||
            payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared)) throw new ArgumentException("Resolve political/physical operations and provider authority first.");
        towns.ValidatePoliticalLosses(game.GetComponent<PoliticalTransitionLedger>().Journal);
        var c = charters.Book.Charter(charter); var binding = charters.Binding(c.Terms.Subject); var city = binding.Settlement;
        var actualCities = Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => !s.Destroyed).ToArray();
        if (c.Phase != RecordedCharterPhase.Active || !charters.Book.PlayerManaged(c) || binding.PlayerGroup || binding.FactionGroup || city == null || !actualCities.Contains(city))
            throw new ArgumentException("An actual active managed source town is required.");
        var source = city.Faction; var target = game.GetComponent<RealmAdministrationLedger>().TerritoryAuthority();
        var factions = Find.FactionManager.AllFactionsListForReading;
        if (source == null || source == target || source.IsPlayer || source.Hidden || source.temporary || source.defeated || !source.HasGoodwill ||
            source.def.permanentEnemy || !source.def.humanlikeFaction || source.def.royalTitleTags?.Count > 0 ||
            source.def.defName == UnionPoliticsLedger.UnionDefName || source.def.defName == RealmAdministrationLedger.DefName || !factions.Contains(source) ||
            source.HostileTo(Faction.OfPlayer) || Faction.OfPlayer.HostileTo(source) || source.leader == null || source.leader.Dead || source.leader.Destroyed || source.leader.Downed ||
            !source.leader.Awake() || source.leader.InMentalState || source.leader.IsPrisoner || source.leader.IsSlave || source.leader.Faction != source ||
            source.leader.HostFaction != null || source.leader.HasExtraHomeFaction() || source.leader.HasExtraMiniFaction() || source.leader.questTags?.Count > 0 ||
            !PawnUtility.IsFactionLeader(source.leader) || factions.Any(f => f != source && f.leader == source.leader))
            throw new ArgumentException("No supported current ordinary faction decision authority.");
        var sourceCities = actualCities.Where(s => s.Faction == source).OrderBy(s => s.GetUniqueLoadID(), StringComparer.Ordinal).ToArray();
        if (sourceCities.Length is < 1 or > 128) throw new ArgumentException("Unsupported complete source-town scope.");
        var scopeTowns = sourceCities.Select(s => {
            string id = "territory/" + s.GetUniqueLoadID();
            var bond = charters.Book.Charters.SingleOrDefault(b => b.Phase == RecordedCharterPhase.Active && b.Terms.Subject == id);
            if (bond == null || !charters.Book.PlayerManaged(bond) || charters.Binding(id).Settlement != s || towns.IndependentlyLostTerritory(id))
                throw new ArgumentException("Every actual source town needs its own existing managed charter; no automatic registration or consent.");
            return new MergerTown(id, bond.Id);
        }).ToArray();
        var people = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Where(p => p != null &&
            (p.Faction == source || p.HostFaction == source || p.SlaveFaction == source || p.GetExtraHomeFaction() == source || p.GetExtraMiniFaction() == source)).Distinct().ToArray();
        if (people.Any(p => p.Destroyed) || people.Select(p => p.GetUniqueLoadID()).Distinct(StringComparer.Ordinal).Count() != people.Length)
            throw new ArgumentException("Unsupported native population identity scope.");
        var other = factions.Where(f => f != source && f != target && f != Faction.OfPlayer && f.HasGoodwill && !f.Hidden && !f.temporary).OrderBy(f => f.GetUniqueLoadID(), StringComparer.Ordinal).ToArray();
        object Relation(Faction a, Faction b)
        {
            var r = a.RelationWith(b, true) ?? throw new ArgumentException("Missing current native diplomacy.");
            if (!RuntimeCompat.IsDefined(r.kind) || r.baseGoodwill is < -100 or > 100) throw new ArgumentException("Unknown current native diplomacy.");
            return new { Owner = a.GetUniqueLoadID(), Other = b.GetUniqueLoadID(), r.baseGoodwill, Kind = (int)r.kind };
        }
        var evidence = new { Internal = new[] { Relation(source, target), Relation(target, source), Relation(source, Faction.OfPlayer), Relation(Faction.OfPlayer, source) },
            External = other.Select(f => new[] { Relation(source, f), Relation(f, source), Relation(target, f), Relation(f, target) }).ToArray() };
        string diplomacy = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evidence))));
        var scope = new FactionMergerScope(source.GetUniqueLoadID(), target.GetUniqueLoadID(), Faction.OfPlayer.GetUniqueLoadID(), source.leader.GetUniqueLoadID(),
            Label(source.Name, source.GetUniqueLoadID()), Label(target.Name, target.GetUniqueLoadID()), Label(source.leader.LabelShort, source.leader.GetUniqueLoadID()),
            charters.Book.Export(), towns.Ledger.Export(), scopeTowns,
            Find.WorldObjects.AllWorldObjects.Where(o => !o.Destroyed && o.Faction == source).Select(o => o.GetUniqueLoadID()).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            people.Select(p => p.GetUniqueLoadID()).OrderBy(id => id, StringComparer.Ordinal).ToArray(), diplomacy,
            source.GoodwillWith(Faction.OfPlayer), source.RelationKindWith(target) == FactionRelationKind.Ally && target.RelationKindWith(source) == FactionRelationKind.Ally,
            other.All(f => source.HostileTo(f) == target.HostileTo(f) && f.HostileTo(source) == f.HostileTo(target)));
        FactionMergerProposals.ValidateScope(scope); return new(game, charter, scope);
    }
    public void Offer(LiveMergerProposalReview reviewed)
    {
        try
        {
            if (reviewed?.Context != Current.Game) throw new ArgumentException("Review the current game.");
            var fresh = Review(reviewed.Charter);
            if (JsonSerializer.Serialize(fresh.Scope) != JsonSerializer.Serialize(reviewed.Scope)) throw new ArgumentException("Merger proposal review changed.");
            var copy = FactionMergerProposals.Restore(Ledger.Export()); copy.Offer(Guid.NewGuid().ToString("N"), fresh.Scope, Find.TickManager.TicksGame); Ledger = copy;
        }
        catch (Exception e) { Reject(e); }
    }
    public void Confirm(string id, string charter, Game context)
    {
        try
        {
            if (context != Current.Game) throw new ArgumentException("Review the current game.");
            var fresh = Review(charter); var copy = FactionMergerProposals.Restore(Ledger.Export()); copy.Confirm(id, fresh.Scope, Find.TickManager.TicksGame); Ledger = copy;
            Messages.Message("FM_Recorded".Translate(), MessageTypeDefOf.NeutralEvent);
        }
        catch (Exception e) { Reject(e); }
    }
    public void Decline(string id, Game context)
    {
        try
        {
            if (context != Current.Game || RestoreFailed) throw new ArgumentException("Review the current game.");
            var copy = FactionMergerProposals.Restore(Ledger.Export()); copy.Decline(id, Find.TickManager.TicksGame); Ledger = copy;
        }
        catch (Exception e) { Reject(e); }
    }
    private static void Reject(Exception e)
    { Log.Warning("[Honor.Contracts.Preview] Separate merger proposal not changed. " + e.GetType().Name); Messages.Message("FM_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
}

public sealed class FactionMergerProposalWindow : Window
{
    private readonly string charter;
    private readonly Game context = Current.Game;
    private LiveMergerProposalReview review;
    private Vector2 scroll;
    private float height = 1000;
    public FactionMergerProposalWindow(string id) { charter = id; forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override Vector2 InitialSize => new(850, 760);
    public override void DoWindowContents(Rect rect)
    {
        if (context != Current.Game) { Widgets.Label(rect, "FM_Blocked".Translate()); return; }
        var area = new Rect(0, 0, rect.width, rect.height - 45); Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, Mathf.Max(12000, height + 1000)));
        list.Label("FM_Limits".Translate()); list.Label("FM_Policy".Translate());
        var component = Current.Game.GetComponent<FactionMergerProposalLedger>();
        if (context != Current.Game || component.RestoreFailed) list.Label("FM_Blocked".Translate());
        else
        {
            if (list.ButtonText("FM_Review".Translate()))
            {
                try { review = component.Review(charter); }
                catch (Exception) { review = null; Messages.Message("FM_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
            }
            if (review != null)
            {
                list.Label("FM_Scope".Translate(review.Scope.SourceLabel, review.Scope.TargetLabel, review.Scope.RepresentativeLabel, review.Scope.Towns.Count, review.Scope.NativePawns.Count));
                if (list.ButtonText("FM_Offer".Translate())) { component.Offer(review); review = null; }
            }
            foreach (var row in component.Ledger.Proposals.Reverse())
            {
                list.Label("FM_History".Translate(row.Scope.SourceLabel, row.Scope.TargetLabel, ("FM_" + row.Phase).Translate(), row.Scope.RepresentativeLabel));
                list.Label(("FM_Reason" + row.Response).Translate());
                if (row.Phase != FactionMergerPhase.NpcAccepted) continue;
                if (Find.TickManager.TicksGame <= row.ExpiresTick && list.ButtonText("FM_Confirm".Translate()))
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("FM_ConfirmText".Translate(), () => component.Confirm(row.Id, charter, context)));
                else if (Find.TickManager.TicksGame > row.ExpiresTick) list.Label("FM_Expired".Translate());
                if (list.ButtonText("FM_Decline".Translate())) component.Decline(row.Id, context);
            }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
