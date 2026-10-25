using Honor.Contracts.Rules;
using Verse;

namespace Honor.Contracts.Preview;

public sealed partial class LiveCharterLedger
{
    private string finalConsentSnapshot;
    private CharterAcceptances finalConsents = new();
    private void ExposeFinalConsents()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) finalConsentSnapshot = finalConsents.Export();
        Scribe_Values.Look(ref finalConsentSnapshot, "honorFinalCharterConsentsV1");
    }
    private void ValidateFinalConsents()
    {
        if (finalConsentSnapshot != null) finalConsents = CharterAcceptances.Restore(finalConsentSnapshot);
        finalConsents.ValidateBook(Book);
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
