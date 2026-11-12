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
        // Previous .97 documents/proofs remain byte-identical with no new permission field.
        string legacy = JsonSerializer.Serialize(new { witness.Id, witness.Transfer, witness.Book, witness.Payments, witness.Physical, witness.Custody, witness.Parties, witness.Observations, witness.Realms, witness.Tick, witness.Regimes });
        Check(JsonSerializer.Serialize(witness) == legacy && !legacy.Contains("ForeignRightsV1"));
        string legacyScope = JsonSerializer.Serialize(new { Action = "CurrentCustodyCompletion/v1", witness.Id, witness.Transfer, witness.Book, witness.Payments, witness.Physical, witness.Custody, witness.Parties, witness.Observations, witness.Realms, witness.Tick });
        Check(CustodyAgreements.Scope(witness) == Hash(legacyScope));
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
        // A new NPC master signs its own contract after the former root ended.
        // Its current realm must be the immutable old physical destination; all lower rights remain intact.
        var foreignBook = CharterBook.Restore(endedBook);
        foreignBook.Register(new("master", "target", false), now + 3);
        var foreignObs = observations.Append(new TerritoryObservation("master", "target", true, false, true)).ToArray();
        foreignBook.OfferNpc("foreign-root", new("master", "town", 45, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), new("target-ruler", "source-ruler"), now + 4, PoliticalWorld.Day, foreignObs);
        foreignBook.SignNpc("foreign-root", "master", "target-ruler", now + 5, foreignObs);
        foreignBook.SignNpc("foreign-root", "town", "source-ruler", now + 6, foreignObs);
        string originalForeignBook = foreignBook.Export();
        var foreignWitness = witness with { Id = "foreign-current", Book = originalForeignBook, Tick = now + 7,
            Parties = [new("child", "third", false), new("master", "target", false), new("p", "player", true), new("town", "source", false)],
            Observations = [observations[2], foreignObs.Last(), observations[0], observations[1]] };
        Reject(() => CustodyAgreements.Validate(foreignWitness)); // No implicit expansion of old/null permission.
        foreignWitness = foreignWitness with { ForeignRightsV1 = CustodyAgreements.CaptureForeignRights(foreignBook, original, foreignWitness.Parties) };
        Check(CustodyAgreements.Validate(foreignWitness) == original);
        Check(CustodyAgreements.Scope(foreignWitness) != CustodyAgreements.Scope(foreignWitness with { ForeignRightsV1 = null }));
        Reject(() => CustodyAgreements.Validate(foreignWitness with { ForeignRightsV1 = foreignWitness.ForeignRightsV1 + " " }));
        Reject(() => CustodyAgreements.Validate(foreignWitness with { Realms = [Realm("source"), Realm("target") with { Willing = false }, Realm("third")] }));
        Reject(() => CustodyAgreements.Validate(foreignWitness with { Parties = foreignWitness.Parties.Where(p => p.Party != "master").ToArray() }));
        Reject(() => CustodyAgreements.CaptureForeignRights(foreignBook, original with { TargetOwner = "other" }, foreignWitness.Parties));
        var foreignUnion = foreignWitness with { Id = "foreign-union", Realms = [Realm("source"), Realm("target") with { Regime = NpcDecisionRegime.Union }, Realm("third")] };
        foreignUnion = foreignUnion with { Regimes = [new("target", "target-ruler", NpcDecisionRegime.Union, CustodyAgreements.Scope(foreignUnion), foreignUnion.Tick, 1, 75, [new("master-seat", true)])] };
        Check(CustodyAgreements.Validate(foreignUnion) == original);
        Reject(() => CustodyAgreements.Validate(foreignUnion with { Regimes = [foreignUnion.Regimes[0] with { Scope = CustodyAgreements.Scope(foreignUnion with { ForeignRightsV1 = null }) }] }));
        var foreignRoyal = foreignWitness with { Id = "foreign-royal", Realms = [Realm("source"), Realm("target") with { Regime = NpcDecisionRegime.Royal }, Realm("third")] };
        foreignRoyal = foreignRoyal with { Regimes = [new("target", "target-ruler", NpcDecisionRegime.Royal, CustodyAgreements.Scope(foreignRoyal), foreignRoyal.Tick, 1, 75, [], "Count", 2)] };
        Check(CustodyAgreements.Validate(foreignRoyal) == original);
        Reject(() => CustodyAgreements.Validate(foreignRoyal with { Regimes = [foreignRoyal.Regimes[0] with { Representative = "dead-ruler" }] }));
        var wrongForeign = foreignWitness with { ForeignRightsV1 = JsonSerializer.Serialize(JsonSerializer.Deserialize<CustodyForeignRight>(foreignWitness.ForeignRightsV1) with { OriginalContract = Hash("unrelated-contract") }) };
        Reject(() => CustodyAgreements.Validate(wrongForeign));
        var foreignArchive = new CustodyAgreements(); foreignArchive.Add(foreignWitness);
        string savedForeign = foreignArchive.Export(); foreignArchive = CustodyAgreements.Restore(savedForeign);
        foreignArchive.RequireCurrent("foreign-current", original, originalForeignBook, paidBefore, witness.Custody);
        var interruptedPhysical = TerritoryTransferLedger.Restore(physical.Export());
        foreignArchive.ValidateHistory(foreignBook, interruptedPhysical, payments);
        interruptedPhysical.Confirm("move", actual with { Owner = "target" }, now + 8);
        foreignArchive.ValidateHistory(foreignBook, TerritoryTransferLedger.Restore(interruptedPhysical.Export()), TributeLedger.Restore(paidBefore));
        Check(foreignBook.Export() == originalForeignBook && foreignBook.Charter("root").Phase == RecordedCharterPhase.Released &&
            foreignBook.Charter("foreign-root").Phase == RecordedCharterPhase.Active && foreignBook.Charter("lower").Phase == RecordedCharterPhase.Active);
        Check(payments.Export() == paidBefore && payments.Bill("debt", 2).Principal == 100 && payments.IsPaid("debt", 1));
        Reject(() => foreignArchive.RequireCurrent("foreign-current", interruptedPhysical.Transfer("move"), originalForeignBook, paidBefore, witness.Custody));
        // A fourth-level current higher party keeps its own rights and realm consent.
        var higherBook = CharterBook.Restore(originalForeignBook);
        higherBook.Register(new("overlord", "target", false), now + 8);
        var higherObs = foreignObs.Append(new TerritoryObservation("overlord", "target", true, false, true)).ToArray();
        higherBook.OfferNpc("higher-root", new("overlord", "master", 15, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), new("target-ruler", "target-ruler"), now + 9, PoliticalWorld.Day, higherObs);
        higherBook.SignNpc("higher-root", "overlord", "target-ruler", now + 10, higherObs);
        higherBook.SignNpc("higher-root", "master", "target-ruler", now + 11, higherObs);
        var higherWitness = foreignWitness with { Id = "higher-consent", Book = higherBook.Export(), Tick = now + 12,
            Parties = [new("child", "third", false), new("master", "target", false), new("overlord", "target", false), new("p", "player", true), new("town", "source", false)],
            Observations = [observations[2], foreignObs.Last(), higherObs.Last(), observations[0], observations[1]] };
        higherWitness = higherWitness with { ForeignRightsV1 = CustodyAgreements.CaptureForeignRights(higherBook, original, higherWitness.Parties) };
        Check(CustodyAgreements.Validate(higherWitness) == original);
        Reject(() => CustodyAgreements.Validate(higherWitness with { Parties = higherWitness.Parties.Where(p => p.Party != "overlord").ToArray() }));
        Reject(() => CustodyAgreements.CaptureForeignRights(higherBook, original, higherWitness.Parties.Select(p => p.Party == "overlord" ? p with { Realm = "third" } : p).ToArray()));
        // Different destination: explicitly approve finishing the first leg as transit, then append a fresh second leg.
        var nextBook = CharterBook.Restore(endedBook); nextBook.Register(new("next-master", "final", false), now + 3);
        var nextObs = observations.Append(new TerritoryObservation("next-master", "final", true, false, true)).ToArray();
        nextBook.OfferNpc("next-root", new("next-master", "town", 55, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), new("final-ruler", "source-ruler"), now + 4, PoliticalWorld.Day, nextObs);
        nextBook.SignNpc("next-root", "next-master", "final-ruler", now + 5, nextObs); nextBook.SignNpc("next-root", "town", "source-ruler", now + 6, nextObs);
        var transit = witness with { Id = "transit", Book = nextBook.Export(), Tick = now + 7,
            Parties = [new("child", "third", false), new("next-master", "final", false), new("p", "player", true), new("town", "source", false)],
            Observations = [observations[2], nextObs.Last(), observations[0], observations[1]], Realms = [Realm("final"), Realm("source"), Realm("target"), Realm("third")] };
        Reject(() => CustodyAgreements.CaptureForeignRights(nextBook, original, transit.Parties));
        transit = transit with { TransitDestinationV1 = "final" };
        transit = transit with { ForeignRightsV1 = CustodyAgreements.CaptureForeignRights(nextBook, original, transit.Parties, transit.TransitDestinationV1) };
        Check(CustodyAgreements.Validate(transit) == original);
        Reject(() => CustodyAgreements.Validate(transit with { TransitDestinationV1 = null }));
        Reject(() => CustodyAgreements.Validate(transit with { Realms = transit.Realms.Where(r => r.Realm != "target").ToArray() }));
        var twoLegs = TerritoryTransferLedger.Restore(physical.Export()); var transitHistory = new CustodyAgreements(); transitHistory.Add(transit);
        Reject(() => twoLegs.PrepareForeignContinuation("too-early", "move", nextBook, "next-root", actual, "final", now + 8));
        twoLegs.Confirm("move", actual with { Owner = "target" }, now + 8);
        string firstCompleted = JsonSerializer.Serialize(twoLegs.Transfer("move"));
        string nextMap = LoadedMapPreservation.CaptureAssets(rootMap, [new("asset-a", "target", Hash("a")), new("asset-b", "target", Hash("b"))]);
        var nextScope = LoadedMapPreservation.Restore(nextMap);
        var nextActual = actual with { Owner = "target", LoadedMap = nextMap, StockProof = nextScope.Proof, ResidentProof = Hash("fresh-clinical-after-positive-first-leg") };
        var nextTransfer = twoLegs.PrepareForeignContinuation("next-move", "move", nextBook, "next-root", nextActual, "final", now + 9);
        var nextWitness = transit with { Id = "next-consent", Transfer = "next-move", Tick = now + 9, Physical = twoLegs.Export(), Custody = Hash("fresh-original-second-leg-references"), TransitDestinationV1 = null,
            Parties = [new("child", "third", false), new("next-master", "final", false), new("town", "target", false)],
            Observations = [observations[2], nextObs.Last(), observations[1]], Realms = [Realm("final"), Realm("target"), Realm("third")] };
        nextWitness = nextWitness with { ForeignRightsV1 = CustodyAgreements.CaptureForeignRights(nextBook, nextTransfer, nextWitness.Parties) };
        Check(CustodyAgreements.Validate(nextWitness) == nextTransfer);
        transitHistory.Add(nextWitness); string chainSaved = transitHistory.Export();
        twoLegs = TerritoryTransferLedger.Restore(twoLegs.Export()); transitHistory = CustodyAgreements.Restore(chainSaved);
        Reject(() => TerritoryTransferLedger.Restore(twoLegs.Export().Replace("\"PreviousTransferV1\":\"move\"", "\"PreviousTransferV1\":\"next-move\"")));
        transitHistory.RequireCurrent("next-consent", twoLegs.Transfer("next-move"), nextBook.Export(), paidBefore, nextWitness.Custody);
        string nextCalls = MapAssetTransfers.Prepare(nextMap, "target", "final");
        nextCalls = MapAssetTransfers.Returned(MapAssetTransfers.Begin(nextCalls, "asset-a", "target"), "asset-a", "final");
        Check(MapAssetTransfers.ExpectedOwner(nextCalls, "asset-b") == "target");
        nextCalls = MapAssetTransfers.Returned(MapAssetTransfers.Begin(nextCalls, "asset-b", "target"), "asset-b", "final");
        MapAssetTransfers.Pair(nextCalls, nextMap, "target", "final", true);
        twoLegs.Confirm("next-move", nextActual with { Owner = "final" }, now + 10);
        transitHistory.ValidateHistory(nextBook, TerritoryTransferLedger.Restore(twoLegs.Export()), payments);
        Check(JsonSerializer.Serialize(twoLegs.Transfer("move")) == firstCompleted && twoLegs.Transfer("next-move").PreviousTransferV1 == "move");
        Check(nextBook.Charter("next-root").Phase == RecordedCharterPhase.Active && nextBook.Charter("lower").Phase == RecordedCharterPhase.Active && payments.Export() == paidBefore);
        // Revocation/change needs a new complete approval; historical normal-return facts stay untouched.
        foreignBook.ConsentNpcRelease("foreign-root", "master", now + 9, foreignObs);
        foreignBook.ConsentNpcRelease("foreign-root", "town", now + 10, foreignObs);
        Reject(() => foreignArchive.RequireCurrent("foreign-current", original, foreignBook.Export(), paidBefore, witness.Custody));
        foreignArchive.ValidateHistory(foreignBook, interruptedPhysical, payments);
        Check(CustodyAgreements.Restore(savedForeign).Export() == savedForeign);
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
