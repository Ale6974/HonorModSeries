using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

public sealed record LawfulIndependenceReview(Game Context, LawfulExitPreparation Closure, PoliticalChangeReview Admission);

public sealed partial class PoliticalTransitionLedger
{
    internal void ValidateLawfulArchive()
    {
        if (Current.Game != context || RestoreFailed) throw new ArgumentException("Original current political archive required.");
        var records = warHistory.Concat(Journal.Current == null ? Array.Empty<string>() : new[] { Journal.Export() });
        LawfulAdmissions.ValidateArchive(records, Charters.Book, context.GetComponent<TributePaymentLedger>().Ledger, context.GetComponent<CharterProtectionLedger>().Ledger);
    }
    public LawfulIndependenceReview ReviewLawfulIndependence(string request)
    {
        Ready(); if (Journal.Pending || Charters.Book.PlayerFollowers().Length != 0 || Territories.Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared))
            throw new ArgumentException("This executor requires no NPC followers and no unresolved physical transfer.");
        var payments = context.GetComponent<TributePaymentLedger>(); var protection = context.GetComponent<CharterProtectionLedger>();
        if (protection.RestoreFailed || protection.Executing) throw new ArgumentException("Original protection history unavailable.");
        ValidateLawfulArchive();
        var closure = LawfulExitPreparation.Prepare(Charters.Book, payments.Ledger, Politics.Model, Territories.Ledger, protection.Ledger,
            request, Find.TickManager.TicksGame, Charters.Observe());
        var root = Politics.UnionCandidate; Politics.CheckTransitionAdmission(root);
        var command = new AllegianceCommand(AllegianceAction.JoinUnion, Find.TickManager.TicksGame, root.GetUniqueLoadID(),
            Consent: true, NoActiveFeudalContract: true, OldContractReleased: true);
        var followers = Territories.ReviewEmptyLawfulFollowers(CharterBook.Restore(closure.AfterBook), root);
        var peace = UnionPeaceCouncil.Review(root, followers.Moves);
        if (!peace.Accepted) throw new ArgumentException("Actual membership council declined independence admission.");
        var admission = new PoliticalChangeReview(root, command, followers, Stamp(root, command, followers, peace: peace), Context: context) { Peace = peace };
        var check = AllegianceTransitionLedger.Restore(Journal.Export());
        if (Territories.Ledger.Transfers.Count == 0) check.PrepareLawfulAdmission("review-lawful", Politics.Model, command, followers, Territories.Ledger, closure, peace);
        else check.PrepareLawfulAdmissionWithHistory("review-lawful", Politics.Model, command, followers, Territories.Ledger, closure, peace);
        return new(context, closure, admission);
    }
    public void StartLawfulIndependence(LawfulIndependenceReview reviewed)
    {
        try
        {
            if (reviewed?.Context != context || Current.Game != context) throw new ArgumentException("Review this current game.");
            var fresh = ReviewLawfulIndependence(reviewed.Closure.Request);
            if (fresh.Closure.Export() != reviewed.Closure.Export() || fresh.Admission.Stamp != reviewed.Admission.Stamp ||
                fresh.Admission.Destination != reviewed.Admission.Destination || reviewed.Admission.Context != context)
                throw new ArgumentException("Original closure, current council or native relations changed.");
            var payments = context.GetComponent<TributePaymentLedger>(); var protection = context.GetComponent<CharterProtectionLedger>();
            fresh.Closure.CheckBefore(Charters.Book, payments.Ledger, Politics.Model, Territories.Ledger, protection.Ledger, Find.TickManager.TicksGame, Charters.Observe());
            var next = AllegianceTransitionLedger.Restore(Journal.Export());
            var id = Guid.NewGuid().ToString("N");
            if (Territories.Ledger.Transfers.Count == 0) next.PrepareLawfulAdmission(id, Politics.Model, fresh.Admission.Command, fresh.Admission.Followers, Territories.Ledger, fresh.Closure, fresh.Admission.Peace);
            else next.PrepareLawfulAdmissionWithHistory(id, Politics.Model, fresh.Admission.Command, fresh.Admission.Followers, Territories.Ledger, fresh.Closure, fresh.Admission.Peace);
            var nextPolitics = PlayerAllegiance.Restore(Politics.Model.Export()); nextPolitics.Apply(fresh.Admission.Command);
            next.ConfirmAllegiance(nextPolitics, Find.TickManager.TicksGame);
            var history = warHistory.ToList();
            if (Journal.Current?.Wars != null || Journal.Current?.Peace != null || Journal.Current?.Feudal != null) history.Add(Journal.Export());
            UnionWarAdmission.ValidateHistory(history, next.Current);
            var book = CharterBook.Restore(fresh.Closure.AfterBook); var tribute = TributeLedger.Restore(fresh.Closure.AfterPayments);
            LawfulAdmissions.ValidateArchive(history.Concat(new[] { next.Export() }), book, tribute, protection.Ledger);
            // Every model and paired archive is complete before the first native diplomacy callback. No callbacks between assignments.
            Charters.CommitLawfulExit(book); payments.CommitCharterRelease(tribute); Journal = next; warHistory = history; Politics.CommitCharterRenewal(nextPolitics);
            Executing = true; Finish(fresh.Admission.Destination, peace: fresh.Admission.Peace);
        }
        catch (Exception e) { Interrupted(e); }
        finally { Executing = false; }
    }
}

public sealed partial class LiveCharterLedger
{
    internal void CommitLawfulExit(CharterBook prepared) => Book = prepared;
    private void ValidateLawfulRestore()
    {
        if (RestoreFailed) return;
        try { context.GetComponent<PoliticalTransitionLedger>().ValidateLawfulArchive(); }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Unpaired lawful-exit archive; original live data retained and changes blocked."); }
    }
}

public sealed partial class TerritoryAdministrationLedger
{
    internal FollowerTransferPlan ReviewEmptyLawfulFollowers(CharterBook book, Faction target)
    {
        if (RestoreFailed || Charters.RestoreFailed || Charters.Book.PlayerFollowers().Length != 0 || book.PlayerFollowers().Length != 0 || Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared))
            throw new ArgumentException("Original resolved physical scope and no followers required.");
        return FollowerTransfers.Review(book, Ledger, Array.Empty<FollowerCandidate>(), target.GetUniqueLoadID(), Find.TickManager.TicksGame, AdministrationId);
    }
}
