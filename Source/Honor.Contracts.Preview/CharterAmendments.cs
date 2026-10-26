using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed record LiveCharterAmendmentReview(Game Context, CharterAmendmentPlan Plan, string Stamp, Faction Owner, Pawn Representative)
{
    public PlayerCharterRenewal PlayerRenewal { get; init; }
}

public sealed partial class LiveCharterLedger
{
    private string amendmentSnapshot;
    private CharterAmendments amendments = new();
    private void ExposeAmendments()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) amendmentSnapshot = amendments.Export();
        Scribe_Values.Look(ref amendmentSnapshot, "honorNegotiatedCharterAmendmentsV1");
        ExposePlayerRenewals();
    }
    private void ValidateAmendments()
    {
        if (amendmentSnapshot != null) amendments = CharterAmendments.Restore(amendmentSnapshot);
        amendments.ValidateBook(Book);
        ValidatePlayerRenewals();
        foreach (var p in amendments.Records)
        {
            var audit = finalConsents.Find(p.Witness.Next);
            if (audit == null || audit.NpcFaction != p.Witness.Decision.Realm || audit.Representative != p.Witness.Decision.Representative || audit.SignedTick != p.Witness.Tick)
                throw new ArgumentException("Amendment needs its matching current final-consent audit.");
        }
    }
    public LiveCharterAmendmentReview ReviewAmendment(string id, CharterOfferDraft draft, string next = null)
    {
        var game = Current.Game;
        if (context != game || Current.ProgramState != ProgramState.Playing || RestoreFailed || game.GetComponent<PoliticalTransitionLedger>().BlocksChanges)
            throw new ArgumentException("Review the current available charter game.");
        var payments = game.GetComponent<TributePaymentLedger>(); var towns = game.GetComponent<TerritoryAdministrationLedger>();
        if (payments.RestoreFailed || towns.RestoreFailed || game.GetComponent<PlayerPoliticsLedger>().RestoreFailed ||
            game.GetComponent<RealmAdministrationLedger>().RestoreFailed || game.GetComponent<UnionPoliticsLedger>().RestoreFailed)
            throw new ArgumentException("Current political or payment records unavailable.");
        var old = Book.Charter(id); bool subject = Book.Entity(old.Terms.Subject).Player;
        var binding = Binding(subject ? old.Terms.Liege : old.Terms.Subject); var town = binding.Settlement;
        if (binding.PlayerGroup || (!subject || !binding.FactionGroup) && (town == null || town.GetType() != typeof(Settlement) || town.Destroyed ||
            !Find.WorldObjects.AllWorldObjects.Contains(town) || town.Faction == null)) throw new ArgumentException("Review the actual current NPC party.");
        var owner = binding.FactionGroup ? binding.OriginalFaction : town.Faction; var decision = NpcDecisionAuthority.Read(owner);
        var plan = CharterAmendments.Review(Book, towns.Ledger, payments.Ledger, id, next ?? Guid.NewGuid().ToString("N"), draft,
            decision, owner.GoodwillWith(Faction.OfPlayer), Find.TickManager.TicksGame, Observe());
        var player = ReviewPlayerRenewal(plan, owner);
        if (player != null) _ = game.GetComponent<PoliticalTransitionLedger>().StageCharterRenewal(player);
        string stamp = JsonSerializer.Serialize(new { Plan = plan, Player = player, RenewalArchive = playerRenewals.Export(), Audit = finalConsents.Export(), Amendments = amendments.Export(),
            WarHistory = game.GetComponent<PoliticalTransitionLedger>().RenewalHistoryToken,
            Payments = payments.Ledger.Export(), Town = town?.GetUniqueLoadID(), Owner = owner.GetUniqueLoadID(), Leader = owner.leader.GetUniqueLoadID(), Observations = Observe() });
        return new(game, plan, stamp, owner, owner.leader) { PlayerRenewal = player };
    }
    public void ApplyAmendment(LiveCharterAmendmentReview reviewed) => Execute(() => {
        if (reviewed?.Context != Current.Game || reviewed.Plan?.Witness == null) throw new ArgumentException("Review first.");
        var w = reviewed.Plan.Witness; var fresh = ReviewAmendment(w.Previous, w.Draft, w.Next);
        if (fresh.Stamp != reviewed.Stamp || fresh.Owner != reviewed.Owner || fresh.Representative != reviewed.Representative)
            throw new ArgumentException("Current terms, authority or evidence changed.");
        var payments = Current.Game.GetComponent<TributePaymentLedger>(); var towns = Current.Game.GetComponent<TerritoryAdministrationLedger>();
        var result = amendments.Commit(fresh.Plan, Book, towns.Ledger, payments.Ledger, NpcDecisionAuthority.Read(fresh.Owner),
            fresh.Owner.GoodwillWith(Faction.OfPlayer), Find.TickManager.TicksGame, Observe());
        var audit = finalConsents.Record(CharterBook.Restore(fresh.Plan.Prepared), result.Book, w.Next,
            fresh.Owner.GetUniqueLoadID(), fresh.Representative.GetUniqueLoadID(), ConsentLabel(fresh.Owner.Name, fresh.Owner.GetUniqueLoadID()),
            ConsentLabel(fresh.Representative.LabelShort, fresh.Representative.GetUniqueLoadID()), Find.TickManager.TicksGame);
        audit.ValidateBook(result.Book); townLosses.ValidateBook(result.Book); ownershipReturns.ValidatePair(townLosses, result.Book);
        var renewalArchive = playerRenewals; PlayerAllegiance polity = null;
        (AllegianceTransitionLedger Journal, List<string> History) political = default;
        if (fresh.PlayerRenewal != null)
        {
            renewalArchive = playerRenewals.Append(fresh.PlayerRenewal); renewalArchive.ValidatePair(result.Archive, result.Book);
            polity = PlayerAllegiance.Restore(fresh.PlayerRenewal.After);
            political = context.GetComponent<PoliticalTransitionLedger>().StageCharterRenewal(fresh.PlayerRenewal);
            renewalArchive.ValidateCurrent(polity, political.Journal);
            context.GetComponent<CharterProtectionLedger>().Ledger.ValidateBook(result.Book);
        }
        var currentBinding = Binding(Book.Entity(Book.Charter(w.Previous).Terms.Subject).Player ? Book.Charter(w.Previous).Terms.Liege : Book.Charter(w.Previous).Terms.Subject);
        var currentOwner = currentBinding.FactionGroup ? currentBinding.OriginalFaction : currentBinding.Settlement?.Faction;
        if (fresh.Owner.leader != fresh.Representative || fresh.Representative.Faction != fresh.Owner || currentOwner != fresh.Owner)
            throw new ArgumentException("Representative or actual owner changed before commit.");
        // All paired books, history and political models have been staged; assignments invoke no native callbacks.
        Book = result.Book; amendments = result.Archive; finalConsents = audit; playerRenewals = renewalArchive;
        if (polity != null)
        {
            context.GetComponent<PlayerPoliticsLedger>().CommitCharterRenewal(polity);
            context.GetComponent<PoliticalTransitionLedger>().CommitCharterRenewal(political.Journal, political.History);
        }
        Messages.Message("CN_Completed".Translate(), MessageTypeDefOf.NeutralEvent);
    });
}

