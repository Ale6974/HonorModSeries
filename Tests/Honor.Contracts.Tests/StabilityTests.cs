using Honor.Contracts.Rules;

static class StabilityTests
{
    public static int Run()
    {
        int count = 0;
        const long Day = 60000;
        void Check(bool condition, string name) { count++; if (!condition) throw new Exception(name); }
        void Near(double a, double b, string name) => Check(Math.Abs(a - b) < 1e-9, name);
        void Reject(Action action, string name)
        {
            bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, name);
        }
        PartyRef F(string id) => new(PartyKind.Faction, id);
        ContractLedger Draft(string id = "AB", string liege = "A", string subject = "B", ContractKind kind = ContractKind.SettlementCharter)
        {
            var scope = kind == ContractKind.PersonalVassalage ? PartyKind.Pawn : PartyKind.Faction;
            return new(id, new(kind, new(scope, liege), new(scope, subject), 100 * Day,
                new[] { new ObligationClause("protect", ObligationKind.Protection, ContractSide.Liege,
                    0, 5, maximumRequests: 1, responseTicks: Day) },
                kind == ContractKind.SettlementCharter ? id + "-town" : null,
                kind == ContractKind.SettlementCharter ? new(true, false, true) : null));
        }
        ContractLedger Active(string id = "AB", string liege = "A", string subject = "B")
        {
            var c = Draft(id, liege, subject); c.Sign(ContractSide.Liege, 0); c.Sign(ContractSide.Subject, 0);
            return c;
        }
        var a = new FactionHonorLedger(F("A"), .75);
        var b = new FactionHonorLedger(F("B"), .75);
        var ab = Active();
        var stability = new ContractStability(ab, a, b, 0);
        stability.Advance(Day);
        Check(stability.Phase == ContractStabilityPhase.Stable && stability.SecessionPressure == 0, "trusted honorable liege has no passive secession pressure");
        var betrayal = a.RecordVerifiedEvent("betrayal", -.75, Day);
        Check(stability.ApplyFactionHonorChange(betrayal), "liege dishonor reaches its direct contract");
        Near(stability.SubjectTrustInLiege, 0, "subject trust drops immediately");
        Near(stability.LiegeTrustInSubject, .75, "innocent subject is not blamed for liege's deed");
        Near(stability.SecessionPressure, 0, "today's dishonor is not applied retroactively over yesterday");
        Check(!stability.ApplyFactionHonorChange(betrayal), "same revision cannot lower trust twice");
        Check(a.RecordVerifiedEvent("betrayal", -.75, 0) == null && a.Revision == 1, "duplicate historical deed is ignored even when replayed late");
        stability.Advance(21 * Day);
        Check(stability.SecessionPressure == 1 && stability.Phase == ContractStabilityPhase.WarningNeeded,
            "large update accumulates pressure but cannot skip a real warning");
        Check(ab.Phase == ContractPhase.Active && ab.BreachingSide == null, "unrest does not automatically break contract or assign guilt");
        Check(stability.RecordVerifiedNoticeDelivered("warning", 21 * Day), "record actual delivered warning");
        Check(!stability.RecordVerifiedNoticeDelivered("warning-copy", 21 * Day), "warning is not repeatedly reissued");
        Check(stability.Phase == ContractStabilityPhase.NoticeGiven, "no immediate secession at delivery");
        stability.Advance(24 * Day - 1);
        Check(stability.Phase == ContractStabilityPhase.NoticeGiven, "grace is not over one tick early");
        stability.Advance(24 * Day);
        Check(stability.Phase == ContractStabilityPhase.SecessionReady, "persisting unrest becomes eligible exactly after grace");
        Check(ab.Phase == ContractPhase.Active, "eligibility remains separate from actual political exit");

