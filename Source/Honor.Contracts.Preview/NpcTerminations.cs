using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

internal sealed record LiveNpcTerminationReview(NpcTerminationWitness Witness, string Stamp, Game Context);

public sealed partial class LiveCharterLedger
{
    private string terminationSnapshot;
    private List<string> terminationPairs = [];
    private NpcSubtreeTerminations terminationHistory = new();
    private DeferredNpcTerminations deferredTerminations = new();
    private string deferredTerminationSnapshot;
    private List<string> deferredTerminationPairs = [];
    private List<string> deferredTerminationStatePairs = [];
    private bool deferredTerminationStatePairsRequired;
    private static List<string> TerminationPairs(NpcSubtreeTerminations history) => history.Records.Select(r => r.Witness.Root + ":" + r.Proof).ToList();
    private void ExposeNpcTerminations()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed && terminationHistory.Records.Count > 0)
        { terminationSnapshot = terminationHistory.Export(); terminationPairs = TerminationPairs(terminationHistory); }
        Scribe_Values.Look(ref terminationSnapshot, "honorNpcTerminationsV1");
        Scribe_Collections.Look(ref terminationPairs, "honorNpcTerminationPairsV1", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed && deferredTerminations.Records.Count > 0)
        {
            deferredTerminationSnapshot = deferredTerminations.Export();
            deferredTerminationPairs = deferredTerminations.Records.Select(r => r.Request.Id + ":" + r.Proof).ToList();
            deferredTerminationStatePairs = deferredTerminations.Records.Select(DeferredNpcTerminations.Marker).ToList();
            deferredTerminationStatePairsRequired = true;
        }
        Scribe_Values.Look(ref deferredTerminationSnapshot, "honorDeferredNpcTerminationsV1");
        Scribe_Collections.Look(ref deferredTerminationPairs, "honorDeferredNpcTerminationPairsV1", LookMode.Value);
        Scribe_Collections.Look(ref deferredTerminationStatePairs, "honorDeferredNpcTerminationStatePairsV1", LookMode.Value);
        Scribe_Values.Look(ref deferredTerminationStatePairsRequired, "honorDeferredNpcTerminationStatePairsRequiredV1", false);
    }
    private void ValidateNpcTerminations()
    {
        terminationPairs ??= [];
        if (terminationSnapshot != null) terminationHistory = NpcSubtreeTerminations.Restore(terminationSnapshot);
        if (terminationPairs.Count > 128 || !terminationPairs.SequenceEqual(TerminationPairs(terminationHistory)))
            throw new ArgumentException("Unpaired original NPC termination records.");
        terminationHistory.ValidateBook(Book);
        deferredTerminationPairs ??= [];
        deferredTerminationStatePairs ??= [];
        if (deferredTerminationSnapshot != null) deferredTerminations = DeferredNpcTerminations.Restore(deferredTerminationSnapshot);
        if (deferredTerminationPairs.Count > 32 || !deferredTerminationPairs.SequenceEqual(deferredTerminations.Records.Select(r => r.Request.Id + ":" + r.Proof)))
            throw new ArgumentException("Unpaired original deferred request.");
        if (deferredTerminationStatePairsRequired ? deferredTerminations.Records.Count == 0 || !deferredTerminationStatePairs.SequenceEqual(deferredTerminations.Records.Select(DeferredNpcTerminations.Marker))
            : deferredTerminationStatePairs.Count != 0)
            throw new ArgumentException("Deferred phase/cancellation markers are missing or changed.");
        deferredTerminations.ValidatePairs(terminationHistory, townLosses, Book);
    }
    private void FinalizeNpcTerminations()
    {
        if (RestoreFailed || terminationHistory.Records.Count == 0 && deferredTerminations.Records.Count == 0) return;
        try
        {
            var transfers = context.GetComponent<TerritoryAdministrationLedger>(); var payments = context.GetComponent<TributePaymentLedger>();
            if (transfers.RestoreFailed || payments.RestoreFailed || terminationHistory.Records.Any(r => r.Witness.Tick > Find.TickManager.TicksGame) ||
                deferredTerminations.Records.Any(r => r.Request.Tick > Find.TickManager.TicksGame || r.ClosedTick > Find.TickManager.TicksGame))
                throw new ArgumentException("Original NPC termination clock or peers unavailable.");
            terminationHistory.ValidateBook(Book); terminationHistory.ValidatePeers(transfers.Ledger, payments.Ledger);
            deferredTerminations.ValidatePairs(terminationHistory, townLosses, Book);
        }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] NPC termination source/peers rejected; raw records retained."); }
    }
    private LiveNpcTerminationReview ReviewNpcTermination(string rootId)
    {
        if (Current.Game != context || Current.ProgramState != ProgramState.Playing || RestoreFailed || UnresolvedTownLosses)
            throw new ArgumentException("Current original NPC subtree is unavailable.");
        var transfers = context.GetComponent<TerritoryAdministrationLedger>(); var payments = context.GetComponent<TributePaymentLedger>();
        var protection = context.GetComponent<CharterProtectionLedger>();
        if (transfers.RestoreFailed || payments.RestoreFailed || protection.RestoreFailed || context.GetComponent<PoliticalTransitionLedger>().BlocksChanges ||
            context.GetComponent<SubtreeExitLedger>().BlocksChanges || context.GetComponent<LeaderJoinLedger>().BlocksChanges || context.GetComponent<LeaderRelocationLedger>().BlocksChanges ||
            transfers.Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) || payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared))
            throw new ArgumentException("Resolve political/physical/payment custody before NPC termination.");
        var root = Book.Charter(rootId);
        if (root.NpcAuthority == null || root.Phase != RecordedCharterPhase.Active || !Book.PlayerManaged(root))
            throw new ArgumentException("Select an active NPC-only incoming contract.");
        terminationHistory.ValidateBook(Book); terminationHistory.ValidatePeers(transfers.Ledger, payments.Ledger);
        reparentHistory.ValidateBook(Book); reparentHistory.ValidatePeers(transfers.Ledger, payments.Ledger);
        var authority = context.GetComponent<PlayerPoliticsLedger>().TerritoryAuthority(); var decision = NpcDecisionAuthority.Observe(authority);
        var realms = new HashSet<Faction> { authority };
        var claims = new List<NpcOwnershipContractSettlement>();
        RetainedRealmConsent Read(string party)
        {
            var actual = SubcharterRepresentative(party); realms.Add(actual);
            if (!transfers.Ledger.VerifiedOwner(party, Book.Entity(party).NativeOwner, actual.GetUniqueLoadID()))
            {
                if (transfers.IndependentlyLostTerritory(party) || transfers.Ledger.Transfers.Any(t => t.Territory == party && t.Phase == TerritoryTransferPhase.Destroyed))
                    throw new ArgumentException("Recorded losses require their own reconciliation; a current controller cannot waive them.");
                string previous = transfers.Ledger.ExpectedOwner(party, Book.Entity(party).NativeOwner);
                var claimant = Find.FactionManager.AllFactionsListForReading.SingleOrDefault(f => f.GetUniqueLoadID() == previous)
                    ?? throw new ArgumentException("Previous known claimant is unavailable.");
                realms.Add(claimant);
                claims.Add(new(party, previous, actual.GetUniqueLoadID(), NpcDecisionAuthority.Observe(claimant), true));
            }
            return new(party, actual.GetUniqueLoadID(), NpcDecisionAuthority.Observe(actual), true);
        }
        var parties = new[] { Read(root.Terms.Liege), Read(root.Terms.Subject) };
        var children = SubtreeRelease.Members(Book, rootId).Where(id => id != rootId).Select(id => Read(Book.Charter(id).Terms.Subject)).ToArray();
        if (realms.Any(a => realms.Any(b => a != b && (a.HostileTo(b) || b.HostileTo(a))))) throw new ArgumentException("Affected realms must be reciprocally peaceful.");
        var witness = new NpcTerminationWitness(rootId, Book.Export(), Find.TickManager.TicksGame, decision, Observe(), parties, children, transfers.Ledger.Export(), payments.Ledger.Export()) {
            OwnershipClaims = claims.Count == 0 ? null : claims.ToArray() };
        witness = witness with { Regimes = NativeNpcContractAuthority.Review(witness, realms) };
        terminationHistory.Append(witness, Book); // Stage complete original terms/scope and enforce capacity before confirmation.
        var stamp = JsonSerializer.Serialize(new { witness, History = terminationHistory.Export(), Reparents = reparentHistory.Export(),
            FinalConsents = finalConsents.Export(), Amendments = amendments.Export(), PlayerRenewals = playerRenewals.Export(),
            Losses = townLosses.Export(), Returns = ownershipReturns.Export(), Protection = protection.Ledger.Export() });
        if (Current.Game != context || Current.ProgramState != ProgramState.Playing) throw new ArgumentException("Game changed during NPC release review.");
        return new(witness, stamp, context);
    }
    public void RequestNpcTermination(string root) => BeginNpcTermination(root, null);
    private void BeginNpcTermination(string root, DeferredNpcState requested)
    {
        Execute(() => {
            var reviewed = ReviewNpcTermination(root);
            var requestsBefore = deferredTerminations.Export();
            if (requested != null)
            {
                if (deferredTerminations.Pending != requested) throw new ArgumentException("Original deferred request changed.");
                deferredTerminations.RequireReady(Book, context.GetComponent<TerritoryAdministrationLedger>().Ledger, context.GetComponent<TributePaymentLedger>().Ledger, Find.TickManager.TicksGame);
            }
            var mandate = reviewed.Witness.Regimes == null ? "" : "\n" + string.Join("\n", reviewed.Witness.Regimes.Select(s => "NT_Mandate".Translate(s.Realm,
                s.Regime == NpcDecisionRegime.Union ? "NT_Council".Translate(s.Ballots.Length) : "NT_Ruler".Translate(s.RoyalTitle), s.Representative).ToString()));
            if (reviewed.Witness.OwnershipClaims != null) mandate += "\n" + string.Join("\n", reviewed.Witness.OwnershipClaims.Select(c => "NT_Claims".Translate(Label(c.Party), c.PreviousKnownOwner, c.ActualOwner).ToString()));
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("NT_Confirm".Translate(Label(Book.Charter(root).Terms.Subject), reviewed.Witness.Children.Length) + mandate, () => Execute(() => {
                if (reviewed.Context != Current.Game) throw new ArgumentException("Review this current game first.");
                var fresh = ReviewNpcTermination(root);
                if (fresh.Stamp != reviewed.Stamp) throw new ArgumentException("Original NPC release review changed.");
                if (deferredTerminations.Export() != requestsBefore) throw new ArgumentException("Saved request changed after confirmation.");
                if (requested != null) deferredTerminations.RequireReady(Book, context.GetComponent<TerritoryAdministrationLedger>().Ledger, context.GetComponent<TributePaymentLedger>().Ledger, Find.TickManager.TicksGame);
                var witness = requested == null ? fresh.Witness : fresh.Witness with { Deferred = new(requested.Request.Id, requested.Proof) };
                var next = terminationHistory.Append(witness, Book);
                var requests = requested == null ? deferredTerminations : deferredTerminations.Close(fresh.Witness.Tick, next.Archive.Records.Last().Proof);
                requests.ValidatePairs(next.Archive, townLosses, next.Book);
                next.Archive.ValidatePeers(context.GetComponent<TerritoryAdministrationLedger>().Ledger, context.GetComponent<TributePaymentLedger>().Ledger);
                reparentHistory.ValidateBook(next.Book); finalConsents.ValidateBook(next.Book); amendments.ValidateBook(next.Book); playerRenewals.ValidatePair(amendments, next.Book);
                townLosses.ValidateBook(next.Book); ownershipReturns.ValidatePair(townLosses, next.Book); context.GetComponent<CharterProtectionLedger>().Ledger.ValidateBook(next.Book);
                if (ReviewNpcTermination(root).Stamp != fresh.Stamp || Current.Game != context || deferredTerminations.Export() != requestsBefore) throw new ArgumentException("Current release consent changed before assignment.");
                var pairs = TerminationPairs(next.Archive);
                Book = next.Book; terminationHistory = next.Archive; terminationPairs = pairs; deferredTerminations = requests; // Staged models; no native callback between assignments.
                Messages.Message("NT_Applied".Translate(), MessageTypeDefOf.NeutralEvent);
            })));
        });
    }
    internal void DrawNpcTerminations(Listing_Standard list)
    {
        var pending = deferredTerminations.Pending;
        if (pending != null)
        {
            list.Label("NT_Queued".Translate(Label(Book.Charter(pending.Request.Root).Terms.Subject), pending.Request.Tick));
            if (list.ButtonText("NT_OpenTransfers".Translate())) Find.WindowStack.Add(new FollowerTransfersWindow());
            if (list.ButtonText("NT_Resume".Translate())) BeginNpcTermination(pending.Request.Root, pending);
            if (list.ButtonText("NT_Cancel".Translate())) EditDeferred(() => deferredTerminations.Close(Find.TickManager.TicksGame));
            if (townLosses.Records.Any(p => p.Root == pending.Request.Root) && list.ButtonText("NT_AcknowledgeLoss".Translate()))
                EditDeferred(() => deferredTerminations.AcknowledgeLoss(townLosses, Book, Find.TickManager.TicksGame));
        }
        foreach (var record in terminationHistory.Records)
            list.Label("NT_Record".Translate(Label(Book.Charter(record.Witness.Root).Terms.Subject), record.Witness.Tick, record.Witness.Children.Length));
        foreach (var record in deferredTerminations.Records.Where(r => r.Phase == 3))
            list.Label("NT_LossRecord".Translate(Label(Book.Charter(record.Request.Root).Terms.Subject), record.ClosedTick.Value));
    }
    private void EditDeferred(Func<DeferredNpcTerminations> action)
    {
        try
        {
            if (Current.Game != context || Current.ProgramState != ProgramState.Playing || RestoreFailed) throw new ArgumentException("Current request context unavailable.");
            var next = action(); next.ValidatePairs(terminationHistory, townLosses, Book);
            if (Current.Game != context) throw new ArgumentException("Game changed before saving request.");
            deferredTerminations = next;
        }
        catch (ArgumentException) { Messages.Message("NT_RequestBlocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
    public void QueueNpcTermination(string root) => EditDeferred(() => {
        var transfers = context.GetComponent<TerritoryAdministrationLedger>(); var payments = context.GetComponent<TributePaymentLedger>();
        if (transfers.RestoreFailed || payments.RestoreFailed) throw new ArgumentException("Original custody journals unavailable.");
        return deferredTerminations.Queue(new(Guid.NewGuid().ToString("N"), root, Book.Export(), transfers.Ledger.Export(), payments.Ledger.Export(), Find.TickManager.TicksGame));
    });
}