public sealed class CharterAmendmentWindow : Window
{
    private readonly Game context = Current.Game;
    private readonly string previous;
    private float value = 100; private int interval = 5, grace = 1, allowance = 10;
    private string valueText = "100", intervalText = "5", graceText = "1", allowanceText = "10";
    private LiveCharterAmendmentReview review;
    private Vector2 scroll;
    private float contentHeight = 1000;
    public CharterAmendmentWindow(string id)
    {
        previous = id; forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
        try
        {
            var d = CharterOffers.Draft(Current.Game.GetComponent<LiveCharterLedger>().Book.Charter(id).Terms);
            value = (float)d.TributeValue; interval = d.IntervalDays; grace = d.GraceDays; allowance = d.AllowancePercent;
            valueText = value.ToString(); intervalText = interval.ToString(); graceText = grace.ToString(); allowanceText = allowance.ToString();
        }
        catch (ArgumentException) { /* New standard draft, never modify uneditable old terms. */ }
    }
    public override Vector2 InitialSize => new(850, 720);
    public override void DoWindowContents(Rect rect)
    {
        if (context != Current.Game) { Widgets.Label(rect, "ND_Unavailable".Translate()); return; }
        var ledger = Current.Game.GetComponent<LiveCharterLedger>(); var old = ledger.Book.Charter(previous);
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, contentHeight));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, 8000));
        bool playerSubject = ledger.Book.Entity(old.Terms.Subject).Player;
        list.Label((playerSubject ? "CN_PlayerTitle" : "CN_Title").Translate()); list.Label((playerSubject ? "CN_PlayerLimits" : "CN_Limits").Translate());
        CharterOfferWindow.DrawTerms(list, old.Terms);
        list.Label("CO_Value".Translate(value)); list.TextFieldNumeric(ref value, ref valueText, 1, 10000); value = Mathf.Clamp(value, 1, 10000);
        list.Label("CO_Interval".Translate(interval)); list.TextFieldNumeric(ref interval, ref intervalText, 1, 60); interval = Mathf.Clamp(interval, 1, 60);
        list.Label("CO_Grace".Translate(grace)); list.TextFieldNumeric(ref grace, ref graceText, 0, 30); grace = Mathf.Clamp(grace, 0, 30);
        list.Label("CO_Allowance".Translate(allowance)); list.TextFieldNumeric(ref allowance, ref allowanceText, 0, 50); allowance = Mathf.Clamp(allowance, 0, 50);
        var draft = new CharterOfferDraft((decimal)value, interval, grace, allowance);
        if (review != null && review.Plan.Witness.Draft != draft) review = null;
        if (list.ButtonText("CN_Review".Translate()))
        {
            try { review = ledger.ReviewAmendment(previous, draft); }
            catch (ArgumentException) { review = null; Messages.Message("CN_Unavailable".Translate(), MessageTypeDefOf.RejectInput); }
        }
        if (review != null)
        {
            list.Label("CN_Ready".Translate(review.Owner.Name, review.Representative.LabelShortCap));
            if (list.ButtonText("CN_Confirm".Translate()))
            {
                var confirmed = review;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation((playerSubject ? "CN_PlayerWarning" : "CN_Warning").Translate(), () => ledger.ApplyAmendment(confirmed)));
            }
        }
        contentHeight = list.CurHeight + 20;
        list.End();
        Widgets.EndScrollView();
    }
}
