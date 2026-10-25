using System.Text.Json;
using Honor.Contracts.Rules;

internal static class PendingTownLossTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool v, string name) { count++; if (!v) throw new Exception("Pending town loss: " + name); }
        void Reject(Action action, string name) { bool failed = false; try { action(); } catch (ArgumentException) { failed = true; } Check(failed, name); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("territory/city", "source", false), 0);
        book.Register(new("child", "source", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        var terms = new RecordedCharterTerms("p", "territory/city", 137, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .23m);
        book.Offer("charter", terms, 0, PoliticalWorld.Day, obs); book.Sign("charter", true, 0, obs); book.Sign("charter", false, 0, obs);
        var ledger = new TerritoryTransferLedger(); var stock = new EmptyTownStock(false, false, -1);
        ledger.Prepare("transfer", book, "charter", new("territory/city", "source", 2, StockProof: stock.Proof), "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 10);
        var witness = new PendingTownLoss("transfer", "city", "source", "town", 20, 20, book.Export(), ledger.Export(), stock);
        var empty = new PendingTownLossArchive(); string originalBook = book.Export(), originalLedger = ledger.Export();
        Check(!originalLedger.Contains("Destruction") && (int)TerritoryTransferPhase.Prepared == 0 && (int)TerritoryTransferPhase.Applied == 1 &&
            (int)TerritoryTransferPhase.Cancelled == 2, "legacy enum values and JSON fields stay unchanged");
        Reject(() => empty.Review("transfer", book, ledger, 30), "missing reference without a positive native witness grants no review");
        var archive = empty.Observe(witness, book, ledger);
        Check(archive.HasPending("transfer") && ledger.Pending("territory/city") && book.Charter("charter").Phase == RecordedCharterPhase.Active &&
            book.Export() == originalBook && ledger.Export() == originalLedger, "observation retains both pending journals without automatically completing or cancelling");
        Reject(() => archive.Observe(witness, book, ledger), "duplicate witness cannot overwrite original event");
        var plan = archive.Review("transfer", book, ledger, 30);
        Check(ledger.Export() == originalLedger && book.Export() == originalBook, "review is read-only");
        var result = archive.Commit(plan, book, ledger, 30);
        var t = result.Ledger.Transfer("transfer"); var c = result.Book.Charter("charter");
        Check(t.Phase == TerritoryTransferPhase.Destroyed && t.Phase != TerritoryTransferPhase.Applied && t.Phase != TerritoryTransferPhase.Cancelled &&
            t.Destruction.Owner == "source" && t.Destruction.LossTick == 20 && t.ResolvedTick == 30, "destruction is a distinct exactly scoped terminal outcome");
        Check(c.Phase == RecordedCharterPhase.TerritoryLost && c.TerritoryLostTick == 20 && c.Terms == terms && c.PlayerReleaseTick == null && c.NpcReleaseTick == null &&
            c.PlayerSignedTick == book.Charter("charter").PlayerSignedTick && c.NpcSignedTick == book.Charter("charter").NpcSignedTick,
            "close only incoming territorial bond with original conditions and signatures; no fake mutual release");
        Check(!result.Ledger.Pending("territory/city") && !result.Ledger.VerifiedOwner("territory/city", "source", "source") &&
            !result.Ledger.VerifiedOwner("territory/city", "source", "target"), "destroyed identity never normalizes a returning or unrelated owner");
        var reload = PendingTownLossArchive.Restore(result.Archive.Export()); reload.ValidatePair(CharterBook.Restore(result.Book.Export()), TerritoryTransferLedger.Restore(result.Ledger.Export()));
        Check(reload.Export() == result.Archive.Export() && !reload.HasPending("transfer"), "positive witness, exact receipt and terminal journals survive reload");
        Reject(() => result.Archive.Review("transfer", result.Book, result.Ledger, 31), "resolved destruction cannot replay");
        Reject(() => new PendingTownLossArchive().ValidatePair(result.Book, result.Ledger), "destroyed transfer requires its actual stored witness");
        Reject(() => archive.Review("transfer", book, ledger, 19), "future witness cannot be acknowledged early");
        Reject(() => archive.Commit(plan, book, ledger, 31), "stale confirmation time");
        Reject(() => archive.Commit(plan with { BookAfter = originalBook }, book, ledger, 30), "cannot omit territorial closure");
        Reject(() => archive.Commit(plan with { TransfersAfter = originalLedger }, book, ledger, 30), "cannot omit transfer outcome");
        Reject(() => archive.Commit(plan with { Proof = new string('A', 64) }, book, ledger, 30), "caller cannot substitute a proof hash");
        Check(book.Export() == originalBook && ledger.Export() == originalLedger && archive.HasPending("transfer"), "failed confirmations mutate no source data");
        var targetWitness = witness with { Owner = "target" }; var targetArchive = empty.Observe(targetWitness, book, ledger);
        var targetResult = targetArchive.Commit(targetArchive.Review("transfer", book, ledger, 30), book, ledger, 30);
        Check(targetResult.Ledger.Transfer("transfer").Destruction.Owner == "target" && targetResult.Ledger.Transfer("transfer").Phase == TerritoryTransferPhase.Destroyed,
            "partly moved actual target owner is retained without claiming a successful transfer");
        foreach (var bad in new[] { witness with { City = "other" }, witness with { Owner = "unrelated" }, witness with { LossTick = 9 },
            witness with { ObservedTick = 19 }, witness with { Stock = new(true, true, 123) }, witness with { ResolvedTick = 30 } })
            Reject(() => empty.Observe(bad, book, ledger), "wrong identity/owner/time/stock/phase is not evidence");
        var changed = CharterBook.Restore(originalBook); changed.Register(new("extra", "source", false), 21);
        Reject(() => empty.Observe(witness, changed, ledger), "changed book cannot accept old observation");
        var upper = CharterBook.Restore(originalBook); var npc = new NpcCharterAuthority("ruler", "ruler");
        upper.OfferNpc("child-bond", terms with { Liege = "territory/city", Subject = "child" }, npc, 11, PoliticalWorld.Day, obs);
        upper.SignNpc("child-bond", "territory/city", "ruler", 11, obs); upper.SignNpc("child-bond", "child", "ruler", 11, obs);
        Reject(() => empty.Observe(witness with { BookBefore = upper.Export() }, upper, ledger), "upper city needs a different coordinated loss adapter");
        var beforeSave = JsonSerializer.Deserialize<TerritoryTransfersSave>(originalLedger);
        void BadSource(TerritoryTransfer alter, string name)
        { string raw = JsonSerializer.Serialize(beforeSave with { Transfers = new[] { alter } }); Reject(() => empty.Observe(witness with { TransfersBefore = raw }, book, TerritoryTransferLedger.Restore(raw)), name); }
        BadSource(beforeSave.Transfers[0] with { ResidentProof = new string('A', 64) }, "resident custody remains unsupported");
        BadSource(beforeSave.Transfers[0] with { Purpose = TerritoryTransferPurpose.ReleasedToUnion }, "released subtree purposes remain excluded");
        var goods = new[] { new CityStockItem("silver", "Silver", null, 1, 100, null) };
        BadSource(beforeSave.Transfers[0] with { StockProof = CityStockProof.Capture(true, true, 123, goods) }, "nonempty original goods cannot become an empty witness");
        var finalSave = JsonSerializer.Deserialize<TerritoryTransfersSave>(result.Ledger.Export());
        Reject(() => TerritoryTransferLedger.Restore(JsonSerializer.Serialize(finalSave with { Transfers = new[] { finalSave.Transfers[0] with { Destruction = null } } })), "destroyed phase requires its own metadata");
        Reject(() => TerritoryTransferLedger.Restore(JsonSerializer.Serialize(finalSave with { Transfers = new[] { finalSave.Transfers[0] with { Phase = TerritoryTransferPhase.Applied } } })), "destruction metadata cannot masquerade as applied");
        var saved = JsonSerializer.Deserialize<PendingTownLossSave>(result.Archive.Export());
        Reject(() => PendingTownLossArchive.Restore(JsonSerializer.Serialize(saved with { Records = new[] { saved.Records[0], saved.Records[0] } })), "duplicate saved witnesses");
        Reject(() => PendingTownLossArchive.Restore(JsonSerializer.Serialize(saved with { Version = 2 })), "unknown witness schema");
        var changedWitness = PendingTownLossArchive.Restore(JsonSerializer.Serialize(saved with { Records = new[] { saved.Records[0] with { Label = "changed" } } }));
        Reject(() => changedWitness.ValidatePair(result.Book, result.Ledger), "stored proof hash binds original witness and display label");
        return count;
    }
}
