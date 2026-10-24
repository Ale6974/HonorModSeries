using Honor.Contracts.Rules;

static class MilitaryTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool condition, string name)
        {
            count++;
            if (!condition) throw new Exception(name);
        }
        void Reject(Action action, string name)
        {
            bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, name);
        }
        PartyRef F(string id) => new(PartyKind.Faction, id);
        ObligationClause Protect() => new("protect", ObligationKind.Protection, ContractSide.Liege,
            0, 5, maximumRequests: 2, responseTicks: 10);
        ContractTerms Terms(ContractOrigin origin = ContractOrigin.Capitulation, string settlement = "town", string liege = "A", string subject = "B")
            => new(ContractKind.SettlementCharter, F(liege), F(subject), 100,
                new[] { Protect() }, settlement, new(true, false, true), origin);
        SettlementConflict Conflict(string id = "war", string town = "town") => new(id, town, F("A"), F("B"), 0);
        ContractLedger Active(ContractTerms terms)
        {
            var contract = new ContractLedger("ordinary", terms);
            contract.Sign(ContractSide.Liege, 0); contract.Sign(ContractSide.Subject, 0);
            return contract;
        }
        var none = Array.Empty<ContractLedger>();

        var siege = Conflict();
        Check(siege.BeginVerifiedSiege(1), "verified siege starts");
        Check(!siege.BeginVerifiedSiege(2), "duplicate siege transition");
        Check(siege.EstablishOccupationCharter(Terms(ContractOrigin.Occupation), "fake-control", none, 10000) == null,
            "elapsed siege time alone cannot establish occupation");
        Check(siege.Phase == SettlementConflictPhase.Besieging && siege.Contract == null, "no passive free settlement capture");
        Check(siege.AcceptVerifiedCapitulation(Terms(settlement: "other"), "wrong-town", none, 10000) == null, "cannot bind different settlement");
        Check(siege.AcceptVerifiedCapitulation(Terms(liege: "C"), "wrong-liege", none, 10000) == null, "cannot bind wrong attacker");
        Check(siege.AcceptVerifiedCapitulation(Terms(subject: "C"), "wrong-owner", none, 10000) == null, "cannot bind wrong defender");
        Check(siege.AcceptVerifiedCapitulation(Terms(ContractOrigin.Negotiated), "wrong-origin", none, 10000) == null, "capitulation records its origin");
        var capitulated = siege.AcceptVerifiedCapitulation(Terms(), "accepted-exact-terms", none, 10001);
        Check(capitulated != null && capitulated.Phase == ContractPhase.Active, "accepted capitulation creates active charter");
        Check(capitulated.Terms.Origin == ContractOrigin.Capitulation && capitulated.StartedTick == 10001, "origin and actual activation time");
        Check(capitulated.HasSigned(ContractSide.Liege) && capitulated.HasSigned(ContractSide.Subject), "verified agreement records both parties");
        Check(siege.AcceptVerifiedCapitulation(Terms(), "repeat", none, 10002) == null, "capitulation cannot create duplicate charter");
        Check(!siege.RecordVerifiedOccupation("repeat-capture", 10002), "settled battle cannot also become new occupation");
        Check(siege.SettlementEvidenceId == "accepted-exact-terms", "acceptance evidence retained");

        var occupation = Conflict("occupation");
        Check(occupation.RecordVerifiedOccupation("defeated-and-controlled", 1), "verified control records occupation");
        Check(occupation.OccupationEvidenceId == "defeated-and-controlled", "occupation evidence retained");
        Check(occupation.EstablishOccupationCharter(Terms(), "wrong-basis", none, 1) == null, "imposed charter cannot masquerade as negotiated surrender");
        var imposed = occupation.EstablishOccupationCharter(Terms(ContractOrigin.Occupation), "occupation-order", none, 2);
        Check(imposed != null && imposed.Phase == ContractPhase.Active, "occupation order establishes imposed charter");
        Check(!imposed.HasSigned(ContractSide.Subject) && !imposed.HasSigned(ContractSide.Liege), "occupation does not fabricate signatures");
        Check(imposed.Terms.Clauses.Any(c => c.Kind == ObligationKind.Protection), "occupier still owes protection");
        Check(occupation.EstablishOccupationCharter(Terms(ContractOrigin.Occupation), "repeat", none, 3) == null, "occupation establishment is idempotent");
        var noControl = new ContractLedger("unverified", Terms(ContractOrigin.Occupation));
        Check(!noControl.Sign(ContractSide.Liege, 0) && !noControl.Sign(ContractSide.Subject, 0) && noControl.Phase == ContractPhase.Proposed,
            "ordinary signature path cannot activate an occupation charter");

        var occupiedSurrender = Conflict("post-occupation");
        occupiedSurrender.RecordVerifiedOccupation("control", 0);
        Check(occupiedSurrender.AcceptVerifiedCapitulation(Terms(), "ratified-terms", none, 1) != null,
            "occupation can end through actual accepted capitulation");
        foreach (bool raze in new[] { false, true })
        {
            var ended = Conflict(raze ? "razed" : "withdrawn");
            Check(raze ? ended.RecordVerifiedRazing(1) : ended.Withdraw(1), "terminal military outcome");
            Check(!ended.RecordVerifiedOccupation("late-control", 2), "withdrawn or razed settlement cannot become occupied");
            Check(ended.AcceptVerifiedCapitulation(Terms(), "late-surrender", none, 2) == null, "no contract after terminal outcome");
        }
        var released = Conflict("same-conflict").AcceptVerifiedCapitulation(Terms(), "first", none, 0);
        released.ConsentToRelease(ContractSide.Liege, 0); released.ConsentToRelease(ContractSide.Subject, 0);
        Check(Conflict("same-conflict").AcceptVerifiedCapitulation(Terms(), "replay", new[] { released }, 1) == null,
            "fresh conflict object cannot replay an already-used conquest ID");
        var bound = Active(Terms(ContractOrigin.Negotiated, liege: "C"));
        var blocked = Conflict("already-bound");
        Check(blocked.AcceptVerifiedCapitulation(Terms(), "accepted", new[] { bound }, 0) == null, "existing charter blocks surrender binding");
        blocked.RecordVerifiedOccupation("control", 0);
        Check(blocked.EstablishOccupationCharter(Terms(ContractOrigin.Occupation), "order", new[] { bound }, 0) == null,
            "occupation cannot silently overwrite previous liege");
        Check(blocked.Phase == SettlementConflictPhase.Occupied && blocked.Contract == null, "binding conflict leaves occupation unresolved");

        foreach (var origin in Enum.GetValues<ContractOrigin>())
        foreach (var kind in Enum.GetValues<DefenseIncidentKind>())
        {
            var defense = origin == ContractOrigin.Occupation
                ? MakeOccupied(Conflict($"defense-{kind}"), Terms(origin), none)
                : Active(Terms(origin));
            DefenseCall Open(string id, string town = "town", string defender = "B", string aggressor = "C", string clause = "protect", long tick = 0)
                => DefenseCalls.TryOpen(defense, clause, id, town, defender, aggressor, kind, tick, tick);
            Check(Open("other-town", town: "other") == null, "settlement charter protects its specified settlement");
            Check(Open("other-faction", defender: "D") == null, "defender must be the contracted subject");
            Check(Open("self-attack", aggressor: "B") == null, "no self attack defense request");
            Check(Open("liege-attack", aggressor: "A") == null, "liege attack needs breach review, not self-protection quest");
            Check(Open("wrong-clause", clause: "missing") == null, "no phantom protection clause");
            var call = Open("attack-1");
            Check(call != null && call.Kind == kind && call.SettlementId == "town", "verified raid or siege opens protection call");
            Check(defense.Obligations.Single().Phase == ObligationPhase.Pending, "opening a call does not fulfill it");
            Check(Open("attack-1") == null && Open("attack-2") == null, "same incident and concurrent calls do not duplicate duty");
            Check(defense.RecordVerifiedFulfillment(call.ObligationId, "verified-relief", 1), "only later verified relief completes duty");
            Check(Open("attack-1", tick: 1) == null, "resolved event cannot be reused");
            Check(Open("attack-2", tick: 1) != null, "a later real attack within quota still creates obligation");
        }

        var expired = Active(Terms(ContractOrigin.Negotiated));
        Check(DefenseCalls.TryOpen(expired, "protect", "late", "town", "B", "C", DefenseIncidentKind.Attack, 100, 100) == null,
            "no new protection obligation after contract term");
        var faction = Active(new(ContractKind.FactionVassalage, F("A"), F("B"), 100, new[] { Protect() }));
        Check(DefenseCalls.TryOpen(faction, "protect", "raid", "another-subject-town", "B", "C", DefenseIncidentKind.Attack, 0, 0) != null,
            "whole faction protection can cover its other verified settlements");
        var personal = Active(new(ContractKind.PersonalVassalage, new(PartyKind.Pawn, "A"), new(PartyKind.Pawn, "B"), 100, new[] { Protect() }));
        Check(DefenseCalls.TryOpen(personal, "protect", "raid", "town", "B", "C", DefenseIncidentKind.Attack, 0, 0) == null,
            "personal vassal does not imply sovereignty over a faction settlement");
        var newContract = new ContractLedger("new", Terms(ContractOrigin.Negotiated));
        newContract.Sign(ContractSide.Liege, 10); newContract.Sign(ContractSide.Subject, 10);
        Check(DefenseCalls.TryOpen(newContract, "protect", "old-raid", "town", "B", "C", DefenseIncidentKind.Attack, 9, 10) == null,
            "old attack cannot silently become new contract's protection duty");
        Reject(() => DefenseCalls.TryOpen(newContract, "protect", "future-raid", "town", "B", "C", DefenseIncidentKind.Attack, 11, 10),
            "event cannot originate in the future");
        Check(DefenseCalls.TryOpen(newContract, "protect", "new-raid", "town", "B", "C", DefenseIncidentKind.Attack, 10, 10) != null,
            "attack at contract activation can trigger protection");
        Reject(() => new SettlementConflict("war", "town", F("A"), F("A"), 0), "cannot conquer own settlement");
        Reject(() => new SettlementConflict("war", "town", new(PartyKind.Pawn, "A"), F("B"), 0), "combat scope requires factions");
        Reject(() => new ContractTerms(ContractKind.FactionVassalage, F("A"), F("B"), 100,
            new[] { Protect() }, origin: ContractOrigin.Capitulation), "single settlement capitulation cannot bind a whole faction");
        Reject(() => new ContractTerms(ContractKind.PersonalVassalage, new(PartyKind.Pawn, "A"), new(PartyKind.Pawn, "B"), 100,
            new[] { Protect() }, origin: ContractOrigin.Occupation), "occupation cannot force a personal oath");
        Reject(() => new ContractTerms(ContractKind.SettlementCharter, F("A"), F("B"), 100,
            new[] { Protect() }, "town", new(true, false, true), (ContractOrigin)999), "unknown contract origin");
        Reject(() => siege.Withdraw(0), "military time cannot run backward");
        Console.WriteLine($"PASS: {count} military contract assertions (capitulation, occupation authority, settlement scope, attack/siege protection calls).");
        return count;
    }

    static ContractLedger MakeOccupied(SettlementConflict conflict, ContractTerms terms, ContractLedger[] existing)
    {
        conflict.RecordVerifiedOccupation("control", 0);
        return conflict.EstablishOccupationCharter(terms, "order", existing, 0);
    }
}
