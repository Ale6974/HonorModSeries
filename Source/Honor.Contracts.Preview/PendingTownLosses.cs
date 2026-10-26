using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

internal sealed record PendingTownLossCandidate(Game Context, Settlement City, PendingTownLoss Proof);
public sealed record LivePendingTownLossReview(Game Context, PendingTownLossPlan Plan, string Label);

public sealed partial class TerritoryAdministrationLedger
{
    private string pendingLossSnapshot;
    private PendingTownLossArchive pendingTownLosses = new();
    private int nativeExecutionDepth;
    internal bool HasPendingTownLosses => pendingTownLosses.Records.Any(r => r.ResolvedTick == null) || politicalTownLosses.Pending;
    internal void ValidatePoliticalLosses(AllegianceTransitionLedger journal)
    {
        pendingTownLosses.ValidatePair(Charters.Book, Ledger); politicalTownLosses.ValidatePair(Charters.Book, Ledger);
        pendingTownLosses.ValidateUpperPair(Charters.LossArchive);
        pendingTownLosses.ValidatePolitical(journal); politicalTownLosses.ValidatePolitical(journal);
    }
    private void ExposePendingTownLosses()
    {
        ExposePoliticalTownLosses();
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) pendingLossSnapshot = pendingTownLosses.Export();
        Scribe_Values.Look(ref pendingLossSnapshot, "honorPendingTownDestructionV1");
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        try { if (pendingLossSnapshot != null) pendingTownLosses = PendingTownLossArchive.Restore(pendingLossSnapshot); }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidOperationException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Pending town-loss archive unavailable; raw data retained. " + e.GetType().Name); }
    }
    private void StandaloneLossReady()
    {
        if (Current.Game != ownerGame || Current.ProgramState != ProgramState.Playing) throw new ArgumentException("Review this city journal's current game.");
        var political = Current.Game.GetComponent<PoliticalTransitionLedger>(); var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (RestoreFailed || Charters.RestoreFailed || Charters.UnresolvedTownLosses || nativeExecutionDepth != 0 || UnionWarCouncil.HasExternalProvider ||
            political.RestoreFailed || political.Executing || Current.Game.GetComponent<LeaderJoinLedger>().BlocksChanges || Current.Game.GetComponent<SubtreeExitLedger>().BlocksChanges ||
            Current.Game.GetComponent<LeaderRelocationLedger>().BlocksChanges || payments.RestoreFailed || payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) ||
            Current.Game.GetComponent<PlayerPoliticsLedger>().RestoreFailed || Current.Game.GetComponent<RealmAdministrationLedger>().RestoreFailed || Current.Game.GetComponent<UnionPoliticsLedger>().RestoreFailed)
            throw new ArgumentException("Only a standalone city journal outside native execution may reconcile an observed empty leaf loss.");
        ValidatePoliticalLosses(political.Journal);
        if (political.Journal.Pending) political.ReviewTownLossContext();
    }
    private void ValidatePendingUpper(TownLossProof proof)
    {
        if (proof.Before != Charters.Book.Export() || Charters.UnresolvedTownLosses || proof.Kind != TownLossKind.Destroyed ||
            proof.ScopeRoot != null || proof.PriorScopes != null || proof.ClosureTick.HasValue || proof.Resolution != null)
            throw new ArgumentException("Only an isolated unchanged original upper scope is supported.");
        var owners = Find.FactionManager.AllFactionsListForReading.Where(f => f != null && f.GetUniqueLoadID() == proof.OwnerAtLoss).Take(2).ToArray();
        if (owners.Length != 1 || owners[0].GetType() != typeof(Faction) || owners[0].def == null || owners[0].def.GetType() != typeof(FactionDef) ||
            !owners[0].def.humanlikeFaction || owners[0].def.permanentEnemy || owners[0].IsPlayer || owners[0].Hidden || owners[0].temporary ||
            owners[0].defeated || !owners[0].HasGoodwill || owners[0].def.royalTitleTags?.Count > 0 || owners[0].def.defName == UnionPoliticsLedger.UnionDefName)
            throw new ArgumentException("Unsupported actual upper realm; no collective authority is inferred.");
        var book = Charters.Book; var root = book.Charter(proof.Root);
        var descendants = book.Charters.Where(c => c.Phase == RecordedCharterPhase.Active && book.IsBelow(c.Terms.Subject, proof.Territory)).ToArray();
        if (descendants.Length == 0 || descendants.Any(c => c.PlayerReleaseTick.HasValue || c.NpcReleaseTick.HasValue))
            throw new ArgumentException("Exact unchanged surviving subtree required.");
        void Survivor(string party)
        {
            var b = Charters.Binding(party); var city = b.Settlement; var entity = book.Entity(party);
            if (b.PlayerGroup || b.FactionGroup || city == null || city.GetType() != typeof(Settlement) || city.Destroyed ||
                !Find.WorldObjects.AllWorldObjects.Contains(city) || city.Faction != owners[0] || !Ledger.VerifiedOwner(party, entity.NativeOwner, proof.OwnerAtLoss))
                throw new ArgumentException("Surviving actual ownership or original higher party changed.");
        }
        foreach (var c in descendants) Survivor(c.Terms.Subject);
        if (!book.Entity(root.Terms.Liege).Player) Survivor(root.Terms.Liege);
        else
        {
            var b = Charters.Binding(root.Terms.Liege);
            if (root.Terms.Liege != Charters.PlayerIdentity || !b.PlayerGroup || b.FactionGroup || b.OriginalFaction != Faction.OfPlayer ||
                book.Entity(root.Terms.Liege).NativeOwner != Faction.OfPlayer.GetUniqueLoadID()) throw new ArgumentException("Original higher player party changed.");
        }
    }
    private static EmptyTownStock Empty(Settlement city)
    {
        if (city == null || city.GetType() != typeof(Settlement) || city.HasMap || city.Faction == null || city.Faction.IsPlayer ||
            city.previouslyGeneratedInhabitants == null || city.previouslyGeneratedInhabitants.Count != 0 ||
            city.trader == null || city.trader.GetType() != typeof(Settlement_TraderTracker) || city.trader.settlement != city ||
            city.trader.GetDirectlyHeldThings()?.Count > 0) throw new ArgumentException("Town contains or may hold people or stock.");
        return new(city.EverVisited, city.trader.GetDirectlyHeldThings() != null, city.trader.NextRestockTick);
    }
    internal PendingTownLossCandidate ReviewPendingDestruction(Settlement city)
    {
        StandaloneLossReady();
        var t = Ledger.Transfers.SingleOrDefault(t => t.Phase == TerritoryTransferPhase.Prepared);
        if (t == null || t.Purpose != TerritoryTransferPurpose.ActiveCharter || t.ReleasedRoot != null || t.ResidentProof != null || t.StockFormat != 0 ||
            pendingTownLosses.Records.Count >= PendingTownLossArchive.Capacity || pendingTownLosses.HasPending(t.Id) || city == null || city.Destroyed ||
            !Find.WorldObjects.AllWorldObjects.Contains(city) || t.Territory != "territory/" + city.GetUniqueLoadID()) return null;
        var reference = references.Single(r => r.Id == t.Id); var binding = Charters.Binding(t.Territory); var c = Charters.Book.Charter(t.Charter);
        if (reference.City != city || binding.Settlement != city || binding.PlayerGroup || binding.FactionGroup || reference.Residents != null ||
            reference.From == null || reference.From.GetUniqueLoadID() != t.FromOwner || reference.From.IsPlayer || reference.Target == null || reference.Target.IsPlayer ||
            reference.Target.GetUniqueLoadID() != t.TargetOwner || city.Faction != reference.From && city.Faction != reference.Target ||
            c.Phase != RecordedCharterPhase.Active || !Charters.Book.PlayerManaged(c) ||
            Charters.Book.Charters.Any(offer => Charters.Book.Phase(offer, Find.TickManager.TicksGame) == RecordedCharterPhase.Offered &&
                (offer.Terms.Subject == t.Territory || offer.Terms.Liege == t.Territory))) return null;
        var stock = Empty(city);
        if (t.StockProof == null ? stock.Visited || stock.ContainerExists || stock.NextRestockTick != -1 : stock.Proof != t.StockProof) return null;
        long now = Find.TickManager.TicksGame;
        string label = string.IsNullOrWhiteSpace(city.Label) ? t.Territory : city.Label;
        if (label.Length > 200) label = label.Substring(0, 200);
        var political = Current.Game.GetComponent<PoliticalTransitionLedger>();
        string context = political.Journal.Pending ? political.ReviewTownLossContext() : null;
        if (context != null && (!political.Journal.Current.Followers.Contains(t.Charter) ||
            PlayerAllegiance.Restore(political.Journal.Current.After).State.Authority != t.TargetOwner)) return null;
        TownLossProof upper = null;
        if (Charters.Book.Charters.Any(child => child.Phase == RecordedCharterPhase.Active && child.Terms.Liege == t.Territory))
        {
            if (context != null || Charters.LossArchive.Records.Count >= TownSuccessionArchive.Capacity) return null;
            upper = new(t.Charter, t.Territory, t.BaselineOwner, city.Faction.GetUniqueLoadID(), label, now, now, Charters.Book.Export());
            ValidatePendingUpper(upper); Charters.LossArchive.CloseForObservation(upper);
        }
        return new(Current.Game, city, new PendingTownLoss(t.Id, city.GetUniqueLoadID(), city.Faction.GetUniqueLoadID(), label, now, now, Charters.Book.Export(), Ledger.Export(), stock,
            PoliticalBefore: context) { UpperScope = upper });
    }
    internal void RecordPendingDestruction(Settlement city, PendingTownLossCandidate candidate)
    {
        if (candidate == null || candidate.Context != Current.Game || candidate.City != city) return;
        StandaloneLossReady();
        if (city == null || !city.Destroyed || Find.WorldObjects.AllWorldObjects.Contains(city) || city.GetUniqueLoadID() != candidate.Proof.City ||
            city.Faction?.GetUniqueLoadID() != candidate.Proof.Owner || Empty(city) != candidate.Proof.Stock)
            throw new ArgumentException("Normal native destruction outcome is not verified.");
        var proof = candidate.Proof with { ObservedTick = Find.TickManager.TicksGame };
        if (proof.UpperScope != null) proof = proof with { UpperScope = proof.UpperScope with { ObservedTick = proof.ObservedTick } };
        var political = Current.Game.GetComponent<PoliticalTransitionLedger>();
        if (proof.PoliticalBefore != (political.Journal.Pending ? political.ReviewTownLossContext() : null)) throw new ArgumentException("Political intent changed during native destruction.");
        var observed = pendingTownLosses.Observe(proof, Charters.Book, Ledger); observed.ValidatePolitical(political.Journal);
        pendingTownLosses = observed;
        Messages.Message("PL_Observed".Translate(proof.Label), MessageTypeDefOf.NeutralEvent);
    }
    private void VerifyDestructionNow(PendingTownLoss proof)
    {
        var t = Ledger.Transfer(proof.Transfer); var reference = references.Single(r => r.Id == t.Id); var binding = Charters.Binding(t.Territory);
        if (reference.Residents != null || t.ResidentProof != null || reference.City != binding.Settlement || binding.PlayerGroup || binding.FactionGroup ||
            Find.WorldObjects.AllWorldObjects.Any(o => o.GetUniqueLoadID() == proof.City)) throw new ArgumentException("Town is present again or references/custody changed.");
        // A missing reference is accepted ONLY with the separately stored positive normal-return destruction witness.
        if (reference.City != null && (!reference.City.Destroyed || reference.City.GetUniqueLoadID() != proof.City ||
            reference.City.Faction?.GetUniqueLoadID() != proof.Owner || Empty(reference.City) != proof.Stock))
            throw new ArgumentException("The observed destroyed object's state changed.");
    }
    public LivePendingTownLossReview ReviewPendingTownLoss(string id)
    {
        StandaloneLossReady();
        var proof = pendingTownLosses.Records.Single(r => r.Transfer == id && r.ResolvedTick == null); VerifyDestructionNow(proof);
        if (proof.UpperScope != null) ValidatePendingUpper(proof.UpperScope);
        return new(Current.Game, pendingTownLosses.Review(id, Charters.Book, Ledger, Find.TickManager.TicksGame), proof.Label);
    }
    public void ApplyPendingTownLoss(LivePendingTownLossReview reviewed)
    {
        try
        {
            if (reviewed?.Context != Current.Game || reviewed.Plan == null) throw new ArgumentException("Review the current game first.");
            var fresh = ReviewPendingTownLoss(reviewed.Plan.Transfer);
            if (fresh.Plan != reviewed.Plan) throw new ArgumentException("Loss review changed.");
            var result = pendingTownLosses.Commit(fresh.Plan, Charters.Book, Ledger, Find.TickManager.TicksGame);
            var upper = result.Archive.StageUpper(fresh.Plan.Transfer, Charters.LossArchive, result.Book);
            result.Archive.ValidateUpperPair(upper);
            var political = Current.Game.GetComponent<PoliticalTransitionLedger>();
            bool coordinated = pendingTownLosses.Records.Single(r => r.Transfer == fresh.Plan.Transfer).PoliticalBefore != null;
            var nextPolitical = coordinated ? political.StageTownLoss(result.Archive, result.Book, result.Ledger, fresh.Plan.Transfer, Find.TickManager.TicksGame) : null;
            politicalTownLosses.ValidatePair(result.Book, result.Ledger);
            politicalTownLosses.ValidatePolitical(nextPolitical ?? political.Journal);
            Charters.ReplaceAfterPendingUpperLoss(fresh.Plan.BookBefore, result.Book, upper);
            Ledger = result.Ledger; pendingTownLosses = result.Archive;
            if (nextPolitical != null) political.CommitTownLoss(nextPolitical);
            Messages.Message("PL_Completed".Translate(fresh.Label), MessageTypeDefOf.NeutralEvent);
            var scope = pendingTownLosses.Records.Single(r => r.Transfer == fresh.Plan.Transfer).UpperScope;
            if (scope != null)
            {
                Messages.Message("PU_Continue".Translate(fresh.Label), MessageTypeDefOf.NeutralEvent);
                try { Find.WindowStack.Add(new TownSuccessionWindow(scope.Root)); }
                catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Loss reconciled; reopen town succession review. " + e.GetType().Name); }
            }
        }
        catch (Exception e) { Log.Warning("[Honor.Contracts.Preview] Pending destruction not reconciled; original journals retained. " + e.GetType().Name); Messages.Message("PL_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
    internal void DrawPendingTownLosses(Listing_Standard list, string charter = null)
    {
        DrawIndependentTownLosses(list, charter);
        list.Label("PL_Limits".Translate());
        list.Label("PU_Limits".Translate());
        if (RestoreFailed) return;
        foreach (var proof in pendingTownLosses.Records.Where(r => r.ResolvedTick == null && (charter == null || Ledger.Transfer(r.Transfer).Charter == charter)).ToArray())
        {
            list.Label("PL_Pending".Translate());
            if (proof.PoliticalBefore != null) list.Label("PL_Coordinated".Translate());
            if (list.ButtonText("PL_Review".Translate(proof.Label)))
            {
                try { var review = ReviewPendingTownLoss(proof.Transfer); Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("PL_Confirm".Translate(review.Label), () => ApplyPendingTownLoss(review))); }
                catch (Exception) { Messages.Message("PL_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
            }
        }
    }
}

[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Destroy), new Type[] { })]
internal static class NativePendingTownDestruction
{
    static void Prefix(WorldObject __instance, out PendingTownLossCandidate __state)
    {
        __state = null;
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null || __instance is not Settlement city) return;
        try { __state = Current.Game.GetComponent<TerritoryAdministrationLedger>().ReviewPendingDestruction(city); }
        catch (Exception) { /* Unsupported/pending other operations grant no loss authority. */ }
    }
    static void Postfix(WorldObject __instance, PendingTownLossCandidate __state)
    {
        if (__state == null || Current.ProgramState != ProgramState.Playing || Current.Game == null || __instance is not Settlement city) return;
        try { Current.Game.GetComponent<TerritoryAdministrationLedger>().RecordPendingDestruction(city, __state); }
        catch (Exception e)
        { Log.Warning("[Honor.Contracts.Preview] Pending town destruction witness unavailable; original journals retained. " + e.GetType().Name); Messages.Message("PL_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
}
