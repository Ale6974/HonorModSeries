using Honor.Contracts.Rules;
using System.Text.Json;

internal static class NpcReparentArchiveTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool ok) { checks++; if (!ok) throw new Exception("NPC reparent archive regression."); }
        void Reject(Action action) { bool bad = false; try { action(); } catch (ArgumentException) { bad = true; } Check(bad); }
        var book = new CharterBook();
        book.Register(new("player", "player-faction", true), 0);
        book.Register(new("a", "realm", false), 0); book.Register(new("b", "realm", false), 0);
        var observations = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string subject) => new("player", subject, 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0);
        foreach (var id in new[] { "a", "b" })
        {
            book.Offer("bond-" + id, Terms(id), 0, PoliticalWorld.Day, observations);
            book.Sign("bond-" + id, true, 0, observations); book.Sign("bond-" + id, false, 0, observations);
        }
        var authority = new NpcRealmDecisionEvidence("realm", "ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var transfers = new TerritoryTransferLedger(); var payments = new TributeLedger();
        var witness = new NpcReparentWitness("bond-b", "a", "a-b", book.Export(), 1, authority, observations, transfers.Export(), payments.Export());
        var archive = new NpcReparentArchive(); var result = archive.Append(witness, book);
        Check(book.Charter("bond-b").Phase == RecordedCharterPhase.Active && archive.Records.Count == 0);
        Check(result.Book.Charter("bond-b").Phase == RecordedCharterPhase.Released && result.Book.Charter("a-b").Reparent?.Previous == "bond-b");
        var restored = NpcReparentArchive.Restore(result.Archive.Export());
        restored.ValidateBook(CharterBook.Restore(result.Book.Export())); Check(restored.Records.Count == 1);
        var unchangedArchive = restored.Export(); var exposed = restored.Records[0].Witness.Observations;
        exposed[0] = exposed[0] with { Exists = false };
        Check(restored.Export() == unchangedArchive);
        Reject(() => new NpcReparentArchive().ValidateBook(result.Book));
        var saved = JsonSerializer.Deserialize<CharterBookSave>(result.Book.Export());
        var changedThirdParty = CharterBook.Restore(JsonSerializer.Serialize(saved with { Charters = saved.Charters.Select(c =>
            c.Id == "bond-a" ? c with { Terms = c.Terms with { TributeValue = 99 } } : c).ToArray() }));
        Reject(() => restored.ValidateBook(changedThirdParty));
        Reject(() => archive.Append(witness with { Before = result.Book.Export() }, book));
        Reject(() => archive.Append(witness with { Observations = observations.Take(2).ToArray() }, book));
        Reject(() => archive.Append(witness with { Decision = authority with { Realm = "different-realm" } }, book));
        Reject(() => NpcReparentArchive.Restore(result.Archive.Export().Replace("\"Proof\":\"", "\"Proof\":\"0")));
        var nextObservations = result.Book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        var again = new NpcReparentWitness("a-b", "player", "b-player", result.Book.Export(), 2, authority, nextObservations, transfers.Export(), payments.Export());
        var twice = restored.Append(again, result.Book); twice.Archive.ValidateBook(CharterBook.Restore(twice.Book.Export()));
        Check(twice.Book.Charter("a-b").Phase == RecordedCharterPhase.Released && twice.Book.Charter("b-player").Reparent.Previous == "a-b");
        Check(twice.Archive.Records.Count == 2 && twice.Book.Charter("bond-a") == book.Charter("bond-a"));
        Check(twice.Archive.Records.All(r => r.Witness.Payments == payments.Export() && r.Witness.Transfers == transfers.Export()));
        var withDebt = CharterBook.Restore(book.Export()); withDebt.Register(new("liege", "realm", false), 0);
        var debtObs = withDebt.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        withDebt.Offer("player-debt", new("liege", "player", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, debtObs);
        withDebt.Sign("player-debt", false, 0, debtObs); withDebt.Sign("player-debt", true, 0, debtObs);
        var debt = new TributeLedger(); debt.Enable(withDebt, "player-debt", 0);
        var debtWitness = new NpcReparentWitness("bond-b", "a", "debt-a-b", withDebt.Export(), PoliticalWorld.Day + 1, authority, debtObs, transfers.Export(), debt.Export());
        var debtResult = archive.Append(debtWitness, withDebt);
        debtResult.Archive.ValidatePeers(transfers, debt); Check(debt.Account("player-debt").StartedTick == 0);
        var past = debtResult.Archive.PlayerPerformanceAt("debt-a-b");
        Check(past.Charter == "player-debt" && past.Performance.Due == 1 && past.Performance.Overdue == 1 && past.Performance.OutstandingValue == 100);
        Check(NpcReparentArchive.Restore(debtResult.Archive.Export()).PlayerPerformanceAt("debt-a-b") == past);
        Check(result.Archive.PlayerPerformanceAt("a-b") == null);
        Reject(() => debtResult.Archive.ValidatePeers(transfers, new TributeLedger()));
        var moved = new TerritoryTransferLedger();
        foreach (var id in new[] { "a", "b" })
        {
            var source = new TerritoryTransferObservation(id, "realm", 3);
            moved.Prepare("move-" + id, book, "bond-" + id, source, "new-realm", "new-realm", null, TerritoryTransferPurpose.ActiveCharter, 1);
            moved.Confirm("move-" + id, source with { Owner = "new-realm" }, 1);
        }
        var migratedWitness = witness with { Created = "migrated-b", Decision = authority with { Realm = "new-realm" }, Transfers = moved.Export() };
        var migrated = archive.Append(migratedWitness, book);
        migrated.Archive.ValidatePeers(moved, payments); Check(migrated.Archive.Records[0].Witness.Transfers == moved.Export());
        Reject(() => migrated.Archive.ValidatePeers(new TerritoryTransferLedger(), payments));
        return checks;
    }
}
