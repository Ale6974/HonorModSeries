using System.Text.Json;
using Honor.Contracts.Rules;

internal static class PoliticalTownLossTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception("Political town loss: " + name); }
        void Reject(Action action, string name) { bool failed = false; try { action(); } catch (ArgumentException) { failed = true; } Check(failed, name); }
        var book = new CharterBook(); book.Register(new("p", "native-player", true), 0);
        foreach (string id in new[] { "a", "b" }) book.Register(new("territory/" + id, "source", false), 0);
        var observations = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        foreach (string id in new[] { "a", "b" })
        {
            book.Offer(id, new("p", "territory/" + id, 137, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .23m), 0, PoliticalWorld.Day, observations);
            book.Sign(id, true, 0, observations); book.Sign(id, false, 0, observations);
        }
        var stock = new EmptyTownStock(false, false, -1); var transfers = new TerritoryTransferLedger();
        var candidates = new[] { "a", "b" }.Select(id => new FollowerCandidate(id, new("territory/" + id, "source", 3, StockProof: stock.Proof))).ToArray();
        var player = new PlayerAllegiance("native-player"); var journal = new AllegianceTransitionLedger();
        var command = new AllegianceCommand(AllegianceAction.JoinUnion, 10, "union", Consent: true, NoActiveFeudalContract: true);
        journal.Prepare("politics", player, command, FollowerTransfers.Review(book, transfers, candidates, "union", 10), transfers);
        string prepared = journal.Export(); player.Apply(command); journal.ConfirmAllegiance(player, 10);
        string applied = journal.Export();
        Check(!applied.Contains("LostFollowers"), "legacy political JSON omits new null receipt slot");
        transfers.Prepare("move-a", book, "a", candidates[0].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 20);
        var witness = new PendingTownLoss("move-a", "a", "source", "A", 25, 25, book.Export(), transfers.Export(), stock, PoliticalBefore: applied);
        var archive = new PendingTownLossArchive();
        Reject(() => archive.Observe(witness with { PoliticalBefore = prepared }, book, transfers), "pre-allegiance intent is not applied loss authority");
        var wrongScope = JsonSerializer.Deserialize<AllegianceTransitionSave>(applied);
        Reject(() => archive.Observe(witness with { PoliticalBefore = JsonSerializer.Serialize(wrongScope with { Current = wrongScope.Current with { Followers = new[] { "b" } } }) }, book, transfers),
            "unreviewed city cannot be silently inserted into the intent");
        archive = archive.Observe(witness, book, transfers); archive.ValidatePolitical(journal);
        var result = archive.Commit(archive.Review("move-a", book, transfers, 30), book, transfers, 30);
        var remaining = FollowerTransfers.Review(result.Book, result.Ledger, new[] { candidates[1] }, "union", 30);
        Reject(() => journal.Complete(player, remaining with { Moves = Array.Empty<string>() }, 30), "closing a city alone does not shrink the political follower review");
        Reject(() => result.Archive.ValidatePolitical(journal), "resolved native loss also requires a political receipt");
        string before = journal.Export();
        Reject(() => journal.AcknowledgeFollowerLoss(archive, book, transfers, "move-a", 30), "pending observation is not a completed loss receipt");
        Check(journal.Export() == before, "failed acknowledgement retains political intent exactly");
        journal.AcknowledgeFollowerLoss(result.Archive, result.Book, result.Ledger, "move-a", 30);
        Check(journal.Current.Followers.SequenceEqual(new[] { "a", "b" }) && journal.Current.LostFollowers.Single().Charter == "a" &&
            journal.MatchesSurvivingFollowers(new[] { "b" }) && !journal.MatchesSurvivingFollowers(Array.Empty<string>()),
            "original complete electorate is immutable; only proved losses are separately accounted for");
        Reject(() => journal.AcknowledgeFollowerLoss(result.Archive, result.Book, result.Ledger, "move-a", 30), "same loss cannot replay");
        var reloaded = AllegianceTransitionLedger.Restore(journal.Export());
        PendingTownLossArchive.Restore(result.Archive.Export()).ValidatePolitical(reloaded);
        Check(reloaded.Export() == journal.Export() && reloaded.IsAfter(player), "pending political loss receipt and original command survive reload");
        Reject(() => new PendingTownLossArchive().ValidatePolitical(reloaded), "missing native archive cannot authorize follower omission");
        var changed = JsonSerializer.Deserialize<AllegianceTransitionSave>(journal.Export());
        var forged = AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(changed with { Current = changed.Current with {
            LostFollowers = new[] { changed.Current.LostFollowers[0] with { Proof = new string('A', 64) } } } }));
        Reject(() => result.Archive.ValidatePolitical(forged), "political receipt is bound to actual witness digest");
        var wrongHead = JsonSerializer.Deserialize<AllegianceTransitionSave>(applied);
        var unrelated = AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(wrongHead with { Current = wrongHead.Current with { Id = "other" } }));
        Reject(() => archive.ValidatePolitical(unrelated), "unresolved observation cannot be abandoned under another intent head");
        var good = AllegianceTransitionLedger.Restore(journal.Export());
        var arrived = TerritoryTransferLedger.Restore(result.Ledger.Export());
        arrived.Prepare("move-b", result.Book, "b", candidates[1].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 40);
        arrived.Confirm("move-b", candidates[1].Observation with { Owner = "union" }, 40);
        var final = FollowerTransfers.Review(result.Book, arrived, new[] { candidates[1] with { Observation = candidates[1].Observation with { Owner = "union" } } }, "union", 40);
        good.Complete(player, final, 40); result.Archive.ValidatePolitical(good);
        Check(good.Current.Phase == AllegianceTransitionPhase.Completed && good.Current.LostFollowers.Length == 1 && arrived.Transfer("move-a").Phase == TerritoryTransferPhase.Destroyed &&
            arrived.Transfer("move-b").Phase == TerritoryTransferPhase.Applied, "completion distinguishes a lost original follower from the surviving successfully moved town");
        Check(AllegianceTransitionLedger.Restore(good.Export()).Export() == good.Export(), "completed mixed result survives roundtrip");
        // A later independent positive loss uses the exact receipt chain, not a rewritten original follower list.
        var second = TerritoryTransferLedger.Restore(result.Ledger.Export());
        second.Prepare("move-b", result.Book, "b", candidates[1].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 40);
        var secondWitness = new PendingTownLoss("move-b", "b", "union", "B", 50, 50, result.Book.Export(), second.Export(), stock, PoliticalBefore: journal.Export());
        var secondArchive = result.Archive.Observe(secondWitness, result.Book, second); secondArchive.ValidatePolitical(journal);
        var both = secondArchive.Commit(secondArchive.Review("move-b", result.Book, second, 60), result.Book, second, 60);
        journal.AcknowledgeFollowerLoss(both.Archive, both.Book, both.Ledger, "move-b", 60);
        both.Archive.ValidatePair(both.Book, both.Ledger); both.Archive.ValidatePolitical(journal);
        Reject(() => journal.Complete(player, new("union", Array.Empty<FollowerCandidate>(), Array.Empty<string>(), "", 59), 59), "receipt clocks prevent backwards completion");
        journal.Complete(player, FollowerTransfers.Review(both.Book, both.Ledger, Array.Empty<FollowerCandidate>(), "union", 60), 60);
        Check(journal.Current.Followers.Count == 2 && journal.Current.LostFollowers.Length == 2 && !journal.Pending, "all separately witnessed losses retain all original followers and no fabricated transfers");
        var reordered = JsonSerializer.Deserialize<AllegianceTransitionSave>(journal.Export());
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(reordered with { Current = reordered.Current with { LostFollowers = reordered.Current.LostFollowers.AsEnumerable().Reverse().ToArray() } })),
            "saved receipt order cannot reverse actual resolution time");
        var warTransfers = new TerritoryTransferLedger(); var warPlayer = new PlayerAllegiance("native-player"); var warJournal = new AllegianceTransitionLedger();
        var targets = new[] { "enemy1", "enemy2" }.Select(id => new UnionWarTarget(id, 0, SharedRelationKind.Neutral, 0, SharedRelationKind.Neutral, true, true)).ToArray();
        var wars = UnionWarAdmission.Review("union", "native-player", "leader", 10, Array.Empty<string>(), targets, 50);
        warJournal.Prepare("wartime", warPlayer, command, FollowerTransfers.Review(book, warTransfers, candidates, "union", 10), warTransfers, wars);
        warPlayer.Apply(command); warJournal.ConfirmAllegiance(warPlayer, 10);
        warJournal.BeginWar("enemy1", 11); warJournal.ObserveWar("enemy1", true, -100, -100, true, 11); warJournal.BeginWar("enemy2", 12);
        warTransfers.Prepare("war-a", book, "a", candidates[0].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 20);
        var warWitness = witness with { Transfer = "war-a", TransfersBefore = warTransfers.Export(), PoliticalBefore = warJournal.Export() };
        var warArchive = new PendingTownLossArchive().Observe(warWitness, book, warTransfers);
        var warResult = warArchive.Commit(warArchive.Review("war-a", book, warTransfers, 30), book, warTransfers, 30);
        warJournal.AcknowledgeFollowerLoss(warResult.Archive, warResult.Book, warResult.Ledger, "war-a", 30);
        Check(warJournal.Current.Wars.Representative == "leader" && warJournal.Current.WarResults[0].Step == UnionWarStep.Verified &&
            warJournal.Current.WarResults[1].Step == UnionWarStep.Applying && !warJournal.WarsReady, "loss acknowledgement preserves original consent and verified/uncertain native war outcomes");
        var savedWar = JsonSerializer.Deserialize<AllegianceTransitionSave>(warJournal.Export());
        void RejectWarMutation(AllegianceTransition altered, string name)
            => Reject(() => warResult.Archive.ValidatePolitical(AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(savedWar with { Current = altered }))), name);
        RejectWarMutation(savedWar.Current with { Wars = savedWar.Current.Wars with { Representative = "other" } }, "historical war consent cannot be substituted after loss");
        RejectWarMutation(savedWar.Current with { WarResults = new[] { new UnionWarResult("enemy1"), savedWar.Current.WarResults[1] } }, "verified declaration cannot be erased or replayed after loss");
        RejectWarMutation(savedWar.Current with { WarResults = new[] { savedWar.Current.WarResults[0], new UnionWarResult("enemy2") } }, "uncertain applying declaration cannot be reset to unattempted");
        warJournal.ReaffirmWars(UnionWarAdmission.Review("union", "native-player", "successor", 31, new[] { "new-seat" }, targets, 50), 31);
        warJournal.ObserveWar("enemy2", true, -80, -80, false, 31); warResult.Archive.ValidatePolitical(warJournal);
        Check(warJournal.WarsReady && warJournal.Current.Wars.Representative == "leader" && warJournal.Current.WarReaffirmations.Single().Representative == "successor" &&
            !warJournal.Current.WarResults[1].FromCompletedCall, "fresh council and observation can progress separately without rewriting original consent or attributing an uncertain call");
        return count;
    }
}
