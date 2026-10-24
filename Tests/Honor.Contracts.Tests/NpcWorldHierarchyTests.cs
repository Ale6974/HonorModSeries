using Honor.Contracts.Rules;

internal static class NpcWorldHierarchyTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool condition, string name) { count++; if (!condition) throw new Exception(name); }
        void Reject(Action action, string name)
        { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        var cities = Enumerable.Range(0, 10).Select(i => new NpcWorldCity("city-" + i.ToString("D2"), "native-realm", "leader")).ToArray();
        var plan = NpcWorldHierarchy.Plan(cities, [], 100);
        Check(plan.Count == 10 && plan.SequenceEqual(NpcWorldHierarchy.Plan(cities.AsEnumerable().Reverse(), [], 100)), "initial NPC hierarchy independent of source enumeration");
        Check(NpcWorldHierarchy.Plan(cities, [], 20).Count == 2 && NpcWorldHierarchy.Plan(cities, [], 0).Count == 0, "configured ratio selects bounded initial NPC towns");
        Check(NpcWorldHierarchy.Plan(cities, ["native-realm"], 100).Count == 0, "a realm with recorded identities is never rewritten by initialization");
        var book = new CharterBook(); book.Register(new("faction/native-realm", "native-realm", false), 0);
        foreach (var city in cities) book.Register(new(city.City, city.Owner, false), 0);
        TerritoryObservation[] Observe() => book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        foreach (var link in plan)
        {
            string id = "seed/" + link.City;
            book.OfferNpc(id, new(link.Liege, link.City, 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m),
                new(link.Representative, link.Representative), 1, PoliticalWorld.Day, Observe());
            book.SignNpc(id, link.Liege, link.Representative, 1, Observe()); book.SignNpc(id, link.City, link.Representative, 1, Observe());
        }
        Check(book.Charters.Count == 10 && book.Charters.All(c => c.Phase == RecordedCharterPhase.Active), "world plan creates real active NPC contracts without a player party");
        Check(book.Root("city-02") == "faction/native-realm" && book.IsBelow("city-02", "city-00"), "world NPC chain is in the common real contract topology");
        Check(book.Charter("seed/city-03").Terms.Liege == "faction/native-realm", "a new branch prevents a fifth hierarchy level");
        Check(book.PlayerFollowers().Length == 0, "NPC-only world contracts are never included in unrelated player migrations");
        Check(book.RecordedPoliticalOwner("city-02") == "native-realm" && book.Entities.All(e => e.NativeOwner == "native-realm"), "initial native sovereignty and every original owner are unchanged");
        Check(CharterBook.Restore(book.Export()).Export() == book.Export(), "initial NPC world with no player entity restores");
        book.Register(new("player", "native-player", true), 1);
        book.Offer("player-entry", new("faction/native-realm", "player", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 2, PoliticalWorld.Day, Observe());
        book.Sign("player-entry", true, 2, Observe()); book.Sign("player-entry", false, 2, Observe());
        Check(book.Root("player") == "faction/native-realm" && book.PlayerFollowers().Length == 0, "player can join an initialized NPC realm without acquiring all its sibling vassals");
        var mixed = cities.Select(c => c with { Eligible = c.City != "city-00" }).ToArray();
        Check(NpcWorldHierarchy.Plan(mixed, [], 100).All(c => c.City != "city-00"), "ineligible city excluded before assigning parents");
        var large = Enumerable.Range(0, 100).Select(i => new NpcWorldCity("town" + i, "owner", "leader"));
        Check(NpcWorldHierarchy.Plan(large, [], 100).Count == 30, "per-setup thirty-city cap");
        Reject(() => NpcWorldHierarchy.Plan([cities[0], cities[0]], [], 20), "duplicate world city IDs rejected");
        Reject(() => NpcWorldHierarchy.Plan([cities[0], cities[1] with { Representative = "other-leader" }], [], 100), "inconsistent current realm representatives rejected");
        Reject(() => NpcWorldHierarchy.Plan(cities, [], 101), "unsupported ratio rejected");
        Reject(() => NpcWorldHierarchy.Plan(cities, [], 10, 31), "unbounded initialization rejected");
        Reject(() => NpcWorldHierarchy.Plan([cities[0] with { Owner = "" }], [], 100), "missing native owner rejected");
        return count;
    }
}
