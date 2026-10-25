using System.Text.Json;
using Honor.Contracts.Rules;

internal static class ResidentMoveTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        void Reject(Action run, string name) { bool caught = false; try { run(); } catch (ArgumentException) { caught = true; } Check(caught, name); }
        string a = new('A', 64), b = new('B', 64);
        ResidentMoveEvidence[] before = [new("pawn-a", a, 2), new("pawn-b", b, null)];
        var set = ResidentMoveSet.Capture(before);
        Check(set.Proof == ResidentMoveSet.Capture(before.AsEnumerable().Reverse()).Proof, "stable resident identity order");
        before[0] = before[0] with { MedicalCare = 4 };
        Check(set.Residents[0].MedicalCare == 2, "input list cannot rewrite saved intent");
        Check(ResidentMoveSet.Restore(set.Export()).Export() == set.Export(), "execution snapshot roundtrip");
        ResidentMoveActual[] Source() => set.Residents.Select(r => new ResidentMoveActual(r.Id, "source", r.Preserved, r.MedicalCare)).ToArray();
        var current = Source();
        Check(set.Review(current.AsEnumerable().Reverse(), "source", "target", false).All(a => a == ResidentMoveAction.ChangeFaction), "fresh exact source set authorizes native changes");
        for (int mask = 0; mask < 4; mask++)
        {
            var partial = current.Select((r, i) => (mask & (1 << i)) != 0 ? r with { Faction = "target" } : r).ToArray();
            var actions = set.Review(partial, "source", "target", true);
            for (int i = 0; i < 2; i++) Check(actions[i] == ((mask & (1 << i)) != 0 ? ResidentMoveAction.Verify : ResidentMoveAction.ChangeFaction), "partial recovery never refactions an already migrated resident");
            if (mask != 3) Reject(() => set.VerifyDestination(partial, "source", "target"), "partial migration cannot complete");
            else { set.VerifyDestination(partial, "source", "target"); Check(true, "all actual target residents verify"); }
        }
        for (int care = 0; care <= 4; care++)
        {
            var changed = current.Select(r => r with { Faction = "target" }).ToArray(); changed[0] = changed[0] with { MedicalCare = care };
            var action = set.Review(changed, "source", "target", true)[0];
            Check(action == (care == 2 ? ResidentMoveAction.Verify : ResidentMoveAction.RestoreMedicalCare), "only target medical reset can be repaired");
            if (care != 2)
            {
                Reject(() => set.Review(changed, "source", "target", false), "medical repair needs executing/recovery authority");
                Reject(() => set.VerifyDestination(changed, "source", "target"), "wrong medical care cannot complete");
                Reject(() => set.Review(current.Select(r => r.Id == "pawn-a" ? r with { MedicalCare = care } : r), "source", "target", true), "source-side clinical changes are not an authorized repair");
            }
        }
        foreach (var row in new[] { current[0] with { Id = "substitute" }, current[0] with { Faction = "third-party" },
            current[0] with { Preserved = b }, current[0] with { MedicalCare = null }, current[0] with { MedicalCare = 99 } })
            Reject(() => set.Review([row, current[1]], "source", "target", true), "do not heal/replace/refaction altered residents");
        Reject(() => set.Review(current.Take(1), "source", "target", true), "missing resident is not death proof or permission");
        Reject(() => set.Review(current.Append(current[0]), "source", "target", true), "additional resident blocks");
        Reject(() => set.Review([current[0], current[0]], "source", "target", true), "duplicate resident blocks");
        Reject(() => set.Review(current, "source", "source", true), "same source target invalid");
        Reject(() => ResidentMoveSet.Capture([]), "empty set cannot waive resident guard");
        Reject(() => ResidentMoveSet.Capture([new("pawn", "invalid", 0)]), "bad preservation proof rejected");
        Reject(() => ResidentMoveSet.Capture([new("pawn", a, -1)]), "bad medical enum rejected");
        Reject(() => ResidentMoveSet.Capture(Enumerable.Range(0, 129).Select(i => new ResidentMoveEvidence("p" + i, a, null))), "execution capacity bounded");
        Reject(() => ResidentMoveSet.Restore(set.Export().Replace(set.Proof, a)), "altered saved proof rejected");
        Reject(() => ResidentMoveSet.Restore(set.Export().Replace("\"Version\":1", "\"Version\":2")), "unknown schema rejected");
        var book = new CharterBook(); book.Register(new("player", "native", true), 0); book.Register(new("town", "source", false), 0);
        TerritoryObservation[] obs = [new("player", "native", true, true, true), new("town", "source", true, false, true)];
        book.Offer("bond", new("player", "town", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, .1m), 0, PoliticalWorld.Day, obs);
        book.Sign("bond", true, 0, obs); book.Sign("bond", false, 0, obs);
        var source = new TerritoryTransferObservation("town", "source", 2, Visited: true, HasInhabitants: true, StockProof: a, ResidentProof: set.Proof);
        var ledger = new TerritoryTransferLedger(); ledger.Prepare("move", book, "bond", source, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 1);
        Check(ledger.Transfer("move").ResidentProof == set.Proof, "physical intent binds resident and stock proofs separately");
        foreach (var changed in new[] { source with { Owner = "target", ResidentProof = b }, source with { Owner = "target", HasInhabitants = false },
            source with { Owner = "target", ResidentProof = null }, source with { Owner = "target", StockProof = b }, source with { Owner = "target", HasMap = true } })
            Reject(() => ledger.Confirm("move", changed, 2), "incomplete physical or resident preservation cannot confirm");
        Check(ledger.Transfer("move").Phase == TerritoryTransferPhase.Prepared, "failed completion retains prepared intent");
        Check(TerritoryTransferLedger.Restore(ledger.Export()).Export() == ledger.Export(), "pending resident physical intent roundtrip");
        ledger.Confirm("move", source with { Owner = "target" }, 2);
        Check(ledger.Transfer("move").Phase == TerritoryTransferPhase.Applied, "actual destination plus both proofs complete");
        var legacy = new TerritoryTransferLedger(); legacy.Prepare("old", book, "bond", source with { HasInhabitants = false, ResidentProof = null }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 1);
        Check(!legacy.Export().Contains("ResidentProof"), "old journal JSON shape unchanged");
        Reject(() => new TerritoryTransferLedger().Prepare("bypass", book, "bond", source with { ResidentProof = null }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 1), "unproved residents remain blocked");
        Reject(() => new TerritoryTransferLedger().Prepare("phantom", book, "bond", source with { HasInhabitants = false }, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 1), "phantom resident proof cannot claim an empty town");
        Reject(() => TerritoryTransferLedger.Restore(ledger.Export().Replace(set.Proof, "invalid")), "invalid persisted resident proof rejected");
        var pendingLedger = new TerritoryTransferLedger();
        pendingLedger.Prepare("pending", book, "bond", source, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 1);
        var ordinary = obs.Select(o => o.Id == "town" ? pendingLedger.ObserveIdentity(o, "source") : o).ToArray();
        Reject(() => book.ValidateCurrent("bond", ordinary), "normal observations never normalize a pending resident town");
        var scoped = ordinary.Select(o => o.Id == "town" ? o with { NativeOwner = "source", Eligible = true } : o).ToArray();
        book.ValidateCurrent("bond", scoped); Check(true, "local verified recovery observation can validate retained consent");
        Reject(() => book.ValidateCurrent("bond", scoped.Select(o => o.Id == "player" ? o with { Eligible = false } : o)), "scoping one pending town cannot waive other connected failures");
        return count;
    }
}
