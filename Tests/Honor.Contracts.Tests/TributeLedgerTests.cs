using System.Text.Json;
using Honor.Contracts.Rules;

internal static class TributeLedgerTests
{
    public static int Run()
    {
        int count = 0; long day = PoliticalWorld.Day;
        void Check(bool result, string name) { count++; if (!result) throw new Exception(name); }
        void Reject(TributeLedger ledger, Action action, string name)
        {
            string before = ledger.Export(); bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected && ledger.Export() == before, name + ": rejection is atomic");
        }
        var observations = new[] { new TerritoryObservation("player", "native-player", true, true, true),
            new TerritoryObservation("npc", "native-npc", true, false, true) };
        CharterBook Book(TributeTransport mode = TributeTransport.SubjectDelivery, decimal rate = .1m)
        {
            var book = new CharterBook(); book.Register(new("player", "native-player", true), 0); book.Register(new("npc", "native-npc", false), 0);
            book.Offer("charter", new("npc", "player", 100, day * 5, day, mode, rate), 0, day, observations);
            book.Sign("charter", false, 0, observations); book.Sign("charter", true, 0, observations); return book;
        }
        var book = Book(); var ledger = new TributeLedger();
        ledger.Enable(book, "charter", day * 20);
        Check(ledger.Bill("charter", 1).DueTick == day * 25, "enabling physical payments never backbills old development records");
        Reject(ledger, () => ledger.Enable(book, "charter", day * 21), "re-enable cannot reset debt clock");
        var bill = ledger.Bill("charter", 1);
        Check(bill.Principal == 100 && bill.RequiredValue == 90 && bill.GraceUntil == day * 26, "fixed principal and transport allowance are separate");
        var goods = new[] { new TributeLine("steel-stack", 10, 2m), new TributeLine("medicine-stack", 5, 14m) };
        Reject(ledger, () => ledger.Prepare("early", "charter", 1, goods, day * 24), "no prepayment farming");
        Reject(ledger, () => ledger.Prepare("short", "charter", 1, [new("stack", 89, 1)], day * 25), "insufficient quoted trade value");
        var planned = ledger.Prepare("receipt", "charter", 1, goods, day * 25);
        goods[0] = new("mutated", 1000, 1000);
        Check(planned.TotalValue == 90 && !ledger.IsPaid("charter", 1), "prepare freezes quote and does not fulfill the bill");
        Reject(ledger, () => ledger.Prepare("double", "charter", 1, [new("other", 100, 1)], day * 25), "reserved bill cannot be paid twice");
        Reject(ledger, () => ledger.Commit("receipt", [new("steel-stack", 10, 2)], day * 25), "partial delivery is not fulfillment");
        Reject(ledger, () => ledger.Commit("receipt", [new("steel-stack", 10, 2), new("medicine-stack", 4, 14)], day * 25), "missing one item");
        Reject(ledger, () => ledger.Commit("receipt", [new("steel-stack", 10, 2), new("medicine-stack", 5, 15)], day * 25), "changed price is not the accepted quote");
        Reject(ledger, () => ledger.CancelAfterVerifiedReturn("receipt", false, day * 25), "unproven return cannot reopen debt");
        var restored = TributeLedger.Restore(ledger.Export());
        Check(restored.Receipt("receipt").Phase == TributeReceiptPhase.Prepared && restored.Receipt("receipt").TotalValue == 90,
            "prepared transfer survives reload and remains reserved");
        restored.Commit("receipt", [new("medicine-stack", 5, 14), new("steel-stack", 10, 2)], day * 26);
        Check(restored.IsPaid("charter", 1) && restored.Receipt("receipt").ResolvedTick == bill.GraceUntil, "full delivery proof can arrive in any order at final grace tick");
        Reject(restored, () => restored.Commit("receipt", planned.Lines, day * 26), "one receipt cannot be committed twice");
        Reject(restored, () => restored.Prepare("another", "charter", 1, planned.Lines, day * 26), "paid bill cannot be paid again");
        restored.Prepare("surplus", "charter", 2, [new("silver", 100, 1)], day * 30);
        restored.Commit("surplus", [new("silver", 100, 1)], day * 30);
        Check(restored.IsPaid("charter", 2) && !restored.IsPaid("charter", 3) && restored.Receipt("surplus").TotalValue == 100,
            "surplus preserves exact delivered value without buying another obligation");

        ledger.CancelAfterVerifiedReturn("receipt", true, day * 26 + 1);
        Check(!ledger.IsPaid("charter", 1) && ledger.Receipt("receipt").Phase == TributeReceiptPhase.Cancelled, "verified complete return reopens bill without claiming payment");
        Reject(ledger, () => ledger.Prepare("receipt", "charter", 1, planned.Lines, day * 26 + 1), "cancelled receipt ID is never reused");
        ledger.Prepare("retry", "charter", 1, planned.Lines, day * 26 + 1);
        ledger.Commit("retry", planned.Lines, day * 26 + 1);
        Check(ledger.IsPaid("charter", 1) && ledger.Receipt("retry").ResolvedTick > bill.GraceUntil, "late physical payment recorded without erasing lateness or automatic dishonor");

