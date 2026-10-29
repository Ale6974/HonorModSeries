using Honor.Contracts.Rules;
using System.Text.Json;

internal static class MixedRealmReparentTests
{
    public static int Run()
    {
        int n = 0;
        void Check(bool b) { n++; if (!b) throw new Exception("Mixed actual realm reparent regression."); }
        void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        var book = new CharterBook();
        book.Register(new("player", "native-player", true), 0);
        foreach (var id in new[] { "a", "b", "c", "d", "e", "creditor" }) book.Register(new(id, "realm", false), 0);
        TerritoryObservation[] Observe() => book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string liege, string subject) => new(liege, subject, 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0);
        foreach (var id in new[] { "a", "b", "c", "d", "e" })
        {
            book.Offer("bond-" + id, Terms("player", id), 0, PoliticalWorld.Day, Observe());
            book.Sign("bond-" + id, true, 0, Observe()); book.Sign("bond-" + id, false, 0, Observe());
        }
        book.Offer("debt", Terms("creditor", "player"), 0, PoliticalWorld.Day, Observe());
        book.Sign("debt", false, 0, Observe()); book.Sign("debt", true, 0, Observe());
        var payments = new TributeLedger(); payments.Enable(book, "debt", 0);
        long now = PoliticalWorld.Day + 1;
        book.ConsentToRelease("debt", true, now, Observe()); book.ConsentToRelease("debt", false, now, Observe());
        payments.SynchronizeClosure(book, "debt", now);
        var archive = new NpcReparentArchive(); var transfers = new TerritoryTransferLedger();
        var authority = new NpcRealmDecisionEvidence("realm", "ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        foreach (var id in new[] { "c", "d" })
        {
            var result = archive.Append(new("bond-" + id, "a", "a-" + id, book.Export(), ++now, authority, Observe(), transfers.Export(), payments.Export()), book);
            book = result.Book; archive = result.Archive;
        }
        foreach (var id in new[] { "c", "d" })
        {
            var source = new TerritoryTransferObservation(id, "realm", 3);
            var target = "foreign-" + id;
            transfers.Prepare("move-" + id, book, "a-" + id, source, target, target, null, TerritoryTransferPurpose.ActiveCharter, ++now);
            transfers.Confirm("move-" + id, source with { Owner = target }, now);
        }
        var retained = new[] { "c", "d" }.Select(id => new RetainedRealmConsent(id, "foreign-" + id,
            authority with { Realm = "foreign-" + id, Representative = "ruler-" + id }, true)).ToArray();
        var witness = new NpcReparentWitness("bond-a", "b", "b-a", book.Export(), ++now, authority, Observe(), transfers.Export(), payments.Export()) { RetainedRealms = retained };
        var original = book.Export(); var originalTransfers = transfers.Export(); var originalPayments = payments.Export();
        var changed = archive.Append(witness, book);
        Check(book.Export() == original && transfers.Export() == originalTransfers && payments.Export() == originalPayments);
        Check(changed.Book.Charter("a-c") == book.Charter("a-c") && changed.Book.Charter("a-d") == book.Charter("a-d"));
        Check(changed.Book.Charter("debt") == book.Charter("debt") && payments.Account("debt").StartedTick == 0 && payments.Bill("debt", 1).RequiredValue == 100);
        Check(changed.Book.HierarchyPath("c").SequenceEqual(new[] { "player", "b", "a", "c" }));
        Check(changed.Book.Charter("bond-a").Phase == RecordedCharterPhase.Released && changed.Book.Charter("b-a").Reparent.Previous == "bond-a");
        var restored = NpcReparentArchive.Restore(changed.Archive.Export());
        restored.ValidateBook(CharterBook.Restore(changed.Book.Export())); restored.ValidatePeers(transfers, payments);
        Check(restored.Records.Last().Witness.RetainedRealms.Length == 2);
        var beforeMutation = restored.Export(); restored.Records.Last().Witness.RetainedRealms[0] = retained[0] with { ActualOwner = "wrong" };
        Check(restored.Export() == beforeMutation);
        Reject(() => archive.Append(witness with { RetainedRealms = null }, book));
        Reject(() => archive.Append(witness with { RetainedRealms = retained.Take(1).ToArray() }, book));
        Reject(() => archive.Append(witness with { RetainedRealms = new[] { retained[0], retained[0] } }, book));
        Reject(() => archive.Append(witness with { RetainedRealms = new[] { retained[0] with { ReciprocalPeace = false }, retained[1] } }, book));
        Reject(() => archive.Append(witness with { RetainedRealms = new[] { retained[0] with { ActualOwner = "unrecorded" }, retained[1] } }, book));
        Reject(() => archive.Append(witness with { RetainedRealms = new[] { retained[0] with { Decision = retained[0].Decision with { Regime = NpcDecisionRegime.Union } }, retained[1] } }, book));
        Reject(() => archive.Append(witness with { RetainedRealms = retained.Append(new("b", "realm", authority, true)).ToArray() }, book));
        Reject(() => archive.Append(witness with { Transfers = new TerritoryTransferLedger().Export() }, book));
        Reject(() => restored.ValidatePeers(transfers, new TributeLedger()));
        var saved = JsonSerializer.Deserialize<CharterBookSave>(changed.Book.Export());
        Reject(() => restored.ValidateBook(CharterBook.Restore(JsonSerializer.Serialize(saved with { Charters = saved.Charters.Select(c => c.Id == "a-c" ? c with { Terms = c.Terms with { TributeValue = 999 } } : c).ToArray() }))));
        Check(!archive.Records[0].Witness.Before.Contains("RetainedRealms") && !archive.Export().Contains("RetainedRealms"));
        var incomingTransfers = TerritoryTransferLedger.Restore(transfers.Export());
        var rootSource = new TerritoryTransferObservation("a", "realm", 3);
        incomingTransfers.Prepare("move-a", book, "bond-a", rootSource, "foreign-a", "foreign-a", null, TerritoryTransferPurpose.ActiveCharter, ++now);
        incomingTransfers.Confirm("move-a", rootSource with { Owner = "foreign-a" }, now);
        var sourceDecision = authority with { Realm = "foreign-a", Representative = "actual-ruler-a" };
        var parties = new[] { new RetainedRealmConsent("a", "foreign-a", sourceDecision, true), new RetainedRealmConsent("b", "realm", authority, true) };
        var incoming = witness with { Created = "cross-b-a", Tick = ++now, Transfers = incomingTransfers.Export(), Parties = parties };
        var cross = archive.Append(incoming, book);
        Check(cross.Book.Charter("cross-b-a").NpcAuthority == new NpcCharterAuthority("ruler", "actual-ruler-a"));
        Check(cross.Book.Charter("a-c") == book.Charter("a-c") && cross.Book.Charter("a-d") == book.Charter("a-d") && cross.Book.Charter("debt") == book.Charter("debt"));
        var crossRestored = NpcReparentArchive.Restore(cross.Archive.Export()); crossRestored.ValidateBook(CharterBook.Restore(cross.Book.Export())); crossRestored.ValidatePeers(incomingTransfers, payments);
        Check(crossRestored.Records.Last().Witness.Parties.Length == 2 && crossRestored.Records.Last().Witness.RetainedRealms.Length == 2);
        var partyPlan = NpcSubcharters.ReviewWithParties(book, "bond-a", "b", "cross-b-a", authority, parties, now, Observe());
        Check(NpcSubcharters.CommitWithParties(book, partyPlan, authority, parties, now, Observe()).Export() == partyPlan.After);
        Reject(() => NpcSubcharters.CommitAuthorized(book, partyPlan, authority, now, Observe()));
        Reject(() => archive.Append(incoming with { Parties = null }, book));
        Reject(() => archive.Append(incoming with { Parties = parties.Take(1).ToArray() }, book));
        Reject(() => archive.Append(incoming with { Parties = new[] { parties[0], parties[0] } }, book));
        Reject(() => archive.Append(incoming with { Parties = new[] { parties[0] with { ReciprocalPeace = false }, parties[1] } }, book));
        Reject(() => archive.Append(incoming with { Parties = new[] { parties[0] with { ActualOwner = "unrecorded" }, parties[1] } }, book));
        Reject(() => archive.Append(incoming with { Parties = new[] { parties[0] with { Decision = sourceDecision with { Regime = NpcDecisionRegime.Royal } }, parties[1] } }, book));
        Reject(() => archive.Append(incoming with { RetainedRealms = null }, book));
        var originalCross = crossRestored.Export(); crossRestored.Records.Last().Witness.Parties[0] = parties[1]; Check(crossRestored.Export() == originalCross);
        Check(book.Export() == original && payments.Export() == originalPayments && !archive.Export().Contains("Parties"));
        var threeTransfers = TerritoryTransferLedger.Restore(incomingTransfers.Export());
        var oldLiegeSource = new TerritoryTransferObservation("b", "realm", 3);
        threeTransfers.Prepare("move-b", cross.Book, "bond-b", oldLiegeSource, "foreign-b", "foreign-b", null, TerritoryTransferPurpose.ActiveCharter, ++now);
        threeTransfers.Confirm("move-b", oldLiegeSource with { Owner = "foreign-b" }, now);
        var threeParties = new[] { parties[0], new RetainedRealmConsent("b", "foreign-b", authority with { Realm = "foreign-b", Representative = "actual-ruler-b" }, true), new RetainedRealmConsent("e", "realm", authority, true) };
        var threeWitness = incoming with { Before = cross.Book.Export(), Previous = "cross-b-a", Parent = "e", Created = "e-a", Tick = ++now, Transfers = threeTransfers.Export(), Parties = threeParties };
        var three = cross.Archive.Append(threeWitness, cross.Book);
        Check(three.Book.Charter("cross-b-a").Phase == RecordedCharterPhase.Released && three.Book.Charter("e-a").NpcAuthority == new NpcCharterAuthority("ruler", "actual-ruler-a"));
        Check(three.Book.Charter("a-c") == cross.Book.Charter("a-c") && three.Book.Charter("bond-b") == cross.Book.Charter("bond-b") && three.Book.Charter("debt") == cross.Book.Charter("debt"));
        Check(three.Book.HierarchyPath("c").SequenceEqual(new[] { "player", "e", "a", "c" }));
        var threeRestored = NpcReparentArchive.Restore(three.Archive.Export()); threeRestored.ValidateBook(CharterBook.Restore(three.Book.Export())); threeRestored.ValidatePeers(threeTransfers, payments);
        Check(threeRestored.Records.Last().Witness.Parties.Length == 3 && threeRestored.Records.Count == 4);
        Reject(() => cross.Archive.Append(threeWitness with { Parties = threeParties.Where(p => p.Party != "b").ToArray() }, cross.Book));
        var termination = new NpcTerminationWitness("e-a", three.Book.Export(), ++now, authority,
            three.Book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray(),
            threeParties.Where(p => p.Party != "b").ToArray(), retained, threeTransfers.Export(), payments.Export());
        var terminations = new NpcSubtreeTerminations(); var ended = terminations.Append(termination, three.Book);
        Check(three.Book.Charter("e-a").Phase == RecordedCharterPhase.Active && ended.Book.Charter("e-a").Phase == RecordedCharterPhase.Released);
        Check(ended.Book.Charter("a-c") == three.Book.Charter("a-c") && ended.Book.Charter("a-d") == three.Book.Charter("a-d"));
        Check(ended.Book.HierarchyPath("c").SequenceEqual(new[] { "a", "c" }) && ended.Book.Charter("debt") == three.Book.Charter("debt"));
        three.Archive.ValidateBook(ended.Book);
        var termRestored = NpcSubtreeTerminations.Restore(ended.Archive.Export()); termRestored.ValidateBook(CharterBook.Restore(ended.Book.Export())); termRestored.ValidatePeers(threeTransfers, payments);
        Check(termRestored.Records.Single().Witness.Children.Length == 2 && payments.Bill("debt", 1).RequiredValue == 100);
        var termRaw = termRestored.Export(); termRestored.Records[0].Witness.Children[0] = retained[1]; Check(termRestored.Export() == termRaw);
        Reject(() => terminations.Append(termination with { Children = retained.Take(1).ToArray() }, three.Book));
        Reject(() => terminations.Append(termination with { Parties = termination.Parties.Take(1).ToArray() }, three.Book));
        Reject(() => terminations.Append(termination with { Transfers = transfers.Export() }, three.Book));
        Reject(() => terminations.Append(termination with { Parties = termination.Parties.Select(p => p with { ReciprocalPeace = false }).ToArray() }, three.Book));
        Reject(() => terminations.Append(termination with { Before = book.Export() }, three.Book));
        Reject(() => ended.Archive.Append(termination, ended.Book));
        Reject(() => termRestored.ValidatePeers(threeTransfers, new TributeLedger()));
        Reject(() => NpcSubtreeTerminations.Restore(termRaw.Replace("\"Proof\":\"", "\"Proof\":\"0")));
        var newer = CharterBook.Restore(ended.Book.Export());
        var newerObs = newer.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        newer.Offer("new-a", Terms("player", "a"), ++now, PoliticalWorld.Day, newerObs); newer.Sign("new-a", true, now, newerObs); newer.Sign("new-a", false, now, newerObs);
        termRestored.ValidateBook(newer); Check(newer.Charter("e-a").Phase == RecordedCharterPhase.Released && newer.Charter("new-a").Phase == RecordedCharterPhase.Active);
        var pendingTransfers = TerritoryTransferLedger.Restore(threeTransfers.Export());
        var pendingSource = new TerritoryTransferObservation("a", "foreign-a", 3);
        pendingTransfers.PrepareJournaled("pending-a", three.Book, "e-a", pendingSource, "pending-target", "pending-target", null, TerritoryTransferPurpose.ActiveCharter, ++now);
        var request = new DeferredNpcRequest("request-a", "e-a", three.Book.Export(), pendingTransfers.Export(), payments.Export(), now);
        var deferred = new DeferredNpcTerminations().Queue(request);
        Check(three.Book.Charter("e-a").Phase == RecordedCharterPhase.Active && deferred.Pending.Proof.Length == 64);
        Reject(() => deferred.RequireReady(three.Book, pendingTransfers, payments, now));
        Reject(() => deferred.Queue(request with { Id = "duplicate" }));
        pendingTransfers.Confirm("pending-a", pendingSource with { Owner = "pending-target" }, ++now);
        deferred = DeferredNpcTerminations.Restore(deferred.Export()); deferred.RequireReady(three.Book, pendingTransfers, payments, now); Check(deferred.Pending != null);
        var completedWitness = termination with { Tick = now, Transfers = pendingTransfers.Export(), Deferred = new(request.Id, deferred.Pending.Proof),
            Parties = termination.Parties.Select(p => p.Party == "a" ? p with { ActualOwner = "pending-target", Decision = sourceDecision with { Realm = "pending-target", Representative = "new-target-ruler" } } : p).ToArray() };
        var completedTermination = new NpcSubtreeTerminations().Append(completedWitness, three.Book);
        var completedRequest = deferred.Close(now, completedTermination.Archive.Records.Single().Proof);
        completedRequest.ValidatePairs(completedTermination.Archive);
        DeferredNpcTerminations.Restore(completedRequest.Export()).ValidatePairs(NpcSubtreeTerminations.Restore(completedTermination.Archive.Export())); Check(completedRequest.Pending == null && completedRequest.Records.Single().Phase == 1);
        Reject(() => completedRequest.ValidatePairs(new NpcSubtreeTerminations()));
        Reject(() => new DeferredNpcTerminations().ValidatePairs(completedTermination.Archive));
        var cancelled = deferred.Close(now); cancelled.ValidatePairs(new NpcSubtreeTerminations()); Check(cancelled.Pending == null && cancelled.Records.Single().Phase == 2 && three.Book.Charter("e-a").Phase == RecordedCharterPhase.Active);
        Check(DeferredNpcTerminations.Marker(cancelled.Records.Single()) != DeferredNpcTerminations.Marker(deferred.Pending));
        Check(DeferredNpcTerminations.Marker(completedRequest.Records.Single()) != DeferredNpcTerminations.Marker(cancelled.Records.Single()));
        Reject(() => cancelled.Close(now));
        var differentBook = CharterBook.Restore(three.Book.Export()); differentBook.Register(new("unrelated", "realm", false), now);
        Reject(() => deferred.RequireReady(differentBook, pendingTransfers, payments, now));
        Reject(() => deferred.RequireReady(three.Book, pendingTransfers, new TributeLedger(), now));
        var pendingSave = JsonSerializer.Deserialize<TerritoryTransfersSave>(pendingTransfers.Export());
        var changedPrefix = TerritoryTransferLedger.Restore(JsonSerializer.Serialize(pendingSave with { Transfers = pendingSave.Transfers.Select(t => t.Id == "pending-a" ? t with { CityCallJournalV1 = false } : t).ToArray() }));
        Reject(() => deferred.RequireReady(three.Book, changedPrefix, payments, now));
        Check(DeferredNpcTerminations.Restore(cancelled.Export()).Records.Single().Request == request);
        NpcTerminationWitness RegimeWitness(NpcDecisionRegime regime)
        {
            var d = authority with { Regime = regime };
            return termination with { Authority = d, Parties = termination.Parties.Select(p => p.ActualOwner == "realm" ? p with { Decision = d } : p).ToArray() };
        }
        var unionWitness = RegimeWitness(NpcDecisionRegime.Union);
        var unionDecision = new ScopedNpcContractDecision("realm", "ruler", NpcDecisionRegime.Union, ScopedNpcTerminationAuthority.Scope(unionWitness), unionWitness.Tick, 1, 65,
            new[] { new NpcTerminationBallot("seat-1", true), new NpcTerminationBallot("seat-2", true), new NpcTerminationBallot("seat-3", true) });
        unionWitness = unionWitness with { Regimes = [unionDecision] };
        var unionEnd = new NpcSubtreeTerminations().Append(unionWitness, three.Book);
        Check(unionEnd.Book.Charter("e-a").Phase == RecordedCharterPhase.Released && unionEnd.Book.Charter("a-c") == three.Book.Charter("a-c"));
        var unionRestored = NpcSubtreeTerminations.Restore(unionEnd.Archive.Export()); unionRestored.ValidateBook(unionEnd.Book); unionRestored.ValidatePeers(threeTransfers, payments);
        Check(unionRestored.Records.Single().Witness.Regimes.Single().Ballots.Length == 3);
        var unionRaw = unionRestored.Export(); unionRestored.Records[0].Witness.Regimes[0].Ballots[0] = new("wrong", false); Check(unionRestored.Export() == unionRaw);
        Reject(() => NpcRealmDecision.Consent(unionWitness.Authority));
        Reject(() => new NpcSubtreeTerminations().Append(unionWitness with { Regimes = null }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(unionWitness with { Regimes = [unionDecision with { Scope = new string('a', 64) }] }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(unionWitness with { Regimes = [unionDecision with { Tick = unionWitness.Tick - 1 }] }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(unionWitness with { Regimes = [unionDecision with { Representative = "substitute" }] }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(unionWitness with { Regimes = [unionDecision with { Ballots = [new("duplicate", true), new("duplicate", true)] }] }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(unionWitness with { Regimes = [unionDecision with { Ballots = [new("seat-1", false)] }] }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(unionWitness with { Regimes = [unionDecision with { RoyalTitle = "invented" }] }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(unionWitness with { Payments = new TributeLedger().Export() }, three.Book));
        var royalWitness = RegimeWitness(NpcDecisionRegime.Royal);
        var royalDecision = new ScopedNpcContractDecision("realm", "ruler", NpcDecisionRegime.Royal, ScopedNpcTerminationAuthority.Scope(royalWitness), royalWitness.Tick, 1, 65, [], "Stellarch", 60);
        royalWitness = royalWitness with { Regimes = [royalDecision] };
        var royalEnd = new NpcSubtreeTerminations().Append(royalWitness, three.Book);
        NpcSubtreeTerminations.Restore(royalEnd.Archive.Export()).ValidateBook(royalEnd.Book); Check(royalEnd.Book.Charter("a-d") == three.Book.Charter("a-d"));
        Reject(() => NpcRealmDecision.Consent(royalWitness.Authority));
        Reject(() => new NpcSubtreeTerminations().Append(royalWitness with { Regimes = [royalDecision with { RoyalTitle = null }] }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(royalWitness with { Regimes = [royalDecision with { Seniority = 0 }] }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(royalWitness with { Regimes = [royalDecision with { Ballots = [new("fake-noble", true)] }] }, three.Book));
        Check(!ended.Archive.Export().Contains("Regimes"));
        var occupier = sourceDecision with { Realm = "unwitnessed-controller", Representative = "occupier-ruler" };
        var claim = new NpcOwnershipContractSettlement("a", "foreign-a", occupier.Realm, sourceDecision, true);
        var disputed = termination with {
            Parties = termination.Parties.Select(p => p.Party == "a" ? p with { ActualOwner = occupier.Realm, Decision = occupier } : p).ToArray(),
            Observations = termination.Observations.Select(o => o.Id == "a" ? o with { NativeOwner = occupier.Realm, Eligible = false } : o).ToArray(),
            OwnershipClaims = [claim] };
        var disputeEnd = new NpcSubtreeTerminations().Append(disputed, three.Book);
        Check(disputeEnd.Book.Charter("e-a").Phase == RecordedCharterPhase.Released && disputeEnd.Book.Entity("a").NativeOwner == three.Book.Entity("a").NativeOwner);
        Check(disputeEnd.Book.Charter("a-c") == three.Book.Charter("a-c") && disputeEnd.Book.Charter("debt") == three.Book.Charter("debt"));
        Check(disputeEnd.Book.Availability("a", disputed.Observations) == CharterAvailability.OwnershipChanged && !threeTransfers.VerifiedOwner("a", "realm", occupier.Realm));
        var disputeRestored = NpcSubtreeTerminations.Restore(disputeEnd.Archive.Export()); disputeRestored.ValidateBook(disputeEnd.Book); disputeRestored.ValidatePeers(threeTransfers, payments);
        Check(disputeRestored.Records.Single().Witness.Observations.Single(o => o.Id == "a").NativeOwner == occupier.Realm);
        var disputeRaw = disputeRestored.Export(); disputeRestored.Records[0].Witness.OwnershipClaims[0] = claim with { NoRecordedLoss = false }; Check(disputeRestored.Export() == disputeRaw);
        Reject(() => new NpcSubtreeTerminations().Append(disputed with { OwnershipClaims = null }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(disputed with { OwnershipClaims = [claim with { PreviousKnownOwner = "invented" }] }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(disputed with { OwnershipClaims = [claim with { PreviousClaimant = null }] }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(disputed with { OwnershipClaims = [claim with { NoRecordedLoss = false }] }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(disputed with { Observations = disputed.Observations.Select(o => o.Id == "a" ? o with { Exists = false } : o).ToArray() }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(disputed with { Observations = disputed.Observations.Select(o => o.Id == "a" ? o with { Player = true } : o).ToArray() }, three.Book));
        Reject(() => new NpcSubtreeTerminations().Append(termination with { OwnershipClaims = [claim] }, three.Book));
        Check(!ended.Archive.Export().Contains("OwnershipClaims"));
        var unionRoot = authority with { Regime = NpcDecisionRegime.Union };
        var scopedReparent = incoming with { Decision = unionRoot, Parties = incoming.Parties.Select(p => p.ActualOwner == "realm" ? p with { Decision = unionRoot } : p).ToArray() };
        var reparentCouncil = unionDecision with { Scope = ScopedNpcReparentAuthority.Scope(scopedReparent), Tick = scopedReparent.Tick };
        scopedReparent = scopedReparent with { Regimes = [reparentCouncil] };
        var councilReparent = archive.Append(scopedReparent, book);
        Check(councilReparent.Book.Charter("cross-b-a").NpcAuthority == new NpcCharterAuthority("ruler", "actual-ruler-a") && councilReparent.Book.Charter("a-c") == book.Charter("a-c"));
        NpcReparentArchive.Restore(councilReparent.Archive.Export()).ValidateBook(councilReparent.Book); Check(councilReparent.Archive.Records.Last().Witness.Regimes[0].Ballots.Length == 3);
        Reject(() => archive.Append(scopedReparent with { Regimes = [unionDecision] }, book));
        Reject(() => archive.Append(scopedReparent with { Created = "different-new-contract" }, book));
        Reject(() => archive.Append(scopedReparent with { Regimes = null }, book));
        var crownRoot = authority with { Regime = NpcDecisionRegime.Royal };
        var crownReparent = incoming with { Decision = crownRoot, Parties = incoming.Parties.Select(p => p.ActualOwner == "realm" ? p with { Decision = crownRoot } : p).ToArray() };
        crownReparent = crownReparent with { Regimes = [royalDecision with { Scope = ScopedNpcReparentAuthority.Scope(crownReparent), Tick = crownReparent.Tick }] };
        var crowned = archive.Append(crownReparent, book); NpcReparentArchive.Restore(crowned.Archive.Export()).ValidateBook(crowned.Book);
        Check(crowned.Book.Charter("a-d") == book.Charter("a-d") && crowned.Book.Charter("debt") == book.Charter("debt"));
        Check(!archive.Export().Contains("Regimes"));
        var requestLoss = new TownLossProof("e-a", "a", "realm", "foreign-a", "A", now + 1, now + 2, request.Book);
        var lossArchive = new TownSuccessionArchive();
        var lossBook = lossArchive.CloseForObservation(requestLoss); lossArchive = lossArchive.Observe(requestLoss, lossBook);
        var acknowledged = deferred.AcknowledgeLoss(lossArchive, lossBook, now + 3);
        acknowledged.ValidatePairs(new NpcSubtreeTerminations(), lossArchive, lossBook);
        Check(acknowledged.Pending == null && acknowledged.Records.Single().Phase == 3 && lossBook.Charter("e-a").Phase == RecordedCharterPhase.TerritoryLost);
        Check(lossBook.Charter("a-c") == three.Book.Charter("a-c") && lossBook.Charter("debt") == three.Book.Charter("debt"));
        var reloadedLossRequest = DeferredNpcTerminations.Restore(acknowledged.Export());
        reloadedLossRequest.ValidatePairs(new NpcSubtreeTerminations(), TownSuccessionArchive.Restore(lossArchive.Export()), CharterBook.Restore(lossBook.Export()));
        Check(reloadedLossRequest.Export() == acknowledged.Export() && DeferredNpcTerminations.Marker(acknowledged.Records.Single()) != DeferredNpcTerminations.Marker(deferred.Pending));
        Reject(() => acknowledged.ValidatePairs(new NpcSubtreeTerminations()));
        Reject(() => acknowledged.ValidatePairs(new NpcSubtreeTerminations(), new TownSuccessionArchive(), lossBook));
        Reject(() => deferred.AcknowledgeLoss(new TownSuccessionArchive(), three.Book, now + 3));
        Check(!cancelled.Export().Contains("\"Loss\""));
        return n;
    }
}
