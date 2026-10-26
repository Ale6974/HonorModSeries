using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed class LeaderRelocationRecord : IExposable
{
    public string Id, Snapshot, SourceLabel, DestinationLabel, LeaderLabel;
    public Pawn Leader;
    public Settlement Source, Destination;
    public Faction Faction;
    public LeaderRelocationPhase Phase;
    public long Finished = -1;
    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id"); Scribe_Values.Look(ref Snapshot, "retainedOfficeAgreementV1");
        Scribe_Values.Look(ref SourceLabel, "sourceLabel"); Scribe_Values.Look(ref DestinationLabel, "destinationLabel"); Scribe_Values.Look(ref LeaderLabel, "leaderLabel");
        Scribe_Values.Look(ref Phase, "phase"); Scribe_Values.Look(ref Finished, "finished", -1);
        Scribe_References.Look(ref Leader, "leader"); Scribe_References.Look(ref Source, "source"); Scribe_References.Look(ref Destination, "destination");
        Scribe_References.Look(ref Faction, "faction");
    }
    public LeaderRelocationPlan Plan() => LeaderRelocationPlan.Restore(Snapshot);
}
public sealed record LeaderRelocationReview(Settlement Source, Settlement Destination, Pawn Leader, Faction Faction, LeaderRelocationPlan Plan);

