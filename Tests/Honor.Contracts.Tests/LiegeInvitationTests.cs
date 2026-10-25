using Honor.Contracts.Rules;

internal static class LiegeInvitationTests
{
    public static int Run()
    {
        int count = 0; long day = PoliticalWorld.Day;
        void Check(bool value, string reason) { count++; if (!value) throw new Exception("Invitation: " + reason); }
        void Reject(Action action, string reason)
        { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, reason); }
        var book = new CharterBook(); book.Register(new("p", "native-p", true), 0);
        for (int i = 0; i < 10; i++) book.Register(new("n" + i, "native-" + i, false), 0);
        var observations = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        var options = new[] { new LiegeInvitationCandidate("n0", 10), new LiegeInvitationCandidate("n1", 50), new LiegeInvitationCandidate("n2", -1) };
        string before = book.Export();
        Check(LiegeInvitations.Rank(book, "p", options, 0).SequenceEqual(new[] { "n1", "n0" }), "peaceful eligible goodwill ranking");
        Check(book.Export() == before, "ranking cannot mutate charter or ownership");
        Check(LiegeInvitations.Rank(book, "p", options.AsEnumerable().Reverse(), 0).SequenceEqual(new[] { "n1", "n0" }), "enumeration order does not alter priority");
        Check(LiegeInvitations.Rank(book, "p", new[] { new LiegeInvitationCandidate("n1", 10), new LiegeInvitationCandidate("n0", 10) }, 0).First() == "n0", "stable identity tie break");
        Reject(() => LiegeInvitations.Rank(book, "n0", options, 0), "player scope required");
        Reject(() => LiegeInvitations.Rank(book, "p", options.Append(options[0]), 0), "duplicate providers rejected");
        Reject(() => LiegeInvitations.Rank(book, "p", new[] { new LiegeInvitationCandidate("p", 10) }, 0), "player cannot invite itself");
        Reject(() => LiegeInvitations.Rank(book, "p", new[] { new LiegeInvitationCandidate("missing", 10) }, 0), "unknown provider rejected");
        Reject(() => LiegeInvitations.Rank(book, "p", new[] { new LiegeInvitationCandidate("n0", 101) }, 0), "invalid goodwill rejected");
        book = CharterOffers.Propose(book, "offer", "p", "n1", true, new(), 50, 0, observations).Book;
        Check(book.Charter("offer").NpcSignedTick == 0 && book.Charter("offer").PlayerSignedTick == null, "NPC invitation never fabricates player consent");
        Check(LiegeInvitations.Rank(book, "p", options, day * 4).SequenceEqual(new[] { "n0" }), "expired proposal retains contact cooldown");
        book.Decline("offer", 0);
        Check(LiegeInvitations.Rank(book, "p", options, day * 29).SequenceEqual(new[] { "n0" }), "declining does not trigger immediate repeat");
        Check(LiegeInvitations.Rank(book, "p", options, day * 30).SequenceEqual(new[] { "n0", "n1" }), "new counterparty precedes previously contacted stronger faction");
        Check(CharterBook.Restore(book.Export()).Export() == book.Export(), "invitation history retained through existing book persistence");
        book = CharterOffers.Propose(book, "renewed", "p", "n1", true, new(), 50, day * 30, observations).Book;
        book.Sign("renewed", true, day * 30, observations);
        Check(!LiegeInvitations.Rank(book, "p", options, day * 90).Contains("n1"), "active liege never sends replacement invitation");
        Check(LiegeInvitations.Rank(book, "p", options, day * 90).Contains("n0"), "having a liege does not forbid another faction invitation");
        Reject(() => LiegeInvitations.Rank(book, "p", options, day), "reversed book clock rejected");
        for (int i = 0; i < 8; i++)
        {
            string npc = "n" + (i < 1 ? 0 : i + 1);
            book = CharterOffers.Propose(book, "pending" + i, "p", npc, true, new(), 50, day * 90, observations).Book;
        }
        Check(LiegeInvitations.Rank(book, "p", new[] { new LiegeInvitationCandidate("n9", 100) }, day * 90).Count == 0, "parallel live offer cap respected");
        Check(LiegeInvitations.Interval == day * 7 && LiegeInvitations.ContactCooldown == day * 30, "bounded seven-day cadence and thirty-day counterparty cooldown");
        return count;
    }
}
