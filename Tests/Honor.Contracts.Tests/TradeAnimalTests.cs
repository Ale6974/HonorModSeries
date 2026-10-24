using System.Text.Json;
using Honor.Contracts.Rules;

internal static class TradeAnimalTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        void Reject(Action action, string name)
        { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        string a = new('A', 64), b = new('B', 64);
        CityStockItem[] goods = [new("Steel_1", "Steel", null, 10, 100, null)];
        CityTradeAnimal[] animals = [new("Pawn_1", "Muffalo", "source", a, a, a), new("Pawn_2", "Alpaca", null, a, a, a)];
        string Proof(IEnumerable<CityTradeAnimal> current) => CityStockProof.CaptureAnimals(true, true, 100, goods, current);
        string proof = Proof(animals);
        Check(CityStockProof.Valid(proof) && Proof(animals.AsEnumerable().Reverse()) == proof, "animal evidence has stable identity order");
        Check(proof != CityStockProof.Capture(true, true, 100, goods), "animal-bearing snapshot cannot collide structurally with the ordinary format");
        foreach (var changed in new[] { animals[0] with { Id = "Pawn_3" }, animals[0] with { Def = "Cow" }, animals[0] with { Faction = "target" },
            animals[0] with { Faction = null }, animals[0] with { Health = b }, animals[0] with { Gear = b }, animals[0] with { Status = b } })
            Check(Proof([changed, animals[1]]) != proof, "animal identity/species/faction/health/gear/status mutations change proof");
        Check(Proof([animals[0]]) != proof, "removed animal cannot count as conserved inventory");
        Reject(() => Proof([]), "animal format requires an animal");
        Reject(() => Proof([animals[0], animals[0]]), "duplicate stock pawn rejected");
        Reject(() => Proof([animals[0] with { Id = "Steel_1" }]), "ordinary and pawn IDs share one uniqueness domain");
        Reject(() => Proof([animals[0] with { Health = "" }]), "incomplete health evidence rejected");
        Reject(() => CityStockProof.CaptureAnimals(false, true, 100, goods, animals), "unvisited stock cannot claim generated animals");
        Reject(() => CityStockProof.CaptureAnimals(true, false, -1, [], animals), "animal without a stock holder rejected");

        var book = new CharterBook(); book.Register(new("player", "native-player", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] observed = [new("player", "native-player", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("player", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, observed);
        book.Sign("bond", true, 0, observed); book.Sign("bond", false, 0, observed);
        var ledger = new TerritoryTransferLedger();
        var source = new TerritoryTransferObservation("town", "source", 2, Visited: true, StockProof: proof, StockFormat: 1);
        ledger.Prepare("move", book, "bond", source, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 1);
        string before = ledger.Export();
        Check(ledger.Transfer("move").StockFormat == 1 && TerritoryTransferLedger.Restore(before).Export() == before, "prepared animal proof and format survive restore");
        foreach (var changed in new[] { source with { StockFormat = 0 }, source with { StockFormat = 2 }, source with { StockProof = null },
            source with { StockProof = Proof([animals[0]]) }, source with { HasInhabitants = true }, source with { HasMap = true } })
        {
            Reject(() => ledger.Confirm("move", changed with { Owner = "target" }, 2), "changed resources or profile cannot confirm a migration");
            Reject(() => ledger.CancelUnmoved("move", changed, 2), "same old owner does not waive preservation evidence");
            Check(ledger.Export() == before, "failed recovery keeps original unresolved intent");
        }
        ledger.Confirm("move", source with { Owner = "target" }, 2);
        Check(ledger.Transfer("move").Phase == TerritoryTransferPhase.Applied, "animal-bearing town completes only with actual destination and exact stock proof");
        var saved = JsonSerializer.Deserialize<TerritoryTransfersSave>(ledger.Export());
        foreach (var bad in new[] { saved.Transfers[0] with { StockFormat = 9 }, saved.Transfers[0] with { StockProof = null } })
            Reject(() => TerritoryTransferLedger.Restore(JsonSerializer.Serialize(saved with { Transfers = [bad] })), "saved proof format cannot be downgraded or invented");
        var legacy = new TerritoryTransferLedger();
        string ordinary = CityStockProof.Capture(true, true, 100, goods);
        legacy.Prepare("old", book, "bond", source with { StockFormat = 0, StockProof = ordinary }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 1);
        Check(!legacy.Export().Contains("StockFormat"), "legacy ordinary-stock journal bytes omit the new default field");
        Reject(() => legacy.Confirm("old", source with { Owner = "target" }, 2), "an ordinary-only pending intent cannot silently accept added animals");
        return count;
    }
}
