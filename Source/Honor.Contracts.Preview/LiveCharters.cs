using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed class TerritoryBinding : IExposable
{
    public string Identity;
    public Settlement Settlement;
    public Faction OriginalFaction;
    public bool PlayerGroup;
    public bool FactionGroup;
    public string LastKnownLabel;
    public void ExposeData()
    {
        Scribe_Values.Look(ref Identity, "identity"); Scribe_Values.Look(ref PlayerGroup, "playerGroup");
        Scribe_Values.Look(ref FactionGroup, "factionGroup");
        Scribe_References.Look(ref Settlement, "settlement"); Scribe_References.Look(ref OriginalFaction, "originalFaction");
        Scribe_Values.Look(ref LastKnownLabel, "lastKnownLabel");
    }
}

/// <summary>Actual references and bilateral consent. Physical and political execution use separate journals.</summary>
public sealed partial class LiveCharterLedger : GameComponent
{
    private string snapshot;
    private List<TerritoryBinding> bindings = [];
    public CharterBook Book { get; private set; } = new();
    public bool RestoreFailed { get; private set; }
    private readonly Game context;
    public LiveCharterLedger(Game game) { context = game; }
    public string PlayerIdentity => "player/" + Faction.OfPlayer.GetUniqueLoadID();
    internal TerritoryBinding Binding(string identity) => bindings.SingleOrDefault(b => b.Identity == identity)
        ?? throw new ArgumentException("Unknown territory binding.");
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) snapshot = Book.Export();
        Scribe_Values.Look(ref snapshot, "honorLiveChartersV1");
        Scribe_Collections.Look(ref bindings, "honorLiveTerritories", LookMode.Deep);
        ExposeNpcWorld();
        ExposeInvitations();
        ExposeFinalConsents();
        ExposeTownLosses();
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        bindings ??= [];
        try
        {
            if (snapshot != null) Book = CharterBook.Restore(snapshot);
            // Lawful closure markers are paired against all restored political records in FinalizeInit.
            ValidateNpcWorld();
            ValidateFinalConsents();
            ValidateTownLosses();
            if (bindings.Any(b => b == null || string.IsNullOrWhiteSpace(b.Identity)) ||
                bindings.GroupBy(b => b.Identity, StringComparer.Ordinal).Any(g => g.Count() != 1) ||
                Book.Entities.Any(e => bindings.All(b => b.Identity != e.Id)) || bindings.Count != Book.Entities.Count ||
                bindings.Any(b => Book.Entity(b.Identity).Player != b.PlayerGroup || b.PlayerGroup && b.FactionGroup))
                throw new ArgumentException("Invalid territory binding registry.");
        }
        catch (Exception e) when (e is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
        {
            RestoreFailed = true;
            Log.Error("[Honor.Contracts.Preview] Live charter restore rejected; original data preserved. " + e.GetType().Name);
        }
    }
    public TerritoryObservation[] Observe() => ObserveForMigration(null);
    internal TerritoryObservation[] ObserveForMigration(string releasedSource)
        => ObserveForMigration(releasedSource, Book, bindings);
    private TerritoryObservation[] ObserveForMigration(string releasedSource, CharterBook book, List<TerritoryBinding> registry)
    {
        var settlements = Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => !s.Destroyed).ToArray();
        var followers = new HashSet<string>(releasedSource == null ? [] : book.PlayerFollowers().Select(c => c.Terms.Subject), StringComparer.Ordinal);
        return registry.Select(b => {
            if (b.PlayerGroup)
            {
                bool exists = b.Identity == PlayerIdentity && settlements.Any(s => s.Faction == Faction.OfPlayer);
                return new TerritoryObservation(b.Identity, Faction.OfPlayer.GetUniqueLoadID(), exists, true, exists);
            }
            if (b.FactionGroup)
            {
                var faction = b.OriginalFaction;
                bool exists = faction != null && !faction.defeated && Find.FactionManager.AllFactionsListForReading.Contains(faction) &&
                    b.Identity == "faction/" + faction.GetUniqueLoadID();
                bool factionEligible = exists && faction != Faction.OfPlayer && faction.HasGoodwill &&
                    !faction.def.permanentEnemy && !faction.HostileTo(Faction.OfPlayer);
                return new TerritoryObservation(b.Identity, faction?.GetUniqueLoadID(), exists, faction == Faction.OfPlayer, factionEligible);
            }
            var settlement = b.Settlement;
            bool alive = settlement != null && settlements.Contains(settlement) &&
                b.Identity == "territory/" + settlement.GetUniqueLoadID();
            var owner = alive ? settlement.Faction : null;
            bool eligible = owner != null && owner != Faction.OfPlayer && owner.HasGoodwill && !owner.defeated &&
                !owner.def.permanentEnemy && (!owner.HostileTo(Faction.OfPlayer) || owner.GetUniqueLoadID() == releasedSource && followers.Contains(b.Identity));
            var administration = Current.Game.GetComponent<TerritoryAdministrationLedger>();
            string baseline = book.Entity(b.Identity).NativeOwner;
            // Only this journal's completed, still-current transfer can account for an owner change.
            // Pending transfers and foreign conquests never become fresh consent observations.
            var observation = new TerritoryObservation(b.Identity, owner?.GetUniqueLoadID(), alive, owner == Faction.OfPlayer, eligible);
            return administration.RestoreFailed || administration.IndependentlyLostTerritory(b.Identity) ? observation with { Eligible = false }
                : administration.Ledger.ObserveIdentity(observation, baseline);
        }).ToArray();
    }
    private string Register(Settlement settlement, bool player, bool factionGroup = false)
        => Register(Book, bindings, settlement, player, factionGroup);
    private string Register(CharterBook book, List<TerritoryBinding> registry, Settlement settlement, bool player, bool factionGroup)
    {
        var owner = player ? Faction.OfPlayer : settlement.Faction;
        string identity = player ? PlayerIdentity : factionGroup ? "faction/" + owner.GetUniqueLoadID() : "territory/" + settlement.GetUniqueLoadID();
        if (book.Entities.Any(e => e.Id == identity))
        {
            if (book.Availability(identity, ObserveForMigration(null, book, registry)) != CharterAvailability.Current || book.Entity(identity).Player != player)
                throw new ArgumentException("Existing identity is not current.");
            return identity;
        }
        book.Register(new(identity, owner.GetUniqueLoadID(), player), Find.TickManager.TicksGame);
        if (registry.All(b => b.Identity != identity)) registry.Add(new() { Identity = identity,
            Settlement = player || factionGroup ? null : settlement, OriginalFaction = owner, PlayerGroup = player, FactionGroup = factionGroup });
        return identity;
    }
    public void EnsurePlayerIdentity()
    {
        if (RestoreFailed || !Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Any(s => !s.Destroyed && s.Faction == Faction.OfPlayer))
            throw new ArgumentException("No current player settlement.");
        Register(null, true);
    }
    internal void ReplaceForSubtree(SubtreeReleasePlan plan)
    {
        Book = SubtreeRelease.Commit(Book, plan, Find.TickManager.TicksGame, Observe());
    }
    internal void ReplaceAfterPendingTownLoss(string before, CharterBook after)
    {
        if (RestoreFailed || Book.Export() != before) throw new ArgumentException("Original consent changed.");
        townLosses.ValidateBook(after); Book = after;
    }
    public string Label(string identity)
    {
        var binding = bindings.SingleOrDefault(b => b.Identity == identity);
        if (binding?.PlayerGroup == true) return "HCL_Player".Translate();
        if (binding?.FactionGroup == true) return binding.OriginalFaction?.Name ?? "HCL_Missing".Translate();
        return binding?.Settlement?.Label ?? binding?.LastKnownLabel ?? "HCL_Missing".Translate();
    }
    public string PoliticalOwner(string identity)
    {
        var root = Book.Root(identity);
        return Label(root);
    }
    public Faction CounterpartyFaction(string charterId)
    {
        var charter = Book.Charter(charterId);
        string npc = Book.Entity(charter.Terms.Liege).Player ? charter.Terms.Subject : charter.Terms.Liege;
        var binding = bindings.Single(b => b.Identity == npc);
        return binding.FactionGroup ? binding.OriginalFaction : binding.Settlement?.Faction;
    }
    private bool Execute(Action action, bool resolvingLoss = false)
    {
        if (context != Current.Game || Current.ProgramState != ProgramState.Playing)
        { Messages.Message("ND_Unavailable".Translate(), MessageTypeDefOf.RejectInput); return false; }
        var transition = Current.Game.GetComponent<PoliticalTransitionLedger>();
        if (resolvingLoss ? transition.BlocksApartFromTownLosses : transition.BlocksChanges)
        { Messages.Message((UnresolvedTownLosses ? "LS_Freeze" : "PS_Pending").Translate(), MessageTypeDefOf.RejectInput); return false; }
        if (RestoreFailed) return false;
        try { action(); return true; }
        catch (NpcDecisionBlocked e) { Messages.Message(e.Key.Translate(), MessageTypeDefOf.RejectInput); return false; }
        catch (ArgumentException) { Messages.Message("HCL_Unavailable".Translate(), MessageTypeDefOf.RejectInput); return false; }
    }
    public void Request(Settlement settlement, bool playerSubject) => Request(settlement, playerSubject, new(), settlement?.Faction, settlement?.Faction?.leader);
    public bool Request(Settlement settlement, bool playerSubject, CharterOfferDraft draft, Faction reviewedOwner, Pawn reviewedLeader)
    {
        return Execute(() => {
            var payments = Current.Game.GetComponent<TributePaymentLedger>();
            if (payments.RestoreFailed || payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared))
                throw new ArgumentException("Resolve reserved physical deliveries before a new proposal.");
            if (playerSubject && settlement?.Faction?.def?.defName == RealmAdministrationLedger.DefName)
                throw new ArgumentException("The player's administration cannot become their sovereign.");
            if (settlement == null || settlement.Destroyed || !Find.WorldObjects.AllWorldObjects.Contains(settlement) || settlement.Faction == null ||
                settlement.Faction == Faction.OfPlayer || !settlement.Faction.HasGoodwill || settlement.Faction.defeated ||
                settlement.Faction.HostileTo(Faction.OfPlayer) || settlement.Faction.def.permanentEnemy ||
                !Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Any(s => s.Faction == Faction.OfPlayer && !s.Destroyed))
                throw new ArgumentException("Ineligible settlement.");
            var leader = settlement.Faction.leader;
            if (leader == null || leader.Dead || leader.Faction != settlement.Faction || settlement.Faction != reviewedOwner || leader != reviewedLeader)
                throw new ArgumentException("No matching living faction representative.");
            // Stage all registrations and the NPC response. Failed validation cannot leak identities or partial offers.
            var copy = CharterBook.Restore(Book.Export()); var registry = bindings.ToList();
            string player = Register(copy, registry, null, true, false), npc = Register(copy, registry, settlement, false, playerSubject);
            string id = Guid.NewGuid().ToString("N"); long now = Find.TickManager.TicksGame;
            var source = playerSubject ? Current.Game.GetComponent<PoliticalTransitionLedger>().ReleasedMigrationSource(settlement.Faction) : null;
            var result = CharterOffers.Propose(copy, id, player, npc, playerSubject, draft, settlement.Faction.GoodwillWith(Faction.OfPlayer), now,
                ObserveForMigration(source, copy, registry), payments.Ledger);
            Book = result.Book; bindings = registry;
            Messages.Message(result.Assessment.Accepted ? "HCL_NpcAgreed".Translate() :
                "CO_Declined".Translate(CharterOfferWindow.Reason(result.Assessment)), result.Assessment.Accepted ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput);
        });
    }
    public void Accept(string id) => Execute(() => {
        var c = Book.Charter(id);
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (payments.RestoreFailed || payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared))
            throw new ArgumentException("Resolve reserved physical deliveries before consent.");
        var currentResponse = AssessOffer(id);
        if (!currentResponse.Accepted)
        { Messages.Message("CO_Declined".Translate(CharterOfferWindow.Reason(currentResponse)), MessageTypeDefOf.RejectInput); return; }
        var representativeFaction = CounterpartyFaction(id); var representative = representativeFaction.leader;
        string source = Book.Entity(c.Terms.Subject).Player
            ? Current.Game.GetComponent<PoliticalTransitionLedger>().ReleasedMigrationSource(CounterpartyFaction(id)) : null;
        var result = CharterOffers.Confirm(Book, id, CounterpartyFaction(id).GoodwillWith(Faction.OfPlayer),
            Find.TickManager.TicksGame, ObserveForMigration(source), payments.Ledger);
        if (!result.Assessment.Accepted) throw new ArgumentException("NPC response changed.");
        if (representative == null || representative.Dead || representativeFaction.leader != representative || representative.Faction != representativeFaction ||
            CounterpartyFaction(id) != representativeFaction) throw new ArgumentException("Current representative changed before final consent.");
        var consent = finalConsents.Record(Book, result.Book, id, representativeFaction.GetUniqueLoadID(), representative.GetUniqueLoadID(),
            ConsentLabel(representativeFaction.Name, representativeFaction.GetUniqueLoadID()), ConsentLabel(representative.LabelShortCap.ToString(), representative.GetUniqueLoadID()),
            Find.TickManager.TicksGame);
        finalConsents = consent;
        Book = result.Book;
    });
    public void Decline(string id) => Execute(() => {
        if (Book.Charter(id).NpcAuthority != null) throw new ArgumentException("Player response cannot withdraw NPC-only offers.");
        Book.Decline(id, Find.TickManager.TicksGame);
    }, resolvingLoss: true);
    public void RequestRelease(string id)
    {
        Execute(() => {
            var review = ReviewRelease(id); var game = context;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CR_Confirm".Translate(review.Owner.Name, review.Leader.LabelShortCap), () => Execute(() => {
                if (game != Current.Game) throw new ArgumentException("Review the current game.");
                var fresh = ReviewRelease(id);
                if (fresh != review) throw new ArgumentException("Release authority, terms or custody changed.");
                var next = CharterBook.Restore(fresh.Plan.After); var payments = TributeLedger.Restore(fresh.Plan.PaymentsAfter);
                finalConsents.ValidateBook(next); amendments.ValidateBook(next); playerRenewals.ValidatePair(amendments, next);
                townLosses.ValidateBook(next); ownershipReturns.ValidatePair(townLosses, next);
                context.GetComponent<CharterProtectionLedger>().Ledger.ValidateBook(next);
                // Both models are validated before either assignment; no native callbacks or physical goods writes.
                Book = next; context.GetComponent<TributePaymentLedger>().CommitCharterRelease(payments);
            })));
        });
    }
    private (CharterReleaseReview Plan, Faction Owner, Pawn Leader) ReviewRelease(string id)
    {
        var payments = context.GetComponent<TributePaymentLedger>(); var towns = context.GetComponent<TerritoryAdministrationLedger>();
        if (payments.RestoreFailed || towns.RestoreFailed || context.GetComponent<PlayerPoliticsLedger>().RestoreFailed ||
            context.GetComponent<RealmAdministrationLedger>().RestoreFailed || context.GetComponent<UnionPoliticsLedger>().RestoreFailed)
            throw new ArgumentException("Current political or payment records unavailable.");
        var c = Book.Charter(id); var binding = Binding(Book.Entity(c.Terms.Subject).Player ? c.Terms.Liege : c.Terms.Subject);
        var owner = binding.FactionGroup ? binding.OriginalFaction : binding.Settlement?.Faction;
        var decision = NpcDecisionAuthority.Read(owner);
        var plan = CharterReleases.Review(Book, payments.Ledger, towns.Ledger, id, decision, Find.TickManager.TicksGame, Observe());
        return (plan, owner, owner.leader);
    }
}

