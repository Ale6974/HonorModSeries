using Honor.Contracts.Rules;
using Verse;
using RimWorld;
using System.Text.Json;

namespace Honor.Contracts.Preview;

public sealed partial class LiveCharterLedger
{
    private string finalConsentSnapshot;
    private CharterAcceptances finalConsents = new();
    private InitialCharterMandates initialMandates = new();
    private string initialMandateSnapshot;
    private List<string> initialMandatePairs = [];
    private void ExposeFinalConsents()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) finalConsentSnapshot = finalConsents.Export();
        Scribe_Values.Look(ref finalConsentSnapshot, "honorFinalCharterConsentsV1");
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed && initialMandates.Records.Count > 0)
        { initialMandateSnapshot = initialMandates.Export(); initialMandatePairs = initialMandates.Records.Select(InitialCharterMandates.Marker).ToList(); }
        Scribe_Values.Look(ref initialMandateSnapshot, "honorInitialCharterMandatesV1");
        Scribe_Collections.Look(ref initialMandatePairs, "honorInitialCharterMandatePairsV1", LookMode.Value);
        ExposeAmendments();
        ExposeScopedReleases();
    }
    private void ValidateFinalConsents()
    {
        if (finalConsentSnapshot != null) finalConsents = CharterAcceptances.Restore(finalConsentSnapshot);
        finalConsents.ValidateBook(Book);
        if (initialMandateSnapshot != null) initialMandates = InitialCharterMandates.Restore(initialMandateSnapshot);
        initialMandatePairs ??= [];
        if (!initialMandatePairs.SequenceEqual(initialMandates.Records.Select(InitialCharterMandates.Marker))) throw new ArgumentException("Initial charter authority pairs changed.");
        initialMandates.ValidateBook(Book);
        initialMandates.ValidateAcceptances(finalConsents);
        ValidateAmendments();
        ValidateScopedReleases();
    }
    private InitialCharterMandate ReviewInitialMandate(CharterBook before, CharterOfferResult result, string id, Faction realm,
        TerritoryObservation[] observations, string payments, long now, bool final)
    {
        if (!result.Assessment.Accepted) return null;
        var d = NpcDecisionAuthority.Observe(realm);
        if (d.Regime == NpcDecisionRegime.Ordinary) return null; // Preserve existing economic-policy minimums; no collective fallback.
        var r = new InitialCharterMandate(id, before.Export(), result.Book.Export(), payments, observations, now, final, d, null);
        return r with { Mandate = NativeNpcContractAuthority.ReviewInitial(r, realm) };
    }
    private void CheckInitialMandate(InitialCharterMandate r, Faction realm, TerritoryObservation[] observations, string payments)
    {
        if (r == null) return;
        var d = NpcDecisionAuthority.Observe(realm);
        var fresh = r with { Decision = d, Observations = observations, Payments = payments, Mandate = null };
        fresh = fresh with { Mandate = NativeNpcContractAuthority.ReviewInitial(fresh, realm) };
        if (Current.Game != context || JsonSerializer.Serialize(fresh) != JsonSerializer.Serialize(r)) throw new ArgumentException("Initial charter authority changed before assignment.");
    }
    private static string ConsentLabel(string label, string fallback)
    {
        string safe = new((label ?? "").Where(c => !char.IsControl(c)).Take(200).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }
    internal void DrawFinalConsent(Listing_Standard list, string id)
    {
        var record = finalConsents.Find(id);
        if (record != null) list.Label("PH_ConsentRecord".Translate(record.FactionLabel, record.RepresentativeLabel,
            (record.SignedTick / (double)PoliticalWorld.Day).ToString("0.##")));
    }
}