        var repair = a.RecordVerifiedEvent("reparation", 1, 24 * Day);
        Check(stability.ApplyFactionHonorChange(repair), "verified restitution begins slow trust recovery");
        Near(stability.SubjectTrustInLiege, .25, "trust repairs more slowly than score");
        var cappedRepair = a.RecordVerifiedEvent("protection-kept", 1, 24 * Day);
        Check(cappedRepair.ActualDelta == 0 && stability.ApplyFactionHonorChange(cappedRepair), "real deed still repairs trust at honor ceiling");
        Near(stability.SubjectTrustInLiege, .5, "capped honor is not a permanent block on trust recovery");
        Near(stability.SecessionPressure, 1, "good deed does not instantly erase accumulated unrest");
        stability.Advance(69 * Day);
        Check(stability.NoticeDeliveredTick == null && stability.Phase == ContractStabilityPhase.Restless, "sustained recovery clears warning with hysteresis");
        stability.Advance(79 * Day);
        Near(stability.SecessionPressure, 0, "unrest can fully recover");
        var renewedLoss = a.RecordVerifiedEvent("new-betrayal", -1, 79 * Day);
        Check(stability.ApplyFactionHonorChange(renewedLoss), "new verified misconduct can cause new unrest");
        stability.Advance(89 * Day);
        Check(stability.Phase == ContractStabilityPhase.WarningNeeded, "new unrest requires a new warning");
        Check(!stability.RecordVerifiedNoticeDelivered("warning", 89 * Day), "old notice cannot be reused to bypass new grace");
        Check(stability.RecordVerifiedNoticeDelivered("warning-2", 89 * Day), "new actual notice starts a new grace");
        stability.Advance(100 * Day);
        Check(stability.Phase == ContractStabilityPhase.Closed && ab.Phase == ContractPhase.Expired, "expired contract no longer produces secession decisions");
        Check(!stability.RecordVerifiedNoticeDelivered("expired-warning", 100 * Day), "closed contract cannot issue warning");

        var moderateA = new FactionHonorLedger(F("A"), .75);
        var severeA = new FactionHonorLedger(F("A"), .75);
        var moderate = new ContractStability(Active("moderate"), moderateA, b, 0);
        var severe = new ContractStability(Active("severe"), severeA, b, 0);
        moderate.ApplyFactionHonorChange(moderateA.RecordVerifiedEvent("loss", -.5, 0));
        severe.ApplyFactionHonorChange(severeA.RecordVerifiedEvent("loss", -.75, 0));
        moderate.Advance(5 * Day); severe.Advance(5 * Day);
        Check(severe.SecessionPressure > moderate.SecessionPressure && moderate.SecessionPressure > 0,
            "worse honor and trust accelerate territorial exit pressure");

        var root = new FactionHonorLedger(F("root"), .75);
        var vassal = new FactionHonorLedger(F("vassal"), .75);
        var sub = new FactionHonorLedger(F("sub"), .75);
        var main = new ContractStability(Active("main", "root", "vassal"), root, vassal, 0);
        var downstream = new ContractStability(Active("downstream", "vassal", "sub"), vassal, sub, 0);
        var rootLoss = root.RecordVerifiedEvent("root-loss", -.5, 0);
        Check(main.ApplyFactionHonorChange(rootLoss) && !downstream.ApplyFactionHonorChange(rootLoss), "no automatic transitive collective guilt");
        var vassalLoss = vassal.RecordVerifiedEvent("vassal-loss", -.5, 0);
        Check(main.ApplyFactionHonorChange(vassalLoss) && downstream.ApplyFactionHonorChange(vassalLoss), "faction's deed reaches each actual contractual role");
        Near(main.LiegeTrustInSubject, .25, "subject misconduct reduces liege's trust");
        Near(main.SubjectTrustInLiege, .25, "subject misconduct does not reverse or duplicate earlier liege loss");
        Near(downstream.SubjectTrustInLiege, .25, "same faction as liege loses its own subjects' trust");

        var subjectOnlyA = new FactionHonorLedger(F("A"), .75);
        var subjectOnlyB = new FactionHonorLedger(F("B"), .75);
        var subjectOnly = new ContractStability(Active("subject"), subjectOnlyA, subjectOnlyB, 0);
        subjectOnly.ApplyFactionHonorChange(subjectOnlyB.RecordVerifiedEvent("own-fraud", -.75, 0));
        subjectOnly.Advance(10 * Day);
        Near(subjectOnly.SecessionPressure, 0, "subject's own misconduct does not manufacture grievance against honorable liege");
        var outsider = new FactionHonorLedger(F("outsider"));
        Check(!subjectOnly.ApplyFactionHonorChange(outsider.RecordVerifiedEvent("foreign", -.5, 10 * Day)), "unrelated faction cannot affect a contract");

