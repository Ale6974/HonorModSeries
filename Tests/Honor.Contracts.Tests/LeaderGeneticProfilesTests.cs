using System.Text.Json;
using Honor.Contracts.Rules;

internal static class LeaderGeneticProfilesTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Leader genetics: " + name); }
        void Reject(Action a, string name) { bool bad = false; try { a(); } catch (ArgumentException) { bad = true; } Check(bad, name); }
        string basic = new('A', 64), stock = new('B', 64);
        var genes = new[] { new ResidentGene("gene1", "body", false, "gene2", 1), new ResidentGene("gene2", "hair", true, null, null) };
        string genetics = ResidentGenetics.Capture("leader", "type", "original", null, true, genes);
        string fp = ResidentMoveProfiles.Compose(basic, basic, 7, basic, basic, genetics);
        string oldFp = ResidentMoveProfiles.Compose(basic, basic, 5, basic, basic);
        Check(LeaderPreservationProfiles.Resolve(0) == 5 && LeaderPreservationProfiles.Resolve(7) == 7, "old zero marker stays profile5 while new7 is explicit");
        Reject(() => LeaderPreservationProfiles.Resolve(5), "an explicit unknown marker is not normalized to old omitted state");
        Check(fp != oldFp && ResidentMoveProfiles.Compose(basic, basic, 7, basic, basic, ResidentGenetics.None) != oldFp, "even a gene-free new proof cannot be substituted for an old profile");
        string changed = ResidentMoveProfiles.Compose(basic, basic, 7, basic, basic,
            ResidentGenetics.Capture("leader", "type", "changed", null, true, genes));
        Check(fp != changed, "original raw genetic metadata is part of actual leader preservation");
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("territory/src", "source", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        book.Offer("c", new("p", "territory/src", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("c", true, 0, obs); book.Sign("c", false, 0, obs);
        var ledger = new TerritoryTransferLedger();
        var legacy = new LeaderJoinPlan("c", "leader", "source", "target", "src", "dst", "steward", book.Export(), ledger.Export(),
            new("leader", basic, 4), stock, stock, ["leader", "citizen"], ["other"], 10, true, SeparateSuccession: true);
        var legacyJournal = LeaderJoinExecution.Prepare(legacy);
        string Hash(string s) { using var sha = System.Security.Cryptography.SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s))).Replace("-", ""); }
        Check(Hash(legacyJournal.Export()) == "2A8F975393917CFBF47B5198FACFB9F754A0F98CD55710476A044D0CA4324728", "published0.3.55 old join receipt stays byte-identical on both hosts");
        Check(!legacyJournal.Export().Contains("ResidentProfile") && legacy.EffectiveResidentProfile == 5 && LeaderJoinExecution.Restore(legacyJournal.Export()).Export() == legacyJournal.Export(), "old joining JSON does not gain a new profile or computed property");
        var plan = legacy with { Actor = new("leader", fp, 4), ResidentProfile = 7 };
        var j = LeaderJoinExecution.Prepare(plan); var actor = new ResidentMoveActual("leader", "source", fp, 4);
        LeaderJoinAction Review(LeaderJoinExecution x, ResidentMoveActual a, string ruler, long now, string[] src = null, string[] dst = null) =>
            x.Review(a, ruler, "steward", book.Export(), ledger.Export(), stock, stock, src ?? plan.SourceRoster.ToArray(), dst ?? plan.DestinationRoster.ToArray(), now);
        Check(j.State.Plan.EffectiveResidentProfile == 7 && Review(j, actor, "leader", 10) == LeaderJoinAction.ChangeOffice, "new joining enters existing separate succession path with explicit genetic evidence");
        Reject(() => LeaderJoinExecution.Prepare(plan with { ResidentProfile = 6 }), "unknown saved leader profiles rejected");
        Reject(() => LeaderJoinExecution.Prepare(plan with { SeparateSuccession = false }), "new genetic joins cannot use old combined succession/affiliation calls");
        Reject(() => Review(j, actor with { Preserved = changed }, "leader", 10), "changed genes block before actual office writes rather than being repaired");
        Reject(() => Review(j, actor with { Preserved = oldFp }, "leader", 10), "old fingerprint cannot confirm a new genetic plan");
        var restored = LeaderJoinExecution.Restore(j.Export());
        Check(restored.State.Plan.ResidentProfile == 7 && restored.State.Plan.Actor.Preserved == fp, "saved profile and original genetic fingerprint survive explicit recovery");
        j.BeginSuccession(11); j.RecordSuccessionReturn("successor", 12);
        j.ObserveSuccession(Review(j, actor, "successor", 12), "successor", 12);
        j.Begin(13); j.RecordReturn("successor", 2, 14);
        var target = actor with { Faction = "target", MedicalCare = 2 };
        j.Observe(Review(j, target, "successor", 14), "successor", 2, 14);
        j.SealCare(Review(j, target, "successor", 14), 14); target = target with { MedicalCare = 4 };
        Reject(() => Review(j, target with { Preserved = changed }, "successor", 14), "changed genes after native return cannot be reset to finish care or roster recovery");
        Check(Review(j, target, "successor", 14) == LeaderJoinAction.AddDestination, "one-shot original care restoration still precedes identity-preserving residency");
        Check(Review(j, target, "successor", 14, dst: ["other", "leader"]) == LeaderJoinAction.RemoveSource, "actual same actor can recover a double-reference roster state");
        j.Finish(Review(j, target, "successor", 14, ["citizen"], ["other", "leader"]), 14);
        Check(LeaderJoinExecution.Restore(j.Export()).State.Phase == LeaderJoinPhase.Completed && j.State.Plan.Actor.Preserved == fp && j.State.Succession.FromNormalReturn,
            "full separate source succession/affiliation/care/roster completion retains original genetics and native return attribution");
        var relocationActor = new ResidentMoveActual("leader", "native", fp, 2);
        var oldRelocation = LeaderRelocationPlan.Capture("charter", "leader", "native", "source", "destination", "book", relocationActor, stock, 0, stock, 1, ["leader", "resident"], ["other"], 10);
        var relocation = LeaderRelocationPlan.CaptureProfiled("charter", "leader", "native", "source", "destination", "book", relocationActor, stock, 0, stock, 1, ["leader", "resident"], ["other"], 10, 7);
        Check(Hash(oldRelocation.Export()) == "EC7C133C6179E955E59DC38F956F3712637E1D8EACECA34267CD632ACB55062A", "published0.3.55 old relocation proof and bytes remain unchanged");
        Check(oldRelocation.EffectiveResidentProfile == 5 && !oldRelocation.Export().Contains("ResidentProfile") && relocation.Proof != oldRelocation.Proof,
            "new relocation marker is included in its proof while old data bytes omit it");
        Check(LeaderRelocationPlan.Restore(relocation.Export()).EffectiveResidentProfile == 7, "relocation recovery uses its saved profile");
        Reject(() => LeaderRelocationPlan.Restore(relocation.Export().Replace("\"ResidentProfile\":7", "\"ResidentProfile\":0")), "removing relocation genetic profile changes proof and cannot silently downgrade recovery");
        LeaderRelocationAction Move(ResidentMoveActual a, string[] src, string[] dst) => relocation.Review(a, "leader", "native", "native", "book", stock, 0, stock, 1, src, dst);
        Check(Move(relocationActor, ["leader", "resident"], ["other"]) == LeaderRelocationAction.AddDestination &&
            Move(relocationActor, ["leader", "resident"], ["other", "leader"]) == LeaderRelocationAction.RemoveSource &&
            Move(relocationActor, ["resident"], ["other", "leader"]) == LeaderRelocationAction.Verify, "same-faction genetic relocation preserves office and actor through each actual roster phase");
        Reject(() => Move(relocationActor with { Preserved = changed }, ["leader", "resident"], ["other"]), "relocation never repairs changed genetic metadata");
        Check(book.Charter("c").Phase == RecordedCharterPhase.Active && ledger.Transfers.Count == 0, "neither personal agreement supplies town ownership/merger authority or rewrites the charter");
        return count;
    }
}
