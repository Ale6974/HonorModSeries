using System.Text.Json;
using Honor.Contracts.Rules;

internal static class TownOwnershipReturnTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string label) { count++; if (!ok) throw new Exception("Town ownership return: " + label); }
        void Reject(Action action, string label)
        { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, label); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0);
        foreach (string id in new[] { "upper", "a", "b", "g" }) book.Register(new(id, "realm", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string parent, string child) => new(parent, child, 137, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .23m);
        book.Offer("root", Terms("p", "upper"), 0, PoliticalWorld.Day, obs); book.Sign("root", true, 0, obs); book.Sign("root", false, 0, obs);
        void Npc(string id, string parent, string child)
        { book.OfferNpc(id, Terms(parent, child), new("old", "old"), 0, PoliticalWorld.Day, obs); book.SignNpc(id, parent, "old", 0, obs); book.SignNpc(id, child, "old", 0, obs); }
        Npc("child-a", "upper", "a"); Npc("child-b", "upper", "b"); Npc("deep", "a", "g");
        string original = book.Export(); book.CloseHostileTransferredTown("root", "upper", "realm", "realm", "captor", 10, 12, true, true);
        var loss = new TownLossProof("root", "upper", "realm", "realm", "Upper", 10, 12, original,
            Kind: TownLossKind.HostileNativeOwnershipChange, CapturingOwner: "captor");
        var losses = new TownSuccessionArchive().Observe(loss, book);
        var returns = new TownOwnershipReturns(); string lostBook = book.Export(), lostArchive = losses.Export();
        Reject(() => returns.RequireActive("root", losses, book, "upper", "realm", 21), "current ownership alone is not a positive native return");
        var witness = new TownOwnershipReturn("root", "upper", "captor", "realm", TownOwnershipReturns.LossDigest(loss), 20, 21);
        var recorded = returns.Observe(witness, losses, book);
        Check(book.Export() == lostBook && losses.Export() == lostArchive && losses.HasUnresolved && recorded.Records.Count == 1,
            "observation preserves ended root and unchanged children without automatic succession or ownership writes");
        var reloaded = TownOwnershipReturns.Restore(recorded.Export()); reloaded.ValidatePair(TownSuccessionArchive.Restore(losses.Export()), CharterBook.Restore(book.Export()));
        Check(reloaded.Export() == recorded.Export() && !recorded.Export().Contains("InvalidatedTick"), "positive native receipt survives reload with no fabricated later attempt");
        var childLoss = new TownLossProof("child-a", "a", "realm", "realm", "Child", 22, 22, book.Export(), ScopeRoot: "root", ScopeOrdinal: 1);
        var extendedBook = losses.CloseForObservation(childLoss); var extendedLosses = losses.Observe(childLoss, extendedBook);
        recorded.ValidatePair(extendedLosses, extendedBook);
        Reject(() => recorded.RequireActive("root", extendedLosses, extendedBook, "upper", "realm", 23),
            "later child losses preserve historical return evidence but cannot broaden isolated return authority");
        string proof = reloaded.RequireActive("root", losses, book, "upper", "realm", 21);
        Reject(() => returns.Observe(witness with { FromOwner = "foreign" }, losses, book), "return must start at actual recorded captor");
        Reject(() => returns.Observe(witness with { ReturnedOwner = "foreign" }, losses, book), "return must end at original actual baseline realm");
        Reject(() => returns.Observe(witness with { LossProof = new string('A', 64) }, losses, book), "original loss digest cannot be substituted");
        Reject(() => returns.Observe(witness with { OccurredTick = 11 }, losses, book), "return cannot precede original normal-return observation");
        Reject(() => recorded.Observe(witness, losses, book), "one original return cannot be overwritten or replayed");
        Reject(() => recorded.RequireActive("root", losses, book, "upper", "captor", 22), "changed current owner grants no return authority");
        Reject(() => recorded.RequireActive("root", losses, book, "other", "realm", 22), "exact territory identity required");
        Reject(() => recorded.RequireActive("root", losses, book, "upper", "realm", 20), "future observation unavailable");
        var interrupted = recorded.Invalidate("root", 22, "foreign"); interrupted.ValidatePair(losses, book);
        Reject(() => interrupted.RequireActive("root", losses, book, "upper", "realm", 23), "later ownership attempt blocks reuse even if native outcome is uncertain or owner is back");
        Check(interrupted.Records.Single().OccurredTick == 20 && interrupted.Records.Single().FromOwner == "captor" &&
            interrupted.Invalidate("root", 23, "captor").Export() == interrupted.Export() && recorded.RequireActive("root", losses, book, "upper", "realm", 23) == proof,
            "retain original factual observation and first attempted change on copies without rewriting it as a successful later ownership change");
        var plan = losses.Review("root", book, "new", new("current", "current"), 30, obs);
        Reject(() => recorded.CommitSuccession(losses, book, plan, 30, obs, new string('B', 64)), "wrong return token cannot authorize succession");
        var wrongOwner = obs.Select(o => o.Id == "upper" ? o with { NativeOwner = "captor" } : o).ToArray();
        Reject(() => recorded.CommitSuccession(losses, book, plan, 30, wrongOwner, proof), "new confirmation must verify actual current returned owner");
        Reject(() => interrupted.CommitSuccession(losses, book, plan, 30, obs, proof), "an interrupted receipt cannot authorize a formerly valid plan");
        var done = recorded.CommitSuccession(losses, book, plan, 30, obs, proof);
        recorded.ValidatePair(done.Archive, done.Book); done.Archive.ValidateBook(done.Book);
        Check(!done.Archive.HasUnresolved && done.Archive.Record("root").Resolution.OwnershipReturnProof == proof &&
            done.Book.Charter("root").Phase == RecordedCharterPhase.TerritoryLost && done.Book.Charter("root").Terms == book.Charter("root").Terms &&
            done.Book.Charter("root").PlayerReleaseTick == null && done.Book.Root("g") == "p" && done.Book.Charter("deep") == book.Charter("deep"),
            "separate current consent reconnects descendants and links exact return while old incoming charter remains ended");
        Check(done.Book.Charter("child-a").Terms == book.Charter("child-a").Terms && done.Book.Charter("child-a").NpcAuthority == book.Charter("child-a").NpcAuthority &&
            done.Book.Entity("upper").NativeOwner == "realm", "preserve old economics, actors, debts and native baseline identity");
        Reject(() => new TownOwnershipReturns().ValidatePair(done.Archive, done.Book), "a new receipt cannot restore without its separate original return witness");
        Reject(() => interrupted.ValidatePair(done.Archive, done.Book), "an interrupted witness cannot match a completed return-authorized receipt");
        var reloadDone = TownSuccessionArchive.Restore(done.Archive.Export());
        TownOwnershipReturns.Restore(recorded.Export()).ValidatePair(reloadDone, CharterBook.Restore(done.Book.Export()));
        Check(reloadDone.Export() == done.Archive.Export(), "paired return and actual successor receipt survive reload exactly");
        Reject(() => recorded.RequireActive("root", done.Archive, done.Book, "upper", "realm", 31), "consumed historical return cannot authorize another succession");
        // Ownership returning does not revive the original charter. Only a new negotiated charter may start later.
        var newBook = CharterBook.Restore(done.Book.Export());
        newBook.Offer("fresh-charter", Terms("p", "upper") with { TributeValue = 211 }, 40, PoliticalWorld.Day, obs);
        Check(newBook.Charter("fresh-charter").Phase == RecordedCharterPhase.Offered && newBook.Charter("root") == book.Charter("root"),
            "new offer does not reactivate old incoming bond or backdate obligations through the lost period");
        newBook.Sign("fresh-charter", true, 41, obs); newBook.Sign("fresh-charter", false, 42, obs);
        recorded.ValidatePair(done.Archive, newBook);
        Check(newBook.Charter("fresh-charter").Phase == RecordedCharterPhase.Active && newBook.Charter("fresh-charter").PlayerSignedTick == 41 &&
            newBook.Charter("fresh-charter").NpcSignedTick == 42 && newBook.Charter("root").TerritoryLostTick == 10 &&
            newBook.Charter("root").Terms.TributeValue == 137 && newBook.Charter("fresh-charter").Terms.TributeValue == 211,
            "only fresh signatures and current terms start a new contract; old economic history and actual loss remain intact");
        var malformed = JsonSerializer.Deserialize<TownOwnershipReturnSave>(recorded.Export());
        Reject(() => TownOwnershipReturns.Restore(JsonSerializer.Serialize(malformed with { Records = new[] { witness, witness } })), "duplicate historical return rejected");
        Reject(() => TownOwnershipReturns.Restore(JsonSerializer.Serialize(malformed with { Records = new[] { witness with { InvalidatedTick = 20 } } })), "attempt cannot precede observed return");
        var changedWitness = TownOwnershipReturns.Restore(JsonSerializer.Serialize(malformed with { Records = new[] { witness with { OccurredTick = 19 } } }));
        Reject(() => changedWitness.ValidatePair(done.Archive, done.Book), "bound succession digest rejects changed original observation");
        var ordinary = losses.Commit(book, plan, 30, obs);
        Check(!ordinary.Archive.Export().Contains("OwnershipReturnProof") && new TownOwnershipReturns().Export() == returns.Export(),
            "legacy ordinary succession JSON and original constructor remain unchanged");
        Check(book.Export() == lostBook && losses.Export() == lostArchive && returns.Records.Count == 0,
            "all staged operations and failures leave source book, loss history and return archive unchanged");
        return count;
    }
}
