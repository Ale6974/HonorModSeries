using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;
internal static class RoyalCustodyTests
{
    public static int Run()
    {
        int n = 0; void Check(bool b) { n++; if (!b) throw new Exception("Royal custody regression."); }
        void Reject(Action a) { bool rejected = false; try { a(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        var book = new CharterBook();
        foreach (var entity in new TerritoryIdentity[] { new("p", "player", true), new("town", "royal", false), new("child", "third", false), new("creditor", "creditor", false) }) book.Register(entity, 0);
        TerritoryObservation[] observations = [new("child", "third", true, false, true), new("creditor", "creditor", true, false, true), new("p", "player", true, true, true), new("town", "royal", true, false, true)];
        book.Offer("debt", new("creditor", "p", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, observations);
        book.Sign("debt", false, 1, observations); book.Sign("debt", true, 2, observations);
        var payments = new TributeLedger(); payments.Enable(book, "debt", 3);
        book.Offer("root", new("p", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 3, PoliticalWorld.Day, observations);
        book.Sign("root", true, 4, observations); book.Sign("root", false, 5, observations);
        book.OfferNpc("lower", new("town", "child", 30, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), new("royal-ruler", "third-ruler"), 6, PoliticalWorld.Day, observations);
        book.SignNpc("lower", "town", "royal-ruler", 7, observations); book.SignNpc("lower", "child", "third-ruler", 8, observations);
        TributeLine[] silver = [new("original-silver", 100, 1)];
        payments.Prepare("paid", "debt", 1, silver, PoliticalWorld.Day + 3); payments.Commit("paid", silver, PoliticalWorld.Day + 4);
        long now = PoliticalWorld.Day * 2 + 10;
        string unchangedBook = book.Export(), unchangedPayments = payments.Export();
        ResidentMoveEvidence[] residents = [new("civilian-a", Hash("original-health-gear-social-a"), 3), new("civilian-b", Hash("original-health-gear-social-b"), null)];
        var set = ResidentMoveSet.Capture(residents);
        string map = LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("same-grid"), CityStockProof.Capture(true, false, -1, []), 0, [new("wall", "Wall", "Wood", 1, 100, 1, 1, null)]);
        map = LoadedMapPreservation.CaptureAssets(map, [new("wall", "royal", Hash("original-asset"))]);
        var scope = LoadedMapPreservation.Restore(map);
        var actual = new TerritoryTransferObservation("town", "royal", 2, HasMap: true, Visited: true, HasInhabitants: true, StockProof: scope.Proof, StockFormat: 12, ResidentProof: set.Proof) { LoadedMap = map };
        var physical = new TerritoryTransferLedger();
        string royalty = Hash("original-empty-personal-royal-rights-and-tracker-flags");
        var original = physical.PrepareRoyalJournaled("royal-move", book, "root", actual, "target", royalty, now);
        Reject(() => physical.CancelUnmoved("royal-move", actual, now));
        NpcRealmDecisionEvidence Realm(string id) => new(id, id + "-ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var royalRealm = Realm("royal") with { Regime = NpcDecisionRegime.Royal };
        var witness = new CustodyAgreement("royal-consent", original.Id, book.Export(), payments.Export(), physical.Export(), Hash("same-refs-census-map-royal-rights"),
            [new("child", "third", false), new("creditor", "creditor", false), new("p", "player", true), new("town", "royal", false)], observations,
            [Realm("creditor"), royalRealm, Realm("target"), Realm("third")], now) { RoyalCustodyV1 = true };
        Reject(() => CustodyAgreements.Validate(witness));
        var mandate = new ScopedNpcContractDecision("royal", "royal-ruler", NpcDecisionRegime.Royal, CustodyAgreements.Scope(witness), now, 1, 75, [], "Count", 2);
        witness = witness with { Regimes = [mandate] };
        Check(CustodyAgreements.Validate(witness) == original && original.RoyalResidentsV1 == royalty);
        Reject(() => CustodyAgreements.Validate(witness with { RoyalCustodyV1 = false }));
        Reject(() => CustodyAgreements.Validate(witness with { Regimes = [mandate with { RoyalTitle = null }] }));
        Reject(() => CustodyAgreements.Validate(witness with { Regimes = [mandate with { Seniority = 0 }] }));
        Reject(() => CustodyAgreements.Validate(witness with { Regimes = [mandate with { Representative = "retired-ruler" }] }));
        Reject(() => CustodyAgreements.Validate(witness with { Regimes = [mandate with { Scope = Hash("roster-enrollment-only") }] }));
        Reject(() => CustodyAgreements.Validate(witness with { Parties = witness.Parties.Skip(1).ToArray() }));
        Reject(() => CustodyAgreements.Validate(witness with { Realms = [Realm("creditor"), royalRealm, Realm("target"), Realm("third") with { Willing = false }] }));
        Reject(() => new TerritoryTransferLedger().PrepareRoyalJournaled("last-base", book, "root", actual with { SourceSettlements = 1 }, "target", royalty, now));
        var history = new CustodyAgreements(); history.Add(witness);
        string census = ResidentTransferCensus.Prepare("town", "royal", "target", 7, set);
        var calls = residents.Select(p => JsonSerializer.Serialize(new ResidentAuthorityState(p.Id))).ToArray();
        var care = residents.Select(p => ResidentCareJournal.Prepare(p.Id, "royal", "target", p.MedicalCare)).ToArray();
        string assetCalls = MapAssetTransfers.Prepare(map, "royal", "target");
        history = CustodyAgreements.Restore(history.Export()); physical = TerritoryTransferLedger.Restore(physical.Export());
        history.RequireCurrent(witness.Id, physical.Transfer(original.Id), unchangedBook, unchangedPayments, witness.Custody);
        for (int i = 0; i < residents.Length; i++)
        {
            calls[i] = ResidentTransferCensus.Begin(calls[i]); care[i].Begin("royal", residents[i].MedicalCare);
            string unknown = calls[i]; Reject(() => ResidentTransferCensus.Begin(unknown));
            calls[i] = ResidentTransferCensus.Returned(calls[i]);
            int? reset = residents[i].MedicalCare.HasValue ? 1 : null;
            care[i].RecordReturn("target", reset);
            if (care[i].MayRestore("target", reset)) care[i].Consume("target", reset); else care[i].SealUnchanged("target", reset);
        }
        ResidentTransferCensus.Check(census, ResidentTransferCensus.Restore(census).Proof, "town", "royal", "target", 7, set, calls, care.Select(c => c.Export()).ToArray(), true, true);
        assetCalls = MapAssetTransfers.Returned(MapAssetTransfers.Begin(assetCalls, "wall", "royal"), "wall", "target");
        MapAssetTransfers.Pair(assetCalls, map, "royal", "target", true);
        physical.Confirm(original.Id, actual with { Owner = "target" }, now + 1);
        history.ValidateHistory(CharterBook.Restore(unchangedBook), TerritoryTransferLedger.Restore(physical.Export()), TributeLedger.Restore(unchangedPayments));
        Check(book.Export() == unchangedBook && payments.Export() == unchangedPayments && payments.IsPaid("debt", 1) && payments.Bill("debt", 2).Principal == 100);
        Check(book.Charter("lower").Phase == RecordedCharterPhase.Active && book.Charter("root").Phase == RecordedCharterPhase.Active && physical.Transfer(original.Id).RoyalResidentsV1 == royalty);
        // A subsequent foreign destination may itself be royal. Keep the first royal leg immutable.
        string originalApplied = JsonSerializer.Serialize(physical.Transfer(original.Id));
        var nextBook = CharterBook.Restore(unchangedBook);
        nextBook.ConsentToRelease("root", true, now + 2, observations); nextBook.ConsentToRelease("root", false, now + 3, observations);
        nextBook.Register(new("new-master", "royal2", false), now + 3);
        var nextObservations = observations.Append(new TerritoryObservation("new-master", "royal2", true, false, true)).ToArray();
        nextBook.OfferNpc("next-root", new("new-master", "town", 60, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), new("royal2-ruler", "target-ruler"), now + 4, PoliticalWorld.Day, nextObservations);
        nextBook.SignNpc("next-root", "new-master", "royal2-ruler", now + 5, nextObservations); nextBook.SignNpc("next-root", "town", "target-ruler", now + 6, nextObservations);
        var nextMap = LoadedMapPreservation.CaptureAssets(LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("same-grid"), CityStockProof.Capture(true, false, -1, []), 0,
            [new("wall", "Wall", "Wood", 1, 100, 1, 1, null)]), [new("wall", "target", Hash("original-asset"))]);
        var nextActual = actual with { Owner = "target", LoadedMap = nextMap, StockProof = LoadedMapPreservation.Restore(nextMap).Proof };
        var nextTransfer = physical.PrepareForeignContinuation("royal-followup", original.Id, nextBook, "next-root", nextActual, "royal2", now + 7, royalResidents: royalty);
        var nextWitness = new CustodyAgreement("royal-followup-consent", nextTransfer.Id, nextBook.Export(), unchangedPayments, physical.Export(), Hash("fresh-followup-royal-refs"),
            [new("child", "third", false), new("new-master", "royal2", false), new("town", "target", false)], [observations[0], nextObservations.Last(), observations[3]],
            [Realm("royal2") with { Regime = NpcDecisionRegime.Royal }, Realm("target"), Realm("third")], now + 7) { RoyalCustodyV1 = true };
        nextWitness = nextWitness with { ForeignRightsV1 = CustodyAgreements.CaptureForeignRights(nextBook, nextTransfer, nextWitness.Parties) };
        nextWitness = nextWitness with { Regimes = [new("royal2", "royal2-ruler", NpcDecisionRegime.Royal, CustodyAgreements.Scope(nextWitness), now + 7, 1, 75, [], "Duke", 3)] };
        Check(CustodyAgreements.Validate(nextWitness) == nextTransfer);
        Reject(() => CustodyAgreements.Validate(nextWitness with { Regimes = [mandate] }));
        history.Add(nextWitness); history = CustodyAgreements.Restore(history.Export()); physical = TerritoryTransferLedger.Restore(physical.Export());
        history.RequireCurrent(nextWitness.Id, physical.Transfer(nextTransfer.Id), nextBook.Export(), unchangedPayments, nextWitness.Custody);
        Reject(() => history.RequireCurrent(nextWitness.Id, nextTransfer with { RoyalResidentsV1 = Hash("new-personal-rights") }, nextBook.Export(), unchangedPayments, nextWitness.Custody));
        physical.Confirm(nextTransfer.Id, nextActual with { Owner = "royal2" }, now + 8);
        history.ValidateHistory(nextBook, TerritoryTransferLedger.Restore(physical.Export()), payments);
        Check(JsonSerializer.Serialize(physical.Transfer(original.Id)) == originalApplied && physical.Transfer(nextTransfer.Id).PreviousTransferV1 == original.Id && payments.Export() == unchangedPayments);
        // Old physical records cannot acquire royal execution authority by adding a later mandate.
        var legacy = new TerritoryTransferLedger(); legacy.PrepareLoadedJournaled("legacy", book, "root", actual, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, now);
        var expanded = witness with { Transfer = "legacy", Physical = legacy.Export(), Regimes = null };
        expanded = expanded with { Regimes = [mandate with { Scope = CustodyAgreements.Scope(expanded) }] };
        Reject(() => CustodyAgreements.Validate(expanded));
        return n;
    }
}
