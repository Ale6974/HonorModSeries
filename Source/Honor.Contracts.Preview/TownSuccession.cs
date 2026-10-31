using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed record LiveTownSuccessionReview(TownSuccessionPlan Plan, string Stamp, string Realm, string Representative, Game Context = null)
{
    public string OwnershipReturnProof { get; init; }
    public bool SeparateRealm { get; init; }
    public Dictionary<string, string> ReturnProofs { get; init; }
    public TownSuccessionMandate Government { get; init; }
    public HistoricalTownReturnReview Historical { get; init; }
    public string[] IndividualLabels { get; init; }
}

public sealed partial class LiveCharterLedger
{
    private string townLossSnapshot;
    private TownSuccessionArchive townLosses = new();
    private TownSuccessionMandates townGovernments = new();
    private string townGovernmentSnapshot;
    private HistoricalTownReturnReconciliations townReturnReconciliations = new();
    private string townReturnReconciliationSnapshot;
    internal IReadOnlyCollection<TownLossProof> TownLosses => townLosses.Records;
    internal bool TownLossReviewRoot(string id) => townLosses.IsReviewRoot(id);
    internal bool UnresolvedTownLosses => townLosses.HasUnresolved;
    internal TownSuccessionArchive LossArchive => townLosses;
    internal void ReplaceAfterPendingUpperLoss(string before, CharterBook after, TownSuccessionArchive staged)
    {
        if (RestoreFailed || Book.Export() != before) throw new ArgumentException("Original consent changed.");
        staged.ValidateBook(after); Book = after; townLosses = staged;
    }
    private void ExposeTownLosses()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) townLossSnapshot = townLosses.Export();
        Scribe_Values.Look(ref townLossSnapshot, "honorUpperTownLossesV1");
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed && townGovernments.Records.Count > 0) townGovernmentSnapshot = townGovernments.Export();
        Scribe_Values.Look(ref townGovernmentSnapshot, "honorTownSuccessionGovernmentsV1");
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed && townReturnReconciliations.Records.Count > 0) townReturnReconciliationSnapshot = townReturnReconciliations.Export();
        Scribe_Values.Look(ref townReturnReconciliationSnapshot, "honorHistoricalTownReturnReconciliationsV1");
        ExposeOwnershipReturns();
    }
    private void ValidateTownLosses()
    {
        if (townLossSnapshot != null) townLosses = TownSuccessionArchive.Restore(townLossSnapshot);
        townLosses.ValidateBook(Book);
        ValidateOwnershipReturns();
        if (townGovernmentSnapshot != null) townGovernments = TownSuccessionMandates.Restore(townGovernmentSnapshot);
        townGovernments.ValidatePair(townLosses, Book);
        if (townReturnReconciliationSnapshot != null) townReturnReconciliations = HistoricalTownReturnReconciliations.Restore(townReturnReconciliationSnapshot);
        townReturnReconciliations.ValidatePair(townLosses, Book, ownershipReturns);
    }
    public LiveTownSuccessionReview ReviewTownSuccession(string root, string prefix = null)
    {
        if (Current.Game != context || Current.ProgramState != ProgramState.Playing) throw new ArgumentException("Review the current original game.");
        if (!LossChangesAvailable()) throw new ArgumentException("Resolve unavailable or pending political/payment records.");
        var custody = context.GetComponent<TerritoryAdministrationLedger>();
        if (custody.RestoreFailed || custody.Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared)) throw new ArgumentException("Resolve original physical custody before contractual succession.");
        if (Current.Game.GetComponent<PlayerPoliticsLedger>().RestoreFailed || Current.Game.GetComponent<RealmAdministrationLedger>().RestoreFailed ||
            Current.Game.GetComponent<UnionPoliticsLedger>().RestoreFailed) throw new ArgumentException("Current player political records are unavailable.");
        var proof = townLosses.Record(root);
        string returnProof = null;
        var returnProofs = new Dictionary<string, string>(StringComparer.Ordinal);
        var actualOwners = new Dictionary<string, string>(StringComparer.Ordinal); bool historical = false;
        foreach (var loss in townLosses.ScopeLosses(root))
        {
            var lostCity = Binding(loss.Territory).Settlement;
            if (loss.Kind == TownLossKind.Destroyed)
            {
                if (lostCity != null && !lostCity.Destroyed && Find.WorldObjects.AllWorldObjects.Contains(lostCity))
                    throw new ArgumentException("The previously destroyed object returned; review its new rights separately.");
            }
            else
            {
                if (lostCity == null || lostCity.Destroyed || !Find.WorldObjects.AllWorldObjects.Contains(lostCity))
                    throw new ArgumentException("The observed town is unavailable.");
                actualOwners.Add(loss.Root, lostCity.Faction?.GetUniqueLoadID());
                if (lostCity.Faction?.GetUniqueLoadID() != loss.CapturingOwner)
                {
                    string returned;
                    try { returned = ownershipReturns.RequireActiveWithLineage(loss.Root, townLosses, Book, "territory/" + lostCity.GetUniqueLoadID(),
                        lostCity.Faction?.GetUniqueLoadID(), Find.TickManager.TicksGame, Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger.Export(), amendments.Export()); }
                    catch (ArgumentException)
                    {
                        returned = ownershipReturns.ReviewHistoricalFact(loss.Root, townLosses, Book, lostCity.Faction?.GetUniqueLoadID(), Find.TickManager.TicksGame,
                            Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger.Export(), amendments.Export());
                        historical = true;
                    }
                    returnProofs.Add(loss.Root, returned); if (loss.Root == root) returnProof = returned;
                }
            }
        }
        var source = CharterBook.Restore(proof.Before); var original = source.Charter(root);
        // A signed NPC charter need not yet have migrated to the player's physical realm.
        // Reconnect within the actual recorded NPC realm, never infer its current consent from player allegiance.
        var owners = Find.FactionManager.AllFactionsListForReading.Where(f => f != null && f.GetUniqueLoadID() == proof.OwnerAtLoss).Take(2).ToArray();
        if (owners.Length != 1) throw new ArgumentException("The recorded NPC realm must still have one exact registered identity.");
        var authority = owners[0];
        var reviewedRuler = authority.leader;
        string realmLabel = authority.Name, rulerLabel = reviewedRuler?.LabelShortCap.ToString();
        var decision = NpcDecisionAuthority.Observe(authority);
        if (reviewedRuler == null || authority.leader != reviewedRuler || decision.Representative != reviewedRuler.GetUniqueLoadID()) throw new ArgumentException("Ruler changed while preparing review labels.");
        if (authority == null || authority.def == null || authority.IsPlayer ||
            authority.GetUniqueLoadID() != proof.OwnerAtLoss || authority.leader == null || authority.leader.Dead ||
            authority.leader.Destroyed || authority.leader.Downed || authority.leader.InMentalState || authority.leader.IsPrisoner || authority.leader.IsSlave ||
            authority.leader.Faction != authority || authority.GoodwillWith(Faction.OfPlayer) < 50)
            throw new ArgumentException("Same surviving willing realm representative is required.");
        bool playerParent = Book.Entity(original.Terms.Liege).Player;
        var higher = Binding(original.Terms.Liege);
        if (playerParent && (original.Terms.Liege != PlayerIdentity || !higher.PlayerGroup || higher.FactionGroup || higher.OriginalFaction != Faction.OfPlayer ||
            Book.Entity(original.Terms.Liege).NativeOwner != Faction.OfPlayer.GetUniqueLoadID())) throw new ArgumentException("Verify the actual original player higher party.");
        var actualHigher = playerParent ? Faction.OfPlayer : SubcharterRepresentative(original.Terms.Liege);
        bool mixed = !playerParent && actualHigher != authority;
        var consent = decision.Regime == NpcDecisionRegime.Ordinary && !mixed ? TownSuccessionAuthority.Review(decision, proof.OwnerAtLoss, original.Terms.Liege, playerParent, PlayerIdentity,
            Faction.OfPlayer.GetUniqueLoadID(), actualHigher.GetUniqueLoadID()) : new NpcCharterAuthority(playerParent ? original.Terms.Liege : decision.Representative, decision.Representative);
        if (mixed) consent = new(actualHigher.leader.GetUniqueLoadID(), decision.Representative);
        long now = Find.TickManager.TicksGame;
        var plan = townLosses.Review(root, Book, prefix ?? Guid.NewGuid().ToString("N"),
            consent, now, Observe());
        var current = plan.Descendants.Select(Book.Charter).ToArray();
        var actual = new List<object>();
        var parties = new List<RetainedRealmConsent>(); var realms = new HashSet<Faction> { authority }; var labels = new List<string>();
        foreach (var party in current.Select(c => c.Terms.Subject).Concat(playerParent ? Array.Empty<string>() : new[] { original.Terms.Liege }).Distinct(StringComparer.Ordinal))
        {
            var owner = SubcharterRepresentative(party); mixed |= owner != authority; realms.Add(owner);
            var binding = Binding(party); var entity = Book.Entity(party);
            var transfers = Current.Game.GetComponent<TerritoryAdministrationLedger>();
            if (transfers.RestoreFailed || !transfers.Ledger.VerifiedOwner(entity.Id, entity.NativeOwner, binding.Settlement.Faction.GetUniqueLoadID()))
                throw new ArgumentException("Unverified native survivor ownership.");
            parties.Add(new(party, owner.GetUniqueLoadID(), NpcDecisionAuthority.Observe(owner), true));
            labels.Add(Label(party) + " / " + owner.Name + " / " + owner.leader.LabelShortCap);
            actual.Add(new { Party = party, Town = binding.Settlement.GetUniqueLoadID(), Owner = owner.GetUniqueLoadID() });
        }
        if (realms.Any(a => realms.Any(b => a != b && (a.HostileTo(b) || b.HostileTo(a))))) throw new ArgumentException("Every affected actual realm must be reciprocally peaceful.");
        if (mixed)
        {
            string map = TownSuccessionMandates.IndividualAuthorities(plan, parties.ToArray());
            string actualPrefix = plan.ScopePrefix ?? plan.Replacements.Values.First().Substring(0, plan.Replacements.Values.First().LastIndexOf('/'));
            plan = townLosses.ReviewWithParties(root, Book, actualPrefix, consent, now, Observe(), map);
        }
        TownSuccessionMandate government = null;
        var currentDecision = new TownSuccessionMandate(plan, townLosses.Export(), Observe(), PlayerIdentity, Faction.OfPlayer.GetUniqueLoadID(), actualHigher.GetUniqueLoadID(), decision, null);
        if (mixed)
        {
            currentDecision = currentDecision with { Parties = JsonSerializer.Serialize(parties.ToArray()), Transfers = context.GetComponent<TerritoryAdministrationLedger>().Ledger.Export() };
            var mandates = NativeNpcContractAuthority.ReviewSuccessionParties(currentDecision, realms) ?? [];
            var others = mandates.Where(d => d.Realm != decision.Realm).ToArray();
            government = currentDecision with { Mandate = mandates.SingleOrDefault(d => d.Realm == decision.Realm), Governments = others.Length == 0 ? null : JsonSerializer.Serialize(others) };
        }
        else if (decision.Regime != NpcDecisionRegime.Ordinary) government = currentDecision with { Mandate = NativeNpcContractAuthority.ReviewSuccession(currentDecision, authority) };
        if (government != null) { TownSuccessionMandates.ValidateWitness(government); currentDecision = government; }
        HistoricalTownReturnReview historicalReview = null;
        if (historical)
        {
            historicalReview = new(currentDecision, ownershipReturns.Export(), Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger.Export(), amendments.Export(), actualOwners, returnProofs);
            HistoricalTownReturnReconciliations.Review(historicalReview);
        }
        string stamp = JsonSerializer.Serialize(new { Plan = plan, Archive = townLosses.Export(), Actual = actual,
            Authority = authority.GetUniqueLoadID(), Leader = authority.leader.GetUniqueLoadID(), Goodwill = authority.GoodwillWith(Faction.OfPlayer),
            Higher = actualHigher.GetUniqueLoadID(), Consent = consent,
            Transfers = Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger.Export(), Observations = Observe(),
            OwnershipReturns = ownershipReturns.Export(), Amendments = amendments.Export(), ReturnProof = returnProof, ReturnProofs = returnProofs, Government = government, GovernmentHistory = townGovernments.Export(), Historical = historicalReview, HistoricalHistory = townReturnReconciliations.Export() });
        return new(plan, stamp, realmLabel, rulerLabel, context) {
            OwnershipReturnProof = returnProof,
            ReturnProofs = returnProofs,
            Government = government,
            Historical = historicalReview,
            IndividualLabels = mixed ? labels.ToArray() : null,
            SeparateRealm = authority != Current.Game.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority() };
    }
    public void ApplyTownSuccession(LiveTownSuccessionReview reviewed)
    {
        Execute(() => {
            if (reviewed == null || reviewed.Context != Current.Game || reviewed.Plan?.Replacements == null || reviewed.Plan.Replacements.Count == 0 && reviewed.Plan.ScopePrefix == null ||
                reviewed.Plan.Replacements.Values.Any(v => string.IsNullOrWhiteSpace(v))) throw new ArgumentException("Review first.");
            string prefix = reviewed.Plan.ScopePrefix;
            if (prefix == null) { string sample = reviewed.Plan.Replacements.Values.First(); int separator = sample.LastIndexOf('/');
                if (separator < 1) throw new ArgumentException("Invalid succession identity."); prefix = sample.Substring(0, separator); }
            var fresh = ReviewTownSuccession(reviewed.Plan.Root, prefix);
            if (fresh.Stamp != reviewed.Stamp) throw new ArgumentException("Succession authority or source changed.");
            var reconciliations = townReturnReconciliations;
            (CharterBook Book, TownSuccessionArchive Archive) result;
            if (fresh.Historical != null)
            {
                var resolved = townReturnReconciliations.Execute(fresh.Historical, ownershipReturns, townLosses, Book);
                result = (resolved.Book, resolved.Archive); reconciliations = resolved.History;
            }
            else result = fresh.ReturnProofs.Count == 0 ? townLosses.Commit(Book, fresh.Plan, Find.TickManager.TicksGame, Observe()) :
                ownershipReturns.CommitSuccessionGroup(townLosses, Book, fresh.Plan, Find.TickManager.TicksGame, Observe(), fresh.ReturnProofs,
                    townLosses.ScopeLosses(fresh.Plan.Root).Where(p => p.Kind == TownLossKind.HostileNativeOwnershipChange).ToDictionary(p => p.Root,
                        p => Binding(p.Territory).Settlement?.Faction?.GetUniqueLoadID(), StringComparer.Ordinal),
                    Current.Game.GetComponent<TerritoryAdministrationLedger>().Ledger.Export(), amendments.Export());
            ownershipReturns.ValidatePair(result.Archive, result.Book);
            ownershipReturns.ValidateAmendmentPair(amendments, result.Book);
            deferredTerminations.ValidatePairs(terminationHistory, result.Archive, result.Book);
            var governments = townGovernments;
            if (fresh.Government != null)
            {
                var linked = townGovernments.Attach(fresh.Government, result.Archive, result.Book);
                governments = linked.History; result = (result.Book, linked.Archive);
            }
            governments.ValidatePair(result.Archive, result.Book);
            reconciliations.ValidatePair(result.Archive, result.Book, ownershipReturns);
            if (ReviewTownSuccession(fresh.Plan.Root, prefix).Stamp != fresh.Stamp) throw new ArgumentException("Current complete loss scope or individual returns changed before assignment.");
            if (Current.Game != context || Current.ProgramState != ProgramState.Playing) throw new ArgumentException("Game changed before resolution assignment.");
            if (Book.Export() != fresh.Plan.Before) throw new ArgumentException("Original live book changed before resolution assignment.");
            Book = result.Book; townLosses = result.Archive; townGovernments = governments; townReturnReconciliations = reconciliations;
            Messages.Message("LS_Completed".Translate(fresh.Plan.Replacements.Count), MessageTypeDefOf.NeutralEvent);
        }, resolvingLoss: true);
    }
}

