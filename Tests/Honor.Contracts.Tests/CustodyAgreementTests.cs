using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;
internal static class CustodyAgreementTests
{
    public static int Run()
    {
        int n = 0; void Check(bool b) { n++; if (!b) throw new Exception("Current custody authority regression."); }
        void Reject(Action a) { bool bad = false; try { a(); } catch (ArgumentException) { bad = true; } Check(bad); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("town", "source", false), 0); book.Register(new("child", "third", false), 0); book.Register(new("creditor", "creditor", false), 0);
        TerritoryObservation[] observations = [new("p", "player", true, true, true), new("town", "source", true, false, true), new("child", "third", true, false, true), new("creditor", "creditor", true, false, true)];
        book.Offer("debt", new("creditor", "p", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, observations);
        book.Sign("debt", false, 1, observations); book.Sign("debt", true, 2, observations);
        var payments = new TributeLedger(); payments.Enable(book, "debt", 3);
        book.Offer("root", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 3, PoliticalWorld.Day, observations);
        book.Sign("root", true, 4, observations); book.Sign("root", false, 5, observations);
        book.OfferNpc("lower", new("town", "child", 30, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), new("source-ruler", "third-ruler"), 6, PoliticalWorld.Day, observations);
        book.SignNpc("lower", "town", "source-ruler", 7, observations); book.SignNpc("lower", "child", "third-ruler", 8, observations);
        TributeLine[] delivery = [new("silver-stack", 100, 1)];
        payments.Prepare("paid", "debt", 1, delivery, PoliticalWorld.Day + 3); payments.Commit("paid", delivery, PoliticalWorld.Day + 4);
        long now = PoliticalWorld.Day * 2 + 10;
        book.ConsentToRelease("debt", true, now - 5, observations); book.ConsentToRelease("debt", false, now - 4, observations); payments.SynchronizeClosure(book, "debt", now - 4);
        string rootMap = LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("grid"), CityStockProof.Capture(true, false, -1, []), 0,
            [new("asset-a", "Wall", "Wood", 1, 100, 0, 0, null), new("asset-b", "Wall", "Steel", 1, 100, 1, 0, null)]);
        string map = LoadedMapPreservation.CaptureAssets(rootMap, [new("asset-a", "source", Hash("a")), new("asset-b", "source", Hash("b"))]);
        var loaded = LoadedMapPreservation.Restore(map); var physical = new TerritoryTransferLedger();
        var actual = new TerritoryTransferObservation("town", "source", 2, HasMap: true, Visited: true, HasInhabitants: true, StockProof: loaded.Proof, StockFormat: 12, ResidentProof: Hash("original-clinical-census")) { LoadedMap = map };
        var original = physical.PrepareLoadedJournaled("move", book, "root", actual, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, now);
        string calls = MapAssetTransfers.Prepare(map, "source", "target");
        calls = MapAssetTransfers.Returned(MapAssetTransfers.Begin(calls, "asset-a", "source"), "asset-a", "target");
        book.ConsentToRelease("root", true, now + 1, observations); book.ConsentToRelease("root", false, now + 2, observations);
        string endedBook = book.Export(), paidBefore = payments.Export();
        NpcRealmDecisionEvidence Realm(string id) => new(id, id + "-ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var witness = new CustodyAgreement("consent", "move", endedBook, paidBefore, physical.Export(), Hash("same-original-references-and-census"),
            [new("child", "third", false), new("p", "player", true), new("town", "source", false)],
            [observations[2], observations[0], observations[1]], [Realm("source"), Realm("target"), Realm("third")], now + 3);
        Check(CustodyAgreements.PartiesFor(book, original).SequenceEqual(["child", "p", "town"]));
        Check(CustodyAgreements.Validate(witness) == original);
        var agreements = new CustodyAgreements(); agreements.Add(witness);
        string archive = agreements.Export(); agreements = CustodyAgreements.Restore(archive);
        agreements.RequireCurrent("consent", original, book.Export(), payments.Export(), witness.Custody);
        Check(book.Export() == endedBook && payments.Export() == paidBefore && book.Charter("root").Phase == RecordedCharterPhase.Released && book.Charter("lower").Phase == RecordedCharterPhase.Active);
        Check(!payments.IsPaid("debt", 2) && payments.Bill("debt", 2).Principal == 100 && payments.IsPaid("debt", 1));
        Reject(() => CustodyAgreements.Validate(witness with { Parties = witness.Parties.Skip(1).ToArray() }));
        Reject(() => CustodyAgreements.Validate(witness with { Realms = witness.Realms.Take(2).ToArray() }));
        Reject(() => CustodyAgreements.Validate(witness with { Realms = [Realm("source"), Realm("target"), Realm("third") with { Willing = false }] }));
        Reject(() => CustodyAgreements.Validate(witness with { Parties = [witness.Parties[0] with { Realm = "unproven" }, witness.Parties[1], witness.Parties[2]] }));
        Reject(() => CustodyAgreements.Validate(witness with { Observations = [observations[2] with { Exists = false }, observations[0], observations[1]] }));
        Reject(() => agreements.RequireCurrent("consent", original, endedBook, paidBefore, Hash("changed-clinical")));
        Reject(() => agreements.RequireCurrent("consent", original with { TargetOwner = "other" }, endedBook, paidBefore, witness.Custody));
        Reject(() => agreements.Add(witness));
        var changed = CharterBook.Restore(endedBook); changed.Register(new("unrelated", "realm", false), now + 4);
        Reject(() => agreements.RequireCurrent("consent", original, changed.Export(), paidBefore, witness.Custody));
        // Scope-specific union and royal approvals are required; old unrelated votes are never substituted.
        var union = witness with { Id = "union-consent", Realms = [Realm("source"), Realm("target"), Realm("third") with { Regime = NpcDecisionRegime.Union }] };
        Reject(() => CustodyAgreements.Validate(union));
        union = union with { Regimes = [new("third", "third-ruler", NpcDecisionRegime.Union, CustodyAgreements.Scope(union), union.Tick, 1, 75, [new("seat-a", true), new("seat-b", true)])] };
        _ = CustodyAgreements.Validate(union); Check(true);
        Reject(() => CustodyAgreements.Validate(union with { Regimes = [union.Regimes[0] with { Scope = Hash("old-release-vote") }] }));
        Reject(() => CustodyAgreements.Validate(union with { Regimes = [union.Regimes[0] with { Representative = "former-ruler" }] }));
        var royal = witness with { Id = "royal-consent", Realms = [Realm("source"), Realm("target"), Realm("third") with { Regime = NpcDecisionRegime.Royal }] };
        royal = royal with { Regimes = [new("third", "third-ruler", NpcDecisionRegime.Royal, CustodyAgreements.Scope(royal), royal.Tick, 1, 75, [], "Count", 2)] };
        _ = CustodyAgreements.Validate(royal); Check(true);
        Reject(() => CustodyAgreements.Validate(royal with { Regimes = [royal.Regimes[0] with { RoyalTitle = null }] }));
        // Continue only the old positive asset prefix. Consent cannot replay an uncertain call or change its scope.
        Check(MapAssetTransfers.ExpectedOwner(calls, "asset-a") == "target" && MapAssetTransfers.ExpectedOwner(calls, "asset-b") == "source");
        string unknown = MapAssetTransfers.Begin(calls, "asset-b", "source");
        Reject(() => MapAssetTransfers.ExpectedOwner(unknown, "asset-b"));
        calls = MapAssetTransfers.Returned(unknown, "asset-b", "target"); MapAssetTransfers.Pair(calls, map, "source", "target", true);
        physical.Confirm("move", actual with { Owner = "target" }, now + 4);
        agreements = CustodyAgreements.Restore(archive); agreements.ValidateHistory(book, TerritoryTransferLedger.Restore(physical.Export()), TributeLedger.Restore(payments.Export()));
        Check(book.Export() == endedBook && payments.Export() == paidBefore && physical.Transfer("move").Charter == "root");
        Reject(() => agreements.RequireCurrent("consent", physical.Transfer("move"), endedBook, paidBefore, witness.Custody));
        var independent = agreements.Records[0]; independent.Witness.Parties[0] = new("hijacked", "third", false);
        Check(agreements.Export() == archive);
        var tampered = JsonSerializer.Deserialize<CustodyAgreementSave>(archive);
        tampered.Records[0] = tampered.Records[0] with { Witness = tampered.Records[0].Witness with { Custody = Hash("forged-original") } };
        Reject(() => CustodyAgreements.Restore(JsonSerializer.Serialize(tampered)));
        return n;
    }
}
