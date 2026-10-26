using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

public sealed partial class LiveCharterLedger
{
    private string playerRenewalSnapshot;
    private PlayerCharterRenewals playerRenewals = new();
    private void ExposePlayerRenewals()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) playerRenewalSnapshot = playerRenewals.Export();
        Scribe_Values.Look(ref playerRenewalSnapshot, "honorSameLiegeCharterRenewalsV1");
    }
    private void ValidatePlayerRenewals()
    {
        if (playerRenewalSnapshot != null) playerRenewals = PlayerCharterRenewals.Restore(playerRenewalSnapshot);
        playerRenewals.ValidatePair(amendments, Book);
    }
    private void ValidatePlayerRenewalLineage()
    {
        if (RestoreFailed || playerRenewals.Records.Count == 0) return;
        try
        {
            var politics = context.GetComponent<PlayerPoliticsLedger>(); var transition = context.GetComponent<PoliticalTransitionLedger>();
            if (politics.RestoreFailed || transition.RestoreFailed) throw new ArgumentException("Original political ledger unavailable.");
            playerRenewals.ValidateCurrent(politics.RenewalModel, transition.Journal);
            transition.ValidateCharterRenewalHistory(playerRenewals.Records);
        }
        catch (Exception e) when (e is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Player charter renewal lineage rejected; original snapshots retained. " + e.GetType().Name); }
    }
    private PlayerCharterRenewal ReviewPlayerRenewal(CharterAmendmentPlan plan, Faction owner)
    {
        if (!Book.Entity(Book.Charter(plan.Witness.Previous).Terms.Subject).Player) return null;
        var politics = context.GetComponent<PlayerPoliticsLedger>(); var transition = context.GetComponent<PoliticalTransitionLedger>();
        if (politics.RestoreFailed || transition.RestoreFailed || politics.State.Mode != PlayerAllegianceMode.Vassal || politics.State.Suspended ||
            politics.State.Contract != plan.Witness.Previous || politics.TransitionAuthority() != owner)
            throw new ArgumentException("Review the actual currently shared same-liege player contract.");
        var followers = context.GetComponent<TerritoryAdministrationLedger>().ReviewFollowersFor(owner);
        return PlayerCharterRenewals.Stage(plan, politics.RenewalModel, transition.Journal, followers.Entries);
    }
}
