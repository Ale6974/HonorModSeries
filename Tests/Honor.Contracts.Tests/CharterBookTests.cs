using System.Text.Json;
using Honor.Contracts.Rules;

internal static class CharterBookTests
{
    public static int Run()
    {
        int count = 0; long day = PoliticalWorld.Day;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        void Reject(CharterBook book, Action action, string name)
        {
            string before = book.Export(); bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected && book.Export() == before, name + ": no partial book mutation");
        }
        CharterBook Seed()
        {
            var book = new CharterBook();
            book.Register(new("player", "native-player", true), 0);
            book.Register(new("town-a", "faction-a", false), 0);
            book.Register(new("town-b", "faction-a", false), 0);
            return book;
        }
        TerritoryObservation[] Observe() => [new("player", "native-player", true, true, true),
            new("town-a", "faction-a", true, false, true), new("town-b", "faction-a", true, false, true)];
        RecordedCharterTerms Terms(string liege = "town-a", string subject = "player") => new(liege, subject,
            100, day * 5, day, TributeTransport.SubjectDelivery, .1m);
        var book = Seed();
        var offer = book.Offer("offer", Terms(), 0, day * 3, Observe());
        Check(book.Root("player") == "player", "proposal does not change political affiliation");
        book.Sign("offer", false, 10, Observe());
        Check(book.Charter("offer").Phase == RecordedCharterPhase.Offered && book.Root("player") == "player", "NPC signature alone cannot bind player");
        Reject(book, () => book.Sign("offer", false, 10, Observe()), "duplicate NPC consent");
        var captured = Observe(); captured[1] = captured[1] with { NativeOwner = "faction-b" };
        Reject(book, () => book.Sign("offer", true, 11, captured), "ownership changed after offer");
        Check(book.Availability("town-a", captured) == CharterAvailability.OwnershipChanged, "changed owner is explicitly suspended");
        var missing = Observe(); missing[1] = missing[1] with { Exists = false };
        Reject(book, () => book.Sign("offer", true, 11, missing), "destroyed territory at final consent");
        var hostile = Observe(); hostile[1] = hostile[1] with { Eligible = false };
        Reject(book, () => book.Sign("offer", true, 11, hostile), "new hostile relation at final consent");
        Reject(book, () => book.Sign("offer", true, 11, Observe().Append(Observe()[1])), "duplicate observation cannot impersonate authoritative world");
        book.Sign("offer", true, 11, Observe());
        Check(book.Root("player") == "town-a" && book.RecordedPoliticalOwner("player") == "faction-a" &&
            book.Entity("player").NativeOwner == "native-player", "player political affiliation preserves native ownership");
        Check(book.Root("town-b") == "town-b", "a settlement bond cannot bind other towns sharing its native faction");
        Check(book.Charter("offer").PlayerSignedTick == 11 && book.Charter("offer").NpcSignedTick == 10, "separate consent timestamps");
        Reject(book, () => book.Offer("other-master", Terms("town-b"), 12, day, Observe()), "second master");
        Reject(book, () => book.Offer("cycle", Terms("player", "town-a"), 12, day, Observe()), "direct cycle");
        book.Offer("subvassal", Terms("player", "town-b"), 12, day, Observe());
        book.Sign("subvassal", true, 12, Observe()); book.Sign("subvassal", false, 12, Observe());
        Check(book.Root("town-b") == "town-a", "nested recorded affiliation");
        Reject(book, () => book.ConsentToRelease("subvassal", true, 13, captured), "missing upper lord suspends connected release too");
        book.ConsentToRelease("offer", true, 13, Observe());
        Check(book.Root("player") == "town-a", "unilateral release request cannot terminate bond");
        book.ConsentToRelease("offer", false, 14, Observe());
        Check(book.Charter("offer").Phase == RecordedCharterPhase.Released && book.Root("town-b") == "player" &&
            book.Charter("subvassal").Phase == RecordedCharterPhase.Active, "release moves subtree and preserves child charter");
        Reject(book, () => book.Sign("offer", true, 15, Observe()), "released record cannot be resurrected");
        Reject(book, () => book.Register(new("town-a", "new-owner", false), 15), "identity owner cannot silently change");
        Reject(book, () => book.Register(new("second-player", "native-player", true), 15), "one player identity covers all colonies");
        var reload = CharterBook.Restore(book.Export());
        Check(reload.Export() == book.Export() && reload.Root("town-b") == "player", "restore preserves native identities, history and nested charter");

