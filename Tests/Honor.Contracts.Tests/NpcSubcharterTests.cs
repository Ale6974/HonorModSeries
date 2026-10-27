using System.Text.Json;
using Honor.Contracts.Rules;

internal static class NpcSubcharterTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        void Reject(CharterBook book, Action action, string name)
        {
            string original = book.Export(); bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected && original == book.Export(), name + ": source unchanged");
        }
        var book = new CharterBook(); book.Register(new("player", "native-player", true), 0);
        foreach (string id in new[] { "a", "b", "c", "d", "e", "outsider" }) book.Register(new(id, "source", false), 0);
        TerritoryObservation[] Observe(CharterBook b) => b.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string liege, string subject) => new(liege, subject, 123, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m);
        foreach (string id in new[] { "a", "b", "c", "d", "e" })
        {
            book.Offer("bond-" + id, Terms("player", id), 0, PoliticalWorld.Day, Observe(book));
            book.Sign("bond-" + id, true, 0, Observe(book)); book.Sign("bond-" + id, false, 0, Observe(book));
        }
        var authority = new NpcCharterAuthority("leader-a", "leader-b");
        Check(!book.Export().Contains("NpcAuthority"), "legacy v1 omits the new null field and preserves source digest shape");
        Reject(book, () => book.Offer("unauthorized", Terms("a", "outsider"), 1, PoliticalWorld.Day, Observe(book)), "legacy player offer cannot invent an NPC pair");
        Reject(book, () => book.OfferNpc("fake", Terms("player", "outsider"), authority, 1, PoliticalWorld.Day, Observe(book)), "NPC authority cannot sign for the player");
        string before = book.Export();
        var plan = NpcSubcharters.Review(book, "bond-b", "a", "a-b", authority, 1, Observe(book));
        Check(book.Export() == before, "successful full subcharter review makes no real consent changes");
        var wrongWorld = Observe(book).Select(o => o.Id == "a" ? o with { Exists = false } : o).ToArray();
        Reject(book, () => NpcSubcharters.Commit(book, plan, 1, wrongWorld), "commit requires fresh actual connected observations");
        Reject(book, () => NpcSubcharters.Commit(book, plan, 2, Observe(book)), "expired review cannot replace a book");
        var after = NpcSubcharters.Commit(book, plan, 1, Observe(book));
        Check(after.Charter("bond-b").Phase == RecordedCharterPhase.Released && after.Charter("a-b").Phase == RecordedCharterPhase.Active, "old bilateral contract released and new NPC contract actually active together");
        Check(after.Charter("a-b").Terms == Terms("a", "b"), "fixed payment rights survive with only immediate liege changed");
        Check(after.IsBelow("b", "a") && after.IsBelow("b", "player") && after.Root("b") == "player", "NPC grandchild uses the real combined book topology");
        Check(after.PlayerFollowers().Length == 5 && after.PlayerFollowers().Any(c => c.Id == "a-b"), "complete follower set includes indirect NPC vassals");
        Check(CharterBook.Restore(after.Export()).Export() == after.Export(), "NPC and legacy contracts restore in the same v1 book");
        Reject(after, () => NpcSubcharters.Review(after, "bond-a", "b", "cycle", authority, 2, Observe(after)), "parent cannot be moved below its own descendant");
        Reject(after, () => NpcSubcharters.Review(after, "a-b", "a", "same", authority, 2, Observe(after)), "same immediate parent does not reset contract history");
        Reject(after, () => NpcSubcharters.Review(after, "bond-c", "outsider", "outside", authority, 2, Observe(after)), "outside realm requires a different authority flow");
        Reject(after, () => after.Sign("a-b", true, 2, Observe(after)), "player cannot use legacy signature slot for an NPC contract");
        Reject(after, () => after.ConsentToRelease("a-b", true, 2, Observe(after)), "player cannot use legacy release slot for an NPC contract");
        var abc = NpcSubcharters.Review(after, "bond-c", "b", "b-c", authority, 2, Observe(after));
        var chain = NpcSubcharters.Commit(after, abc, 2, Observe(after));
        Check(chain.IsBelow("c", "a") && chain.IsBelow("c", "b"), "four-level root/player-NPC chain includes descendants");
        Check(chain.HierarchyPath("c").SequenceEqual(["player", "a", "b", "c"]), "active hierarchy displays the actual complete four-level path");
        Reject(chain, () => NpcSubcharters.Review(chain, "bond-d", "c", "too-deep", authority, 3, Observe(chain)), "fifth level rejected before replacing any old contract");
        Reject(chain, () => NpcSubcharters.Review(chain, "bond-a", "d", "d-a", authority, 3, Observe(chain)), "moving a parent validates the depth of all retained descendants");
        var move = NpcSubcharters.Review(after, "bond-a", "d", "d-a", authority, 3, Observe(after));
        var moved = NpcSubcharters.Commit(after, move, 3, Observe(after));
        Check(moved.Charter("a-b") == after.Charter("a-b") && moved.IsBelow("b", "d") && moved.Root("b") == "player", "reparenting a middle lord retains its child's exact contract and follows the subtree");
        Check(moved.Charter("bond-a").Phase == RecordedCharterPhase.Released && moved.Charter("d-a").Terms.TributeValue == 123, "replacement does not reset original financial history");
        var promotePlan = NpcSubcharters.Review(moved, "d-a", "player", "promoted-a", authority, 4, Observe(moved));
        var promoted = NpcSubcharters.Commit(moved, promotePlan, 4, Observe(moved));
        Check(promoted.Charter("promoted-a").NpcAuthority == null && promoted.Charter("promoted-a").Terms.Liege == "player" &&
            promoted.Charter("a-b") == moved.Charter("a-b"), "a middle NPC lord can return to direct player allegiance without resetting its child contract");
        Check(promoted.IsBelow("b", "a") && !promoted.IsBelow("a", "d") && promoted.PlayerFollowers().Length == 5, "promotion retains the subtree while reducing depth");
        Check(promoted.HierarchyPath("b").SequenceEqual(["player", "a", "b"]), "promoted hierarchy follows the current active contract rather than released history");
        var modified = CharterBook.Restore(after.Export()); modified.Register(new("fresh", "source", false), 2);
        Reject(modified, () => NpcSubcharters.Commit(modified, move, 3, Observe(modified)), "unrelated intervening registry mutation invalidates reviewed book");
        Reject(after, () => NpcSubcharters.Commit(after, move with { After = move.Before }, 3, Observe(after)), "invented after-state cannot be committed");

        after.OfferNpc("pending-npc", Terms("a", "outsider"), authority, 2, PoliticalWorld.Day, Observe(after));
        Reject(after, () => after.SignNpc("pending-npc", "a", "unknown", 2, Observe(after)), "wrong representative cannot sign NPC consent");
        Reject(after, () => after.SignNpc("pending-npc", "player", "leader-a", 2, Observe(after)), "player cannot impersonate either NPC party");
        after.SignNpc("pending-npc", "a", "leader-a", 2, Observe(after));
        Check(after.Charter("pending-npc").Phase == RecordedCharterPhase.Offered, "one NPC signature does not create an active contract");
        Reject(after, () => after.SignNpc("pending-npc", "a", "leader-a", 2, Observe(after)), "repeated NPC signature rejected");
        after.SignNpc("pending-npc", "outsider", "leader-b", 2, Observe(after));
        after.ConsentNpcRelease("pending-npc", "a", 3, Observe(after));
        Check(after.Charter("pending-npc").Phase == RecordedCharterPhase.Active, "one NPC release signature does not terminate the contract");
        after.ConsentNpcRelease("pending-npc", "outsider", 3, Observe(after));
        Check(after.Charter("pending-npc").Phase == RecordedCharterPhase.Released, "two current NPC parties may release without faking a player signature");

        var transfer = new TerritoryTransferLedger();
        var candidates = chain.PlayerFollowers().Select(c => new FollowerCandidate(c.Id, new(c.Terms.Subject, "source", 6))).ToArray();
        var migration = FollowerTransfers.Review(chain, transfer, candidates, "new-realm", 4);
        var player = new PlayerAllegiance("native-player");
        var command = new AllegianceCommand(AllegianceAction.JoinUnion, 4, "new-realm", Consent: true, NoActiveFeudalContract: true);
        var transition = new AllegianceTransitionLedger();
        transition.Prepare("nested-transition", player, command, migration, transfer);
        player.Apply(command); transition.ConfirmAllegiance(player, 4);
        Check(migration.Moves.Count == 5 && migration.Moves.Contains("a-b") && migration.Moves.Contains("b-c"), "realm migration reviews grandchildren and great-grandchildren, not only direct NPC vassals");
        bool omitted = false;
        try { FollowerTransfers.Review(chain, transfer, candidates.Where(c => c.Charter != "b-c"), "new-realm", 4); } catch (ArgumentException) { omitted = true; }
        Check(omitted && transfer.Transfers.Count == 0, "omitting an indirect follower blocks the whole preflight without ownership intent");
        int remaining = 6;
        foreach (string id in migration.Moves)
        {
            var city = candidates.Single(c => c.Charter == id).Observation with { SourceSettlements = remaining-- };
            transfer.Prepare(id, chain, id, city, "new-realm", "new-realm", null, TerritoryTransferPurpose.ActiveCharter, 4);
            transfer.Confirm(id, city with { Owner = "new-realm" }, 4);
        }
        Check(transfer.Transfers.Count == 5 && transfer.Transfers.All(t => t.Phase == TerritoryTransferPhase.Applied), "actual owner observations confirm every indirect follower independently");
        Check(TerritoryTransferLedger.Restore(transfer.Export()).Export() == transfer.Export(), "NPC descendant transfer histories roundtrip");
        var final = candidates.Select(c => c with { Observation = c.Observation with { Owner = "new-realm", SourceSettlements = 5 } }).ToArray();
        transition.Complete(player, FollowerTransfers.Review(chain, transfer, final, "new-realm", 4), 4);
        Check(transition.Current.Phase == AllegianceTransitionPhase.Completed && transition.Current.Followers.Count == 5,
            "coordinated allegiance finishes only after the full indirect follower set reaches the realm");
        var saved = JsonSerializer.Deserialize<CharterBookSave>(chain.Export());
        foreach (var invalid in new[] { saved.Charters.Single(c => c.Id == "a-b") with { NpcAuthority = null },
            saved.Charters.Single(c => c.Id == "a-b") with { NpcAuthority = new("", "leader-b") },
            saved.Charters.Single(c => c.Id == "bond-a") with { NpcAuthority = authority } })
        {
            var corrupted = saved with { Charters = saved.Charters.Select(c => c.Id == invalid.Id ? invalid : c).ToArray() };
            bool rejected = false;
            try { CharterBook.Restore(JsonSerializer.Serialize(corrupted)); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "saved NPC/player consent mode and representative identities are structurally checked");
        }
        return count;
    }
}
