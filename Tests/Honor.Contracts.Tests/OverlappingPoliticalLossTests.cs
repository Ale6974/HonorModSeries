using System.Text.Json;
using Honor.Contracts.Rules;

internal static class OverlappingPoliticalLossTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string label) { count++; if (!ok) throw new Exception("Overlapping political losses: " + label); }
        void Reject(Action action, string label)
        { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, label); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0);
        foreach (string id in new[] { "a", "b", "c", "d" }) book.Register(new("territory/" + id, "source", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        foreach (string id in new[] { "a", "b", "c", "d" })
        {
            book.Offer(id, new("p", "territory/" + id, 137, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .23m), 0, PoliticalWorld.Day, obs);
            book.Sign(id, true, 0, obs); book.Sign(id, false, 0, obs);
        }
        var stock = new EmptyTownStock(false, false, -1); var transfers = new TerritoryTransferLedger(); var player = new PlayerAllegiance("player");
        var candidates = new[] { "a", "b", "c", "d" }.Select(id => new FollowerCandidate(id, new("territory/" + id, "source", 10, StockProof: stock.Proof))).ToArray();
        var journal = new AllegianceTransitionLedger(); var cmd = new AllegianceCommand(AllegianceAction.JoinUnion, 10, "union", Consent: true, NoActiveFeudalContract: true);
        journal.Prepare("intent", player, cmd, FollowerTransfers.Review(book, transfers, candidates, "union", 10), transfers);
        player.Apply(cmd); journal.ConfirmAllegiance(player, 10);
        string beforeBook = book.Export(), beforeTransfers = transfers.Export(), beforePolitical = journal.Export();
        PoliticalTownLoss Witness(string id) => new("loss-" + id, id, id, "source", id, 20, 22, beforeBook, beforeTransfers, beforePolitical, stock);
        var archive = new PoliticalTownLossArchive();
        foreach (string id in new[] { "a", "b", "c" }) archive = archive.Observe(Witness(id), book, transfers, journal);
        Check(archive.Pending && archive.Records.Count == 3 && archive.Records.All(r => r.ResolvedTick == null && r.AcknowledgedBefore == null),
            "three distinct normal-return observations share immutable original scopes without inventing transfers");
        var stale = archive.Review("loss-a", book, transfers, journal, 30);
        var first = archive.Commit(archive.Review("loss-b", book, transfers, journal, 30), book, transfers, journal, 30);
        first.Archive.ValidatePair(first.Book, transfers); first.Archive.ValidatePolitical(first.Political);
        Check(first.Political.Current.Followers.Count == 4 && first.Political.Current.TownLosses.Single().Charter == "b" &&
            first.Political.MatchesSurvivingFollowers(new[] { "a", "c", "d" }) && first.Archive.Pending,
            "one explicit acknowledgement removes only its proven follower from remaining migration");
        Reject(() => first.Archive.Commit(stale, first.Book, transfers, first.Political, 30), "pre-sibling confirmation cannot omit committed outcomes");
        var reload = PoliticalTownLossArchive.Restore(first.Archive.Export());
        var head = CharterBook.Restore(first.Book.Export()); var intent = AllegianceTransitionLedger.Restore(first.Political.Export());
        reload.ValidatePair(head, TerritoryTransferLedger.Restore(transfers.Export())); reload.ValidatePolitical(intent);
        Check(reload.Export() == first.Archive.Export(), "partially acknowledged cohort survives reload exactly");
        var review = reload.Review("loss-a", head, transfers, intent, 30);
        Check(review.BookBefore == head.Export() && review.PoliticalBefore == intent.Export() && review.PoliticalBefore != beforePolitical,
            "fresh confirmation binds actual command head instead of rewriting original event scope");
        var second = reload.Commit(review, head, transfers, intent, 30);
        Check(second.Archive.Records.Single(r => r.Id == "loss-a").AcknowledgedBefore == first.Political.Export() &&
            second.Archive.Records.Single(r => r.Id == "loss-a").PoliticalBefore == beforePolitical &&
            second.Political.Current.TownLosses.Select(r => r.Charter).SequenceEqual(new[] { "b", "a" }),
            "same-tick acknowledgement order is saved separately from immutable original political evidence");
        var third = second.Archive.Commit(second.Archive.Review("loss-c", second.Book, transfers, second.Political, 40), second.Book, transfers, second.Political, 40);
        third.Archive.ValidatePair(third.Book, transfers); third.Archive.ValidatePolitical(third.Political);
        Check(!third.Archive.Pending && third.Political.MatchesSurvivingFollowers(new[] { "d" }) && transfers.Export() == beforeTransfers &&
            third.Book.Charter("d") == book.Charter("d"), "all losses close individually while unrelated town and complete transfer history remain intact");
        Check(new[] { "a", "b", "c" }.All(id => third.Book.Charter(id).Terms == book.Charter(id).Terms && third.Book.Charter(id).PlayerSignedTick == book.Charter(id).PlayerSignedTick &&
            third.Book.Charter(id).NpcSignedTick == book.Charter(id).NpcSignedTick && third.Book.Charter(id).PlayerReleaseTick == null && third.Book.Charter(id).TerritoryLostTick == 20),
            "retain economics, signatures, original loss time and debt basis without fabricated release");
        var arriving = TerritoryTransferLedger.Restore(transfers.Export());
        arriving.Prepare("move-d", third.Book, "d", candidates[3].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 50);
        arriving.Confirm("move-d", candidates[3].Observation with { Owner = "union" }, 50);
        var remaining = FollowerTransfers.Review(third.Book, arriving, new[] { candidates[3] with { Observation = candidates[3].Observation with { Owner = "union" } } }, "union", 50);
        var completed = AllegianceTransitionLedger.Restore(third.Political.Export()); completed.Complete(player, remaining, 50);
        var final = PoliticalTownLossArchive.Restore(third.Archive.Export()); final.ValidatePair(third.Book, arriving); final.ValidatePolitical(completed);
        Check(!completed.Pending && completed.Current.Followers.Count == 4 && completed.Current.TownLosses.Length == 3 && completed.Current.LostFollowers == null,
            "surviving city arrival completes original coordinated migration with all loss receipts retained");
        var altered = CharterBook.Restore(first.Book.Export()); altered.Register(new("extra", "source", false), 31);
        Reject(() => first.Archive.Review("loss-a", altered, transfers, first.Political, 40), "unrelated book changes cannot be rebased");
        Reject(() => first.Archive.Review("loss-a", first.Book, transfers, journal, 40), "old command head cannot erase acknowledged sibling");
        var newer = Witness("d") with { BookBefore = first.Book.Export(), PoliticalBefore = first.Political.Export(), LossTick = 31, ObservedTick = 31 };
        Reject(() => first.Archive.Observe(newer, first.Book, transfers, first.Political), "another epoch cannot strand unresolved earlier observations");
        Reject(() => archive.Observe(Witness("a") with { Id = "duplicate" }, book, transfers, journal), "same city cannot acquire duplicate observation authority");
        Reject(() => new PoliticalTownLossArchive().Observe(Witness("a") with { AcknowledgedBefore = beforePolitical }, book, transfers, journal), "unresolved witness cannot invent acknowledgement lineage");
        var saved = JsonSerializer.Deserialize<PoliticalTownLossSave>(third.Archive.Export());
        var without = PoliticalTownLossArchive.Restore(JsonSerializer.Serialize(saved with { Records = saved.Records.Where(r => r.Id != "loss-b").ToArray() }));
        Reject(() => without.ValidatePolitical(completed), "missing original native evidence cannot explain follower omissions");
        var wrongHead = PoliticalTownLossArchive.Restore(JsonSerializer.Serialize(saved with { Records = saved.Records.Select(r => r.Id == "loss-a" ? r with { AcknowledgedBefore = null } : r).ToArray() }));
        Reject(() => wrongHead.ValidatePolitical(completed), "dropping actual acknowledgement head cannot reorder same-tick receipts");
        var alteredHead = JsonSerializer.Deserialize<AllegianceTransitionSave>(first.Political.Export());
        Reject(() => PoliticalTownLossArchive.Restore(JsonSerializer.Serialize(saved with { Records = saved.Records.Select(r => r.Id == "loss-a" ? r with {
            AcknowledgedBefore = JsonSerializer.Serialize(alteredHead with { Current = alteredHead.Current with { Id = "other-intent" } }) } : r).ToArray() })),
            "acknowledgement cannot replace original command identity or authority");
        var changedWitness = PoliticalTownLossArchive.Restore(JsonSerializer.Serialize(saved with { Records = saved.Records.Select(r => r.Id == "loss-a" ? r with { Label = "changed" } : r).ToArray() }));
        Reject(() => changedWitness.ValidatePolitical(completed), "acknowledgement lineage cannot rewrite native witness digest");
        var reordered = JsonSerializer.Deserialize<AllegianceTransitionSave>(third.Political.Export());
        var badOrder = AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(reordered with { Current = reordered.Current with {
            TownLosses = new[] { reordered.Current.TownLosses[1], reordered.Current.TownLosses[0], reordered.Current.TownLosses[2] } } }));
        Reject(() => third.Archive.ValidatePolitical(badOrder), "same-tick receipts must preserve actual explicit order");
        var standalone = new PoliticalTownLossArchive().Observe(Witness("a"), book, transfers, journal);
        var legacy = standalone.Commit(standalone.Review("loss-a", book, transfers, journal, 30), book, transfers, journal, 30);
        Check(!legacy.Archive.Export().Contains("AcknowledgedBefore") && !JsonSerializer.Serialize(Witness("a")).Contains("AcknowledgedBefore"),
            "single-loss legacy shape, positional constructor and original digest remain unchanged");
        Reject(() => final.Review("loss-a", third.Book, transfers, third.Political, 51), "completed acknowledgement cannot replay");
        Check(book.Export() == beforeBook && transfers.Export() == beforeTransfers && journal.Export() == beforePolitical && archive.Records.All(r => r.ResolvedTick == null),
            "successful staging and rejected changes never mutate original book, journal, stock or witnesses");
        return count;
    }
}
