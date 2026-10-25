using System.Text.Json;
using Honor.Contracts.Rules;

internal static class SubtreeReleaseTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool condition, string name) { count++; if (!condition) throw new Exception(name); }
        void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        var b = new CharterBook(); b.Register(new("player", "native", true), 0);
        foreach (string id in new[] { "a", "b", "c", "sibling" }) b.Register(new(id, "source", false), 0);
        TerritoryObservation[] Obs(CharterBook book) => book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string parent, string child) => new(parent, child, 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m);
        foreach (string id in new[] { "a", "sibling" })
        { b.Offer(id, Terms("player", id), 0, PoliticalWorld.Day, Obs(b)); b.Sign(id, true, 0, Obs(b)); b.Sign(id, false, 0, Obs(b)); }
        var authority = new NpcCharterAuthority("liege", "subject");
        b.OfferNpc("b", Terms("a", "b"), authority, 0, PoliticalWorld.Day, Obs(b));
        b.SignNpc("b", "a", "liege", 0, Obs(b)); b.SignNpc("b", "b", "subject", 0, Obs(b));
        b.OfferNpc("c", Terms("b", "c"), authority, 0, PoliticalWorld.Day, Obs(b));
        b.SignNpc("c", "b", "liege", 0, Obs(b)); b.SignNpc("c", "c", "subject", 0, Obs(b));
        string original = b.Export(); var plan = SubtreeRelease.Review(b, "a", 1, Obs(b));
        Check(b.Export() == original, "review preserves source book");
        Check(plan.Charters.SequenceEqual(new[] { "a", "b", "c" }), "exact descendants without sibling");
        var after = SubtreeRelease.Commit(b, plan, 1, Obs(b));
        Check(after.Charter("a").Phase == RecordedCharterPhase.Released && after.Charter("a").PlayerReleaseTick == 1 && after.Charter("a").NpcReleaseTick == 1, "mutual incoming release");
        foreach (string id in new[] { "b", "c", "sibling" }) Check(after.Charter(id) == b.Charter(id), "internal/sibling contract preserved " + id);
        Check(after.Root("c") == "a" && after.PlayerFollowers().Select(c => c.Id).SequenceEqual(new[] { "sibling" }), "subtree leaves player management together");
        Check(SubtreeRelease.Authorizes(after, "a", "a") && SubtreeRelease.Authorizes(after, "a", "b") && SubtreeRelease.Authorizes(after, "a", "c"), "release scope includes root/descendants");
        Check(!SubtreeRelease.Authorizes(after, "a", "sibling") && !SubtreeRelease.Authorizes(b, "a", "b"), "active/unrelated root cannot grant scope");
        Reject(() => SubtreeRelease.Commit(b, plan with { Charters = new[] { "a" } }, 1, Obs(b)), "omitted descendants rejected");
        Reject(() => SubtreeRelease.Commit(b, plan with { After = original }, 1, Obs(b)), "altered book rejected");
        Reject(() => SubtreeRelease.Commit(b, plan, 2, Obs(b)), "review time changed");
        Reject(() => SubtreeRelease.Commit(b, plan, 1, Obs(b).Select(o => o.Id == "c" ? o with { Exists = false } : o)), "fresh missing descendant blocks release");
        Reject(() => SubtreeRelease.Review(after, "a", 2, Obs(after)), "already released subtree not re-released");
        var nested = SubtreeRelease.Review(b, "b", 1, Obs(b));
        Check(nested.Charters.SequenceEqual(new[] { "b", "c" }), "nested root only cuts its own branch");
        var nestedAfter = SubtreeRelease.Commit(b, nested, 1, Obs(b));
        Check(nestedAfter.Charter("a") == b.Charter("a") && nestedAfter.Charter("c") == b.Charter("c"), "nested release retains parent and child");
        var pending = CharterBook.Restore(original); pending.Register(new("new", "source", false), 1);
        pending.OfferNpc("new-b", Terms("b", "new"), authority, 1, PoliticalWorld.Day, Obs(pending));
        Reject(() => SubtreeRelease.Review(pending, "a", 2, Obs(pending)), "open descendant offer blocks exit");
        var transfers = new TerritoryTransferLedger(); var journal = new SubtreeExitJournal();
        journal.Prepare("exit", "union", plan, transfers);
        Check(journal.Pending && journal.Current.Phase == SubtreeExitPhase.Prepared, "intent before mutation");
        Reject(() => journal.ConfirmRelease(b), "unreleased actual book rejected");
        Reject(() => journal.Prepare("other", "union", plan, transfers), "overlapping exits blocked");
        var prepared = SubtreeExitJournal.Restore(journal.Export());
        Check(prepared.Export() == journal.Export() && prepared.Pending, "prepared persistence no auto apply");
        prepared.CancelUnchanged(b, transfers, 2);
        Check(!prepared.Pending && prepared.Current.Phase == SubtreeExitPhase.Cancelled, "unchanged cancel");
        Check(SubtreeExitJournal.Restore(prepared.Export()).Export() == prepared.Export(), "cancelled persistence");
        journal.ConfirmRelease(after);
        Check(journal.Current.Phase == SubtreeExitPhase.Released && journal.Pending, "release alone not complete");
        Reject(() => journal.CancelUnchanged(b, transfers, 2), "applied release cannot cancel");
        var observations = plan.Charters.Select(id => new TerritoryTransferObservation(id, "source", 5)).ToArray();
        Reject(() => journal.Complete(after, transfers, observations, 2), "consent alone is not migration");
        for (int i = 0; i < observations.Length; i++)
        {
            string id = plan.Charters[i]; var o = observations[i];
            Reject(() => transfers.Prepare("unscoped", after, id, o, "union", null, "union", TerritoryTransferPurpose.ActiveCharter, 2, releasedRoot: "a"), "scope not active-charter authority " + id);
            transfers.Prepare("move-" + id, after, id, o, "union", null, "union", TerritoryTransferPurpose.ReleasedToUnion, 2, releasedRoot: "a");
            Check(transfers.Transfer("move-" + id).ReleasedRoot == "a", "persist exact release root " + id);
            Reject(() => journal.Complete(after, transfers, observations.Select(x => x with { Owner = "union" }), 2), "prepared city cannot complete whole exit " + id);
            transfers.Confirm("move-" + id, o with { Owner = "union" }, 2);
        }
        Check(TerritoryTransferLedger.Restore(transfers.Export()).Export() == transfers.Export(), "scoped physical journal persistence");
        var actual = observations.Select(x => x with { Owner = "union" }).ToArray();
        Reject(() => journal.Complete(after, transfers, actual.Take(2), 3), "partial town set rejected");
        Reject(() => journal.Complete(after, transfers, actual.Append(actual[0]), 3), "duplicate town observation rejected");
        Reject(() => journal.Complete(after, transfers, actual.Select(x => x.Territory == "c" ? x with { Eligible = false } : x), 3), "fresh eligibility required");
        Reject(() => journal.Complete(after, transfers, actual, 1), "cannot complete before physical journal time");
        journal.Complete(after, transfers, actual, 3);
        Check(!journal.Pending && journal.Current.Phase == SubtreeExitPhase.Completed && journal.Current.CompletedTick == 3, "all actual destinations verified");
        Check(SubtreeExitJournal.Restore(journal.Export()).Export() == journal.Export(), "completed persistence");
        var proof = transfers.Transfer("move-b");
        Check(journal.Authorizes(proof) && SubtreeExitJournal.Restore(journal.Export()).Authorizes(proof), "live and archived intent prove exact child transfer");
        foreach (var altered in new[] { proof with { ReleasedRoot = null }, proof with { ReleasedRoot = "b" }, proof with { Charter = "sibling" },
            proof with { Territory = "c" }, proof with { TargetOwner = "other" }, proof with { BaselineOwner = "other" },
            proof with { PreparedTick = 0 }, proof with { PreparedTick = 4 }, proof with { Purpose = TerritoryTransferPurpose.ActiveCharter } })
            Check(!journal.Authorizes(altered), "archive does not authorize another scope, owner or time");
        Check(!prepared.Authorizes(proof), "cancelled intent cannot prove a transfer");
        Reject(() => SubtreeExitJournal.Restore(journal.Export().Replace("\"CompletedTick\":3", "\"CompletedTick\":0")), "corrupt finish time rejected");
        Reject(() => SubtreeExitJournal.Restore(journal.Export().Replace("\"Version\":1", "\"Version\":9")), "unsupported schema rejected");
        var staleCancel = new SubtreeExitJournal(); staleCancel.Prepare("cancel", "union", plan, new());
        Reject(() => staleCancel.CancelUnchanged(b, transfers, 3), "changed city journal prevents cancellation");
        Reject(() => new SubtreeExitJournal().Prepare("bad", "native", plan, new()), "native player destination rejected");
        var legacy = new TerritoryTransferLedger(); legacy.Prepare("old", b, "a", new("a", "source", 5), "dest", "dest", null, TerritoryTransferPurpose.ActiveCharter, 1);
        Check(!legacy.Export().Contains("ReleasedRoot"), "old JSON default omitted");
        Reject(() => new TerritoryTransferLedger().Prepare("sibling", after, "sibling", new("sibling", "source", 5), "union", null, "union", TerritoryTransferPurpose.ReleasedToUnion, 2, releasedRoot: "a"), "unrelated sibling cannot use scope");
        Reject(() => new TerritoryTransferLedger().Prepare("last", after, "c", new("c", "source", 1), "union", null, "union", TerritoryTransferPurpose.ReleasedToUnion, 2, releasedRoot: "a"), "last source base remains protected");
        Reject(() => new TerritoryTransferLedger().Prepare("residents", after, "c", new("c", "source", 5, HasInhabitants: true), "union", null, "union", TerritoryTransferPurpose.ReleasedToUnion, 2, releasedRoot: "a"), "resident guard remains");
        var debtBook = CharterBook.Restore(after.Export()); debtBook.Register(new("external", "foreign", false), 1);
        debtBook.Offer("player-debt", Terms("external", "player"), 1, PoliticalWorld.Day, Obs(debtBook));
        debtBook.Sign("player-debt", true, 1, Obs(debtBook)); debtBook.Sign("player-debt", false, 1, Obs(debtBook));
        var debt = new TributeLedger(); debt.Enable(debtBook, "player-debt", 1);
        var due = debt.Bill("player-debt", 1); string debtBefore = debt.Export();
        var debtExit = SubtreeRelease.Review(debtBook, "sibling", due.GraceUntil + 1, Obs(debtBook));
        var debtAfter = SubtreeRelease.Commit(debtBook, debtExit, due.GraceUntil + 1, Obs(debtBook));
        Check(debtAfter.Charter("player-debt") == debtBook.Charter("player-debt") && debt.Export() == debtBefore, "unrelated outstanding account and incoming player obligation untouched");
        Check(debt.Bills("player-debt", due.GraceUntil + 1).First() == due && !debt.IsPaid("player-debt", 1), "old overdue bill remains owed without invented receipt");
        var invalidJournal = JsonSerializer.Serialize(new { Version = 1, Current = journal.Current with { Plan = plan with { Charters = new[] { "a" } } } });
        Reject(() => SubtreeExitJournal.Restore(invalidJournal), "persisted incomplete subtree intent rejected");
        Reject(() => SubtreeExitJournal.Restore(JsonSerializer.Serialize(new { Version = 1, Current = journal.Current with { TransferBaseline = "bad" } })), "invalid saved transfer baseline rejected");
        Reject(() => TerritoryTransferLedger.Restore(transfers.Export().Replace("\"Purpose\":1", "\"Purpose\":0")), "scoped archive cannot become active-authority permission");
        return count;
    }
}
