using System.Text.Json;
using Honor.Contracts.Rules;

internal static class PackedTradeAnimalTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string text) { count++; if (!ok) throw new Exception("Packed stock: " + text); }
        void Reject(Action call, string text) { bool rejected = false; try { call(); } catch (ArgumentException) { rejected = true; } Check(rejected, text); }
        string h = new('A', 64);
        CityStockItem[] goods = [new("root", "Steel", null, 50, 100, null)];
        CityTradeAnimal[] animals = [new("animal1", "Muffalo", "source", h, h, h), new("animal2", "Alpaca", null, h, h, h)];
        CityStockItem[] carried = [new("cargo1", "Silver", null, 123, 100, null), new("cargo2", "Longsword", "Steel", 1, 70, 4)];
        var cargo = new CityAnimalCargo("animal1", carried, true, new[] { "cargo2" }, new[] { "cargo1" });
        string Proof(params CityAnimalCargo[] rows) => CityStockProof.CapturePackedAnimals(true, true, 100, goods, animals, rows);
        string proof = Proof(cargo);
        Check(CityStockProof.Valid(proof) && proof != CityStockProof.CaptureAnimals(true, true, 100, goods, animals), "Packed stock has a distinct bounded proof, not an old empty-animal proof.");
        Check(Proof(JsonSerializer.Deserialize<CityAnimalCargo>(JsonSerializer.Serialize(cargo))) == proof, "Holder allocation and native sale/unpack settings survive snapshot recreation.");
        Check(CityStockProof.CapturePackedAnimals(true, true, 100, goods, animals.AsEnumerable().Reverse(), [cargo with { Goods = carried.AsEnumerable().Reverse().ToArray() }]) == proof,
            "Enumeration order alone does not change conserved stock.");
        foreach (var changed in new[] { carried[0] with { Id = "other" }, carried[0] with { Def = "Gold" }, carried[0] with { Stuff = "WoodLog" },
            carried[0] with { Count = 122 }, carried[0] with { HitPoints = 99 }, carried[0] with { Quality = 3 } })
            Check(Proof(cargo with { Goods = new[] { changed, carried[1] }, Unpacked = new[] { changed.Id } }) != proof, "Original carried IDs/kind/material/count/health/quality are bound.");
        foreach (var changed in new[] { cargo with { Animal = "animal2" }, cargo with { UnloadEverything = false },
            cargo with { NotForSale = Array.Empty<string>() }, cargo with { Unpacked = Array.Empty<string>() } })
            Check(Proof(changed) != proof, "Changing holder, raw unload flag or sale/unpack configuration invalidates the review.");
        Check(Proof(cargo with { Goods = new[] { carried[0] }, NotForSale = Array.Empty<string>() }) != proof, "Missing cargo cannot be accepted as unchanged stock.");
        Reject(() => Proof(), "Format2 needs an actual nonempty pack inventory.");
        Reject(() => Proof(cargo with { Goods = Array.Empty<CityStockItem>() }), "A claimed load cannot have no goods.");
        Reject(() => Proof(cargo with { Animal = "foreign" }), "Every load belongs to an actual captured trade animal.");
        Reject(() => Proof(cargo, cargo), "Duplicate holder scope is rejected.");
        Reject(() => Proof(cargo with { Goods = new[] { carried[0], carried[0] } }), "Duplicate item references cannot inflate cargo.");
        Reject(() => Proof(cargo, cargo with { Animal = "animal2" }), "The same original item cannot belong to two animals.");
        Reject(() => Proof(cargo with { Goods = goods, NotForSale = [], Unpacked = [] }), "Root and animal inventory share an exact identity domain.");
        Reject(() => Proof(cargo with { Goods = new[] { carried[0] with { Id = "animal1" } }, NotForSale = [], Unpacked = [] }), "A pawn cannot also be counted as cargo.");
        Reject(() => Proof(cargo with { NotForSale = null }), "Missing sale configuration is not normalized.");
        Reject(() => Proof(cargo with { Unpacked = new[] { "foreign" } }), "External unpack references do not authorize other goods.");
        Reject(() => Proof(cargo with { NotForSale = new[] { "cargo2", "cargo2" } }), "Repeated protected-sale references remain unsupported.");
        Reject(() => Proof(cargo with { Goods = Enumerable.Range(0, 10000).Select(i => carried[0] with { Id = "item" + i }).ToArray(), NotForSale = [], Unpacked = [] }),
            "One shared bound counts root goods, animals and carried entries together.");
        Reject(() => CityStockProof.CapturePackedAnimals(false, true, 100, goods, animals, [cargo]), "Cargo review never generates an unvisited town's stock.");
        Reject(() => CityStockProof.CapturePackedAnimals(true, false, -1, goods, animals, [cargo]), "All pack animals retain their actual root stock container.");
        var book = new CharterBook(); book.Register(new("player", "native-player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] observations = [new("player", "native-player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("player", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, observations);
        book.Sign("bond", true, 0, observations); book.Sign("bond", false, 0, observations);
        string bookBefore = book.Export();
        var ledger = new TerritoryTransferLedger();
        var original = new TerritoryTransferObservation("town", "source", 2, Visited: true, StockProof: proof, StockFormat: 2);
        ledger.Prepare("move", book, "bond", original, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 1);
        string saved = ledger.Export(); Check(TerritoryTransferLedger.Restore(saved).Export() == saved, "Real pending city execution keeps format2 through saves.");
        foreach (var changed in new[] { original with { StockFormat = 0 }, original with { StockFormat = 1 }, original with { StockProof = Proof(cargo with { Animal = "animal2" }) },
            original with { HasMap = true }, original with { StockProof = null } })
        {
            Reject(() => ledger.Confirm("move", changed with { Owner = "target" }, 2), "Changed/emptied/reattributed cargo cannot confirm native movement.");
            Reject(() => ledger.CancelUnmoved("move", changed, 2), "Old town ownership alone does not prove unchanged pack resources.");
            Check(ledger.Export() == saved, "Rejected recovery retains original pending evidence.");
        }
        var cancelled = TerritoryTransferLedger.Restore(saved); cancelled.CancelUnmoved("move", original, 2);
        Check(cancelled.Transfer("move").Phase == TerritoryTransferPhase.Cancelled, "Wholly unchanged stock can cancel before movement.");
        ledger.Confirm("move", original with { Owner = "target" }, 2);
        Check(ledger.Transfer("move").Phase == TerritoryTransferPhase.Applied && TerritoryTransferLedger.Restore(ledger.Export()).Export() == ledger.Export(), "Actual target ownership and original cargo proof finish and restore.");
        Check(book.Export() == bookBefore, "Physical cargo completion neither replaces contracts nor creates new currency.");
        var bad = JsonSerializer.Deserialize<TerritoryTransfersSave>(saved);
        Reject(() => TerritoryTransferLedger.Restore(JsonSerializer.Serialize(bad with { Transfers = [bad.Transfers[0] with { StockProof = null }] })), "Saved cargo format cannot lose its required original proof.");
        return count;
    }
}
