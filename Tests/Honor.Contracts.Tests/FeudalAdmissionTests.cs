using System.Text.Json;
using Honor.Contracts.Rules;

internal static class FeudalAdmissionTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Feudal admission: " + name); }
        void Reject(Action a, string name) { bool bad = false; try { a(); } catch (ArgumentException) { bad = true; } Check(bad, name); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("n", "realm", false), 0);
        book.Register(new("a", "source", false), 0); book.Register(new("b", "source", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string l, string s) => new(l, s, 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0);
        book.Offer("pledge", Terms("n", "p"), 0, PoliticalWorld.Day, obs); book.Sign("pledge", false, 0, obs); book.Sign("pledge", true, 0, obs);
        book.Offer("upper", Terms("p", "a"), 0, PoliticalWorld.Day, obs); book.Sign("upper", false, 0, obs); book.Sign("upper", true, 0, obs);
        book.OfferNpc("lower", Terms("a", "b"), new("historic", "historic"), 0, PoliticalWorld.Day, obs); book.SignNpc("lower", "a", "historic", 0, obs); book.SignNpc("lower", "b", "historic", 0, obs);
        var originalBook = book.Export(); var ledger = new TerritoryTransferLedger();
        var candidates = new[] { new FollowerCandidate("upper", new("a", "source", 3)), new FollowerCandidate("lower", new("b", "source", 3)) };
        var plan = FollowerTransfers.Review(book, ledger, candidates, "realm", 10);
        var decision = new NpcRealmDecisionEvidence("realm", "chief", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var consent = FeudalAdmissions.Review(book, "player", "pledge", 10, decision, plan.Moves);
        var actual = new PlayerAllegiance("player"); var command = new AllegianceCommand(AllegianceAction.BindLiege, 10, "realm", "pledge", Consent: true);
        var journal = new AllegianceTransitionLedger(); journal.PrepareReviewedLiegeAdmission("join", actual, command, plan, ledger, consent);
        string prepared = journal.Export();
        Check(journal.Current.Feudal.Decision.Representative == "chief" && journal.Current.FollowerReview.Original == plan.Stamp && actual.State.Mode == PlayerAllegianceMode.Independent,
            "fresh ordinary NPC consent and actual full follower source are staged before allegiance changes");
        Check(AllegianceTransitionLedger.Restore(prepared).Export() == prepared && book.Export() == originalBook && ledger.Transfers.Count == 0, "prepared restore retains all original contracts without native ownership writes");
        foreach (var bad in new[] { decision with { Regime = NpcDecisionRegime.Royal }, decision with { Regime = NpcDecisionRegime.Union },
            decision with { DecisionCapable = false }, decision with { UniqueOffice = false }, decision with { IndependentAuthority = false }, decision with { Willing = false } })
            Reject(() => FeudalAdmissions.Review(book, "player", "pledge", 10, bad, plan.Moves), "individual leader cannot impersonate collective or unavailable authority");
        Reject(() => FeudalAdmissions.Review(book, "foreign", "pledge", 10, decision, plan.Moves), "actual native player must own the original charter party");
        Reject(() => FeudalAdmissions.Review(book, "player", "pledge", 10, decision with { Realm = "foreign" }, plan.Moves), "source authority must be actual recorded political root");
        Reject(() => new AllegianceTransitionLedger().PrepareReviewedLiegeAdmission("bad", actual, command, plan, ledger, consent with { Moves = ["upper"] }), "all actual original incoming cities are required");
        Reject(() => journal.ReaffirmFeudal(consent, 10), "no applied admission re-review before actual allegiance");
        actual.Apply(command); journal.ConfirmAllegiance(actual, 10);
        Reject(() => journal.Complete(actual, plan with { Moves = [] }, 10), "cannot complete modern admission without actual current representative review");
        var successor = consent with { Tick = 11, Decision = decision with { Representative = "successor" } };
        Check(!FeudalAdmissions.SameReview(consent, successor), "a real successor needs explicit new review");
        journal.ReaffirmFeudal(successor, 11);
        Check(journal.Current.Feudal.Decision.Representative == "chief" && journal.Current.FeudalReaffirmations.Single().Decision.Representative == "successor", "original representative/history remain while fresh successor consent is appended");
        ledger.Prepare("move-a", book, "upper", candidates[0].Observation, "realm", "realm", null, TerritoryTransferPurpose.ActiveCharter, 11);
        ledger.Confirm("move-a", candidates[0].Observation with { Owner = "realm" }, 11);
        ledger.Prepare("move-b", book, "lower", candidates[1].Observation with { SourceSettlements = 2 }, "realm", "realm", null, TerritoryTransferPurpose.ActiveCharter, 12);
        ledger.Confirm("move-b", candidates[1].Observation with { Owner = "realm" }, 12);
        var final = FollowerTransfers.Review(book, ledger, candidates.Select(c => c with { Observation = c.Observation with { Owner = "realm", SourceSettlements = 3 } }), "realm", 12);
        journal.Complete(actual, final, 12);
        Check(AllegianceTransitionLedger.Restore(journal.Export()).Export() == journal.Export() && journal.Current.Wars == null && journal.Current.Peace == null && book.Export() == originalBook,
            "two-city/deeper-contract ordinary admission completes with original source and no invented wars, votes or changed terms");
        var state = JsonSerializer.Deserialize<AllegianceTransitionSave>(journal.Export());
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(state with { Current = state.Current with { FollowerReview = null } })), "new feudal receipt requires the preserved original actual full review");
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(state with { Current = state.Current with { FeudalReaffirmations = null } })), "completed ordinary admission cannot lose current consent evidence");
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(state with { Current = state.Current with { Feudal = null } })), "orphan successor consent cannot stand for a missing original decision");
        var war = UnionWarAdmission.ReviewFeudal("realm", "player", "chief", 10, [new("enemy", 0, SharedRelationKind.Neutral, 0, SharedRelationKind.Neutral, true, true)], 75);
        var warJournal = new AllegianceTransitionLedger(); warJournal.PrepareReviewedLiegeAdmission("war", new("player"), command, plan, new(), consent, war);
        Check(warJournal.Current.Wars.AuthorityKind == AdmissionAuthorityKind.FeudalLeader && warJournal.Current.Feudal != null, "separate actual war acceptance remains distinct from ordinary charter admission consent");
        Reject(() => new AllegianceTransitionLedger().PrepareReviewedLiegeAdmission("bad", new("player"), command, plan, new(), consent, war with { Representative = "other" }), "initial war and ordinary consent cannot use different current representatives");
        var legacy = new AllegianceTransitionLedger(); legacy.Prepare("legacy", new("player"), command, plan, new());
        Check(!legacy.Export().Contains("\"Feudal\":") && AllegianceTransitionLedger.Restore(legacy.Export()).Export() == legacy.Export(), "old null feudal records retain exact JSON and public API behavior without fabricated old consent");
        var amendment = CharterAmendments.Review(book, ledger, new(), "pledge", "renewed", new(120, 5, 1, 10), decision with { Representative = "successor" }, 75, 20, obs);
        var renewal = PlayerCharterRenewals.Stage(amendment, actual, journal, candidates.Select(c => c with { Observation = c.Observation with { Owner = "realm", SourceSettlements = 3 } }));
        UnionWarAdmission.ValidateHistory([journal.Export()], AllegianceTransitionLedger.Restore(renewal.TransitionAfter).Current);
        Check(renewal.TransitionBefore == journal.Export(), "same-liege economic renewal can retain actual original admission/successor history on exact command lineage");
        var cancelled = new AllegianceTransitionLedger(); cancelled.PrepareReviewedLiegeAdmission("cancel", new("player"), command, plan, new(), consent);
        cancelled.CancelUnchanged(new("player"), new(), 10);
        Check(cancelled.Current.Feudal != null && AllegianceTransitionLedger.Restore(cancelled.Export()).Export() == cancelled.Export(), "unchanged cancellation retains actual original ruler consent without membership writes");
        var pending = new AllegianceTransitionLedger(); pending.PrepareReviewedLiegeAdmission("pending", new("player"), command, plan, new(), consent);
        pending.ConfirmAllegiance(PlayerAllegiance.Restore(pending.Current.After), 10);
        Reject(() => pending.ReaffirmFeudal(consent with { Moves = ["upper"] }, 11), "recovery cannot shorten the original full movement scope");
        Reject(() => pending.ReaffirmFeudal(consent with { Tick = 11, Decision = decision with { Realm = "foreign" } }, 11), "recovery cannot substitute another real sovereign");
        for (int i = 0; i < 64; i++) pending.ReaffirmFeudal(consent with { Tick = 10 + i }, 10 + i);
        string full = pending.Export(); Reject(() => pending.ReaffirmFeudal(consent with { Tick = 74 }, 74), "full successor history never evicts old decisions");
        Check(pending.Export() == full && AllegianceTransitionLedger.Restore(full).Export() == full, "capacity failure preserves the exact recoverable original/current consents");
        return count;
    }
}
