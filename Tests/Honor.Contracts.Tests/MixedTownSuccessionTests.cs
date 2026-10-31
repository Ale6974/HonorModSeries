using Honor.Contracts.Rules;
using System.Text.Json;

internal static class MixedTownSuccessionTests
{
    public static int Run()
    {
        int n = 0; void Check(bool b) { n++; if (!b) throw new Exception("Mixed town succession regression."); }
        void Reject(Action f) { bool rejected = false; try { f(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        foreach (bool compound in new[] { false, true })
        foreach (int returnMode in compound ? new[] { 0, 1, 2 } : new[] { 0, 1 })
        {
            bool historical = returnMode == 2, returned = returnMode != 0;
            var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("h", "higher", false), 0);
            foreach (string id in new[] { "x", "a", "b" }) book.Register(new(id, "realm", false), 0);
            TerritoryObservation[] Observations() => book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
            var obs = Observations();
            RecordedCharterTerms Terms(string l, string s) => new(l, s, 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0);
            book.Offer("upper", Terms("p", "h"), 0, PoliticalWorld.Day, obs); book.Sign("upper", true, 0, obs); book.Sign("upper", false, 0, obs);
            foreach (var row in new[] { (Id: "lost", L: "h", S: "x"), (Id: "a", L: "x", S: "a"), (Id: "b", L: "x", S: "b") })
            { book.OfferNpc(row.Id, Terms(row.L, row.S), new("old", "old"), 0, PoliticalWorld.Day, obs); book.SignNpc(row.Id, row.L, "old", 0, obs); book.SignNpc(row.Id, row.S, "old", 0, obs); }
            var transfers = new TerritoryTransferLedger(); long tick = 1;
            foreach (string id in compound ? new[] { "b" } : new[] { "a", "b" })
            {
                var o = new TerritoryTransferObservation(id, "realm", 3);
                transfers.Prepare("move-" + id, book, id, o, "foreign-" + id, "foreign-" + id, null, TerritoryTransferPurpose.ActiveCharter, tick);
                transfers.Confirm("move-" + id, o with { Owner = "foreign-" + id }, tick++);
            }
            string originalTransfers = transfers.Export();
            var loss = new TownLossProof("lost", "x", "realm", "realm", "X", 10, 11, book.Export(),
                Kind: returned ? TownLossKind.HostileNativeOwnershipChange : TownLossKind.Destroyed, CapturingOwner: returned ? "captor" : null);
            var losses = new TownSuccessionArchive(); book = losses.CloseForObservation(loss); losses = losses.Observe(loss, book);
            var returns = new TownOwnershipReturns();
            if (historical) returns = returns.Observe(new("lost", "x", "captor", "realm", TownOwnershipReturns.LossDigest(loss), 12, 13), losses, book);
            if (compound)
            {
                var child = new TownLossProof("a", "a", "realm", "realm", "A", 14, 15, book.Export(), ScopeRoot: "lost", ScopeOrdinal: 1);
                book = losses.CloseForObservation(child); losses = losses.Observe(child, book);
            }
            if (returnMode == 1) returns = returns.Observe(new TownOwnershipReturn("lost", "x", "captor", "realm", TownOwnershipReturns.LossDigest(loss), 16, 17) { ScopeSnapshot = compound ? losses.Export() : null }, losses, book);
            string original = book.Export(); var current = Observations().Select(o => o.Id == "x" && !returned || compound && o.Id == "a" ? o with { Exists = false, Eligible = false } : o).ToArray();
            var root = new NpcRealmDecisionEvidence("realm", "root-new", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
            var higher = root with { Realm = "higher", Representative = "higher-new" };
            var parties = new List<RetainedRealmConsent> { new("h", "higher", higher, true) };
            if (!compound) parties.Add(new("a", "foreign-a", root with { Realm = "foreign-a", Representative = "a-new", Regime = NpcDecisionRegime.Royal }, true));
            parties.Add(new("b", "foreign-b", root with { Realm = "foreign-b", Representative = "b-new", Regime = NpcDecisionRegime.Union }, true));
            var authority = new NpcCharterAuthority("higher-new", "root-new");
            var plain = losses.Review("lost", book, "mixed", authority, 20, current);
            var plan = losses.ReviewWithParties("lost", book, "mixed", authority, 20, current, TownSuccessionMandates.IndividualAuthorities(plain, parties.ToArray()));
            TownSuccessionMandate Sign(TownSuccessionMandate w)
            {
                var all = JsonSerializer.Deserialize<RetainedRealmConsent[]>(w.Parties);
                var mandates = all.Select(p => p.Decision).Where(d => d.Regime != NpcDecisionRegime.Ordinary).Select(d => new ScopedNpcContractDecision(d.Realm, d.Representative, d.Regime,
                    TownSuccessionMandates.Scope(w), w.Plan.Tick, 1, 70, d.Regime == NpcDecisionRegime.Union ? [new("actual-seat", true)] : [], d.Regime == NpcDecisionRegime.Royal ? "Stellarch" : null, d.Regime == NpcDecisionRegime.Royal ? 60 : 0)).ToArray();
                return w with { Governments = JsonSerializer.Serialize(mandates) };
            }
            var witness = Sign(new TownSuccessionMandate(plan, losses.Export(), current, "p", "player", "higher", root, null) { Parties = JsonSerializer.Serialize(parties), Transfers = transfers.Export() });
            TownSuccessionMandates.ValidateWitness(witness);
            var completed = losses.Commit(book, plan, 20, current);
            HistoricalTownReturnReconciliations history = null;
            if (historical)
            {
                var r = returns.Records.Single();
                var reviewed = new HistoricalTownReturnReview(witness, returns.Export(), transfers.Export(), null,
                    new Dictionary<string, string> { ["lost"] = "realm" }, new Dictionary<string, string> { ["lost"] = TownOwnershipReturns.ReceiptDigest(r) });
                var resolved = new HistoricalTownReturnReconciliations().Execute(reviewed, returns, losses, book);
                completed = (resolved.Book, resolved.Archive); history = resolved.History;
            }
            else if (returned)
            {
                string proof = returns.RequireActive("lost", losses, book, "x", "realm", 20);
                completed = returns.CommitSuccessionGroup(losses, book, plan, 20, current, new Dictionary<string, string> { ["lost"] = proof },
                    new Dictionary<string, string> { ["lost"] = "realm" }, transfers.Export(), null);
            }
            var paired = new TownSuccessionMandates().Attach(witness, completed.Archive, completed.Book);
            TownSuccessionMandates.Restore(paired.History.Export()).ValidatePair(TownSuccessionArchive.Restore(paired.Archive.Export()), CharterBook.Restore(completed.Book.Export()));
            if (history != null) HistoricalTownReturnReconciliations.Restore(history.Export()).ValidatePair(paired.Archive, completed.Book, returns);
            returns.ValidatePair(paired.Archive, completed.Book);
            foreach (var p in plan.Replacements)
            {
                var child = completed.Book.Charter(p.Value); var originalChild = book.Charter(p.Key);
                Check(child.NpcAuthority == new NpcCharterAuthority("higher-new", p.Key + "-new") && child.Terms == originalChild.Terms with { Liege = "h" });
                Check(child.PlayerSignedTick == 20 && child.NpcSignedTick == 20 && completed.Book.Entity(child.Terms.Subject) == book.Entity(child.Terms.Subject));
            }
            Check(book.Export() == original && transfers.Export() == originalTransfers && completed.Book.Charter("lost").Phase == RecordedCharterPhase.TerritoryLost);
            Reject(() => new TownSuccessionMandates().ValidatePair(paired.Archive, completed.Book));
            Reject(() => TownSuccessionMandates.ValidateWitness(Sign(witness with { Parties = JsonSerializer.Serialize(parties.Where(p => p.Party != "b")) })));
            Reject(() => TownSuccessionMandates.ValidateWitness(Sign(witness with { Transfers = new TerritoryTransferLedger().Export() })));
            Reject(() => TownSuccessionMandates.ValidateWitness(Sign(witness with { Parties = JsonSerializer.Serialize(parties.Select(p => p.Party == "b" ? p with { ReciprocalPeace = false } : p)) })));
            Reject(() => TownSuccessionMandates.ValidateWitness(witness with { Governments = null }));
            Check(!losses.Export().Contains("PartyAuthorities") && !plain.After.Contains("PartyAuthorities"));
        }
        {
            var b = new CharterBook(); b.Register(new("p", "player", true), 0);
            foreach (string id in new[] { "x", "a", "g" }) b.Register(new(id, "realm", false), 0);
            var obs = b.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
            RecordedCharterTerms T(string l, string s) => new(l, s, 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0);
            b.Offer("lost", T("p", "x"), 0, PoliticalWorld.Day, obs); b.Sign("lost", true, 0, obs); b.Sign("lost", false, 0, obs);
            foreach (var row in new[] { (Id: "a", L: "x", S: "a"), (Id: "g", L: "a", S: "g") })
            { b.OfferNpc(row.Id, T(row.L, row.S), new("old", "old"), 0, PoliticalWorld.Day, obs); b.SignNpc(row.Id, row.L, "old", 0, obs); b.SignNpc(row.Id, row.S, "old", 0, obs); }
            var ledger = new TerritoryTransferLedger();
            foreach (string id in new[] { "a", "g" }) { var o = new TerritoryTransferObservation(id, "realm", 3); ledger.Prepare("move-" + id, b, id, o, "foreign-" + id, "foreign-" + id, null, TerritoryTransferPurpose.ActiveCharter, 1); ledger.Confirm("move-" + id, o with { Owner = "foreign-" + id }, 1); }
            var loss = new TownLossProof("lost", "x", "realm", "realm", "X", 2, 2, b.Export());
            var losses = new TownSuccessionArchive(); b = losses.CloseForObservation(loss); losses = losses.Observe(loss, b);
            var current = obs.Select(o => o.Id == "x" ? o with { Exists = false, Eligible = false } : o).ToArray();
            var d = new NpcRealmDecisionEvidence("realm", "root", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
            var parties = new[] { new RetainedRealmConsent("a", "foreign-a", d with { Realm = "foreign-a", Representative = "ruler-a" }, true), new("g", "foreign-g", d with { Realm = "foreign-g", Representative = "ruler-g" }, true) };
            var plain = losses.Review("lost", b, "retained", new("p", "root"), 3, current);
            var plan = losses.ReviewWithParties("lost", b, "retained", plain.Authority, 3, current, TownSuccessionMandates.IndividualAuthorities(plain, parties));
            var w = new TownSuccessionMandate(plan, losses.Export(), current, "p", "player", "player", d, null) { Parties = JsonSerializer.Serialize(parties), Transfers = ledger.Export() };
            var done = losses.Commit(b, plan, 3, current); var paired = new TownSuccessionMandates().Attach(w, done.Archive, done.Book);
            paired.History.ValidatePair(paired.Archive, done.Book);
            Check(done.Book.Charter("g") == b.Charter("g") && plan.Replacements.Count == 1 && done.Book.Charter(plan.Replacements["a"]).NpcAuthority == null);
            Check(JsonSerializer.Deserialize<Dictionary<string, NpcCharterAuthority>>(plan.PartyAuthorities)["a"] == new NpcCharterAuthority("p", "ruler-a"));
            Reject(() => TownSuccessionMandates.ValidateWitness(w with { Parties = JsonSerializer.Serialize(parties.Take(1)) }));
        }
        return n;
    }
}