public sealed class LiveChartersWindow : Window
{
    private readonly Settlement target;
    private Vector2 scroll;
    private float height = 1200;
    public LiveChartersWindow(Settlement settlement = null)
    { target = settlement; forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override Vector2 InitialSize => new(850, 760);
    public override void DoWindowContents(Rect rect)
    {
        var ledger = Current.Game.GetComponent<LiveCharterLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, Mathf.Max(3000, height + 1000)));
        list.Label("HCL_Warning".Translate()); list.Gap();
        if (ledger.RestoreFailed) list.Label("HCP_RestoreFailed".Translate());
        else
        {
            list.CheckboxLabeled("LI_Receive".Translate(), ref ledger.ReceiveLiegeInvitations);
            list.Label("LI_Policy".Translate());
            var reminders = Current.Game.GetComponent<TributeReminderLedger>();
            if (!reminders.RestoreFailed)
            {
                bool enabled = reminders.Model.Enabled;
                list.CheckboxLabeled("PH_ReceiveNotices".Translate(), ref enabled);
                reminders.Model.SetEnabled(enabled, Find.TickManager.TicksGame);
                if (reminders.Model.Notices.Count >= TributeNotices.MaxNotices) list.Label("PH_NoticeCapacity".Translate());
            }
            else list.Label("PH_NoticeUnavailable".Translate());
            if (target != null && target.Faction != Faction.OfPlayer)
            {
                list.Label("HCL_Target".Translate(target.Label, target.Faction?.Name ?? "HCL_Missing".Translate()));
                list.Label("HCL_Terms".Translate());
                if (target.Faction?.def?.defName != RealmAdministrationLedger.DefName && list.ButtonText("HCL_SeekLiege".Translate())) Find.WindowStack.Add(new CharterOfferWindow(target, true));
                if (list.ButtonText("HCL_OfferProtection".Translate())) Find.WindowStack.Add(new CharterOfferWindow(target, false));
            }
            var observations = ledger.Observe(); long now = Find.TickManager.TicksGame;
            if (list.ButtonText("NS_Open".Translate())) Find.WindowStack.Add(new NpcSubchartersWindow());
            if (list.ButtonText("LS_Open".Translate())) Find.WindowStack.Add(new TownSuccessionWindow());
            if (list.ButtonText("SX_Open".Translate())) Find.WindowStack.Add(new SubtreeExitWindow());
            if (list.ButtonText("NW_Open".Translate())) Find.WindowStack.Add(new NpcWorldSetupWindow());
            foreach (var entity in ledger.Book.Entities)
                list.Label("HCL_Territory".Translate(ledger.Label(entity.Id), ledger.PoliticalOwner(entity.Id),
                    ("HCL_" + ledger.Book.Availability(entity.Id, observations)).Translate()));
            if (ledger.Book.Charters.Count == 0) list.Label("HCL_Empty".Translate());
            foreach (var charter in ledger.Book.Charters.Reverse().ToArray())
            {
                list.Gap();
                var phase = ledger.Book.Phase(charter, now);
                list.Label("HCL_Charter".Translate(ledger.Label(charter.Terms.Liege), ledger.Label(charter.Terms.Subject), ("HCL_" + phase).Translate()));
                CharterOfferWindow.DrawTerms(list, charter.Terms);
                ledger.DrawFinalConsent(list, charter.Id);
                if (phase == RecordedCharterPhase.Offered && charter.NpcAuthority == null)
                {
                    try { list.Label("PH_CurrentResponse".Translate(CharterOfferWindow.Reason(ledger.AssessOffer(charter.Id, false)))); }
                    catch (ArgumentException) { list.Label("PH_Unavailable".Translate()); }
                    list.Label("HCL_Expiry".Translate(((charter.ExpiresTick - now) / (double)PoliticalWorld.Day).ToString("F1")));
                    bool bound = ledger.Book.Charters.Any(c => c.Phase == RecordedCharterPhase.Active && c.Terms.Subject == charter.Terms.Subject);
                    if (charter.ConditionalOffer) list.Label("CO_Conditional".Translate());
                    if (bound) list.Label("CO_ReleaseFirst".Translate());
                    if (!bound && charter.NpcSignedTick.HasValue && !charter.PlayerSignedTick.HasValue && list.ButtonText("HCL_Accept".Translate())) ledger.Accept(charter.Id);
                    if (list.ButtonText("HCL_Decline".Translate())) ledger.Decline(charter.Id);
                }
                if (phase == RecordedCharterPhase.Active && charter.NpcAuthority == null && list.ButtonText("HCL_Release".Translate())) ledger.RequestRelease(charter.Id);
                if (phase == RecordedCharterPhase.Active && charter.NpcAuthority == null && list.ButtonText("CN_Open".Translate()))
                    Find.WindowStack.Add(new CharterAmendmentWindow(charter.Id));
                if (charter.NpcAuthority != null) list.Label("NS_NpcConsent".Translate());
                if (phase is RecordedCharterPhase.Active or RecordedCharterPhase.Released && charter.NpcAuthority == null && ledger.Book.Entity(charter.Terms.Subject).Player &&
                    list.ButtonText("CP_Open".Translate())) Find.WindowStack.Add(new CharterProtectionWindow(charter.Id));
                if (phase is RecordedCharterPhase.Active or RecordedCharterPhase.Released && ledger.Book.PlayerManaged(charter) &&
                    list.ButtonText("CT_Open".Translate())) Find.WindowStack.Add(new TerritoryAdministrationWindow(charter.Id));
                if (phase is RecordedCharterPhase.Active or RecordedCharterPhase.Released && ledger.Book.Entity(charter.Terms.Subject).Player)
                    TributePaymentWindow.DrawAccount(list, ledger, charter);
            }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
