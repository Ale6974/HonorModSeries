using System.Text.Json;
using Honor.Contracts.Rules;

internal static class FeudalWarAdmissionTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception("Feudal war admission: " + name); }
        void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        var target = new UnionWarTarget("enemy", 0, SharedRelationKind.Neutral, 0, SharedRelationKind.Neutral, true, true);
        UnionWarConsent Decision(int goodwill = 50, long now = 10, string leader = "liege-leader", UnionWarTarget enemy = null)
            => UnionWarAdmission.ReviewFeudal("liege", "player", leader, now, new[] { enemy ?? target }, goodwill);
        var consent = Decision();
        Check(consent.Accepted && consent.AuthorityKind == AdmissionAuthorityKind.FeudalLeader && !consent.Founding &&
            consent.Votes.Length == 1 && consent.Votes[0].Seat == "liege/liege", "leader decision is distinct from a town council or founding admission");
        Check(!Decision(49).Accepted && Decision(49).Votes[0].Reason == UnionAdmissionVoteReason.Relationship, "current relationship boundary");
        Check(!Decision(100, enemy: target with { Kind = SharedRelationKind.Ally, ReciprocalKind = SharedRelationKind.Ally }).Accepted, "signed charter does not erase a liege alliance");
        Check(!Decision(100, enemy: target with { NativeRoute = false }).Accepted, "leader cannot bypass missing native permission");
        Check(!Decision(100, enemy: target with { ApplicantHostile = false }).Accepted, "no invented applicant war");
        Check(Decision(50, enemy: target with { Kind = SharedRelationKind.Hostile, ReciprocalKind = SharedRelationKind.Hostile }).Accepted, "existing hostility needs no repeated native declaration");
        Reject(() => UnionWarAdmission.Validate(consent with { Founding = true }), "no founding exemption for a feudal ruler");
        Reject(() => UnionWarAdmission.Validate(consent with { Votes = new[] { consent.Votes[0], new UnionAdmissionVote("town", 1, true, UnionAdmissionVoteReason.Accepted) } }), "no fabricated council seats on leader consent");
        Reject(() => UnionWarAdmission.Validate(consent with { Votes = new[] { consent.Votes[0] with { Seat = "liege/other" } } }), "leader seat binds actual authority");
        Reject(() => UnionWarAdmission.Validate(consent with { AuthorityKind = (AdmissionAuthorityKind)99 }), "unknown regime");
        Check(!UnionWarAdmission.MatchesAction(consent, AllegianceAction.JoinUnion) && UnionWarAdmission.MatchesAction(consent, AllegianceAction.BindLiege), "consent cannot authorize another political action");
        var union = UnionWarAdmission.Review("liege", "player", "liege-leader", 10, new[] { "town" }, new[] { target }, 50);
        Check(!UnionWarAdmission.SameWars(union, consent), "same parties/enemy do not substitute a council decision for a ruler decision");
        Check(!JsonSerializer.Serialize(union).Contains("AuthorityKind") && JsonSerializer.Serialize(consent).Contains("AuthorityKind"), "legacy union JSON remains byte-compatible while feudal kind is explicit");
        const string legacyUnion = """{"Union":"liege","Player":"player","Representative":"liege-leader","Tick":10,"Founding":false,"Targets":[{"Enemy":"enemy","Goodwill":0,"Kind":0,"ReciprocalGoodwill":0,"ReciprocalKind":0,"ApplicantHostile":true,"NativeRoute":true}],"Votes":[{"Seat":"town","Weight":1,"Approve":true,"Reason":0}],"TotalWeight":1,"ApprovedWeight":1,"Accepted":true}""";
        Check(JsonSerializer.Serialize(union) == legacyUnion && JsonSerializer.Serialize(JsonSerializer.Deserialize<UnionWarConsent>(legacyUnion)) == legacyUnion,
            "exact pre-regime union consent bytes and saved default survive read/write");
        var model = new PlayerAllegiance("player"); var transfers = new TerritoryTransferLedger();
        var book = new CharterBook(); book.Register(new("p", "player", true), 0);
        FollowerTransferPlan Plan(long now) => FollowerTransfers.Review(book, transfers, [], "liege", now);
        var command = new AllegianceCommand(AllegianceAction.BindLiege, 10, "liege", "signed-contract", Consent: true, OldContractReleased: true);
        var journal = new AllegianceTransitionLedger();
        void AtomicReject(UnionWarConsent value, string name)
        { string before = journal.Export(); Reject(() => journal.Prepare("bad", model, command, Plan(10), transfers, value), name); Check(journal.Export() == before, name + " leaves intent unchanged"); }
        AtomicReject(union, "wrong decision authority");
        AtomicReject(Decision(49), "refused additional war consent");
        AtomicReject(consent with { Representative = "" }, "missing current representative");
        journal.Prepare("join-liege", model, command, Plan(10), transfers, consent);
        Check(journal.Pending && !journal.WarsReady && model.State.Mode == PlayerAllegianceMode.Independent, "preparation does not apply allegiance or declare war");
        Reject(() => journal.BeginWar("enemy", 10), "native write waits for recorded allegiance stage");
        model.Apply(command); journal.ConfirmAllegiance(model, 10);
        Reject(() => journal.Complete(model, Plan(10), 10), "signed allegiance alone cannot complete pending wars");
        journal.BeginWar("enemy", 10); var interrupted = AllegianceTransitionLedger.Restore(journal.Export());
        Check(interrupted.Pending && interrupted.Current.WarResults[0].Step == UnionWarStep.Applying &&
            interrupted.Current.Wars.AuthorityKind == AdmissionAuthorityKind.FeudalLeader, "interrupted native intent restores exact regime and scope");
        interrupted.ReaffirmWars(Decision(50, 11, "new-current-leader"), 11);
        Check(interrupted.Current.Wars.Representative == "liege-leader" && interrupted.Current.WarReaffirmations[0].Representative == "new-current-leader", "fresh successor consent does not rewrite old representative");
        Reject(() => interrupted.ReaffirmWars(union with { Tick = 11 }, 11), "recovery cannot switch to union votes");
        interrupted.ObserveWar("enemy", true, -75, -75, false, 11); interrupted.Complete(model, Plan(11), 11);
        Check(interrupted.Current.Phase == AllegianceTransitionPhase.Completed && !interrupted.Current.WarResults[0].FromCompletedCall,
            "uncertain already-hostile outcome is observed without replay or false attribution");
        var saved = JsonSerializer.Deserialize<AllegianceTransitionSave>(interrupted.Export());
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(saved with { Current = saved.Current with { Wars = union } })), "restored ruler transition cannot claim council authority");
        Check(AllegianceTransitionLedger.Restore(interrupted.Export()).Export() == interrupted.Export(), "feudal consent, reaffirmation and native results round-trip");
        var nextModel = PlayerAllegiance.Restore(model.Export());
        var next = new AllegianceTransitionLedger();
        var nextCommand = new AllegianceCommand(AllegianceAction.JoinUnion, 12, "next-union", Consent: true, NoActiveFeudalContract: true, OldContractReleased: true);
        next.Prepare("next", nextModel, nextCommand, Plan(12) with { Target = "next-union" }, transfers);
        UnionWarAdmission.ValidateHistory(new[] { interrupted.Export() }, next.Current);
        Check(true, "later released transition retains feudal war history on the same player command lineage");
        // A current ruler's acceptance never substitutes for physical completion of the entire inherited subtree.
        var inherited = new CharterBook(); inherited.Register(new("p", "player", true), 0);
        inherited.Register(new("lord", "liege", false), 0);
        foreach (string id in new[] { "a", "b", "g" }) inherited.Register(new(id, "source", false), 0);
        var observed = inherited.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string parent, string child) => new(parent, child, 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m);
        foreach (string id in new[] { "a", "b" })
        { inherited.Offer(id, Terms("p", id), 0, PoliticalWorld.Day, observed); inherited.Sign(id, true, 0, observed); inherited.Sign(id, false, 0, observed); }
        var npc = new NpcCharterAuthority("source-leader", "source-leader");
        inherited.OfferNpc("g", Terms("a", "g"), npc, 0, PoliticalWorld.Day, observed);
        inherited.SignNpc("g", "a", "source-leader", 0, observed); inherited.SignNpc("g", "g", "source-leader", 0, observed);
        inherited.Offer("signed-contract", Terms("lord", "p"), 0, PoliticalWorld.Day, observed);
        inherited.Sign("signed-contract", false, 0, observed); inherited.Sign("signed-contract", true, 0, observed);
        var physical = new TerritoryTransferLedger(); var applicant = new PlayerAllegiance("player"); var coordinated = new AllegianceTransitionLedger();
        var candidates = new[] { "a", "b", "g" }.Select(id => new FollowerCandidate(id, new(id, "source", 4))).ToArray();
        FollowerTransferPlan InheritedPlan(long now) => FollowerTransfers.Review(inherited, physical, candidates, "liege", now);
        coordinated.Prepare("inherited", applicant, command, InheritedPlan(10), physical, consent);
        applicant.Apply(command); coordinated.ConfirmAllegiance(applicant, 10); coordinated.BeginWar("enemy", 10);
        coordinated.ObserveWar("enemy", true, -75, -75, true, 10);
        Check(coordinated.Current.Followers.Count == 3 && coordinated.WarsReady, "original intent includes indirect as well as direct followers");
        Reject(() => coordinated.Complete(applicant, InheritedPlan(10), 10), "verified war outcomes cannot hide remaining physical moves");
        for (int i = 0; i < candidates.Length; i++)
        {
            var row = candidates[i]; string id = "physical-" + i;
            physical.Prepare(id, inherited, row.Charter, row.Observation, "liege", "liege", null, TerritoryTransferPurpose.ActiveCharter, 11 + i);
            physical.Confirm(id, row.Observation with { Owner = "liege" }, 11 + i);
            candidates[i] = row with { Observation = row.Observation with { Owner = "liege", SourceSettlements = i + 1 } };
            for (int j = i + 1; j < candidates.Length; j++)
                candidates[j] = candidates[j] with { Observation = candidates[j].Observation with { SourceSettlements = 3 - i } };
            // Current target settlement counts must agree across every already-moved city.
            for (int j = 0; j <= i; j++) candidates[j] = candidates[j] with { Observation = candidates[j].Observation with { SourceSettlements = i + 1 } };
            if (i == 0) Reject(() => coordinated.Complete(applicant, InheritedPlan(11), 11), "partial direct-child completion retains the indirect follower");
        }
        coordinated.Complete(applicant, InheritedPlan(13), 13);
        Check(coordinated.Current.Phase == AllegianceTransitionPhase.Completed && inherited.Charter("g").Terms.Liege == "a" && inherited.Charter("signed-contract").Phase == RecordedCharterPhase.Active,
            "only complete native-owner proofs finish; inherited internal contracts and signed incoming bond remain unchanged");
        return count;
    }
}
