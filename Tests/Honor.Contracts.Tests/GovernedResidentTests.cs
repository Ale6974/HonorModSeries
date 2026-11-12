using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;
internal static class GovernedResidentTests
{
    public static int Run()
    {
        int n = 0; void Check(bool b) { n++; if (!b) throw new Exception("Governed resident execution regression."); }
        void Reject(Action a) { bool bad = false; try { a(); } catch (ArgumentException) { bad = true; } Check(bad); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        var authority = new NpcRealmDecisionEvidence("source", "ruler", NpcDecisionRegime.Union, true, true, true, true, true, true);
        ResidentMoveEvidence[] people = [new("a", Hash("clinical-a"), 3), new("b", Hash("clinical-b"), null)];
        string book = Hash("same-contracts-debts-third-party-rights");
        var draft = new ResidentEnrollmentState("bond", "town", "map", "source", book, authority, ["old"], people, 0, false, false, false, 10, null);
        string scope = ResidentEnrollment.DecisionScope(draft);
        var vote = new ScopedNpcContractDecision("source", "ruler", NpcDecisionRegime.Union, scope, 10, 1, 75, [new("seat-1", true), new("seat-2", true)]);
        string decision = JsonSerializer.Serialize(vote);
        Reject(() => ResidentEnrollment.Prepare("bond", "town", "map", "source", book, authority, ["old"], people, 10));
        string j = ResidentEnrollment.Prepare("bond", "town", "map", "source", book, authority, ["old"], people, 10, decision);
        Check(ResidentEnrollment.Restore(j).GovernmentDecisionV1 == decision);
        Reject(() => ResidentEnrollment.Review(j, ["old"], authority, book));
        Reject(() => ResidentEnrollment.Review(j, ["old"], authority, book, JsonSerializer.Serialize(vote with { Ballots = [new("changed-seat", true)] })));
        Reject(() => ResidentEnrollment.Review(j, ["old"], authority with { Representative = "successor" }, book, decision));
        Reject(() => ResidentEnrollment.Prepare("other-bond", "town", "map", "source", book, authority, ["old"], people, 10, decision));
        Reject(() => ResidentEnrollment.Prepare("bond", "town", "other-map", "source", book, authority, ["old"], people, 10, decision));
        Reject(() => ResidentEnrollment.Prepare("bond", "town", "map", "source", book, authority, ["old"], [people[0] with { MedicalCare = 1 }, people[1]], 10, decision));
        Reject(() => ResidentEnrollment.Prepare("bond", "town", "map", "source", book, authority with { Regime = NpcDecisionRegime.Ordinary }, ["old"], people, 10, decision));
        string uncertain = ResidentEnrollment.Begin(j, ["old"], authority, book, decision);
        Reject(() => ResidentEnrollment.Review(uncertain, ["old", "a"], authority, book, decision));
        j = ResidentEnrollment.Returned(uncertain, ["old", "a"]);
        var saved = ResidentEnrollment.Restore(j);
        Check(saved.Added == 1 && ResidentEnrollment.DecisionScope(saved) == scope && saved.GovernmentDecisionV1 == decision);
        ResidentEnrollment.Review(j, ["old", "a"], authority, book, decision);
        j = ResidentEnrollment.Returned(ResidentEnrollment.Begin(j, ["old", "a"], authority, book, decision), ["old", "a", "b"]);
        j = ResidentEnrollment.Finish(j, people); saved = ResidentEnrollment.Restore(j);
        Check(saved.Verified && saved.GovernmentDecisionV1 == decision && saved.Book == book && saved.Residents.SequenceEqual(people));
        Reject(() => ResidentEnrollment.Review(j, ["old", "a", "b"], authority, book, decision));
        // Royal decisions use the current ruler's own title; no council seats or ordinary fallback.
        var royal = authority with { Regime = NpcDecisionRegime.Royal };
        var royalDraft = draft with { Authority = royal };
        var mandate = vote with { Regime = NpcDecisionRegime.Royal, Scope = ResidentEnrollment.DecisionScope(royalDraft), Ballots = [], RoyalTitle = "Count", Seniority = 2 };
        string r = ResidentEnrollment.Prepare("bond", "town", "map", "source", book, royal, ["old"], people, 10, JsonSerializer.Serialize(mandate));
        Check(ResidentEnrollment.Restore(r).Authority.Regime == NpcDecisionRegime.Royal);
        Reject(() => ResidentEnrollment.Prepare("bond", "town", "map", "source", book, royal, ["old"], people, 10, JsonSerializer.Serialize(mandate with { RoyalTitle = null })));
        Reject(() => ResidentEnrollment.Prepare("bond", "town", "map", "source", book, royal, ["old"], people, 10, JsonSerializer.Serialize(mandate with { Representative = "former-ruler" })));
        // A wait is a DIFFERENT action. Original job observations never become a clinical/job reset permission.
        DeferredTownPerson[] work = [new("a", "Job_4", Hash("work-a")), new("old", "Job_5", Hash("work-old"))];
        var wait = new DeferredTownState("wait", "bond", "town", "map", "source", "target", book, authority, ["old"], ["prior"], work, 10, DeferredTownPhase.Waiting, null, null);
        string waitScope = DeferredTownTransfer.DecisionScope(wait);
        var waitVote = vote with { Scope = waitScope };
        string waitDecision = JsonSerializer.Serialize(waitVote);
        Reject(() => DeferredTownTransfer.Prepare("wait", "bond", "town", "map", "source", "target", book, authority, ["old"], ["prior"], work, 10, decision));
        string pending = DeferredTownTransfer.Prepare("wait", "bond", "town", "map", "source", "target", book, authority, ["old"], ["prior"], work, 10, waitDecision);
        DeferredTownTransfer.Review(pending, book, authority, ["a", "old"], waitDecision);
        Reject(() => DeferredTownTransfer.Begin(pending, book, authority, ["a", "old"]));
        Reject(() => DeferredTownTransfer.Begin(pending, book, authority, ["a", "old"], JsonSerializer.Serialize(waitVote with { Goodwill = 49 })));
        Reject(() => DeferredTownTransfer.Prepare("wait", "bond", "town", "map", "source", "different-target", book, authority, ["old"], ["prior"], work, 10, waitDecision));
        string started = DeferredTownTransfer.Begin(pending, book, authority, ["a", "old"], waitDecision);
        Check(DeferredTownTransfer.Restore(started).Phase == DeferredTownPhase.Started && DeferredTownTransfer.DecisionScope(DeferredTownTransfer.Restore(started)) == waitScope);
        Reject(() => DeferredTownTransfer.Begin(started, book, authority, ["a", "old"], waitDecision));
        var receipt = new TerritoryTransfer("new-transfer", "town", "source", "source", "target", "bond", TerritoryTransferPurpose.ActiveCharter, 11, TerritoryTransferPhase.Applied, 12);
        string applied = DeferredTownTransfer.Applied(started, receipt);
        Check(DeferredTownTransfer.Restore(applied).Transfer == "new-transfer" && DeferredTownTransfer.Restore(applied).GovernmentDecisionV1 == waitDecision);
        Check(DeferredTownTransfer.Restore(DeferredTownTransfer.Stop(pending)).GovernmentDecisionV1 == waitDecision);
        // Existing ordinary/null documents remain byte-for-byte compatible.
        var ordinary = authority with { Regime = NpcDecisionRegime.Ordinary };
        var legacy = ResidentEnrollment.Restore(ResidentEnrollment.Prepare("bond", "town", "map", "source", book, ordinary, ["old"], people, 10));
        Check(JsonSerializer.Serialize(legacy) == JsonSerializer.Serialize(new { legacy.Charter, legacy.Town, legacy.Map, legacy.Source, legacy.Book, legacy.Authority, legacy.Before, legacy.Residents, legacy.Added, legacy.Started, legacy.Complete, legacy.Verified, legacy.Tick, legacy.Proof }));
        var oldWait = DeferredTownTransfer.Restore(DeferredTownTransfer.Prepare("wait", "bond", "town", "map", "source", "target", book, ordinary, ["old"], ["prior"], work, 10));
        Check(JsonSerializer.Serialize(oldWait) == JsonSerializer.Serialize(new { oldWait.Id, oldWait.Charter, oldWait.Town, oldWait.Map, oldWait.Source, oldWait.Target, oldWait.Book, oldWait.Authority, oldWait.Before, oldWait.PreviousTransfers, oldWait.People, oldWait.Tick, oldWait.Phase, oldWait.Transfer, oldWait.Proof }));
        return n;
    }
}