        var commonLiege = new FactionHonorLedger(F("common"), .75);
        var firstSubject = new FactionHonorLedger(F("first"), .75);
        var secondSubject = new FactionHonorLedger(F("second"), .75);
        var firstTerritory = new ContractStability(Active("first-territory", "common", "first"), commonLiege, firstSubject, 0);
        var secondTerritory = new ContractStability(Active("second-territory", "common", "second"), commonLiege, secondSubject, 0);
        var publicBetrayal = commonLiege.RecordVerifiedEvent("public-betrayal", -.75, 0);
        Check(firstTerritory.ApplyFactionHonorChange(publicBetrayal) && secondTerritory.ApplyFactionHonorChange(publicBetrayal),
            "one public deed can lower all directly affiliated territories' trust");
        firstTerritory.Advance(Day); secondTerritory.Advance(Day);
        Check(firstTerritory.SecessionPressure > 0 && secondTerritory.SecessionPressure > 0,
            "both affiliated territories accrue exit pressure");
        Check(!firstTerritory.ApplyFactionHonorChange(publicBetrayal), "fanout does not permit duplicate application to same contract");

        var orderA = new FactionHonorLedger(F("A"), .75);
        var ordered = new ContractStability(Active("ordered"), orderA, b, 0);
        var first = orderA.RecordVerifiedEvent("first", -.25, 0);
        var second = orderA.RecordVerifiedEvent("second", -.25, 0);
        Check(!ordered.ApplyFactionHonorChange(second), "missing revision cannot silently be skipped");
        Check(ordered.ApplyFactionHonorChange(first) && ordered.ApplyFactionHonorChange(second), "ordered replay of verified missing events");
        var lateSnapshot = new ContractStability(Active("late-snapshot"), orderA, b, 0);
        Near(lateSnapshot.SubjectTrustInLiege, .25, "new treaty cannot launder existing low faction honor");
        Check(!lateSnapshot.ApplyFactionHonorChange(first), "snapshot does not replay old deeds twice");

        var weak = new FactionHonorLedger(F("A"), .2);
        var single = new ContractStability(Active("single"), weak, b, 0);
        var many = new ContractStability(Active("many"), weak, b, 0);
        single.Advance(5 * Day);
        for (long tick = 1000; tick <= 5 * Day; tick += 1000) many.Advance(tick);
        Near(single.SecessionPressure, many.SecessionPressure, "pressure is invariant to polling frequency");
        var released = Active("released");
        var releasedState = new ContractStability(released, weak, b, 0);
        released.ConsentToRelease(ContractSide.Liege, 0); released.ConsentToRelease(ContractSide.Subject, 0);
        releasedState.Advance(Day);
        Check(releasedState.Phase == ContractStabilityPhase.Closed && releasedState.SecessionPressure == 0, "mutual release stops pressure");

        var clamp = new FactionHonorLedger(F("clamp"), .5);
        Near(clamp.RecordVerifiedEvent("down", -1, 0).After, 0, "faction honor lower clamp");
        Near(clamp.RecordVerifiedEvent("up", 1, 0).After, 1, "faction honor upper clamp");
        Check(clamp.History.Count == 2 && clamp.Revision == 2, "verified history and revision");
        Reject(() => new FactionHonorLedger(new(PartyKind.Pawn, "pawn")), "personal Need is not faction honor");
        Reject(() => new FactionHonorLedger(F("bad"), double.NaN), "invalid initial faction honor");
        Reject(() => clamp.RecordVerifiedEvent("bad", double.PositiveInfinity, 0), "invalid event delta");
        Reject(() => clamp.RecordVerifiedEvent("bad", -2, 0), "unbounded event delta");
        Reject(() => clamp.RecordVerifiedEvent("old", .1, -1), "honor time cannot reverse");
        Reject(() => new ContractStabilityPolicy(warningThreshold: .9, exitThreshold: .8), "warning must precede exit threshold");
        Reject(() => new ContractStabilityPolicy(noticeGraceTicks: 0), "exit needs a real response period");
        Reject(() => new ContractStabilityPolicy(pressurePerDay: double.NaN), "invalid pressure rate");
        Reject(() => new ContractStability(Draft(), orderA, b, 0), "proposal is not a territorial binding");
        Reject(() => new ContractStability(Active("wrong"), outsider, b, 0), "actual faction parties required");
        Reject(() => new ContractStability(Active("future"), new(F("A"), .5, Day), b, 0), "future faction snapshot cannot rewrite earlier history");
        Reject(() => many.Advance(0), "stability time cannot reverse");
        Console.WriteLine($"PASS: {count} faction/stability assertions (public honor, directional trust, acceleration, ordered events, warnings, recovery).");
        return count;
    }
}