/// <summary>Explicit NPC retention/relocation agreement. No citizenship change, removal from office or succession.</summary>
public sealed class LeaderRelocationLedger : GameComponent
{
    private List<LeaderRelocationRecord> records = [];
    public bool RestoreFailed { get; private set; }
    public bool BlocksChanges => RestoreFailed || records == null || records.Any(r => r?.Phase == LeaderRelocationPhase.Prepared);
    internal bool ReservesResident(Pawn p) => records?.Any(r => r?.Leader == p && (RestoreFailed || r.Phase == LeaderRelocationPhase.Prepared)) == true;
    private LiveCharterLedger Charters => Current.Game.GetComponent<LiveCharterLedger>();
    public LeaderRelocationLedger(Game game) { }
    public override void ExposeData()
    {
        Scribe_Collections.Look(ref records, "honorLeaderRelocationsV1", LookMode.Deep);
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        records ??= [];
        try
        {
            if (records.Count > 200 || records.Any(r => r == null || string.IsNullOrWhiteSpace(r.Id) || !RuntimeCompat.IsDefined(r.Phase)) ||
                records.Select(r => r.Id).Distinct().Count() != records.Count || records.Count(r => r.Phase == LeaderRelocationPhase.Prepared) > 1)
                throw new ArgumentException("Invalid retained-office registry.");
            foreach (var r in records)
            {
                var p = r.Plan();
                if (r.Phase == LeaderRelocationPhase.Prepared ? r.Finished != -1 : r.Finished < p.Tick)
                    throw new ArgumentException("Invalid relocation clock.");
                if (r.Phase == LeaderRelocationPhase.Prepared) References(r);
                else r.Leader = null;
            }
        }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Retained-office relocation restore unavailable; originals retained and edits blocked."); }
    }
    public override void FinalizeInit()
    {
        if (RestoreFailed) return;
        try
        {
            if (records.Any(r => r.Plan().Tick > Find.TickManager.TicksGame || r.Finished > Find.TickManager.TicksGame))
                throw new ArgumentException("Future relocation history.");
            foreach (var r in records.Where(r => r.Phase == LeaderRelocationPhase.Prepared)) References(r);
        }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Retained-office relocation references/clock unavailable; no automatic recovery."); }
    }
    private static Settlement[] Cities() => Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => !s.Destroyed).ToArray();
    private void Ready(bool recovering = false)
    {
        var political = Current.Game.GetComponent<PoliticalTransitionLedger>(); var territory = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (RestoreFailed || Current.Game.GetComponent<LeaderJoinLedger>().BlocksChanges || !recovering && BlocksChanges || Charters.RestoreFailed || Charters.UnresolvedTownLosses || political.RestoreFailed || political.Journal.Pending ||
            Current.Game.GetComponent<SubtreeExitLedger>().BlocksChanges || territory.RestoreFailed || territory.Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) ||
            payments.RestoreFailed || payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) ||
            Current.Game.GetComponent<PlayerPoliticsLedger>().RestoreFailed || Current.Game.GetComponent<RealmAdministrationLedger>().RestoreFailed ||
            Current.Game.GetComponent<UnionPoliticsLedger>().RestoreFailed)
            throw new ArgumentException("Resolve political and payment journals first.");
        ResidentExecution.EnsureRetention();
    }
    private void Town(Settlement city, Faction faction)
    {
        if (city == null || city.GetType() != typeof(Settlement) || city.Destroyed || city.HasMap || !city.def.canHaveFaction || city.Faction != faction ||
            !Cities().Contains(city) || city.previouslyGeneratedInhabitants == null || city.previouslyGeneratedInhabitants.Count > 128 ||
            city.previouslyGeneratedInhabitants.Any(p => p == null) || city.previouslyGeneratedInhabitants.Distinct().Count() != city.previouslyGeneratedInhabitants.Count ||
            faction == null || faction.IsPlayer || faction.defeated || faction.Hidden || faction.temporary || !faction.HasGoodwill || faction.def.permanentEnemy ||
            faction.HostileTo(Faction.OfPlayer) || !Find.FactionManager.AllFactionsListForReading.Contains(faction))
            throw new ArgumentException("Unsupported relocation town or faction.");
    }
    private Settlement Source(string charter)
    {
        var c = Charters.Book.Charter(charter); var binding = Charters.Binding(c.Terms.Subject);
        if (Charters.Book.Phase(c, Find.TickManager.TicksGame) != RecordedCharterPhase.Active || !Charters.Book.PlayerManaged(c) || binding.PlayerGroup || binding.FactionGroup)
            throw new ArgumentException("An active player-managed NPC town charter is required.");
        Charters.Book.ValidateCurrent(charter, Charters.Observe());
        return binding.Settlement ?? throw new ArgumentException("Missing charter town.");
    }
    private void Destination(Settlement city)
    {
        string id = "territory/" + city.GetUniqueLoadID();
        if (Charters.Book.Charters.Any(c => Charters.Book.Phase(c, Find.TickManager.TicksGame) is RecordedCharterPhase.Active or RecordedCharterPhase.Offered &&
            (c.Terms.Subject == id || c.Terms.Liege == id)))
            throw new ArgumentException("Destination must remain outside current town contracts and proposals.");
    }
    private static string[] Roster(Settlement s) => s.previouslyGeneratedInhabitants.Select(p => p?.GetUniqueLoadID()).ToArray();
    private static string Stock(Settlement city, out int format) => CityStockPreservation.Capture(city, out format)
        ?? throw new ArgumentException("Stock preservation cannot be verified.");
    public LeaderRelocationReview Review(string charter, Settlement destination)
    {
        Ready(); if (records.Count >= 200) throw new ArgumentException("Relocation archive full.");
        var source = Source(charter); var faction = source.Faction; Town(source, faction); Town(destination, faction); Destination(destination);
        var leader = faction.leader; var actor = ResidentExecution.ReadRetainedLeader(leader);
        if (source == destination || source.previouslyGeneratedInhabitants.Count(p => p == leader) != 1 || destination.previouslyGeneratedInhabitants.Count >= 128 ||
            Cities().Any(s => s != source && s.previouslyGeneratedInhabitants?.Contains(leader) == true))
            throw new ArgumentException("Leader must reside only at the source town.");
        var sourceStock = Stock(source, out int sourceFormat); var destinationStock = Stock(destination, out int destinationFormat);
        var plan = LeaderRelocationPlan.Capture(charter, leader.GetUniqueLoadID(), faction.GetUniqueLoadID(), source.GetUniqueLoadID(), destination.GetUniqueLoadID(),
            Charters.Book.Export(), actor, sourceStock, sourceFormat, destinationStock, destinationFormat, Roster(source), Roster(destination), Find.TickManager.TicksGame);
        return new(source, destination, leader, faction, plan);
    }
    private static void References(LeaderRelocationRecord r)
    {
        var p = r.Plan();
        if (r.Leader == null || r.Leader.GetUniqueLoadID() != p.Leader || r.Source == null || r.Source.GetUniqueLoadID() != p.Source ||
            r.Destination == null || r.Destination.GetUniqueLoadID() != p.Destination || r.Faction == null || r.Faction.GetUniqueLoadID() != p.Faction)
            throw new ArgumentException("Relocation identity references changed.");
    }
    private LeaderRelocationAction Review(LeaderRelocationRecord r)
    {
        Ready(true); References(r); var plan = r.Plan();
        if (r.Phase != LeaderRelocationPhase.Prepared || Source(plan.Charter) != r.Source) throw new ArgumentException("No matching pending relocation.");
        Town(r.Source, r.Faction); Town(r.Destination, r.Faction); Destination(r.Destination);
        if (Cities().Any(s => s != r.Source && s != r.Destination && s.previouslyGeneratedInhabitants?.Contains(r.Leader) == true))
            throw new ArgumentException("Leader acquired an unrelated residency.");
        var sourceStock = Stock(r.Source, out int sourceFormat); var destinationStock = Stock(r.Destination, out int destinationFormat);
        return plan.Review(ResidentExecution.ReadRetainedLeader(r.Leader), r.Faction.leader?.GetUniqueLoadID(), r.Source.Faction.GetUniqueLoadID(), r.Destination.Faction.GetUniqueLoadID(),
            Charters.Book.Export(), sourceStock, sourceFormat, destinationStock, destinationFormat, Roster(r.Source), Roster(r.Destination));
    }
    public void Start(LeaderRelocationReview reviewed)
    {
        try
        {
            var current = Review(reviewed.Plan.Charter, reviewed.Destination);
            if (current.Plan.Proof != reviewed.Plan.Proof || current.Leader != reviewed.Leader || current.Faction != reviewed.Faction || current.Source != reviewed.Source)
                throw new ArgumentException("Reviewed agreement changed.");
            // Explicit request: this limited NPC policy accepts only retention of its own office/faction and a verified own town.
            // This is a relocation agreement, never personal foreign-join consent or proof of an earlier refusal.
            var r = new LeaderRelocationRecord { Id = Guid.NewGuid().ToString("N"), Snapshot = current.Plan.Export(), Leader = current.Leader,
                Source = current.Source, Destination = current.Destination, Faction = current.Faction,
                SourceLabel = current.Source.Label, DestinationLabel = current.Destination.Label, LeaderLabel = current.Leader.LabelShort };
            records.Add(r); Finish(r);
        }
        catch (Exception e) { Interrupted(e); }
    }
    private void Finish(LeaderRelocationRecord r)
    {
        var action = Review(r); Find.WorldPawns.gc.CancelGCPass();
        if (action == LeaderRelocationAction.AddDestination)
        {
            r.Destination.previouslyGeneratedInhabitants.Capacity = Math.Max(r.Destination.previouslyGeneratedInhabitants.Capacity, r.Destination.previouslyGeneratedInhabitants.Count + 1);
            r.Destination.previouslyGeneratedInhabitants.Add(r.Leader); action = Review(r);
        }
        if (action == LeaderRelocationAction.RemoveSource)
        {
            if (!r.Source.previouslyGeneratedInhabitants.Remove(r.Leader)) throw new ArgumentException("Source residency disappeared.");
            action = Review(r);
        }
        if (action != LeaderRelocationAction.Verify) throw new ArgumentException("Leader residency incomplete.");
        r.Phase = LeaderRelocationPhase.Completed; r.Finished = Find.TickManager.TicksGame; r.Leader = null;
        Messages.Message("LR_Completed".Translate(r.LeaderLabel, r.DestinationLabel), MessageTypeDefOf.NeutralEvent);
    }
    public void Recover(string id, bool cancel)
    {
        try
        {
            var r = records.Single(x => x.Id == id); var action = Review(r);
            if (!cancel) Finish(r);
            else
            {
                if (action != LeaderRelocationAction.AddDestination) throw new ArgumentException("Cannot cancel a residency already changed.");
                r.Phase = LeaderRelocationPhase.Cancelled; r.Finished = Find.TickManager.TicksGame; r.Leader = null;
            }
        }
        catch (Exception e) { Interrupted(e); }
    }
    private static void Interrupted(Exception e)
    {
        Log.Warning("[Honor.Contracts.Preview] Retained-office relocation unavailable/incomplete; references and intent retained. " + e.GetType().Name);
        Messages.Message("LR_Unavailable".Translate(), MessageTypeDefOf.RejectInput); Find.TickManager.Pause();
    }
    internal void Draw(Listing_Standard list, string charter)
    {
        list.Label("LR_Limits".Translate());
        if (RestoreFailed) { list.Label("HCP_RestoreFailed".Translate()); return; }
        foreach (var pending in records.Where(r => r.Phase == LeaderRelocationPhase.Prepared))
        {
            list.Label("LR_Pending".Translate(pending.LeaderLabel, pending.SourceLabel, pending.DestinationLabel));
            if (list.ButtonText("LR_Recover".Translate())) Recover(pending.Id, false);
            if (list.ButtonText("LR_Cancel".Translate())) Recover(pending.Id, true);
        }
        if (BlocksChanges) return;
        if (list.ButtonText("LR_Review".Translate()))
        {
            var options = new List<FloatMenuOption>();
            foreach (var city in Cities())
            {
                try
                {
                    var review = Review(charter, city);
                    options.Add(new FloatMenuOption("LR_Proposal".Translate(review.Leader.LabelShort, city.Label), () => Find.WindowStack.Add(
                        Dialog_MessageBox.CreateConfirmation("LR_Confirm".Translate(review.Leader.LabelShort, review.Source.Label, city.Label), () => Start(review)))));
                }
                catch (ArgumentException) { }
            }
            if (options.Count == 0) Messages.Message("LR_Unavailable".Translate(), MessageTypeDefOf.RejectInput);
            else Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
