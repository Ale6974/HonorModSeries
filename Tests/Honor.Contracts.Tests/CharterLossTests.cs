using System.Text.Json;
using Honor.Contracts.Rules;

internal static class CharterLossTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        void Reject(CharterBook book, Action run, string name)
        { string before = book.Export(); bool rejected = false; try { run(); } catch (ArgumentException) { rejected = true; } Check(rejected && book.Export() == before, name); }
        var book = new CharterBook(); book.Register(new("player", "native", true), 0);
        foreach (string id in new[] { "town", "sibling", "child", "external" }) book.Register(new(id, "source", false), 0);
        TerritoryObservation[] Obs(CharterBook b) => b.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string liege, string subject) => new(liege, subject, 123, PoliticalWorld.Day, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m);
        foreach (string id in new[] { "town", "sibling" })
            book.Offer(id, Terms("player", id), 0, PoliticalWorld.Day, Obs(book));
        foreach (string id in new[] { "town", "sibling" }) { book.Sign(id, true, 2, Obs(book)); book.Sign(id, false, 2, Obs(book)); }
        string original = book.Export();
        Check(!original.Contains("TerritoryLostTick"), "legacy source JSON omits new null field");
        Reject(book, () => book.CloseDestroyedLeaf("town", "town", "source", 3, 3, false), "missing reference/removal alone is not destruction authority");
        Reject(book, () => book.CloseDestroyedLeaf("town", "sibling", "source", 3, 3, true), "wrong destroyed town rejected");
        Reject(book, () => book.CloseDestroyedLeaf("town", "town", "foreign", 3, 3, true), "wrong original identity rejected");
        Reject(book, () => book.CloseDestroyedLeaf("town", "town", "source", 1, 3, true), "loss cannot precede bilateral activation");
        Reject(book, () => book.CloseDestroyedLeaf("town", "town", "source", 4, 3, true), "future loss rejected");
        var transfers = new TerritoryTransferLedger();
        transfers.Prepare("move", book, "town", new("town", "source", 2), "authority", "authority", null, TerritoryTransferPurpose.ActiveCharter, 3);
        transfers.Confirm("move", new("town", "authority", 1), 3);
        string journal = transfers.Export();
        var before = book.Charter("town"); var sibling = book.Charter("sibling");
        book.CloseDestroyedLeaf("town", "town", "source", 4, 4, true);
        var ended = book.Charter("town");
        Check(ended.Phase == RecordedCharterPhase.TerritoryLost && ended.TerritoryLostTick == 4, "actual NPC leaf loss closes incoming bond");
        Check(ended.Terms == before.Terms && ended.PlayerSignedTick == before.PlayerSignedTick && ended.NpcSignedTick == before.NpcSignedTick, "original terms/signatures retained");
        Check(!ended.PlayerReleaseTick.HasValue && !ended.NpcReleaseTick.HasValue, "loss does not forge mutual release");
        Check(book.Charter("sibling") == sibling && book.PlayerFollowers().Select(c => c.Id).SequenceEqual(new[] { "sibling" }), "unrelated contracts unaffected and lost town no longer a follower");
        Check(transfers.Export() == journal, "physical history is never erased by loss");
        Check(CharterBook.Restore(book.Export()).Export() == book.Export(), "closed loss persists without dead town references");
        Reject(book, () => book.CloseDestroyedLeaf("town", "town", "source", 5, 5, true), "duplicate loss cannot rewrite first event");
        Reject(book, () => book.ConsentToRelease("town", true, 5, Obs(book)), "terminated lease cannot be released again");
        var nested = CharterBook.Restore(original);
        nested.OfferNpc("child", Terms("town", "child"), new("leader", "leader"), 3, PoliticalWorld.Day, Obs(nested));
        nested.SignNpc("child", "town", "leader", 3, Obs(nested)); nested.SignNpc("child", "child", "leader", 3, Obs(nested));
        Reject(nested, () => nested.CloseDestroyedLeaf("town", "town", "source", 4, 4, true), "living descendants require coordinated succession, not orphaning");
        nested.CloseDestroyedLeaf("child", "child", "source", 4, 4, true);
        Check(nested.Charter("town").Phase == RecordedCharterPhase.Active && nested.Charter("child").Phase == RecordedCharterPhase.TerritoryLost, "NPC-only leaf closes without changing its parent");
        var pending = CharterBook.Restore(original);
        pending.OfferNpc("proposal", Terms("town", "child"), new("leader", "leader"), 3, PoliticalWorld.Day, Obs(pending));
        Reject(pending, () => pending.CloseDestroyedLeaf("town", "town", "source", 4, 4, true), "open rights proposal not silently cancelled or signed");
        var player = CharterBook.Restore(original);
        player.Offer("player-bond", Terms("external", "player"), 3, PoliticalWorld.Day, Obs(player));
        player.Sign("player-bond", true, 3, Obs(player)); player.Sign("player-bond", false, 3, Obs(player));
        Reject(player, () => player.CloseDestroyedLeaf("player-bond", "player", "native", 4, 4, true), "native player group excluded");
        var debt = new TributeLedger(); debt.Enable(player, "player-bond", 3); string debtSnapshot = debt.Export();
        player.CloseDestroyedLeaf("town", "town", "source", 4, 4, true);
        Check(debt.Export() == debtSnapshot && player.Charter("player-bond").Phase == RecordedCharterPhase.Active, "player debt/incoming obligations remain untouched");
        var partial = CharterBook.Restore(original); partial.ConsentToRelease("town", true, 3, Obs(partial));
        Reject(partial, () => partial.CloseDestroyedLeaf("town", "town", "source", 2, 4, true), "loss cannot predate recorded partial release");
        partial.CloseDestroyedLeaf("town", "town", "source", 4, 4, true);
        Check(CharterBook.Restore(partial.Export()).Charter("town").PlayerReleaseTick == 3 && !partial.Charter("town").NpcReleaseTick.HasValue, "one-sided release evidence preserved without inventing the other signature");
        foreach (var damaged in new[] { ended with { TerritoryLostTick = null }, ended with { TerritoryLostTick = 1 },
            ended with { TerritoryLostTick = 9 }, ended with { Phase = RecordedCharterPhase.Active }, ended with { PlayerSignedTick = null } })
        {
            var save = JsonSerializer.Deserialize<CharterBookSave>(book.Export());
            string altered = JsonSerializer.Serialize(save with { Charters = save.Charters.Select(c => c.Id == ended.Id ? damaged : c).ToArray() });
            bool rejected = false; try { CharterBook.Restore(altered); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "invalid persisted loss data rejected");
        }
        book.Register(new("rebuilt", "source", false), 5);
        book.Offer("new", Terms("player", "rebuilt"), 5, PoliticalWorld.Day, Obs(book));
        book.Sign("new", true, 6, Obs(book)); book.Sign("new", false, 6, Obs(book));
        Check(book.Charter("town") == ended && book.Charter("new").PlayerSignedTick == 6, "new town/new contract never resurrects old charter or backdates consent");
        return count;
    }
}
