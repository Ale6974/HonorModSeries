using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
using UnityEngine;

namespace Honor.Contracts.Preview;

public sealed record SubtreeExitReview(SubtreeReleasePlan Plan, string Target, string Stamp, int Moves);
internal sealed class SubtreeCityBlocked : ArgumentException
{
    internal string City { get; }
    internal string Reason { get; }
    internal SubtreeCityBlocked(string city, string reason) { City = city; Reason = reason; }
}

/// <summary>Explicit mutual release and real union migration. Internal contracts and native player ownership never change.</summary>
public sealed class SubtreeExitLedger : GameComponent
{
    private string snapshot;
    private List<string> history = [];
    private List<SubtreeExitJournal> archived = [];
    public SubtreeExitJournal Journal { get; private set; } = new();
    public bool RestoreFailed { get; private set; }
    public bool Executing { get; private set; }
    public bool BlocksChanges => RestoreFailed || Journal.Pending;
    private LiveCharterLedger Charters => Current.Game.GetComponent<LiveCharterLedger>();
    private TerritoryAdministrationLedger Territories => Current.Game.GetComponent<TerritoryAdministrationLedger>();
    public SubtreeExitLedger(Game game) { }
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) snapshot = Journal.Export();
        Scribe_Values.Look(ref snapshot, "honorSubtreeExitV1");
        Scribe_Collections.Look(ref history, "honorSubtreeExitHistoryV1", LookMode.Value);
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        history ??= [];
        try
        {
            if (snapshot != null) Journal = SubtreeExitJournal.Restore(snapshot);
            if (history.Count > 2000 || history.Sum(s => (long)(s?.Length ?? 24000001)) > 32000000) throw new ArgumentException("Invalid exit archive size.");
            var loaded = history.Select(SubtreeExitJournal.Restore).ToList();
            if (loaded.Any(j => j.Current?.Phase is not (SubtreeExitPhase.Completed or SubtreeExitPhase.Cancelled))) throw new ArgumentException("Unfinished exit archive.");
            var all = loaded.Select(j => j.Current).Append(Journal.Current).Where(c => c != null).ToArray();
            if (history.Count > 0 && Journal.Current == null || all.Select(c => c.Id).Distinct().Count() != all.Length ||
                all.Skip(1).Where((c, i) => all[i].CompletedTick > c.Plan.Tick).Any()) throw new ArgumentException("Duplicate or unordered exit intent.");
            archived = loaded;
        }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Subtree exit restore rejected; original journal retained."); }
    }
    public override void FinalizeInit()
    {
        if (RestoreFailed || Journal.Current == null) return;
        if (archived.Select(j => j.Current).Append(Journal.Current).Any(c => c.Plan.Tick > Find.TickManager.TicksGame || c.CompletedTick > Find.TickManager.TicksGame) ||
            Journal.Pending && (Charters.RestoreFailed || Journal.Current.Plan.After != Charters.Book.Export() &&
                (Journal.Current.Phase != SubtreeExitPhase.Prepared || Journal.Current.Plan.Before != Charters.Book.Export())))
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Subtree exit source/clock mismatch; changes blocked."); }
    }
    internal bool Proves(TerritoryTransfer transfer)
    {
        if (RestoreFailed || transfer.ReleasedRoot == null) return false;
        return archived.Append(Journal).Any(j => j.Authorizes(transfer));
    }
    internal string Scope(string charter) => !RestoreFailed && Journal.Pending && Journal.Current.Phase == SubtreeExitPhase.Released &&
        Journal.Current.Plan.After == Charters.Book.Export() && Journal.Current.Plan.Charters.Contains(charter) ? Journal.Current.Plan.Root : null;
    private void Ready()
    {
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        var political = Current.Game.GetComponent<PoliticalTransitionLedger>();
        if (RestoreFailed || Current.Game.GetComponent<LeaderJoinLedger>().BlocksChanges || Current.Game.GetComponent<LeaderRelocationLedger>().BlocksChanges || Charters.RestoreFailed || Charters.UnresolvedTownLosses || Territories.RestoreFailed || political.RestoreFailed || political.Journal.Pending ||
            payments.RestoreFailed || payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) ||
            Current.Game.GetComponent<PlayerPoliticsLedger>().RestoreFailed || Current.Game.GetComponent<UnionPoliticsLedger>().RestoreFailed ||
            Current.Game.GetComponent<RealmAdministrationLedger>().RestoreFailed)
            throw new ArgumentException("Resolve unavailable or pending political/payment records.");
    }
    private Faction Union()
    {
        var union = Current.Game.GetComponent<UnionPoliticsLedger>().Union;
        if (union == null || union.IsPlayer || union.def.defName != UnionPoliticsLedger.UnionDefName || union.defeated || union.Hidden ||
            !Find.FactionManager.AllFactionsListForReading.Contains(union) || !union.HasGoodwill || union.HostileTo(Faction.OfPlayer) ||
            union.GoodwillWith(Faction.OfPlayer) < 50 || union.leader == null || union.leader.Dead || union.leader.Faction != union)
            throw new ArgumentException("No current willing union representative.");
        return union;
    }
    private (int Moves, string Stamp, TerritoryTransferObservation[] Observations) Physical(SubtreeReleasePlan plan, CharterBook book, Faction union)
    {
        if (Territories.Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared)) throw new ArgumentException("Recover the pending city first.");
        var copy = TerritoryTransferLedger.Restore(Territories.Ledger.Export()); int moves = 0;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal); var all = new List<TerritoryTransferObservation>();
        var administration = Current.Game.GetComponent<RealmAdministrationLedger>().OwnedFaction;
        foreach (string id in plan.Charters)
        {
            var c = book.Charter(id); var binding = Charters.Binding(c.Terms.Subject); var city = binding.Settlement;
            if (binding.PlayerGroup || binding.FactionGroup || city?.Faction == null || city.Destroyed) throw new ArgumentException("No current NPC town.");
            var source = city.Faction;
            // War admission by an actual council is not implemented. Do not invent acceptance or erase a war.
            if (source != union && (source.HostileTo(union) || Find.FactionManager.AllFactionsListForReading.Any(f => f != Faction.OfPlayer && f != source && f != union &&
                source.HostileTo(f) && !union.HostileTo(f)))) throw new SubtreeCityBlocked(city.Label, "SX_WarUnavailable");
            var o = TerritoryAdministrationLedger.Observe(city, c.Terms.Subject, union); all.Add(o);
            var entity = book.Entity(c.Terms.Subject);
            if (!o.Exists || o.Player || !o.Eligible || !o.TargetEligible || !copy.VerifiedOwner(entity.Id, entity.NativeOwner, o.Owner))
                throw new ArgumentException("Town ownership/eligibility changed.");
            if (source == union) continue;
            if (o.HasMap) throw new SubtreeCityBlocked(city.Label, "SX_MapUnavailable");
            if (o.HasInhabitants && o.ResidentProof == null) throw new SubtreeCityBlocked(city.Label, ResidentExecution.BlockReasonKey(city, union));
            if (o.StockProof == null && o.Visited) throw new SubtreeCityBlocked(city.Label, "SX_StockUnavailable");
            if (!counts.ContainsKey(o.Owner)) counts.Add(o.Owner, o.SourceSettlements);
            if (counts[o.Owner] < 2 && source != administration) throw new SubtreeCityBlocked(city.Label, "SX_MergerUnavailable");
            string execution = "subtree-review-" + Guid.NewGuid().ToString("N");
            copy.Prepare(execution, book, id, o with { SourceSettlements = counts[o.Owner] }, union.GetUniqueLoadID(), null, union.GetUniqueLoadID(),
                TerritoryTransferPurpose.ReleasedToUnion, Find.TickManager.TicksGame, administration?.GetUniqueLoadID(), plan.Root);
            copy.Confirm(execution, o with { Owner = union.GetUniqueLoadID() }, Find.TickManager.TicksGame);
            counts[o.Owner]--; moves++;
        }
        string stamp = JsonSerializer.Serialize(new { Plan = plan, CurrentBook = Charters.Book.Export(), Transfers = Territories.Ledger.Export(), Observations = all,
            Target = union.GetUniqueLoadID(), Leader = union.leader.GetUniqueLoadID(), Goodwill = union.GoodwillWith(Faction.OfPlayer),
            SourceRelations = plan.Charters.Select(id => { var f = Charters.Binding(book.Charter(id).Terms.Subject).Settlement.Faction;
                return new { Faction = f.GetUniqueLoadID(), Leader = f.leader?.GetUniqueLoadID(), Goodwill = f.GoodwillWith(Faction.OfPlayer),
                    Relations = Find.FactionManager.AllFactionsListForReading.Where(x => x.HasGoodwill && !x.IsPlayer && x != f).Select(x => new { Id = x.GetUniqueLoadID(), Hostile = f.HostileTo(x) }).ToArray() }; }).ToArray() });
        return (moves, stamp, all.ToArray());
    }
    public SubtreeExitReview Review(string root)
    {
        Ready(); if (Journal.Pending || history.Count >= 2000 || history.Sum(s => (long)s.Length) + (Journal.Current == null ? 0 : Journal.Export().Length) > 32000000)
            throw new ArgumentException("Resolve prior subtree exit or archive capacity.");
        var c = Charters.Book.Charter(root); var authority = Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority();
        if (!Charters.Book.Entity(c.Terms.Liege).Player && Charters.SubcharterRepresentative(c.Terms.Liege) != authority)
            throw new ArgumentException("The immediate liege must consent within the current realm.");
        var plan = SubtreeRelease.Review(Charters.Book, root, Find.TickManager.TicksGame, Charters.Observe());
        foreach (string id in plan.Charters)
            if (Charters.SubcharterRepresentative(Charters.Book.Charter(id).Terms.Subject) != authority)
                throw new ArgumentException("Align actual realm and current representatives first.");
        var union = Union(); var physical = Physical(plan, CharterBook.Restore(plan.After), union);
        return new(plan, union.GetUniqueLoadID(), physical.Stamp, physical.Moves);
    }
    public void Start(SubtreeExitReview reviewed)
    {
        try
        {
            if (reviewed == null) throw new ArgumentException("Review first.");
            var fresh = Review(reviewed.Plan.Root);
            if (fresh.Stamp != reviewed.Stamp || fresh.Target != reviewed.Target) throw new ArgumentException("Consent/world changed.");
            var next = new SubtreeExitJournal();
            next.Prepare(Guid.NewGuid().ToString("N"), fresh.Target, fresh.Plan, Territories.Ledger);
            if (Journal.Current != null) { history.Add(Journal.Export()); archived.Add(Journal); }
            Journal = next;
            Charters.ReplaceForSubtree(fresh.Plan); Journal.ConfirmRelease(Charters.Book);
            Executing = true; Finish(Union());
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { ReportBlocked(e); }
        finally { Executing = false; }
    }
    public SubtreeExitReview ReviewRemaining()
    {
        Ready(); if (!Journal.Pending || Journal.Current.Plan.After != Charters.Book.Export()) throw new ArgumentException("Unapplied preparation can only cancel.");
        var union = Union(); if (union.GetUniqueLoadID() != Journal.Current.Target) throw new ArgumentException("Union changed.");
        var state = Physical(Journal.Current.Plan, Charters.Book, union);
        return new(Journal.Current.Plan, Journal.Current.Target, state.Stamp, state.Moves);
    }
    public void Resume(SubtreeExitReview reviewed)
    {
        try
        {
            var fresh = ReviewRemaining(); if (reviewed == null || fresh.Stamp != reviewed.Stamp) throw new ArgumentException("Recovery review changed.");
            Journal.ConfirmRelease(Charters.Book); Executing = true; Finish(Union());
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { ReportBlocked(e); }
        finally { Executing = false; }
    }
    private void Finish(Faction union)
    {
        Ready(); var plan = ReviewRemaining();
        foreach (string id in plan.Plan.Charters)
        {
            var binding = Charters.Binding(Charters.Book.Charter(id).Terms.Subject);
            if (binding.Settlement.Faction == union) continue;
            var single = Territories.Review(id);
            if (single.TargetIdentity != plan.Target || !Territories.Apply(single, false)) throw new ArgumentException("City migration interrupted.");
        }
        var actual = Physical(plan.Plan, Charters.Book, union);
        if (actual.Moves != 0) throw new ArgumentException("Not every city reached the union.");
        Journal.Complete(Charters.Book, Territories.Ledger, actual.Observations, Find.TickManager.TicksGame);
        Messages.Message("SX_Completed".Translate(), MessageTypeDefOf.NeutralEvent);
    }
    public void CancelUnchanged()
    {
        try { Ready(); Journal.CancelUnchanged(Charters.Book, Territories.Ledger, Find.TickManager.TicksGame); }
        catch (ArgumentException) { Interrupted(); }
    }
    private static void Interrupted()
    {
        Log.Warning("[Honor.Contracts.Preview] Subtree exit unavailable or interrupted; existing intent and city records retained.");
        Messages.Message("SX_Blocked".Translate(), MessageTypeDefOf.RejectInput); Find.TickManager.Pause();
    }
    internal static void ReportBlocked(Exception e)
    {
        if (e is SubtreeCityBlocked city) Messages.Message("SX_CityBlocked".Translate(city.City, city.Reason.Translate()), MessageTypeDefOf.RejectInput);
        else Interrupted();
    }
}

public sealed class SubtreeExitWindow : Window
{
    private string root;
    private SubtreeExitReview review;
    private Vector2 scroll;
    private float height = 1200;
    public SubtreeExitWindow() { forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override Vector2 InitialSize => new(850, 760);
    public override void DoWindowContents(Rect rect)
    {
        var exits = Current.Game.GetComponent<SubtreeExitLedger>(); var charters = Current.Game.GetComponent<LiveCharterLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, Mathf.Max(12000, height + 1000)));
        list.Label("SX_Warning".Translate());
        list.Label("RE_Limits".Translate());
        if (exits.RestoreFailed || charters.RestoreFailed) list.Label("HCP_RestoreFailed".Translate());
        else
        {
            if (!exits.Journal.Pending && list.ButtonText("SX_Select".Translate(root == null ? "HCL_Missing".Translate() : charters.Label(charters.Book.Charter(root).Terms.Subject))))
            {
                var options = charters.Book.PlayerFollowers().Select(c => new FloatMenuOption(charters.Label(c.Terms.Subject), () => { root = c.Id; review = null; })).ToList();
                if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
            }
            if ((root != null || exits.Journal.Pending) && list.ButtonText("SX_Review".Translate()))
            {
                try { review = exits.Journal.Pending ? exits.ReviewRemaining() : exits.Review(root); }
                catch (ArgumentException e) { review = null; SubtreeExitLedger.ReportBlocked(e); }
            }
            if (review != null)
            {
                list.Label("SX_Plan".Translate(review.Plan.Charters.Length, review.Moves));
                foreach (string id in review.Plan.Charters) list.Label(charters.Label(charters.Book.Charter(id).Terms.Subject));
                if (list.ButtonText("SX_Confirm".Translate())) { if (exits.Journal.Pending) exits.Resume(review); else exits.Start(review); review = null; }
            }
            if (exits.Journal.Pending)
            {
                list.Label("SX_Pending".Translate());
                if (exits.Journal.Current.Phase == SubtreeExitPhase.Prepared && list.ButtonText("PS_Cancel".Translate())) { exits.CancelUnchanged(); review = null; }
                foreach (var t in Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger.Transfers.Where(t => t.Phase == TerritoryTransferPhase.Prepared))
                    if (list.ButtonText("CT_Recover".Translate())) { Current.Game.GetComponent<TerritoryAdministrationLedger>().Recover(t.Id); review = null; }
            }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
