using Honor.Contracts.Rules;
using System.Text.Json;

internal static class ScopedContractAuthorityTests
{
    public static int Run()
    {
        int n = 0; void Check(bool b) { n++; if (!b) throw new Exception("Scoped bilateral contract authority regression."); }
        void Reject(Action f) { bool rejected = false; try { f(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        foreach (var regime in new[] { NpcDecisionRegime.Union, NpcDecisionRegime.Royal })
        foreach (bool subject in new[] { true, false })
        {
            var b = new CharterBook(); b.Register(new("p", "player", true), 0); b.Register(new("n", "realm", false), 0);
            var obs = b.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
            var terms = new CharterOfferDraft().Bind(subject ? "n" : "p", subject ? "p" : "n");
            b.Offer("c", terms, 0, PoliticalWorld.Day, obs); b.Sign("c", false, 0, obs); b.Sign("c", true, 0, obs);
            var paid = new TributeLedger(); if (subject) paid.Enable(b, "c", 0); var transfers = new TerritoryTransferLedger();
            var d = new NpcRealmDecisionEvidence("realm", "ruler", regime, true, true, true, true, true, true);
            ScopedNpcContractDecision Mandate(string scope, long tick) => new(d.Realm, d.Representative, regime, scope, tick, 1, 70,
                regime == NpcDecisionRegime.Union ? [new("actual-seat", true)] : [], regime == NpcDecisionRegime.Royal ? "Stellarch" : null, regime == NpcDecisionRegime.Royal ? 60 : 0);
            var w = new ScopedCharterReleaseWitness("c", PoliticalWorld.Day * 6, b.Export(), paid.Export(), transfers.Export(), JsonSerializer.Serialize(obs), d, null);
            w = w with { GovernmentDecision = JsonSerializer.Serialize(Mandate(ScopedCharterReleases.Scope(w), w.Tick)) };
            var plan = CharterReleases.ReviewGovernment(w); var released = CharterBook.Restore(plan.After); var closed = TributeLedger.Restore(plan.PaymentsAfter);
            var history = new ScopedCharterReleases().Append(w, released, closed); history.ValidatePeers(transfers, closed);
            ScopedCharterReleases.Restore(history.Export()).ValidateBook(CharterBook.Restore(released.Export()));
            Check(released.Charter("c").Phase == RecordedCharterPhase.Released && released.Charter("c").Terms == terms && (!subject || closed.Account("c").ClosedTick == w.Tick));
            Check(subject ? paid.Account("c").ClosedTick == null && closed.Account("c").StartedTick == paid.Account("c").StartedTick : closed.Accounts.Count == 0);
            Reject(() => CharterReleases.Review(b, paid, transfers, "c", d, w.Tick, obs));
            Reject(() => CharterReleases.ReviewGovernment(w with { GovernmentDecision = null }));
            Reject(() => CharterReleases.ReviewGovernment(w with { Tick = w.Tick + 1 }));
            Reject(() => CharterReleases.ReviewGovernment(w with { Decision = d with { Representative = "replacement" } }));
            if (subject) Reject(() => history.ValidatePeers(transfers, new TributeLedger()));
            var amendment = new CharterAmendmentWitness("c", "new", 1, b.Export(), transfers.Export(), new(), d, 70) {
                Payments = subject ? paid.Export() : null, ScopePayments = paid.Export(), ScopeObservations = JsonSerializer.Serialize(obs) };
            amendment = amendment with { GovernmentDecision = JsonSerializer.Serialize(Mandate(CharterAmendments.GovernmentScope(amendment), 1)) };
            var reviewed = CharterAmendments.Replay(amendment);
            var applied = new CharterAmendments().Commit(reviewed, b, transfers, paid, d, 70, 1, obs);
            CharterAmendments.Restore(applied.Archive.Export()).ValidateBook(CharterBook.Restore(applied.Book.Export()));
            Check(applied.Book.Charter("c").Phase == RecordedCharterPhase.Released && applied.Book.Charter("new").Phase == RecordedCharterPhase.Active);
            Check(applied.Book.Charter("c").Terms == terms && b.Charter("c").Phase == RecordedCharterPhase.Active && (!subject || paid.Account("c").ClosedTick == null));
            if (subject)
            {
                var polity = new PlayerAllegiance("player"); polity.Apply(new(AllegianceAction.BindLiege, 0, "realm", "c", Consent: true));
                var journal = new AllegianceTransitionLedger();
                var renewal = PlayerCharterRenewals.Stage(reviewed, polity, journal, Array.Empty<FollowerCandidate>());
                var renewals = new PlayerCharterRenewals().Append(renewal);
                PlayerCharterRenewals.Restore(renewals.Export()).ValidatePair(CharterAmendments.Restore(applied.Archive.Export()), CharterBook.Restore(applied.Book.Export()));
                var after = PlayerAllegiance.Restore(renewal.After);
                Check(after.State.Contract == "new" && after.NativePlayer == "player" && after.State.Authority == "realm");
            }
            Reject(() => CharterAmendments.Replay(amendment with { GovernmentDecision = w.GovernmentDecision }));
            Reject(() => CharterAmendments.Replay(amendment with { ScopePayments = null }));
            Reject(() => new CharterAmendments().Commit(reviewed, b, transfers, paid, d with { Representative = "replacement" }, 70, 1, obs));
        }
        return n;
    }
}
