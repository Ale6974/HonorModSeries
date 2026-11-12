using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;
internal static class PrivateRoyalRightsTests
{
    public static int Run(bool mapped = false)
    {
        int n = 0; void Check(bool b) { n++; if (!b) throw new Exception("Private royal rights regression."); }
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
        if (mapped) residents = residents.Select((r, i) => r with { Preserved = ResidentMapped.BindRoyal(r.Preserved, new(r.Id, "map", i + 10, 0)) }).ToArray();
        var set = ResidentMoveSet.Capture(residents);
        string map = LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("same-grid"), CityStockProof.Capture(true, false, -1, []), 0, [new("wall", "Wall", "Wood", 1, 100, 1, 1, null)]);
        map = LoadedMapPreservation.CaptureAssets(map, [new("wall", "royal", Hash("original-asset"))]);
        if (mapped)
        {
            string neutral = LoadedMapPreservation.Capture("town", "map", 20, 20, Hash("same-grid"), CityStockProof.Capture(true, false, -1, []), 0, [new("wall", "Wall", "Wood", 1, 100, 1, 1, null)]);
            string civic = LoadedMapPreservation.CaptureResidents(neutral, residents.Select(r => new NativeMapResident(r.Id, r.Preserved)));
            map = LoadedMapPreservation.CaptureRoyalMapped(LoadedMapPreservation.CaptureCivicAssets(civic, [new("wall", "royal", Hash("original-asset"))], false, false));
            Check(LoadedMapPreservation.Format(map) == 22 && !civic.Contains("RoyalMappedV1"));
            ResidentMapped.Pair(ResidentMapped.Prepare(residents.Select((r, i) => new ResidentMapBinding(r.Id, "map", i + 10, 0))), set, map);
            string finalMap = map;
            Reject(() => LoadedMapPreservation.Pair(finalMap, "town", LoadedMapPreservation.Restore(finalMap).Proof, 15));
            Reject(() => LoadedMapPreservation.CaptureRoyalMapped(finalMap));
        }
        var scope = LoadedMapPreservation.Restore(map);
        var actual = new TerritoryTransferObservation("town", "royal", 2, HasInhabitants: true, ResidentProof: set.Proof);
        if (mapped) actual = actual with { HasMap = true, Visited = true, StockProof = scope.Proof, StockFormat = 22, LoadedMap = map };
        var physical = new TerritoryTransferLedger();
        string rights = RoyalRights.Capture(residents.Select(p => new RoyalRightRow(p.Id, "{\"Favor\":17,\"Title\":\"Count\",\"PermitLastUsed\":120,\"AbilityCooldown\":300}", ["grantor"], ["heir"], ["Ability_9" + p.Id])));
        string royalty = RoyalRights.Proof(rights);
        RoyalRights.Pair(rights, royalty, residents.Select(p => p.Id));
        Check(RoyalRights.Restore(rights).Rows.Length == 2 && RoyalRights.Realms(rights).SequenceEqual(new[] { "grantor" }));
        Reject(() => RoyalRights.Pair(rights, royalty, ["civilian-a"]));
        Reject(() => RoyalRights.Pair(rights, Hash("different"), residents.Select(p => p.Id)));
        foreach (string field in new[] { "17", "Count", "120", "300", "heir", "Ability_9" })
        {
            string changed = field.All(char.IsDigit) ? rights.Replace(field, "999") : rights.Replace(field, field + "changed");
            Reject(() => RoyalRights.RequireOriginal(rights, changed));
        }
        Reject(() => RoyalRights.Capture([new("a", "{}", [], [], ["Ability_1"]), new("b", "{}", [], [], ["Ability_1"])]));
        Reject(() => RoyalRights.Restore(rights + " "));
        Reject(() => RoyalRights.Capture([new("a", "{}", ["z", "a"], [], [])]));
        Reject(() => RoyalRights.Capture([new("a", "{}", [], [], []), new("a", "{}", [], [], [])]));
        var original = physical.PreparePrivateRoyalJournaled("royal-move", book, "root", actual, "target", rights, now);
        Reject(() => physical.CancelUnmoved("royal-move", actual, now));
        if (mapped) Reject(() => new TerritoryTransferLedger().PrepareRoyalJournaled("old-marker", book, "root", actual, "target", royalty, now));
        NpcRealmDecisionEvidence Realm(string id) => new(id, id + "-ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var royalRealm = Realm("royal") with { Regime = NpcDecisionRegime.Royal };
        var witness = new CustodyAgreement("royal-consent", original.Id, book.Export(), payments.Export(), physical.Export(), Hash("same-refs-census-map-royal-rights"),
            [new("child", "third", false), new("creditor", "creditor", false), new("p", "player", true), new("town", "royal", false)], observations,
            [Realm("creditor"), Realm("grantor") with { Regime = NpcDecisionRegime.Royal }, royalRealm, Realm("target"), Realm("third")], now) { RoyalCustodyV1 = true, RoyalStakeholdersV2 = ["grantor"] };
        Reject(() => CustodyAgreements.Validate(witness));
        var mandate = new ScopedNpcContractDecision("royal", "royal-ruler", NpcDecisionRegime.Royal, CustodyAgreements.Scope(witness), now, 1, 75, [], "Count", 2);
        var grantor = new ScopedNpcContractDecision("grantor", "grantor-ruler", NpcDecisionRegime.Royal, CustodyAgreements.Scope(witness), now, 1, 75, [], "Count", 2);
        witness = witness with { Regimes = [grantor, mandate] };
        Reject(() => CustodyAgreements.Validate(witness with { RoyalStakeholdersV2 = null }));
        Reject(() => CustodyAgreements.Validate(witness with { Realms = witness.Realms.Where(r => r.Realm != "grantor").ToArray() }));
        Reject(() => CustodyAgreements.Validate(witness with { Regimes = [mandate] }));
        Reject(() => CustodyAgreements.Validate(witness with { Regimes = [grantor with { Scope = Hash("another-title-action") }, mandate] }));
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
        Check(TerritoryTransferLedger.Restore(physical.Export()).Transfer(original.Id).RoyalRightsV2 == rights);
        string census = ResidentTransferCensus.Prepare("town", "royal", "target", mapped ? 13 : 12, set);
        var calls = residents.Select(p => JsonSerializer.Serialize(new ResidentAuthorityState(p.Id))).ToArray();
        var care = residents.Select(p => ResidentCareJournal.Prepare(p.Id, "royal", "target", p.MedicalCare)).ToArray();

        string assetCalls = mapped ? MapAssetTransfers.Prepare(map, "royal", "target") : null;
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
            // Save between residents without inferring any result from ownership.

        history = CustodyAgreements.Restore(history.Export()); physical = TerritoryTransferLedger.Restore(physical.Export());
            calls = JsonSerializer.Deserialize<string[]>(JsonSerializer.Serialize(calls));
            care = care.Select(c => ResidentCareJournal.Restore(c.Export())).ToArray();
            history.RequireCurrent(witness.Id, physical.Transfer(original.Id), unchangedBook, unchangedPayments, witness.Custody);
            Check(physical.Transfer(original.Id).RoyalRightsV2 == rights);
        }
        ResidentTransferCensus.Check(census, ResidentTransferCensus.Restore(census).Proof, "town", "royal", "target", mapped ? 13 : 12, set, calls, care.Select(c => c.Export()).ToArray(), true, true);


        if (mapped)
        {
            assetCalls = MapAssetTransfers.Begin(assetCalls, "wall", "royal");
            string uncertain = assetCalls;
            Reject(() => MapAssetTransfers.Begin(uncertain, "wall", "royal"));
            assetCalls = MapAssetTransfers.Returned(assetCalls, "wall", "target");
            MapAssetTransfers.Pair(assetCalls, map, "royal", "target", true);
        }
        physical.Confirm(original.Id, actual with { Owner = "target" }, now + 1);
        history.ValidateHistory(CharterBook.Restore(unchangedBook), TerritoryTransferLedger.Restore(physical.Export()), TributeLedger.Restore(unchangedPayments));
        Check(book.Export() == unchangedBook && payments.Export() == unchangedPayments && payments.IsPaid("debt", 1) && payments.Bill("debt", 2).Principal == 100);
        Check(book.Charter("lower").Phase == RecordedCharterPhase.Active && book.Charter("root").Phase == RecordedCharterPhase.Active && physical.Transfer(original.Id).RoyalResidentsV1 == royalty);
        return n;
    }
}