public sealed class TownSuccessionWindow : Window
{
    private readonly Game context = Current.Game;
    private string root;
    private LiveTownSuccessionReview review;
    private Vector2 scroll;
    private float height = 1200;
    public TownSuccessionWindow() { forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    internal TownSuccessionWindow(string initialRoot) : this() { root = initialRoot; }
    public override Vector2 InitialSize => new(850, 760);
    public override void DoWindowContents(Rect rect)
    {
        if (context != Current.Game) { Widgets.Label(rect, "ND_Unavailable".Translate()); return; }
        var ledger = Current.Game.GetComponent<LiveCharterLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, Mathf.Max(12000, height + 1000)));
        list.Label("LS_Warning".Translate());
        list.Label("LS_CurrentAuthority".Translate());
        list.Label("ND_Limits".Translate());
        list.Label("HL_Limits".Translate());
        list.Label("OR_Limits".Translate());
        list.Label("LC_Warning".Translate());
        list.Label("LM_Warning".Translate());
        if (ledger.UnresolvedTownLosses) list.Label("LS_Freeze".Translate());
        if (ledger.RestoreFailed) list.Label("HCP_RestoreFailed".Translate());
        else
        {
            if (list.ButtonText("LS_Select".Translate(root == null ? "HCL_Missing".Translate() : ledger.TownLosses.Single(r => r.Root == root).Label)))
            {
                var options = ledger.TownLosses.Where(r => r.Resolution == null && ledger.TownLossReviewRoot(r.Root)).Select(r => new FloatMenuOption(r.Label, () => { root = r.Root; review = null; })).ToList();
                if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options)); else list.Label("LS_Empty".Translate());
            }
            if (root != null && list.ButtonText("LS_Review".Translate()))
            {
                try { review = ledger.ReviewTownSuccession(root); }
                catch (NpcDecisionBlocked e) { review = null; Messages.Message(e.Key.Translate(), MessageTypeDefOf.RejectInput); }
                catch (ArgumentException) { review = null; Messages.Message("LS_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
            }
            if (review != null)
            {
                if (review.Plan.ScopeRoots != null)
                {
                    list.Label("LC_Scope".Translate(review.Plan.ScopeRoots.Length));
                    foreach (string id in review.Plan.ScopeRoots) list.Label("LC_Loss".Translate(ledger.TownLosses.Single(r => r.Root == id).Label));
                }
                list.Label("LS_Plan".Translate(review.Realm, review.Representative, ledger.Label(review.Plan.Parent), review.Plan.Replacements.Count, review.Plan.Descendants.Length));
                if (review.IndividualLabels != null)
                {
                    list.Label("LS_Individual".Translate(review.Plan.Replacements.Count));
                    foreach (string label in review.IndividualLabels) list.Label(label);
                }
                if (review.SeparateRealm) list.Label("LS_SeparateRealm".Translate());
                if (review.OwnershipReturnProof != null) list.Label("OR_Review".Translate());
                foreach (var p in review.Plan.Replacements) list.Label(ledger.Label(ledger.Book.Charter(p.Key).Terms.Subject));
                if (list.ButtonText("LS_Confirm".Translate())) { ledger.ApplyTownSuccession(review); review = null; }
            }
            foreach (var proof in ledger.TownLosses.Where(r => r.Resolution != null)) list.Label("LS_History".Translate(proof.Label, proof.Resolution.Replacements.Count));
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
