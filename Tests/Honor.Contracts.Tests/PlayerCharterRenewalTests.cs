using System.Text.Json;
using Honor.Contracts.Rules;

internal static class PlayerCharterRenewalTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string label) { count++; if (!ok) throw new Exception("Player charter renewal: " + label); }
        void Reject(Action action, string label) { bool bad = false; try { action(); } catch (ArgumentException) { bad = true; } Check(bad, label); }
        var book = new CharterBook(); book.Register(new("n", "realm", false), 0); book.Register(new("p", "player", true), 0);
        book.Register(new("a", "realm", false), 0); book.Register(new("b", "realm", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string l, string s) => new(l, s, 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m);
        book.Offer("old", Terms("n", "p"), 0, PoliticalWorld.Day, obs); book.Sign("old", true, 0, obs); book.Sign("old", false, 0, obs);
        book.Offer("child", Terms("p", "a"), 0, PoliticalWorld.Day, obs); book.Sign("child", true, 0, obs); book.Sign("child", false, 0, obs);
        book.OfferNpc("deep", Terms("a", "b"), new("historic", "historic"), 0, PoliticalWorld.Day, obs);
        book.SignNpc("deep", "a", "historic", 0, obs); book.SignNpc("deep", "b", "historic", 0, obs);
        string original = book.Export(); var transfers = new TerritoryTransferLedger(); var payments = new TributeLedger();
        var actual = new PlayerAllegiance("player"); actual.Apply(new(AllegianceAction.BindLiege, 1, "realm", "old", Consent: true));
        string oldPolitics = actual.Export(), oldTransfers = transfers.Export(), oldPayments = payments.Export();
        var currentJournal = new AllegianceTransitionLedger(); var decision = new NpcRealmDecisionEvidence("realm", "ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var candidates = new[] { new FollowerCandidate("child", new("a", "realm", 3)), new FollowerCandidate("deep", new("b", "realm", 3)) };
        var plan = CharterAmendments.Review(book, transfers, payments, "old", "new", new(120, 5, 1, 10), decision, 75, 10, obs);
        Check(plan.Witness.Payments == oldPayments && book.Export() == original && actual.Export() == oldPolitics,
            "player renewal binds exact actual tribute evidence without releasing original contract or allegiance during review");
        var receipt = PlayerCharterRenewals.Stage(plan, actual, currentJournal, candidates);
        var done = new CharterAmendments().Commit(plan, book, transfers, payments, decision, 75, 10, obs);
        var archive = new PlayerCharterRenewals().Append(receipt); archive.ValidatePair(done.Archive, done.Book);
        var after = PlayerAllegiance.Restore(receipt.After); var journal = AllegianceTransitionLedger.Restore(receipt.TransitionAfter);
        archive.ValidateCurrent(after, journal);
        Check(after.State.Authority == "realm" && after.State.Contract == "new" && after.State.Mode == PlayerAllegianceMode.Vassal && !after.State.Suspended &&
            after.NativePlayer == "player" && journal.Current.Phase == AllegianceTransitionPhase.Completed && !journal.Pending && journal.Current.Wars == null,
            "same real authority and native player control remain while the actual contract pointer changes atomically with a completed no-move intent");
        Check(done.Book.Charter("old").Phase == RecordedCharterPhase.Released && done.Book.Charter("old").Terms == book.Charter("old").Terms &&
            done.Book.Charter("new").Phase == RecordedCharterPhase.Active && done.Book.Charter("child") == book.Charter("child") && done.Book.Charter("deep") == book.Charter("deep") &&
            done.Book.Root("b") == "n" && transfers.Export() == oldTransfers && payments.Export() == oldPayments,
            "old debt/signature basis, four-level descendants and all physical/payment journals are unchanged");
        PlayerCharterRenewals.Restore(archive.Export()).ValidatePair(CharterAmendments.Restore(done.Archive.Export()), CharterBook.Restore(done.Book.Export()));
        Check(PlayerCharterRenewals.Restore(archive.Export()).Export() == archive.Export(), "coupled consent and political renewal archive survives restore");
        Reject(() => new PlayerCharterRenewals().ValidatePair(done.Archive, done.Book), "player-subject amendment cannot restore without political coupling");
        Reject(() => archive.ValidateCurrent(actual, journal), "old player contract pointer cannot survive a committed new charter");
        Reject(() => archive.ValidateCurrent(after, new()), "missing actual political journal blocks restore");
        Reject(() => PlayerCharterRenewals.Stage(plan, new PlayerAllegiance("player"), currentJournal, candidates), "signed-but-unapplied or independent player is not this same-authority renewal path");
        var suspended = PlayerAllegiance.Restore(oldPolitics); suspended.Apply(new(AllegianceAction.Suspend, 2));
        Reject(() => PlayerCharterRenewals.Stage(plan, suspended, currentJournal, candidates), "suspended diplomacy must be resolved separately");
        var wrong = new PlayerAllegiance("player"); wrong.Apply(new(AllegianceAction.BindLiege, 1, "foreign", "old", Consent: true));
        Reject(() => PlayerCharterRenewals.Stage(plan, wrong, currentJournal, candidates), "renewal cannot change the actual superior or assume new wars");
        Reject(() => PlayerCharterRenewals.Stage(plan, actual, currentJournal, candidates.Take(1)), "every direct and indirect NPC follower remains in the review");
        Reject(() => CharterAmendments.Review(book, transfers, payments, "old", "bad", new(10, 5, 1, 10), decision, 75, 10, obs), "fresh NPC revenue acceptance policy applies");
        Reject(() => PlayerCharterRenewals.Stage(plan with { Witness = plan.Witness with { Payments = null } }, actual, currentJournal, candidates), "legacy null payment field cannot authorize player-subject renewal");
        var pendingJournal = new AllegianceTransitionLedger();
        pendingJournal.Prepare("pending", new PlayerAllegiance("player"), new(AllegianceAction.BindLiege, 1, "realm", "old", Consent: true),
            FollowerTransfers.Review(book, transfers, candidates, "realm", 1), transfers);
        Reject(() => PlayerCharterRenewals.Stage(plan, actual, pendingJournal, candidates), "pending political execution cannot be replaced by a renewal");
        var moved = new TerritoryTransferLedger(); moved.Prepare("move", book, "child", new("a", "realm", 3), "other", "other", null, TerritoryTransferPurpose.ActiveCharter, 2);
        moved.Confirm("move", new("a", "other", 3), 3);
        var moveCandidates = candidates.Select(c => c.Charter == "child" ? c with { Observation = c.Observation with { Owner = "other" } } : c).ToArray();
        var movePlan = CharterAmendments.Review(book, moved, payments, "old", "new", new(120, 5, 1, 10), decision, 75, 10, obs);
        Check(FollowerTransfers.Review(CharterBook.Restore(movePlan.After), moved, moveCandidates, "realm", 10).Moves.Count == 1, "fixture contains a genuine otherwise eligible follower move");
        Reject(() => PlayerCharterRenewals.Stage(movePlan, actual, currentJournal, moveCandidates), "economic renewal never performs or hides a real follower migration");
        var owing = new TributeLedger(); owing.Enable(book, "old", 0);
        Reject(() => CharterAmendments.Review(book, transfers, owing, "old", "new", new(120, 5, 1, 10), decision, 75, PoliticalWorld.Day * 7, obs),
            "same-party actual overdue tribute blocks fresh NPC renewal consent without erasing debt");
        var laterPlan = CharterAmendments.Review(done.Book, transfers, payments, "new", "third", new(130, 5, 1, 10), decision with { Representative = "successor" }, 75, 20, obs);
        var laterReceipt = PlayerCharterRenewals.Stage(laterPlan, after, journal, candidates);
        var laterBook = done.Archive.Commit(laterPlan, done.Book, transfers, payments, decision with { Representative = "successor" }, 75, 20, obs);
        var laterArchive = archive.Append(laterReceipt); laterArchive.ValidatePair(laterBook.Archive, laterBook.Book);
        laterArchive.ValidateCurrent(PlayerAllegiance.Restore(laterReceipt.After), AllegianceTransitionLedger.Restore(laterReceipt.TransitionAfter));
        Check(laterArchive.Records.Count == 2 && laterBook.Book.Charter("old") == done.Book.Charter("old") && laterBook.Book.Charter("new").Phase == RecordedCharterPhase.Released,
            "repeated renewals preserve command prefixes, original economics and prior mutual releases");
        var war = UnionWarAdmission.ReviewFeudal("realm", "player", "ruler", 1, [new("enemy", -100, SharedRelationKind.Hostile, -100, SharedRelationKind.Hostile, true, true)], 75);
        var warJournal = new AllegianceTransitionLedger();
        warJournal.Prepare("war", new PlayerAllegiance("player"), new(AllegianceAction.BindLiege, 1, "realm", "old", Consent: true), FollowerTransfers.Review(book, transfers, candidates, "realm", 1), transfers, war);
        warJournal.ConfirmAllegiance(actual, 1); warJournal.ObserveWar("enemy", true, -100, -100, false, 1); warJournal.Complete(actual, FollowerTransfers.Review(book, transfers, candidates, "realm", 1), 1);
        var wartime = PlayerCharterRenewals.Stage(plan, actual, warJournal, candidates);
        UnionWarAdmission.ValidateHistory([warJournal.Export()], AllegianceTransitionLedger.Restore(wartime.TransitionAfter).Current);
        Check(wartime.TransitionBefore == warJournal.Export() && AllegianceTransitionLedger.Restore(wartime.TransitionAfter).Current.Wars == null,
            "original completed war consent can be archived on exact command lineage without new declarations or replay");
        var saved = JsonSerializer.Deserialize<PlayerCharterRenewalSave>(archive.Export());
        Reject(() => PlayerCharterRenewals.Restore(JsonSerializer.Serialize(saved with { Records = [saved.Records[0], saved.Records[0]] })), "duplicate renewal archive rejected");
        var changed = new PlayerCharterRenewals().Append(receipt with { After = receipt.Before });
        Reject(() => changed.ValidatePair(done.Archive, done.Book), "political pointer cannot be omitted from paired transaction");
        Check(book.Export() == original && actual.Export() == oldPolitics && currentJournal.Current == null && payments.Export() == oldPayments,
            "all review/failure/staging branches leave actual original state untouched");
        return count;
    }
}
