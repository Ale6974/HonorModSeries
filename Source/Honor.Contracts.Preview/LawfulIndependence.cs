using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

public sealed record LawfulIndependenceReview(Game Context, LawfulExitPreparation Closure, PoliticalChangeReview Admission)
{
    public string Residents { get; init; }
}

public sealed partial class PoliticalTransitionLedger
{
    private static void RequireLawfulSource(Faction root, string authority)
    {
        var sources = Find.FactionManager.AllFactionsListForReading.Where(f => f != null && f.GetUniqueLoadID() == authority).Take(2).ToArray();
        if (sources.Length != 1 || sources[0].GetType() != typeof(Faction) || sources[0].defeated ||
            sources[0].HostileTo(root) || root.HostileTo(sources[0]))
            throw new ArgumentException("The unique original realm and union must remain reciprocally nonhostile.");
    }
    internal void ValidateLawfulArchive()
    {
        if (Current.Game != context || RestoreFailed) throw new ArgumentException("Original current political archive required.");
        var records = warHistory.Concat(Journal.Current == null ? Array.Empty<string>() : new[] { Journal.Export() });
        LawfulAdmissions.ValidateArchive(records, Charters.Book, context.GetComponent<TributePaymentLedger>().Ledger, context.GetComponent<CharterProtectionLedger>().Ledger);
        foreach (var saved in records)
            Territories.ValidateLawfulResidentExecution(AllegianceTransitionLedger.Restore(saved).Current, false);
    }
    internal void RequireHaulCustodyParent(TerritoryTransfer transfer)
    {
        var parent = Journal.Current;
        if (Current.Game != context || RestoreFailed || Executing || Charters.RestoreFailed || Charters.UnresolvedTownLosses ||
            Territories.RestoreFailed || Territories.HasPendingTownLosses || context.GetComponent<CharterProtectionLedger>().Executing ||
            Territories.Ledger.Transfers.Any(row => row.Phase == TerritoryTransferPhase.Prepared && row.Id != transfer.Id) ||
            !Journal.Pending || parent?.LawfulResidents == null ||
            transfer.LawfulHaulParentProof == null || transfer.LawfulHaulParentProof != LawfulExitPreparation.Restore(parent.LawfulExit).Proof ||
            transfer.Phase != TerritoryTransferPhase.Prepared || Territories.Ledger.Transfer(transfer.Id) != transfer ||
            !Journal.IsAfter(Politics.Model) || Politics.TransitionAuthority()?.GetUniqueLoadID() != transfer.TargetOwner)
            throw new ArgumentException("Original current lawful parent and pending city required.");
        LawfulAdmissions.ValidatePair(parent, Charters.Book, context.GetComponent<TributePaymentLedger>().Ledger,
            context.GetComponent<CharterProtectionLedger>().Ledger);
        LawfulAdmissions.ValidateCurrentPhysical(parent, Territories.Ledger);
        if (!Journal.MatchesSurvivingFollowers(Charters.Book.PlayerFollowers().Select(c => c.Id)))
            throw new ArgumentException("The lawful follower scope changed.");
        UnionPeaceCouncil.Verify(Politics.RecordedAuthority, parent);
        UnionWarCouncil.VerifyCurrentCouncil(Politics.RecordedAuthority, parent);
    }
    public LawfulIndependenceReview ReviewLawfulIndependence(string request)
    {
        Ready(); if (Journal.Pending || Territories.Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared))
            throw new ArgumentException("This executor requires no unresolved physical transfer.");
        var payments = context.GetComponent<TributePaymentLedger>(); var protection = context.GetComponent<CharterProtectionLedger>();
        if (protection.RestoreFailed || protection.Executing) throw new ArgumentException("Original protection history unavailable.");
        ValidateLawfulArchive();
        var closure = LawfulExitPreparation.Prepare(Charters.Book, payments.Ledger, Politics.Model, Territories.Ledger, protection.Ledger,
            request, Find.TickManager.TicksGame, Charters.Observe());
        var root = Politics.UnionCandidate; Politics.CheckTransitionAdmission(root);
        var command = new AllegianceCommand(AllegianceAction.JoinUnion, Find.TickManager.TicksGame, root.GetUniqueLoadID(),
            Consent: true, NoActiveFeudalContract: true, OldContractReleased: true);
        var original = Territories.ReviewFollowersFor(root);
        if (original.Entries.Any(e => e.Observation.Owner != Politics.State.Authority))
            throw new ArgumentException("Every original follower must remain in the exact recorded old realm.");
        var residents = Territories.CaptureLawfulResidents(original);
        if (original.Entries.Count > 0)
        {
            RequireLawfulSource(root, Politics.State.Authority);
            FutureRelations(root, original);
        }
        var followers = Territories.ReviewLawfulFollowers(CharterBook.Restore(closure.AfterBook), root, original.Entries);
        var peace = UnionPeaceCouncil.Review(root, followers.Moves);
        if (!peace.Accepted) throw new ArgumentException("Actual membership council declined independence admission.");
        var admission = new PoliticalChangeReview(root, command, followers, Stamp(root, command, followers, peace: peace), Context: context) { Peace = peace };
        var check = AllegianceTransitionLedger.Restore(Journal.Export());
        if (residents != null) check.PrepareLawfulResidentAdmission("review-lawful", Politics.Model, command, followers, Territories.Ledger, closure, peace, JsonSerializer.Deserialize<LawfulResidentScope[]>(residents));
        else if (followers.Entries.Count > 0) check.PrepareLawfulFollowerAdmission("review-lawful", Politics.Model, command, followers, Territories.Ledger, closure, peace);
        else if (Territories.Ledger.Transfers.Count == 0) check.PrepareLawfulAdmission("review-lawful", Politics.Model, command, followers, Territories.Ledger, closure, peace);
        else check.PrepareLawfulAdmissionWithHistory("review-lawful", Politics.Model, command, followers, Territories.Ledger, closure, peace);
        return new(context, closure, admission) { Residents = residents };
    }
    public void StartLawfulIndependence(LawfulIndependenceReview reviewed)
    {
        try
        {
            if (reviewed?.Context != context || Current.Game != context) throw new ArgumentException("Review this current game.");
            var fresh = ReviewLawfulIndependence(reviewed.Closure.Request);
            if (fresh.Closure.Export() != reviewed.Closure.Export() || fresh.Residents != reviewed.Residents || fresh.Admission.Stamp != reviewed.Admission.Stamp ||
                fresh.Admission.Destination != reviewed.Admission.Destination || reviewed.Admission.Context != context)
                throw new ArgumentException("Original closure, current council or native relations changed.");
            var payments = context.GetComponent<TributePaymentLedger>(); var protection = context.GetComponent<CharterProtectionLedger>();
            fresh.Closure.CheckBefore(Charters.Book, payments.Ledger, Politics.Model, Territories.Ledger, protection.Ledger, Find.TickManager.TicksGame, Charters.Observe());
            if (Current.Game != context) throw new ArgumentException("Current game changed during final review.");
            Politics.CheckTransitionAdmission(fresh.Admission.Destination);
            if (fresh.Admission.Followers.Entries.Count > 0) RequireLawfulSource(fresh.Admission.Destination, Politics.State.Authority);
            var council = UnionPeaceCouncil.Review(fresh.Admission.Destination, fresh.Admission.Followers.Moves);
            if (!council.Accepted || !UnionPeaceAdmission.SameReview(fresh.Admission.Peace, council))
                throw new ArgumentException("Current union consent changed during final review.");
            var next = AllegianceTransitionLedger.Restore(Journal.Export());
            var id = Guid.NewGuid().ToString("N");
            if (fresh.Residents != null) next.PrepareLawfulResidentAdmission(id, Politics.Model, fresh.Admission.Command, fresh.Admission.Followers, Territories.Ledger, fresh.Closure, fresh.Admission.Peace, JsonSerializer.Deserialize<LawfulResidentScope[]>(fresh.Residents));
            else if (fresh.Admission.Followers.Entries.Count > 0) next.PrepareLawfulFollowerAdmission(id, Politics.Model, fresh.Admission.Command, fresh.Admission.Followers, Territories.Ledger, fresh.Closure, fresh.Admission.Peace);
            else if (Territories.Ledger.Transfers.Count == 0) next.PrepareLawfulAdmission(id, Politics.Model, fresh.Admission.Command, fresh.Admission.Followers, Territories.Ledger, fresh.Closure, fresh.Admission.Peace);
            else next.PrepareLawfulAdmissionWithHistory(id, Politics.Model, fresh.Admission.Command, fresh.Admission.Followers, Territories.Ledger, fresh.Closure, fresh.Admission.Peace);
            var nextPolitics = PlayerAllegiance.Restore(Politics.Model.Export()); nextPolitics.Apply(fresh.Admission.Command);
            next.ConfirmAllegiance(nextPolitics, Find.TickManager.TicksGame);
            var history = warHistory.ToList();
            if (Journal.Current?.Wars != null || Journal.Current?.Peace != null || Journal.Current?.Feudal != null) history.Add(Journal.Export());
            UnionWarAdmission.ValidateHistory(history, next.Current);
            var book = CharterBook.Restore(fresh.Closure.AfterBook); var tribute = TributeLedger.Restore(fresh.Closure.AfterPayments);
            LawfulAdmissions.ValidateArchive(history.Concat(new[] { next.Export() }), book, tribute, protection.Ledger);
            var final = ReviewLawfulIndependence(reviewed.Closure.Request);
            if (final.Context != context || Current.Game != context || final.Closure.Export() != fresh.Closure.Export() || final.Admission.Stamp != fresh.Admission.Stamp || final.Residents != fresh.Residents)
                throw new ArgumentException("Original residents, authority or legal closure changed before commit.");
            // Every model and paired archive is complete before the first native diplomacy callback. No callbacks between assignments.
            Charters.CommitLawfulExit(book); payments.CommitCharterRelease(tribute); Journal = next; warHistory = history; Politics.CommitCharterRenewal(nextPolitics);
            Executing = true; Finish(fresh.Admission.Destination, peace: fresh.Admission.Peace);
        }
        catch (Exception e)
        {
            if (Current.Game == context) Interrupted(e);
            else Log.Warning("[Honor.Contracts.Preview] Lawful admission interrupted after the current game changed; original intent retained. " + e.GetType().Name);
        }
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
    internal string CaptureLawfulResidents(FollowerTransferPlan original)
    {
        var rows = new List<LawfulResidentScope>();
        foreach (var e in original.Entries.Where(e => e.Observation.HasInhabitants))
        {
            var city = Charters.Binding(e.Observation.Territory).Settlement;
            var target = Find.FactionManager.AllFactionsListForReading.Single(f => f.GetUniqueLoadID() == original.Target);
            var record = ResidentExecution.CaptureForTransfer(city); record.PrepareCareJournal(city.Faction, target, city);
            if (record.Set().Proof != e.Observation.ResidentProof ||
                record.LoadedMapScope != e.Observation.LoadedMap)
                throw new ArgumentException("Resident roster or original map changed during lawful independence review.");
            var scope = new LawfulResidentScope(e.Charter, record.Census);
            if (record.Profile == 16)
            {
                if (!NativeTransferBodies.HaulFactionBodies || !record.Pawns.All(ResidentActiveWork.CanSurvey))
                    throw new ArgumentException("Original installed hauling scope required for lawful independence.");
                NativeWorkFactionCall.Prepare();
                ResidentActiveWork.Validate(record);
                scope = scope with { HaulMapProof = LoadedMapPreservation.Restore(record.LoadedMapScope).Proof };
            }
            rows.Add(scope);
        }
        return rows.Count == 0 ? null : JsonSerializer.Serialize(rows);
    }
    private void CheckLawfulResidentPreparation(string charter, ResidentExecutionRecord residents)
    {
        var intent = Current.Game.GetComponent<PoliticalTransitionLedger>().Journal.Current;
        if (intent?.LawfulResidents == null || !Current.Game.GetComponent<PoliticalTransitionLedger>().Journal.Pending) return;
        var expected = JsonSerializer.Deserialize<LawfulResidentScope[]>(intent.LawfulResidents).SingleOrDefault(r => r.Charter == charter);
        if (expected?.Census != residents?.Census) throw new ArgumentException("Resident execution differs from original lawful admission scope.");
        if (residents != null && residents.LoadedMapScope != LawfulAdmissions.OriginalMap(intent, charter)) throw new ArgumentException("Original lawful resident map scope changed.");
    }
    private Action ResidentCallAuthority(TerritoryTransferReference reference)
    {
        var game = Current.Game;
        var political = game.GetComponent<PoliticalTransitionLedger>();
        var parent = political.Journal.Current;
        bool lawful = political.Journal.Pending && parent?.LawfulResidents != null;
        if (!lawful && reference.CustodyAgreementV1 == null) return null;
        return () => {
            if (Current.Game != game) throw new ArgumentException("Resident transfer belongs to another game.");
            if (lawful)
            {
                if (!political.Journal.Pending || political.Journal.Current != parent)
                    throw new ArgumentException("Original lawful resident authority changed.");
                LawfulAdmissions.ValidatePair(parent, Charters.Book, game.GetComponent<TributePaymentLedger>().Ledger,
                    game.GetComponent<CharterProtectionLedger>().Ledger);
                LawfulAdmissions.ValidateCurrentPhysical(parent, Ledger);
                CheckLawfulResidentPreparation(Ledger.Transfer(reference.Id).Charter, reference.Residents);
                political.VerifyPeace();
            }
            if (reference.CustodyAgreementV1 != null) RequireCurrentCustody(reference);
        };
    }
    internal void ValidateLawfulResidentExecution(AllegianceTransition intent, bool pending)
    {
        if (intent?.LawfulResidents == null) return;
        var approved = LawfulAdmissions.OriginalFollowers(intent);
        var closure = LawfulExitPreparation.Restore(intent.LawfulExit);
        var original = TerritoryTransferLedger.Restore(closure.BeforeTransfers);

        foreach (var scope in JsonSerializer.Deserialize<LawfulResidentScope[]>(intent.LawfulResidents))
        {
            var observation = approved[scope.Charter];
            var census = ResidentTransferCensus.Restore(scope.Census);
            var candidates = Ledger.Transfers.Skip(original.Transfers.Count).Where(t => t.Charter == scope.Charter && t.PreparedTick >= intent.PreparedTick &&
                (!intent.FinishedTick.HasValue || t.PreparedTick <= intent.FinishedTick)).ToArray();
            int applied = Array.FindIndex(candidates, t => t.Phase == TerritoryTransferPhase.Applied);
            var rows = applied < 0 ? candidates : candidates.Take(applied + 1).ToArray();
            if (intent.Phase == AllegianceTransitionPhase.Completed && applied < 0) throw new ArgumentException("Completed lawful resident admission lost its actual original city outcome.");
            foreach (var row in rows)
            {
                var reference = references.SingleOrDefault(r => r.Id == row.Id);
                if (row.Territory != census.Town || row.FromOwner != census.Source || row.TargetOwner != census.Target || row.Phase == TerritoryTransferPhase.Destroyed ||
                    row.Purpose != TerritoryTransferPurpose.ActiveCharter || row.ReleasedRoot != null || row.LoadedMap != observation.LoadedMap ||
                    row.StockFormat != observation.StockFormat || row.StockProof != observation.StockProof || row.ResidentProof != observation.ResidentProof ||
                    reference?.Residents?.Census != scope.Census || reference.ResidentCensusProof != census.Proof || !reference.ResidentCareJournalRequired || !reference.CityCallV1 ||
                    row.Phase == TerritoryTransferPhase.Applied && (!reference.CityCallStarted || !reference.CityCallReturned))
                    throw new ArgumentException("Missing original lawful resident execution/call evidence.");
                ResidentExecution.ValidateCensus(reference.Residents, census.Proof, row.Territory, row.FromOwner, row.TargetOwner, row.Phase == TerritoryTransferPhase.Applied);
                bool current = pending && row.Phase == TerritoryTransferPhase.Prepared;
                if (current && reference.OriginalHaulDropReturn != null)
                {
                    if (row.LawfulHaulParentProof != closure.Proof || scope.HaulMapProof != row.StockProof)
                        throw new ArgumentException("Original delivering city must belong to this approved lawful exit.");
                    if (RetainsDeliveredHaul(row, reference)) current = false;
                }
                ValidateMapReference(row, reference, current);
            }
            if (!pending) continue;
            if (rows.Any(t => t.Phase is TerritoryTransferPhase.Prepared or TerritoryTransferPhase.Applied)) continue;
            var city = Charters.Binding(census.Town).Settlement;

            var currentMap = city.HasMap ? LoadedMapRecords.Capture(city).Snapshot : null;
            if (currentMap != observation.LoadedMap)
                throw new ArgumentException("Unstarted lawful resident map changed since approval.");
            var fresh = ResidentExecution.CaptureScoped(city, census.Profile, currentMap);
            if (city.Faction?.GetUniqueLoadID() != census.Source || fresh.Profile != census.Profile || fresh.Set().Export() != census.Residents)
                throw new ArgumentException("Unstarted lawful resident scope changed; do not silently rebase original medical evidence.");
        }
    }
    internal FollowerTransferPlan ReviewLawfulFollowers(CharterBook book, Faction target, IReadOnlyList<FollowerCandidate> original)
    {
        if (RestoreFailed || Charters.RestoreFailed || original == null || Ledger.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared))
            throw new ArgumentException("Original resolved physical scope required.");
        return FollowerTransfers.Review(book, Ledger, original, target.GetUniqueLoadID(), Find.TickManager.TicksGame, AdministrationId);
    }
}
