using System.Text.Json;
using Honor.Contracts.Rules;

internal static class IndependentTownLossTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Independent town loss: " + name); }
        void Reject(Action action, string name) { bool failed = false; try { action(); } catch (ArgumentException) { failed = true; } Check(failed, name); }
        foreach (bool moved in new[] { false, true })
        {
            var book = new CharterBook(); book.Register(new("p", "player", true), 0);
            foreach (string id in new[] { "a", "b" }) book.Register(new("territory/" + id, "source", false), 0);
            var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
            foreach (string id in new[] { "a", "b" })
            {
                book.Offer(id, new("p", "territory/" + id, 137, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .23m), 0, PoliticalWorld.Day, obs);
                book.Sign(id, true, 0, obs); book.Sign(id, false, 0, obs);
            }
            var stock = new EmptyTownStock(false, false, -1); var transfers = new TerritoryTransferLedger(); var player = new PlayerAllegiance("player");
            var candidates = new[] { "a", "b" }.Select(id => new FollowerCandidate(id, new("territory/" + id, "source", 3, StockProof: stock.Proof))).ToArray();
            var journal = new AllegianceTransitionLedger(); var cmd = new AllegianceCommand(AllegianceAction.JoinUnion, 10, "union", Consent: true, NoActiveFeudalContract: true);
            journal.Prepare("intent", player, cmd, FollowerTransfers.Review(book, transfers, candidates, "union", 10), transfers);
            string prepared = journal.Export(); player.Apply(cmd); journal.ConfirmAllegiance(player, 10);
            if (moved)
            {
                transfers.Prepare("arrived-a", book, "a", candidates[0].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 20);
                transfers.Confirm("arrived-a", candidates[0].Observation with { Owner = "union" }, 20);
            }
            string beforeTransfers = transfers.Export(), beforeBook = book.Export(), beforePolitical = journal.Export();
            Check(!beforePolitical.Contains("TownLosses") && !beforeBook.Contains("PoliticalLoss"), "new null political/book slots leave old JSON unchanged");
            var witness = new PoliticalTownLoss("loss-a", "a", "a", moved ? "union" : "source", "A", 25, 25, beforeBook, beforeTransfers, beforePolitical, stock);
            var empty = new PoliticalTownLossArchive();
            Reject(() => empty.Review("loss-a", book, transfers, journal, 30), "missing reference alone is not loss evidence");
            Reject(() => empty.Observe(witness with { PoliticalBefore = prepared }, book, transfers, AllegianceTransitionLedger.Restore(prepared)), "unapplied intent has no loss authority");
            Reject(() => empty.Observe(witness with { Owner = "foreign" }, book, transfers, journal), "foreign owner cannot become a lawful prior transfer");
            Reject(() => empty.Observe(witness with { Stock = new(false, true, 10) }, book, transfers, journal), "unsupported empty stock descriptor is not invented");
            var archive = empty.Observe(witness, book, transfers, journal); archive.ValidatePair(book, transfers); archive.ValidatePolitical(journal);
            var plan = archive.Review("loss-a", book, transfers, journal, 30);
            Check(book.Export() == beforeBook && transfers.Export() == beforeTransfers && journal.Export() == beforePolitical && archive.Pending,
                "observation and review do not change contracts, past transfers or political intent");
            Reject(() => archive.Commit(plan with { BookAfter = beforeBook }, book, transfers, journal, 30), "cannot omit actual charter closure");
            Reject(() => archive.Commit(plan with { Proof = new string('A', 64) }, book, transfers, journal, 30), "cannot substitute witness digest");
            Reject(() => archive.Commit(plan, book, transfers, journal, 31), "stale confirmation is rejected");
            var result = archive.Commit(plan, book, transfers, journal, 30);
            Check(transfers.Export() == beforeTransfers && result.Book.Charter("a") == book.Charter("a") with { Phase = RecordedCharterPhase.TerritoryLost, TerritoryLostTick = 25, PoliticalLoss = new("loss-a", plan.Proof) } &&
                result.Political.Current.Followers.SequenceEqual(new[] { "a", "b" }) && result.Political.Current.TownLosses.Single().Loss == "loss-a" &&
                result.Political.Current.LostFollowers == null, "loss closes only incoming charter; immutable original followers and separate event receipt persist");
            Check(moved ? transfers.Transfer("arrived-a").Phase == TerritoryTransferPhase.Applied : transfers.Transfers.Count == 0,
                "preserve prior successful transfer or absence of a transfer; never fabricate Prepared/Destroyed rows");
            var reloaded = PoliticalTownLossArchive.Restore(result.Archive.Export());
            reloaded.ValidatePair(CharterBook.Restore(result.Book.Export()), TerritoryTransferLedger.Restore(transfers.Export()));
            reloaded.ValidatePolitical(AllegianceTransitionLedger.Restore(result.Political.Export()));
            Check(!reloaded.Pending && reloaded.LostTerritory("territory/a") && !reloaded.LostTerritory("territory/b"), "positive loss and scoped tombstone survive reload");
            Reject(() => new PoliticalTownLossArchive().ValidatePolitical(result.Political), "lost political receipt requires native witness archive");
            Reject(() => new PoliticalTownLossArchive().ValidatePair(result.Book, transfers), "book binding detects a missing native archive even after a later intent replaces current political receipts");
            var savedBook = JsonSerializer.Deserialize<CharterBookSave>(result.Book.Export());
            Reject(() => reloaded.ValidatePair(CharterBook.Restore(JsonSerializer.Serialize(savedBook with { Charters = savedBook.Charters.Select(c => c.Id == "a" ? c with { PoliticalLoss = null } : c).ToArray() })), transfers),
                "cannot silently remove the closed charter's independent witness binding");
            Reject(() => reloaded.ValidatePair(CharterBook.Restore(JsonSerializer.Serialize(savedBook with { Charters = savedBook.Charters.Select(c => c.Id == "a" ? c with { PoliticalLoss = new("other-loss", plan.Proof) } : c).ToArray() })), transfers),
                "changed charter-to-native-witness identity is rejected");
            var originalSave = JsonSerializer.Deserialize<CharterBookSave>(beforeBook);
            Reject(() => CharterBook.Restore(JsonSerializer.Serialize(originalSave with { Charters = originalSave.Charters.Select(c => c.Id == "a" ? c with { PoliticalLoss = new("loss-a", plan.Proof) } : c).ToArray() })),
                "active charters cannot acquire a fabricated lost-town binding");
            Reject(() => reloaded.Review("loss-a", result.Book, transfers, result.Political, 31), "completed loss cannot replay");
            var damaged = JsonSerializer.Deserialize<PoliticalTownLossSave>(reloaded.Export());
            var changed = PoliticalTownLossArchive.Restore(JsonSerializer.Serialize(damaged with { Records = new[] { damaged.Records[0] with { Label = "changed" } } }));
            Reject(() => changed.ValidatePolitical(result.Political), "original native witness is digest-bound to political receipt");
            Reject(() => PoliticalTownLossArchive.Restore(JsonSerializer.Serialize(damaged with { Records = new[] { damaged.Records[0], damaged.Records[0] } })), "duplicate witness rejected");
            var activePhysical = TerritoryTransferLedger.Restore(transfers.Export());
            activePhysical.Prepare("pending-b", book, "b", candidates[1].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 21);
            Reject(() => empty.Observe(witness with { TransfersBefore = activePhysical.Export() }, book, activePhysical, journal), "independent observation cannot absorb another pending city's custody");
            var arrival = TerritoryTransferLedger.Restore(transfers.Export());
            arrival.Prepare("arrived-b", result.Book, "b", candidates[1].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 40);
            arrival.Confirm("arrived-b", candidates[1].Observation with { Owner = "union" }, 40);
            var final = FollowerTransfers.Review(result.Book, arrival, new[] { candidates[1] with { Observation = candidates[1].Observation with { Owner = "union" } } }, "union", 40);
            var completed = AllegianceTransitionLedger.Restore(result.Political.Export()); completed.Complete(player, final, 40);
            reloaded.ValidatePair(result.Book, arrival); reloaded.ValidatePolitical(completed);
            Check(!completed.Pending && completed.Current.TownLosses.Length == 1 && completed.Current.Followers.Count == 2,
                "survivor arrival completes exact mixed political results without rewriting history");
            // Interleave a later prepared-transfer loss with the independent event; both witness types remain mandatory.
            var later = TerritoryTransferLedger.Restore(transfers.Export());
            later.Prepare("pending-b", result.Book, "b", candidates[1].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 40);
            var oldWitness = new PendingTownLoss("pending-b", "b", "source", "B", 50, 50, result.Book.Export(), later.Export(), stock, PoliticalBefore: result.Political.Export());
            var oldArchive = new PendingTownLossArchive().Observe(oldWitness, result.Book, later);
            var oldResult = oldArchive.Commit(oldArchive.Review("pending-b", result.Book, later, 60), result.Book, later, 60);
            var mixed = AllegianceTransitionLedger.Restore(result.Political.Export()); mixed.AcknowledgeFollowerLoss(oldResult.Archive, oldResult.Book, oldResult.Ledger, "pending-b", 60);
            reloaded.ValidatePair(oldResult.Book, oldResult.Ledger); reloaded.ValidatePolitical(mixed); oldResult.Archive.ValidatePolitical(mixed);
            mixed.Complete(player, FollowerTransfers.Review(oldResult.Book, oldResult.Ledger, Array.Empty<FollowerCandidate>(), "union", 60), 60);
            Check(mixed.LostCharters.Count() == 2 && mixed.Current.TownLosses.Length == 1 && mixed.Current.LostFollowers.Length == 1 && mixed.Current.Followers.Count == 2,
                "independent and prepared-transfer loss receipts coexist with the full original set");
            var reverseTransfers = TerritoryTransferLedger.Restore(transfers.Export());
            reverseTransfers.Prepare("reverse-b", book, "b", candidates[1].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 40);
            var reverseWitness = new PendingTownLoss("reverse-b", "b", "source", "B", 50, 50, book.Export(), reverseTransfers.Export(), stock, PoliticalBefore: journal.Export());
            var reverseArchive = new PendingTownLossArchive().Observe(reverseWitness, book, reverseTransfers);
            var reverseResult = reverseArchive.Commit(reverseArchive.Review("reverse-b", book, reverseTransfers, 60), book, reverseTransfers, 60);
            var reverseJournal = AllegianceTransitionLedger.Restore(journal.Export()); reverseJournal.AcknowledgeFollowerLoss(reverseResult.Archive, reverseResult.Book, reverseResult.Ledger, "reverse-b", 60);
            var independentLater = witness with { Id = "later-a", LossTick = 70, ObservedTick = 70, BookBefore = reverseResult.Book.Export(),
                TransfersBefore = reverseResult.Ledger.Export(), PoliticalBefore = reverseJournal.Export() };
            var reverseIndependent = new PoliticalTownLossArchive().Observe(independentLater, reverseResult.Book, reverseResult.Ledger, reverseJournal);
            var reversed = reverseIndependent.Commit(reverseIndependent.Review("later-a", reverseResult.Book, reverseResult.Ledger, reverseJournal, 80),
                reverseResult.Book, reverseResult.Ledger, reverseJournal, 80);
            reverseResult.Archive.ValidatePair(reversed.Book, reverseResult.Ledger); reverseResult.Archive.ValidatePolitical(reversed.Political);
            reversed.Archive.ValidatePair(reversed.Book, reverseResult.Ledger); reversed.Archive.ValidatePolitical(reversed.Political);
            Check(reversed.Political.LostCharters.Count() == 2 && reversed.Political.Current.LostFollowers.Single().Transfer == "reverse-b",
                "prepared loss followed by independent loss retains both original receipt prefixes in reverse interleaving");
        }
        var nested = new CharterBook(); nested.Register(new("p", "player", true), 0);
        nested.Register(new("territory/parent", "source", false), 0); nested.Register(new("territory/leaf", "source", false), 0);
        var nestedObs = nested.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        var parentTerms = new RecordedCharterTerms("p", "territory/parent", 111, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .2m);
        nested.Offer("parent", parentTerms, 0, PoliticalWorld.Day, nestedObs); nested.Sign("parent", true, 0, nestedObs); nested.Sign("parent", false, 0, nestedObs);
        var npc = new NpcCharterAuthority("ruler", "ruler");
        nested.OfferNpc("leaf", parentTerms with { Liege = "territory/parent", Subject = "territory/leaf", TributeValue = 77 }, npc, 0, PoliticalWorld.Day, nestedObs);
        nested.SignNpc("leaf", "territory/parent", "ruler", 0, nestedObs); nested.SignNpc("leaf", "territory/leaf", "ruler", 0, nestedObs);
        var nestedTransfers = new TerritoryTransferLedger(); var nestedPlayer = new PlayerAllegiance("player"); var nestedJournal = new AllegianceTransitionLedger();
        var nestedCandidates = new[] { "leaf", "parent" }.Select(id => new FollowerCandidate(id, new("territory/" + id, "source", 3))).ToArray();
        var nestedCommand = new AllegianceCommand(AllegianceAction.JoinUnion, 10, "union", Consent: true, NoActiveFeudalContract: true);
        nestedJournal.Prepare("nested", nestedPlayer, nestedCommand, FollowerTransfers.Review(nested, nestedTransfers, nestedCandidates, "union", 10), nestedTransfers);
        nestedPlayer.Apply(nestedCommand); nestedJournal.ConfirmAllegiance(nestedPlayer, 10);
        var nestedWitness = new PoliticalTownLoss("leaf-loss", "leaf", "leaf", "source", "Leaf", 20, 20, nested.Export(), nestedTransfers.Export(), nestedJournal.Export(), new(false, false, -1));
        var nestedArchive = new PoliticalTownLossArchive().Observe(nestedWitness, nested, nestedTransfers, nestedJournal);
        var nestedResult = nestedArchive.Commit(nestedArchive.Review("leaf-loss", nested, nestedTransfers, nestedJournal, 30), nested, nestedTransfers, nestedJournal, 30);
        Check(nestedResult.Book.Charter("leaf").NpcAuthority == npc && nestedResult.Book.Charter("parent") == nested.Charter("parent") &&
            nestedResult.Book.PlayerFollowers().Select(c => c.Id).SequenceEqual(new[] { "parent" }), "indirect NPC leaf loss preserves parent charter and original NPC authority/history");
        Reject(() => new PoliticalTownLossArchive().Observe(nestedWitness with { Id = "upper", Charter = "parent", City = "parent" }, nested, nestedTransfers, nestedJournal),
            "upper town loss with living active children remains outside leaf authority");
        nestedTransfers.Prepare("parent-move", nestedResult.Book, "parent", nestedCandidates[1].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 40);
        nestedTransfers.Confirm("parent-move", nestedCandidates[1].Observation with { Owner = "union" }, 40);
        nestedResult.Political.Complete(nestedPlayer, FollowerTransfers.Review(nestedResult.Book, nestedTransfers,
            new[] { nestedCandidates[1] with { Observation = nestedCandidates[1].Observation with { Owner = "union" } } }, "union", 40), 40);
        nestedResult.Archive.ValidatePair(nestedResult.Book, nestedTransfers); nestedResult.Archive.ValidatePolitical(nestedResult.Political);
        Check(nestedResult.Political.Current.Followers.Count == 2 && !nestedResult.Political.Pending, "indirect leaf loss plus surviving parent's actual arrival completes the original entire follower review");
        return count;
    }
}
