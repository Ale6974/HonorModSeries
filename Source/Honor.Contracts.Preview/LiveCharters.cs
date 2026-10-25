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
    public void ExposeData()
    {
        Scribe_Values.Look(ref Identity, "identity"); Scribe_Values.Look(ref PlayerGroup, "playerGroup");
        Scribe_Values.Look(ref FactionGroup, "factionGroup");
        Scribe_References.Look(ref Settlement, "settlement"); Scribe_References.Look(ref OriginalFaction, "originalFaction");
    }
}

/// <summary>Actual references and bilateral consent. Physical and political execution use separate journals.</summary>
public sealed partial class LiveCharterLedger : GameComponent
{
    private string snapshot;
    private List<TerritoryBinding> bindings = [];
    public CharterBook Book { get; private set; } = new();
    public bool RestoreFailed { get; private set; }
    public LiveCharterLedger(Game game) { }
    public string PlayerIdentity => "player/" + Faction.OfPlayer.GetUniqueLoadID();
    internal TerritoryBinding Binding(string identity) => bindings.SingleOrDefault(b => b.Identity == identity)
        ?? throw new ArgumentException("Unknown territory binding.");
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) snapshot = Book.Export();
        Scribe_Values.Look(ref snapshot, "honorLiveChartersV1");
        Scribe_Collections.Look(ref bindings, "honorLiveTerritories", LookMode.Deep);
        ExposeNpcWorld();
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        bindings ??= [];
        try
        {
            if (snapshot != null) Book = CharterBook.Restore(snapshot);
            ValidateNpcWorld();
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
    {
        var settlements = Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => !s.Destroyed).ToArray();
        var followers = new HashSet<string>(releasedSource == null ? [] : Book.PlayerFollowers().Select(c => c.Terms.Subject), StringComparer.Ordinal);
        return bindings.Select(b => {
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
            string baseline = Book.Entity(b.Identity).NativeOwner;
            // Only this journal's completed, still-current transfer can account for an owner change.
            // Pending transfers and foreign conquests never become fresh consent observations.
            var observation = new TerritoryObservation(b.Identity, owner?.GetUniqueLoadID(), alive, owner == Faction.OfPlayer, eligible);
            return administration.RestoreFailed ? observation with { Eligible = false }
                : administration.Ledger.ObserveIdentity(observation, baseline);
        }).ToArray();
    }
    private string Register(Settlement settlement, bool player, bool factionGroup = false)
    {
        var owner = player ? Faction.OfPlayer : settlement.Faction;
        string identity = player ? PlayerIdentity : factionGroup ? "faction/" + owner.GetUniqueLoadID() : "territory/" + settlement.GetUniqueLoadID();
        if (Book.Entities.Any(e => e.Id == identity))
        {
            if (Book.Availability(identity, Observe()) != CharterAvailability.Current || Book.Entity(identity).Player != player)
                throw new ArgumentException("Existing identity is not current.");
            return identity;
        }
        Book.Register(new(identity, owner.GetUniqueLoadID(), player), Find.TickManager.TicksGame);
        if (bindings.All(b => b.Identity != identity)) bindings.Add(new() { Identity = identity,
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
    public string Label(string identity)
    {
        var binding = bindings.SingleOrDefault(b => b.Identity == identity);
        if (binding?.PlayerGroup == true) return "HCL_Player".Translate();
        if (binding?.FactionGroup == true) return binding.OriginalFaction?.Name ?? "HCL_Missing".Translate();
        return binding?.Settlement?.Label ?? "HCL_Missing".Translate();
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
    private bool Execute(Action action)
    {
        if (Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges)
        { Messages.Message("PS_Pending".Translate(), MessageTypeDefOf.RejectInput); return false; }
        if (RestoreFailed) return false;
        try { action(); return true; }
        catch (ArgumentException) { Messages.Message("HCL_Unavailable".Translate(), MessageTypeDefOf.RejectInput); return false; }
    }
    public void Request(Settlement settlement, bool playerSubject)
    {
        Execute(() => {
            if (playerSubject && settlement?.Faction?.def?.defName == RealmAdministrationLedger.DefName)
                throw new ArgumentException("The player's administration cannot become their sovereign.");
            // Recheck current ownership, political eligibility and actual representative before recording NPC consent.
            if (settlement == null || settlement.Destroyed || !Find.WorldObjects.AllWorldObjects.Contains(settlement) || settlement.Faction == null ||
                settlement.Faction == Faction.OfPlayer || !settlement.Faction.HasGoodwill || settlement.Faction.defeated ||
                settlement.Faction.HostileTo(Faction.OfPlayer) || settlement.Faction.def.permanentEnemy ||
                !Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Any(s => s.Faction == Faction.OfPlayer && !s.Destroyed))
                throw new ArgumentException("Ineligible settlement.");
            var leader = settlement.Faction.leader;
            if (leader == null || leader.Dead || leader.Faction != settlement.Faction) throw new ArgumentException("No living faction representative.");
            string player = Register(null, true), npc = Register(settlement, false, playerSubject);
            string id = Guid.NewGuid().ToString("N"); long now = Find.TickManager.TicksGame;
            var terms = new RecordedCharterTerms(playerSubject ? npc : player, playerSubject ? player : npc,
                100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m);
            var source = playerSubject ? Current.Game.GetComponent<PoliticalTransitionLedger>().ReleasedMigrationSource(settlement.Faction) : null;
            Book.Offer(id, terms, now, PoliticalWorld.Day * 3, ObserveForMigration(source));
            // Initial transparent policy, pending richer honor/obligation negotiations. No hidden random acceptance.
            int minimum = playerSubject ? 0 : 50;
            if (settlement.Faction.GoodwillWith(Faction.OfPlayer) >= minimum)
            {
                Book.Sign(id, false, now, ObserveForMigration(source));
                Messages.Message("HCL_NpcAgreed".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Book.Decline(id, now);
                Messages.Message("HCL_NpcDeclined".Translate(minimum), MessageTypeDefOf.RejectInput);
            }
        });
    }
    public void Accept(string id) => Execute(() => {
        var c = Book.Charter(id);
        string source = Book.Entity(c.Terms.Subject).Player
            ? Current.Game.GetComponent<PoliticalTransitionLedger>().ReleasedMigrationSource(CounterpartyFaction(id)) : null;
        Book.Sign(id, true, Find.TickManager.TicksGame, ObserveForMigration(source));
    });
    public void Decline(string id) => Execute(() => Book.Decline(id, Find.TickManager.TicksGame));
    public void RequestRelease(string id)
    {
        Execute(() => {
            if (Book.Charter(id).NpcAuthority != null) throw new ArgumentException("NPC subcharters require bilateral current NPC authority.");
            var release = Book.Charter(id);
            if (!Book.Entity(release.Terms.Subject).Player && Book.PlayerFollowers().Any(c => Book.IsBelow(c.Terms.Subject, release.Terms.Subject)))
            { Messages.Message("NS_ReleaseBlocked".Translate(), MessageTypeDefOf.RejectInput); return; }
            if (Current.Game.GetComponent<TributePaymentLedger>().HasPending(id)) throw new ArgumentException("Resolve pending physical delivery before release.");
            var charter = Book.Charter(id);
            string npcId = Book.Entity(charter.Terms.Liege).Player ? charter.Terms.Subject : charter.Terms.Liege;
            var binding = bindings.Single(b => b.Identity == npcId);
            var faction = binding.FactionGroup ? binding.OriginalFaction : binding.Settlement?.Faction;
            // No player-facing button can fabricate the NPC's release signature.
            if (faction?.leader == null || faction.leader.Dead || faction.leader.Faction != faction)
                throw new ArgumentException("No current NPC representative.");
            if (!charter.PlayerReleaseTick.HasValue) Book.ConsentToRelease(id, true, Find.TickManager.TicksGame, Observe());
            if (faction.GoodwillWith(Faction.OfPlayer) < 20)
            { Messages.Message("HCL_ReleaseRefused".Translate(), MessageTypeDefOf.RejectInput); return; }
            Book.ConsentToRelease(id, false, Find.TickManager.TicksGame, Observe());
            Current.Game.GetComponent<TributePaymentLedger>().Synchronize(id);
        });
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
            if (target != null && target.Faction != Faction.OfPlayer)
            {
                list.Label("HCL_Target".Translate(target.Label, target.Faction?.Name ?? "HCL_Missing".Translate()));
                list.Label("HCL_Terms".Translate());
                if (target.Faction?.def?.defName != RealmAdministrationLedger.DefName && list.ButtonText("HCL_SeekLiege".Translate())) ledger.Request(target, true);
                if (list.ButtonText("HCL_OfferProtection".Translate())) ledger.Request(target, false);
            }
            var observations = ledger.Observe(); long now = Find.TickManager.TicksGame;
            if (list.ButtonText("NS_Open".Translate())) Find.WindowStack.Add(new NpcSubchartersWindow());
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
                list.Label("HCL_Terms".Translate());
                if (phase == RecordedCharterPhase.Offered && charter.NpcAuthority == null)
                {
                    list.Label("HCL_Expiry".Translate(((charter.ExpiresTick - now) / (double)PoliticalWorld.Day).ToString("F1")));
                    if (charter.NpcSignedTick.HasValue && !charter.PlayerSignedTick.HasValue && list.ButtonText("HCL_Accept".Translate())) ledger.Accept(charter.Id);
                    if (list.ButtonText("HCL_Decline".Translate())) ledger.Decline(charter.Id);
                }
                if (phase == RecordedCharterPhase.Active && charter.NpcAuthority == null && list.ButtonText("HCL_Release".Translate())) ledger.RequestRelease(charter.Id);
                if (charter.NpcAuthority != null) list.Label("NS_NpcConsent".Translate());
                if (phase is RecordedCharterPhase.Active or RecordedCharterPhase.Released && ledger.Book.PlayerManaged(charter) &&
                    list.ButtonText("CT_Open".Translate())) Find.WindowStack.Add(new TerritoryAdministrationWindow(charter.Id));
                if (phase is RecordedCharterPhase.Active or RecordedCharterPhase.Released && ledger.Book.Entity(charter.Terms.Subject).Player)
                    TributePaymentWindow.DrawAccount(list, ledger, charter);
            }
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
