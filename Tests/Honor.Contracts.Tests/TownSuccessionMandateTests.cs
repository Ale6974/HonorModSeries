using Honor.Contracts.Rules;
using System.Text.Json;

internal static class TownSuccessionMandateTests
{
    public static int Run()
    {
        int n = 0; void Check(bool b) { n++; if (!b) throw new Exception("Town succession government regression."); }
        void Reject(Action f) { bool rejected = false; try { f(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        foreach (var regime in new[] { NpcDecisionRegime.Union, NpcDecisionRegime.Royal })
        {
            var b = new CharterBook(); b.Register(new("p", "native-player", true), 0);
            foreach (string id in new[] { "upper", "child" }) b.Register(new(id, "realm", false), 0);
            var obs = b.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
            var terms = new RecordedCharterTerms("p", "upper", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0);
            b.Offer("root", terms, 0, PoliticalWorld.Day, obs); b.Sign("root", true, 0, obs); b.Sign("root", false, 0, obs);
            b.OfferNpc("child", terms with { Liege = "upper", Subject = "child" }, new("old", "old"), 0, PoliticalWorld.Day, obs);
            b.SignNpc("child", "upper", "old", 0, obs); b.SignNpc("child", "child", "old", 0, obs);
            var loss = new TownLossProof("root", "upper", "realm", "realm", "Upper", 10, 11, b.Export());
            var losses = new TownSuccessionArchive(); b = losses.CloseForObservation(loss); losses = losses.Observe(loss, b);
            var current = obs.Select(o => o.Id == "upper" ? o with { Exists = false, Eligible = false } : o).ToArray();
            var plan = losses.Review("root", b, "gov", new("p", "current"), 20, current);
            var d = new NpcRealmDecisionEvidence("realm", "current", regime, true, true, true, true, true, true);
            var w = new TownSuccessionMandate(plan, losses.Export(), current, "p", "native-player", "native-player", d, null);
            w = w with { Mandate = new(d.Realm, d.Representative, regime, TownSuccessionMandates.Scope(w), 20, 1, 70,
                regime == NpcDecisionRegime.Union ? [new("actual-seat", true)] : [], regime == NpcDecisionRegime.Royal ? "Stellarch" : null, regime == NpcDecisionRegime.Royal ? 60 : 0) };
            var completed = losses.Commit(b, plan, 20, current);
            var linked = new TownSuccessionMandates().Attach(w, completed.Archive, completed.Book);
            linked.History.ValidatePair(linked.Archive, completed.Book);
            Check(linked.Archive.Record("root").Resolution.GovernmentProof == linked.History.Records.Single().Proof && completed.Book.Charter("root").Phase == RecordedCharterPhase.TerritoryLost);
            TownSuccessionMandates.Restore(linked.History.Export()).ValidatePair(TownSuccessionArchive.Restore(linked.Archive.Export()), CharterBook.Restore(completed.Book.Export()));
            Check(completed.Book.Charter(plan.Replacements["child"]).Terms == terms with { Subject = "child" });
            Reject(() => new TownSuccessionMandates().ValidatePair(linked.Archive, completed.Book));
            Reject(() => linked.History.ValidatePair(completed.Archive, completed.Book));
            Reject(() => TownSuccessionMandates.ValidateWitness(w with { ActualHigherOwner = "foreign" }));
            Reject(() => TownSuccessionMandates.ValidateWitness(w with { Decision = d with { Representative = "substitute" } }));
            Reject(() => TownSuccessionMandates.ValidateWitness(w with { Mandate = null }));
            Reject(() => TownSuccessionMandates.ValidateWitness(w with { Plan = w.Plan with { Replacements = null } }));
            var raw = linked.History.Export(); if (regime == NpcDecisionRegime.Union) linked.History.Records[0].Witness.Mandate.Ballots[0] = new("changed", false); Check(linked.History.Export() == raw);
            Check(!completed.Archive.Export().Contains("GovernmentProof"));
        }
        return n;
    }
}
