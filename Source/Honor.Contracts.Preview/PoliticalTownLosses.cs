using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

internal sealed record PoliticalTownLossCandidate(Game Context, Settlement City, PoliticalTownLoss Proof);
public sealed record LivePoliticalTownLossReview(Game Context, PoliticalTownLossPlan Plan, string Label);

public sealed partial class TerritoryAdministrationLedger
{
    private string politicalLossSnapshot;
    private PoliticalTownLossArchive politicalTownLosses = new();
    internal bool IndependentlyLostTerritory(string id) => politicalTownLosses.LostTerritory(id);
    private void ExposePoliticalTownLosses()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) politicalLossSnapshot = politicalTownLosses.Export();
        Scribe_Values.Look(ref politicalLossSnapshot, "honorIndependentPoliticalTownLossV1");
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        try { if (politicalLossSnapshot != null) politicalTownLosses = PoliticalTownLossArchive.Restore(politicalLossSnapshot); }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Independent political loss archive unavailable; raw evidence retained. " + e.GetType().Name); }
    }
    private string IndependentLossReady()
    {
        StandaloneLossReady();
        if (Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) || pendingTownLosses.Records.Any(r => r.ResolvedTick == null))
            throw new ArgumentException("Resolve actual pending physical city transfers separately.");
        return Current.Game.GetComponent<PoliticalTransitionLedger>().ReviewTownLossContext();
    }
    internal PoliticalTownLossCandidate ReviewIndependentDestruction(Settlement city)
    {
        string context = IndependentLossReady(); var political = Current.Game.GetComponent<PoliticalTransitionLedger>();
        if (city == null || city.Destroyed || !Find.WorldObjects.AllWorldObjects.Contains(city) ||
            politicalTownLosses.Records.Count >= PoliticalTownLossArchive.Capacity) return null;
        if (politicalTownLosses.Records.Any(r => r.ResolvedTick == null && (r.BookBefore != Charters.Book.Export() ||
            r.TransfersBefore != Ledger.Export() || r.PoliticalBefore != context))) return null;
        string territory = "territory/" + city.GetUniqueLoadID();
        var charter = Charters.Book.Charters.SingleOrDefault(c => c.Terms.Subject == territory && c.Phase == RecordedCharterPhase.Active);
        if (charter == null || !Charters.Book.PlayerManaged(charter) || !political.Journal.Current.Followers.Contains(charter.Id) ||
            political.Journal.LostCharters.Contains(charter.Id) || IndependentlyLostTerritory(territory)) return null;
        var binding = Charters.Binding(territory);
        if (binding.Settlement != city || binding.PlayerGroup || binding.FactionGroup || city.Faction == null ||
            !Ledger.VerifiedOwner(territory, Charters.Book.Entity(territory).NativeOwner, city.Faction.GetUniqueLoadID()) ||
            Charters.Book.Charters.Any(c => c.Phase == RecordedCharterPhase.Active && c.Terms.Liege == territory) ||
            Charters.Book.Charters.Any(c => Charters.Book.Phase(c, Find.TickManager.TicksGame) == RecordedCharterPhase.Offered &&
                (c.Terms.Liege == territory || c.Terms.Subject == territory))) return null;
        var stock = Empty(city); long now = Find.TickManager.TicksGame;
        string label = string.IsNullOrWhiteSpace(city.Label) ? territory : city.Label;
        if (label.Length > 200) label = label.Substring(0, 200);
        return new(Current.Game, city, new(Guid.NewGuid().ToString("N"), charter.Id, city.GetUniqueLoadID(), city.Faction.GetUniqueLoadID(), label,
            now, now, Charters.Book.Export(), Ledger.Export(), context, stock));
    }
    internal void RecordIndependentDestruction(Settlement city, PoliticalTownLossCandidate candidate)
    {
        if (candidate == null || candidate.Context != Current.Game || candidate.City != city) return;
        string context = IndependentLossReady();
        if (context != candidate.Proof.PoliticalBefore || city == null || !city.Destroyed || Find.WorldObjects.AllWorldObjects.Contains(city) ||
            city.GetUniqueLoadID() != candidate.Proof.City || city.Faction?.GetUniqueLoadID() != candidate.Proof.Owner || Empty(city) != candidate.Proof.Stock)
            throw new ArgumentException("Independent destruction did not return with the exact reviewed native state.");
        var proof = candidate.Proof with { ObservedTick = Find.TickManager.TicksGame };
        politicalTownLosses = politicalTownLosses.Observe(proof, Charters.Book, Ledger, Current.Game.GetComponent<PoliticalTransitionLedger>().Journal);
        Messages.Message("PL_Observed".Translate(proof.Label), MessageTypeDefOf.NeutralEvent);
    }
    private void VerifyIndependentDestructionNow(PoliticalTownLoss proof)
    {
        var binding = Charters.Binding("territory/" + proof.City);
        if (binding.PlayerGroup || binding.FactionGroup || Find.WorldObjects.AllWorldObjects.Any(o => o.GetUniqueLoadID() == proof.City))
            throw new ArgumentException("An independently lost town is present again or changed identity.");
        // A missing reference grants nothing without this previously saved positive normal-return witness.
        var city = binding.Settlement;
        if (city != null && (!city.Destroyed || city.GetUniqueLoadID() != proof.City || city.Faction?.GetUniqueLoadID() != proof.Owner || Empty(city) != proof.Stock))
            throw new ArgumentException("Independent positive loss state changed.");
    }
    public LivePoliticalTownLossReview ReviewIndependentTownLoss(string id)
    {
        IndependentLossReady();
        var proof = politicalTownLosses.Records.Single(r => r.Id == id && r.ResolvedTick == null); VerifyIndependentDestructionNow(proof);
        return new(Current.Game, politicalTownLosses.Review(id, Charters.Book, Ledger, Current.Game.GetComponent<PoliticalTransitionLedger>().Journal, Find.TickManager.TicksGame), proof.Label);
    }
    public void ApplyIndependentTownLoss(LivePoliticalTownLossReview reviewed)
    {
        try
        {
            if (reviewed?.Context != Current.Game || reviewed.Plan == null) throw new ArgumentException("Review the current game.");
            var fresh = ReviewIndependentTownLoss(reviewed.Plan.Id);
            if (fresh.Plan != reviewed.Plan) throw new ArgumentException("Independent loss review changed.");
            var political = Current.Game.GetComponent<PoliticalTransitionLedger>();
            var result = politicalTownLosses.Commit(fresh.Plan, Charters.Book, Ledger, political.Journal, Find.TickManager.TicksGame);
            pendingTownLosses.ValidatePair(result.Book, Ledger); pendingTownLosses.ValidatePolitical(result.Political);
            Charters.ReplaceAfterPendingTownLoss(fresh.Plan.BookBefore, result.Book);
            politicalTownLosses = result.Archive; political.CommitTownLoss(result.Political);
            Messages.Message("PL_Completed".Translate(fresh.Label), MessageTypeDefOf.NeutralEvent);
        }
        catch (Exception e)
        { Log.Warning("[Honor.Contracts.Preview] Independent town loss not reconciled; evidence retained. " + e.GetType().Name); Messages.Message("PL_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
    private void DrawIndependentTownLosses(Listing_Standard list, string charter)
    {
        if (RestoreFailed) return;
        if (politicalTownLosses.Records.Count(r => r.ResolvedTick == null) > 1) list.Label("PI_Overlap".Translate());
        foreach (var proof in politicalTownLosses.Records.Where(r => r.ResolvedTick == null && (charter == null || r.Charter == charter)).ToArray())
        {
            list.Label("PI_Pending".Translate());
            if (list.ButtonText("PL_Review".Translate(proof.Label)))
            {
                try { var review = ReviewIndependentTownLoss(proof.Id); Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("PI_Confirm".Translate(proof.Label), () => ApplyIndependentTownLoss(review))); }
                catch (Exception) { Messages.Message("PL_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
            }
        }
    }
}

[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Destroy), new Type[] { })]
internal static class NativeIndependentPoliticalTownDestruction
{
    static void Prefix(WorldObject __instance, out PoliticalTownLossCandidate __state)
    {
        __state = null;
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null || __instance is not Settlement city) return;
        try { __state = Current.Game.GetComponent<TerritoryAdministrationLedger>().ReviewIndependentDestruction(city); }
        catch (Exception) { /* Unsupported, executing and overlapping operations grant no authority. */ }
    }
    static void Postfix(WorldObject __instance, PoliticalTownLossCandidate __state)
    {
        if (__state == null || Current.ProgramState != ProgramState.Playing || Current.Game == null || __instance is not Settlement city) return;
        try { Current.Game.GetComponent<TerritoryAdministrationLedger>().RecordIndependentDestruction(city, __state); }
        catch (Exception e)
        { Log.Warning("[Honor.Contracts.Preview] Independent town destruction proof unavailable; journals retained. " + e.GetType().Name); Messages.Message("PL_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
}