        foreach (bool npcFirst in new[] { true, false })
        {
            var expiry = Seed(); expiry.Offer("expires", Terms(), 0, day, Observe());
            expiry.Sign("expires", !npcFirst, day - 1, Observe());
            expiry.Sign("expires", npcFirst, day, Observe());
            Check(expiry.Charter("expires").Phase == RecordedCharterPhase.Active, "final response tick is inclusive in either signature order");
            var late = Seed(); late.Offer("expires", Terms(), 0, day, Observe());
            late.Sign("expires", npcFirst, 0, Observe());
            Reject(late, () => late.Sign("expires", !npcFirst, day + 1, Observe()), "expired consent cannot activate");
            Check(late.Phase(late.Charter("expires"), day + 1) == RecordedCharterPhase.Expired, "expiry is displayed without erasing proposal");
        }
        var pending = Seed(); pending.Offer("one", Terms(), 0, day, Observe());
        Reject(pending, () => pending.Offer("two", Terms("town-b"), 0, day, Observe()), "pending proposal spam");
        pending.Decline("one", 0); pending.Offer("two", Terms("town-b"), 0, day, Observe());
        Check(pending.Charters.Count == 2 && pending.Charter("one").Phase == RecordedCharterPhase.Declined, "decline allows new offer but preserves history");
        var race = Seed();
        race.Offer("first", Terms(), 0, day, Observe());
        race.Offer("opposite", Terms("player", "town-a"), 0, day, Observe());
        race.Sign("first", false, 0, Observe()); race.Sign("opposite", false, 0, Observe());
        race.Sign("opposite", true, 1, Observe());
        Reject(race, () => race.Sign("first", true, 2, Observe()), "topology is rechecked at second signature after another contract activated");
        Check(race.Charter("first").Phase == RecordedCharterPhase.Offered && race.Root("town-a") == "player", "rejected activation cannot rewrite the accepted graph");

        void Corrupt(CharterBookSave save, string name)
        {
            bool rejected = false;
            try { CharterBook.Restore(JsonSerializer.Serialize(save)); } catch (ArgumentException) { rejected = true; }
            Check(rejected, name);
        }
        var snapshot = JsonSerializer.Deserialize<CharterBookSave>(book.Export());
        Corrupt(snapshot with { Version = 99 }, "future schema rejected");
        Corrupt(snapshot with { Entities = snapshot.Entities.Append(snapshot.Entities[0]).ToArray() }, "duplicate saved identity rejected");
        Corrupt(snapshot with { LastTick = 0 }, "future consent cannot precede saved clock");
        Corrupt(snapshot with { Charters = snapshot.Charters.Select(c => c.Id == "subvassal" ? c with { NpcSignedTick = null } : c).ToArray() }, "unsigned active charter rejected");
        Corrupt(snapshot with { Charters = snapshot.Charters.Select(c => c.Id == "subvassal" ? c with { PlayerReleaseTick = 0 } : c).ToArray() }, "release cannot predate activation");
        Corrupt(snapshot with { Charters = snapshot.Charters.Select(c => c.Id == "subvassal" ? c with { Terms = c.Terms with { TributeValue = -1 } } : c).ToArray() }, "corrupt terms rejected");
        Corrupt(snapshot with { Entities = snapshot.Entities.Where(e => e.Id != "town-b").ToArray() }, "missing saved participant rejected");
        var badGraph = JsonSerializer.Deserialize<CharterBookSave>(race.Export());
        Corrupt(badGraph with { Charters = badGraph.Charters.Select(c => c.Id == "first" ?
            c with { Phase = RecordedCharterPhase.Active, PlayerSignedTick = 1 } : c).ToArray() }, "cyclic graph cannot be loaded as two active contracts");
        foreach (var transport in RuntimeCompat.EnumValues<TributeTransport>())
        foreach (decimal rate in new[] { 0m, .1m, .5m })
        {
            var roundtrip = Seed();
            roundtrip.Offer("terms", Terms() with { Transport = transport, TransportRate = rate }, 0, day, Observe());
            Check(CharterBook.Restore(roundtrip.Export()).Charter("terms").Terms == roundtrip.Charter("terms").Terms,
                "immutable terms survive serialization for both transport directions");
        }
        return count;
    }
}
