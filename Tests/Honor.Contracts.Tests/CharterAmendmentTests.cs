using System.Text.Json;
using Honor.Contracts.Rules;

internal static class CharterAmendmentTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string label) { count++; if (!ok) throw new Exception("Charter amendment: " + label); }
        void Reject(Action action, string label)
        { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, label); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0);
        foreach (string id in new[] { "city", "child", "grandchild" }) book.Register(new(id, "realm", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string parent, string child) => new(parent, child, 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m);
        book.Offer("old", Terms("p", "city"), 0, PoliticalWorld.Day, obs); book.Sign("old", false, 0, obs); book.Sign("old", true, 0, obs);
        void Npc(string id, string parent, string child)
        { book.OfferNpc(id, Terms(parent, child), new("historic", "historic"), 0, PoliticalWorld.Day, obs); book.SignNpc(id, parent, "historic", 0, obs); book.SignNpc(id, child, "historic", 0, obs); }
        Npc("child-contract", "city", "child"); Npc("deep-contract", "child", "grandchild");
        var moves = new TerritoryTransferLedger(); var payments = new TributeLedger(); var archive = new CharterAmendments();
        var decision = new NpcRealmDecisionEvidence("realm", "current", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var draft = new CharterOfferDraft(90, 6, 2, 15); string before = book.Export(), physical = moves.Export(), money = payments.Export();
        var plan = CharterAmendments.Review(book, moves, payments, "old", "new", draft, decision, 60, 10, obs);
        var prepared = CharterBook.Restore(plan.Prepared); var staged = CharterBook.Restore(plan.After);
        Check(book.Export() == before && archive.Records.Count == 0 && book.Charter("old").Phase == RecordedCharterPhase.Active,
            "review stages both actual NPC responses on copies without releasing current contract");
        Check(prepared.Charter("old").Phase == RecordedCharterPhase.Released && prepared.Charter("old").PlayerReleaseTick == 10 && prepared.Charter("old").NpcReleaseTick == 10 &&
            prepared.Charter("new").NpcSignedTick == 10 && prepared.Charter("new").PlayerSignedTick == null && prepared.Charter("new").Amendment.Previous == "old",
            "prepare contains explicit mutual release and new NPC response, not a fabricated old signature or implicit player acceptance");
        Check(staged.Charter("new").Phase == RecordedCharterPhase.Active && staged.Root("grandchild") == "p" &&
            staged.Charter("child-contract") == book.Charter("child-contract") && staged.Charter("deep-contract") == book.Charter("deep-contract"),
            "new incoming contract retains every existing descendant bond and four-level graph");
        var done = archive.Commit(plan, book, moves, payments, decision, 60, 10, obs);
        var audit = new CharterAcceptances().Record(prepared, done.Book, "new", "realm", "current", "Realm", "Current", 10); audit.ValidateBook(done.Book);
        Check(done.Book.Charter("old").Terms == book.Charter("old").Terms && done.Book.Charter("old").PlayerSignedTick == 0 && done.Book.Charter("old").NpcSignedTick == 0 &&
            done.Book.Charter("new").Terms == draft.Bind("p", "city") && done.Book.Charter("new").PlayerSignedTick == 10 && moves.Export() == physical && payments.Export() == money,
            "retain old economics, signatures, identity and debt basis; new terms start only with fresh signatures and no physical changes");
        var restored = CharterAmendments.Restore(done.Archive.Export()); restored.ValidateBook(CharterBook.Restore(done.Book.Export()));
        Check(restored.Export() == done.Archive.Export() && audit.Find("new").Representative == "current", "negotiation and separate final-consent audit survive reload");
        Reject(() => archive.ValidateBook(done.Book), "amendment link cannot restore without its original negotiation archive");
        Reject(() => done.Archive.Commit(plan, book, moves, payments, decision, 60, 10, obs), "old transition cannot replay");
        Reject(() => archive.Commit(plan, book, moves, payments, decision with { Representative = "successor" }, 60, 10, obs), "current representative must be re-reviewed");
        Reject(() => archive.Commit(plan, book, moves, payments, decision, 60, 11, obs), "stale confirmation time rejected");
        Reject(() => archive.Commit(plan with { After = before }, book, moves, payments, decision, 60, 10, obs), "caller cannot omit new consent or original release");
        Reject(() => CharterAmendments.Review(book, moves, payments, "old", "new", draft, decision with { Regime = NpcDecisionRegime.Union }, 100, 10, obs), "one ruler cannot consent for a collective union");
        Reject(() => CharterAmendments.Review(book, moves, payments, "old", "new", draft, decision with { IndependentAuthority = false }, 100, 10, obs), "provider authority is never silently replaced");
        Reject(() => CharterAmendments.Review(book, moves, payments, "old", "new", draft, decision with { Realm = "foreign" }, 60, 10, obs), "actual current owner must match registered identity provenance");
        Reject(() => CharterAmendments.Review(book, moves, payments, "old", "new", new(1000, 1, 1, 0), decision, 100, 10, obs), "NPC affordability policy applies to renegotiation");
        Reject(() => CharterAmendments.Review(book, moves, payments, "old", "new", draft, decision, 19, 10, obs), "release is not inferred from a player request");
        var partial = CharterBook.Restore(before); partial.ConsentToRelease("old", true, 5, obs);
        Reject(() => CharterAmendments.Review(partial, moves, payments, "old", "new", draft, decision, 60, 10, obs), "partial release intent cannot be overwritten");
        var pending = new TerritoryTransferLedger(); pending.Prepare("pending", book, "old", new("city", "realm", 3), "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 1);
        Reject(() => CharterAmendments.Review(book, pending, payments, "old", "new", draft, decision, 60, 10, obs), "pending assets freeze contract renegotiation");
        var changed = obs.Select(o => o.Id == "grandchild" ? o with { NativeOwner = "foreign" } : o).ToArray();
        Reject(() => CharterAmendments.Review(book, moves, payments, "old", "new", draft, decision, 60, 10, changed), "a deeper changed owner cannot disappear from consent review");
        var next = CharterAmendments.Review(done.Book, moves, payments, "new", "third", new(80, 6, 2, 15), decision with { Representative = "next-ruler" }, 60, 20, obs);
        var twice = done.Archive.Commit(next, done.Book, moves, payments, decision with { Representative = "next-ruler" }, 60, 20, obs);
        twice.Archive.ValidateBook(twice.Book); CharterAmendments.Restore(twice.Archive.Export()).ValidateBook(CharterBook.Restore(twice.Book.Export()));
        Check(twice.Book.Charter("old") == done.Book.Charter("old") && twice.Book.Charter("new").Phase == RecordedCharterPhase.Released &&
            twice.Book.Charter("third").Amendment.Previous == "new" && twice.Book.Root("grandchild") == "p", "later amendments retain original releases and linked economic history");
        var laterLoss = CharterBook.Restore(done.Book.Export()); laterLoss.CloseDestroyedTown("new", "city", "realm", 20, 20, true, true); done.Archive.ValidateBook(laterLoss);
        Check(laterLoss.Charter("old").Phase == RecordedCharterPhase.Released && laterLoss.Charter("new").Phase == RecordedCharterPhase.TerritoryLost,
            "later actual territorial loss preserves negotiated history without reviving old terms");
        var broken = JsonSerializer.Deserialize<CharterAmendmentSave>(done.Archive.Export());
        Reject(() => CharterAmendments.Restore(JsonSerializer.Serialize(broken with { Records = new[] { plan, plan } })), "duplicate amendment history rejected");
        Reject(() => CharterAmendments.Restore(JsonSerializer.Serialize(broken with { Records = new[] { plan with { Proof = new string('A', 64) } } })), "changed negotiation digest rejected");
        Check(!before.Contains("\"Amendment\"") && book.Export() == before && moves.Export() == physical && payments.Export() == money,
            "new null field preserves legacy bytes and all failed operations leave original state untouched");
        return count;
    }
}
