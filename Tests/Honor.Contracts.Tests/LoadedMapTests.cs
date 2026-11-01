using System.Text.Json;
using Honor.Contracts.Rules;

internal static class LoadedMapTests
{
    public static int Run()
    {
        int n = 0; void Check(bool ok) { n++; if (!ok) throw new Exception("Loaded neutral-map custody regression."); }
        void Reject(Action f) { bool rejected = false; try { f(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        string stock = CityStockProof.Capture(true, true, 0, []);
        NeutralMapThing[] things = [new("item", "Steel", null, 50, 100, 10, 0, null), new("wall", "Wall", "Granite", 1, 400, 11, 1, null)];
        string map = LoadedMapPreservation.Capture("town", "map", 20, 20, new string('A', 64), stock, 0, things);
        var original = LoadedMapPreservation.Restore(map);
        string missing = CityStockProof.Capture(true, false, -1, []);
        Check(missing != CityStockProof.Capture(false, false, -1, []));
        Check(LoadedMapPreservation.Restore(LoadedMapPreservation.Capture("town", "map", 20, 20, new string('A', 64), missing, 0, things)).Stock == missing);
        LoadedMapPreservation.Pair(map, "town", original.Proof);
        Reject(() => LoadedMapPreservation.Pair(map, "other", original.Proof));
        Reject(() => LoadedMapPreservation.Restore(JsonSerializer.Serialize(original with { Map = "other" })));
        Reject(() => LoadedMapPreservation.Capture("town", "map", 20, 20, new string('A', 64), stock, 0, [things[0], things[0]]));
        Reject(() => LoadedMapPreservation.Capture("town", "map", 20, 20, new string('A', 64), stock, 0, [things[0] with { Cell = 400 }]));
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("p", "player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", false, 1, obs); book.Sign("bond", true, 2, obs);
        var source = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, StockProof: original.Proof, StockFormat: 3) { LoadedMap = map };
        var ledger = new TerritoryTransferLedger(); string before = ledger.Export();
        Reject(() => ledger.Prepare("legacy", book, "bond", source, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        Check(ledger.Export() == before && !ledger.Export().Contains("LoadedMap"));
        Reject(() => ledger.PrepareLoaded("no-map", book, "bond", source with { HasMap = false }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3));
        var plan = FollowerTransfers.Review(book, ledger, [new("bond", source)], "target", 3);
        Check(plan.Moves.SequenceEqual(["bond"]) && plan.Entries[0].Observation.LoadedMap == map);
        ledger.PrepareLoadedJournaled("map-move", book, "bond", source, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        var restored = TerritoryTransferLedger.Restore(ledger.Export());
        Check(restored.Transfer("map-move").LoadedMap == map && restored.Transfer("map-move").CityCallJournalV1);
        string pending = restored.Export();
        var changedMap = LoadedMapPreservation.Capture("town", "map", 20, 20, new string('A', 64), stock, 0, [things[0] with { Count = 49 }, things[1]]);
        Reject(() => restored.Confirm("map-move", source with { Owner = "target", LoadedMap = changedMap, StockProof = LoadedMapPreservation.Restore(changedMap).Proof }, 4));
        Reject(() => restored.Confirm("map-move", source with { Owner = "target", HasMap = false, LoadedMap = null, StockFormat = 0, StockProof = stock }, 4));
        Check(restored.Export() == pending);
        var saved = JsonSerializer.Deserialize<TerritoryTransfersSave>(pending);
        Reject(() => TerritoryTransferLedger.Restore(JsonSerializer.Serialize(saved with { Transfers = [saved.Transfers[0] with { CityCallJournalV1 = false }] })));
        Reject(() => TerritoryTransferLedger.Restore(JsonSerializer.Serialize(saved with { Transfers = [saved.Transfers[0] with { LoadedMap = null }] })));
        restored.Confirm("map-move", source with { Owner = "target" }, 4);
        Check(TerritoryTransferLedger.Restore(restored.Export()).Transfer("map-move").Phase == TerritoryTransferPhase.Applied && restored.VerifiedOwner("town", "source", "target"));
        var cancelled = TerritoryTransferLedger.Restore(pending); cancelled.CancelUnmoved("map-move", source, 4);
        Check(cancelled.Transfer("map-move").Phase == TerritoryTransferPhase.Cancelled && cancelled.Transfer("map-move").LoadedMap == map);
        var residents = ResidentMoveSet.Capture([new("pawn", new string('B', 64), 4)]);
        var residentObservation = source with { HasInhabitants = true, ResidentProof = residents.Proof };
        var residentLedger = new TerritoryTransferLedger(); residentLedger.PrepareLoaded("resident", book, "bond", residentObservation, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 3);
        Reject(() => residentLedger.Confirm("resident", residentObservation with { Owner = "target", ResidentProof = null }, 4));
        residentLedger.Confirm("resident", residentObservation with { Owner = "target" }, 4);
        Check(residentLedger.Transfer("resident").ResidentProof == residents.Proof && residentLedger.Transfer("resident").LoadedMap == map);
        return n;
    }
}
