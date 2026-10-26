using System.Text.Json;
using Honor.Contracts.Rules;

internal static class PendingUpperTownLossTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string label) { count++; if (!ok) throw new Exception("Pending upper town: " + label); }
        void Reject(Action action, string label)
        { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, label); }
        var book = new CharterBook(); book.Register(new("p", "native-player", true), 0);
        foreach (string id in new[] { "territory/upper", "a", "b", "g" }) book.Register(new(id, "realm", false), 0);
        var observations = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string parent, string child) => new(parent, child, 137, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .23m);
        book.Offer("root", Terms("p", "territory/upper"), 0, PoliticalWorld.Day, observations);
        book.Sign("root", true, 0, observations); book.Sign("root", false, 0, observations);
        void Npc(string id, string parent, string child)
        {
            book.OfferNpc(id, Terms(parent, child), new("old", "old"), 0, PoliticalWorld.Day, observations);
            book.SignNpc(id, parent, "old", 0, observations); book.SignNpc(id, child, "old", 0, observations);
        }
        Npc("child-a", "territory/upper", "a"); Npc("child-b", "territory/upper", "b"); Npc("deep", "a", "g");
        var ledger = new TerritoryTransferLedger(); var stock = new EmptyTownStock(false, false, -1);
        ledger.Prepare("move", book, "root", new("territory/upper", "realm", 5, StockProof: stock.Proof), "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 5);
        string beforeBook = book.Export(), beforeLedger = ledger.Export();
        var scope = new TownLossProof("root", "territory/upper", "realm", "realm", "Upper town", 10, 12, beforeBook);
        var witness = new PendingTownLoss("move", "upper", "realm", "Upper town", 10, 12, beforeBook, beforeLedger, stock) { UpperScope = scope };
        var empty = new PendingTownLossArchive(); var losses = new TownSuccessionArchive();
        Reject(() => empty.Observe(witness with { UpperScope = null }, book, ledger), "legacy leaf path still rejects children");
        Reject(() => book.CloseDestroyedLeaf("root", "territory/upper", "realm", 10, 12, true), "leaf API never acquires upper authority");
        var archive = empty.Observe(witness, book, ledger); archive.ValidateUpperPair(losses);
        Check(archive.HasPending("move") && !losses.HasUnresolved && book.Export() == beforeBook && ledger.Export() == beforeLedger,
            "positive witness alone changes neither book, transfer nor succession archive");
        Reject(() => archive.StageUpper("move", losses, book), "cannot register upper closure before explicit reconciliation");
        var plan = archive.Review("move", book, ledger, 20);
        Check(book.Export() == beforeBook && ledger.Export() == beforeLedger, "review is read only");
        Reject(() => archive.Commit(plan, book, ledger, 21), "stale confirmation clock");
        Reject(() => archive.Commit(plan with { BookAfter = beforeBook }, book, ledger, 20), "cannot omit root closure");
        var closed = archive.Commit(plan, book, ledger, 20);
        var upper = closed.Archive.StageUpper("move", losses, closed.Book);
        closed.Archive.ValidatePair(closed.Book, closed.Ledger); closed.Archive.ValidateUpperPair(upper); upper.ValidateBook(closed.Book);
        Check(closed.Book.Charter("root").TerritoryLostTick == 10 && closed.Book.LastTick == 20 && upper.Record("root").ObservedTick == 12 &&
            upper.Record("root").ClosureTick == 20 && closed.Ledger.Transfer("move").Phase == TerritoryTransferPhase.Destroyed,
            "retain actual loss, observation and later explicit closure times separately");
        Check(new[] { "child-a", "child-b", "deep" }.All(id => closed.Book.Charter(id) == book.Charter(id)) && upper.HasUnresolved,
            "all children, exact economics and historic signatures remain pending separate succession");
        Reject(() => closed.Archive.ValidateUpperPair(losses), "missing paired upper archive blocks restore");
        Reject(() => empty.ValidateUpperPair(upper), "missing native witness blocks delayed closure");
        Reject(() => archive.ValidateUpperPair(upper), "unresolved witness cannot accompany already registered closure");
        var reloadPending = PendingTownLossArchive.Restore(closed.Archive.Export());
        var reloadUpper = TownSuccessionArchive.Restore(upper.Export()); var reloadBook = CharterBook.Restore(closed.Book.Export());
        reloadPending.ValidatePair(reloadBook, TerritoryTransferLedger.Restore(closed.Ledger.Export())); reloadPending.ValidateUpperPair(reloadUpper); reloadUpper.ValidateBook(reloadBook);
        Check(reloadPending.Export() == closed.Archive.Export() && reloadUpper.Export() == upper.Export(), "all three paired archives survive reload exactly");
        var absent = observations.Select(o => o.Id == "territory/upper" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var succession = reloadUpper.Review("root", reloadBook, "next", new("current", "current"), 30, absent);
        var done = reloadUpper.Commit(reloadBook, succession, 30, absent);
        reloadPending.ValidatePair(done.Book, closed.Ledger); reloadPending.ValidateUpperPair(done.Archive); done.Archive.ValidateBook(done.Book);
        Check(!done.Archive.HasUnresolved && done.Book.Root("g") == "p" && done.Book.Charter("deep") == book.Charter("deep") &&
            done.Book.Entity("g").NativeOwner == "realm", "separate atomic succession reconnects all descendants without native ownership migration");
        Check(done.Book.Charter("child-a").Terms == book.Charter("child-a").Terms && done.Book.Charter("child-a").NpcAuthority == book.Charter("child-a").NpcAuthority &&
            done.Book.Charter("child-a").PlayerReleaseTick == null && done.Book.Charter(succession.Replacements["child-a"]).Terms == book.Charter("child-a").Terms with { Liege = "p" },
            "preserve old terms/consent, create only reviewed successors without fabricated release");
        foreach (var bad in new[] { witness with { Owner = "target" }, witness with { Label = "changed" }, witness with { PoliticalBefore = "{}" },
            witness with { UpperScope = scope with { ClosureTick = 20 } }, witness with { UpperScope = scope with { ObservedTick = 11 } },
            witness with { UpperScope = scope with { ScopeRoot = "root" } }, witness with { UpperScope = scope with { PriorScopes = new[] { "root" } } } })
            Reject(() => empty.Observe(bad, book, ledger), "scope cannot substitute owner, times, political context or overlap");
        Reject(() => losses.CloseForObservation(scope with { ClosureTick = 11 }), "closure cannot precede observation");
        var saved = JsonSerializer.Deserialize<TownLossSave>(upper.Export());
        var changedTime = TownSuccessionArchive.Restore(JsonSerializer.Serialize(saved with { Records = saved.Records.Select(r => r with { ClosureTick = 21 }).ToArray() }));
        Reject(() => reloadPending.ValidateUpperPair(changedTime), "cross-slot altered closure rejected");
        Reject(() => changedTime.ValidateBook(closed.Book), "future closure incompatible with book clock");
        Check(!JsonSerializer.Serialize(scope).Contains("ClosureTick") && !JsonSerializer.Serialize(witness with { UpperScope = null }).Contains("UpperScope"),
            "legacy null fields omitted from serialized bytes and hashes");
        Check(book.Export() == beforeBook && ledger.Export() == beforeLedger && archive.HasPending("move") && losses.Records.Count == 0,
            "all failed and successful staged operations leave original journals untouched");
        return count;
    }
}