        book.ConsentToRelease("charter", true, day * 27, observations); book.ConsentToRelease("charter", false, day * 27, observations);
        ledger.SynchronizeClosure(book, "charter", day * 27);
        Check(ledger.Bills("charter", day * 40).Count() == 1, "mutual release stops future assessments at the actual date");
        Reject(ledger, () => ledger.Prepare("post-release", "charter", 2, planned.Lines, day * 40), "no post-release new obligation");
        Check(TributeLedger.Restore(ledger.Export()).IsPaid("charter", 1), "released account retains paid history");
        var unpaidBook = Book(); var unpaid = new TributeLedger(); unpaid.Enable(unpaidBook, "charter", 0);
        unpaidBook.ConsentToRelease("charter", true, day * 6, observations); unpaidBook.ConsentToRelease("charter", false, day * 6, observations);
        unpaid.SynchronizeClosure(unpaidBook, "charter", day * 6);
        unpaid.Prepare("old-debt", "charter", 1, [new("silver", 90, 1)], day * 10);
        unpaid.Commit("old-debt", [new("silver", 90, 1)], day * 10);
        Check(unpaid.IsPaid("charter", 1), "old debt remains payable after mutual release");
        var inconsistentBook = Book(); var inconsistent = new TributeLedger(); inconsistent.Enable(inconsistentBook, "charter", 0);
        inconsistent.Prepare("cancelled", "charter", 2, [new("silver", 90, 1)], day * 10);
        inconsistent.CancelAfterVerifiedReturn("cancelled", true, day * 10);
        inconsistentBook.ConsentToRelease("charter", true, day * 6, observations);
        inconsistentBook.ConsentToRelease("charter", false, day * 6, observations);
        Reject(inconsistent, () => inconsistent.SynchronizeClosure(inconsistentBook, "charter", day * 10),
            "even cancelled history cannot be invalidated by a backdated cross-ledger release");
        var npcBook = new CharterBook(); npcBook.Register(new("player", "native-player", true), 0); npcBook.Register(new("npc", "native-npc", false), 0);
        npcBook.Offer("charter", new("player", "npc", 100, day, 0, TributeTransport.SubjectDelivery, 0), 0, day, observations);
        npcBook.Sign("charter", true, 0, observations); npcBook.Sign("charter", false, 0, observations);
        var noMinting = new TributeLedger();
        Reject(noMinting, () => noMinting.Enable(npcBook, "charter", 0), "this outgoing adapter cannot invent NPC tribute goods");

        foreach (var mode in Enum.GetValues<TributeTransport>())
        foreach (decimal rate in new[] { 0m, .1m, .5m })
        {
            var priced = new TributeLedger(); priced.Enable(Book(mode, rate), "charter", 0);
            Check(priced.Bill("charter", 1).RequiredValue == 100 * (mode == TributeTransport.SubjectDelivery ? 1 - rate : 1 + rate), "delivery and collection use explicit opposite transport adjustments");
        }
        void Corrupt(TributeLedgerSave save, string name)
        {
            bool rejected = false;
            try { TributeLedger.Restore(JsonSerializer.Serialize(save)); } catch (ArgumentException) { rejected = true; }
            Check(rejected, name);
        }
        var snapshot = JsonSerializer.Deserialize<TributeLedgerSave>(restored.Export());
        Corrupt(snapshot with { Version = 99 }, "future payment schema rejected");
        Corrupt(snapshot with { Accounts = snapshot.Accounts.Append(snapshot.Accounts[0]).ToArray() }, "duplicate account rejected");
        Corrupt(snapshot with { Receipts = snapshot.Receipts.Append(snapshot.Receipts[0] with { Id = "duplicate" }).ToArray() }, "two receipts cannot claim one obligation");
        Corrupt(snapshot with { Receipts = [snapshot.Receipts[0] with { ResolvedTick = null }] }, "committed receipt needs completion time");
        Corrupt(snapshot with { Receipts = [snapshot.Receipts[0] with { Lines = [new("fake", 1, 1)] }] }, "insufficient saved delivery rejected");
        Corrupt(snapshot with { Receipts = [snapshot.Receipts[0] with { PreparedTick = 0 }] }, "future bill cannot be paid before due");
        Corrupt(snapshot with { Receipts = [snapshot.Receipts[0] with { Lines = [new("same", 90, 1), new("same", 90, 1)] }] }, "duplicate saved source rejected");

        // Sweep actual quoted unit values and integer stack counts; only sufficient physical deliveries may commit.
        foreach (decimal unit in new[] { .01m, .33m, 1m, 14.7m, 150m })
        for (int quantity = 1; quantity <= 100; quantity++)
        {
            var sample = new TributeLedger(); sample.Enable(Book(), "charter", 0);
            var line = new TributeLine("stack", quantity, unit);
            if (quantity * unit < 90) Reject(sample, () => sample.Prepare("quote", "charter", 1, [line], day * 5), "insufficient fractional trade value");
            else
            {
                sample.Prepare("quote", "charter", 1, [line], day * 5); sample.Commit("quote", [line], day * 5);
                Check(sample.IsPaid("charter", 1) && TributeLedger.Restore(sample.Export()).Receipt("quote").TotalValue == quantity * unit,
                    "integer goods preserve fractional actual trade value through commit and reload");
            }
        }
        return count;
    }
}
