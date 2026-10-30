using Honor.Contracts.Rules;
using Verse;

namespace Honor.Contracts.Preview;

public sealed partial class LiveCharterLedger
{
    private ScopedCharterReleases scopedReleases = new();
    private string scopedReleaseSnapshot;
    private List<string> scopedReleasePairs = [];
    private void ExposeScopedReleases()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed && scopedReleases.Records.Count > 0)
        { scopedReleaseSnapshot = scopedReleases.Export(); scopedReleasePairs = scopedReleases.Records.Select(ScopedCharterReleases.Marker).ToList(); }
        Scribe_Values.Look(ref scopedReleaseSnapshot, "honorScopedCharterReleasesV1");
        Scribe_Collections.Look(ref scopedReleasePairs, "honorScopedCharterReleasePairsV1", LookMode.Value);
    }
    private void ValidateScopedReleases()
    {
        if (scopedReleaseSnapshot != null) scopedReleases = ScopedCharterReleases.Restore(scopedReleaseSnapshot);
        scopedReleasePairs ??= [];
        if (!scopedReleasePairs.SequenceEqual(scopedReleases.Records.Select(ScopedCharterReleases.Marker))) throw new ArgumentException("Scoped release markers missing or changed.");
        scopedReleases.ValidateBook(Book);
    }
    private void FinalizeScopedReleases()
    {
        if (RestoreFailed || scopedReleases.Records.Count == 0) return;
        try
        {
            var transfers = context.GetComponent<TerritoryAdministrationLedger>(); var payments = context.GetComponent<TributePaymentLedger>();
            if (transfers.RestoreFailed || payments.RestoreFailed || scopedReleases.Records.Any(r => r.Witness.Tick > Find.TickManager.TicksGame)) throw new ArgumentException("Scoped release peers/clock unavailable.");
            scopedReleases.ValidateBook(Book); scopedReleases.ValidatePeers(transfers.Ledger, payments.Ledger);
        }
        catch (Exception e) when (e is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Scoped releases rejected; original raw history retained. " + e.GetType().Name); }
    }
}
