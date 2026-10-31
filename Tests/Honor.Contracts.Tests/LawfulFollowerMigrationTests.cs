using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;

internal static class LawfulFollowerMigrationTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new Exception("Lawful follower migration: " + why); }
        void Reject(Action f, string why) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad, why); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("n", "realm", false), 0); book.Register(new("town", "realm", false), 0); book.Register(new("town2", "realm", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string l, string s) => new(l, s, 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0);
        book.Offer("c", Terms("n", "p"), 0, PoliticalWorld.Day, obs); book.Sign("c", true, 0, obs); book.Sign("c", false, 0, obs);
        book.Offer("child", Terms("p", "town"), 0, PoliticalWorld.Day, obs); book.Sign("child", true, 0, obs); book.Sign("child", false, 0, obs);
        book.OfferNpc("deep", Terms("town", "town2"), new("lord1", "lord2"), 0, PoliticalWorld.Day, obs);
        book.SignNpc("deep", "town", "lord1", 0, obs); book.SignNpc("deep", "town2", "lord2", 0, obs);
        var politics = new PlayerAllegiance("player"); politics.Apply(new(AllegianceAction.BindLiege, 0, "realm", "c", Consent: true));
        var payments = new TributeLedger(); payments.Enable(book, "c", 0); var physical = new TerritoryTransferLedger(); long now = PoliticalWorld.Day + 10;
        var d = new NpcRealmDecisionEvidence("realm", "ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var aid = new CharterAidPlan("refusal", "c", "realm", "ruler", "map", ["enemy"], ["enemy"], now, Hash(book.Export()), Hash(politics.Export()), d);
        var protection = new CharterProtection().Agree(book, "c", d, 75, true, 1).Refuse(book, aid, d, true, "Honor_Vile", 1, 120, now);
        var closure = LawfulExitPreparation.Prepare(book, payments, politics, physical, protection, "refusal", now, obs);
        var after = CharterBook.Restore(closure.AfterBook); var afterPayments = TributeLedger.Restore(closure.AfterPayments);
        var actualTown = new TerritoryTransferObservation("town", "realm", 3);
        var actualDeep = new TerritoryTransferObservation("town2", "realm", 3);
        var plan = FollowerTransfers.Review(after, physical, [new("child", actualTown), new("deep", actualDeep)], "union", now);
        Reject(() => FollowerTransfers.Review(after, physical, [new("child", actualTown)], "union", now), "a deeper original follower cannot be omitted from consent or migration");
        Reject(() => FollowerTransfers.Review(after, physical, [new("child", actualTown with { SourceSettlements = 2 }), new("deep", actualDeep with { SourceSettlements = 2 })], "union", now),
            "cumulative full-graph planning cannot remove the original realm's last base");
        var command = new AllegianceCommand(AllegianceAction.JoinUnion, now, "union", Consent: true, NoActiveFeudalContract: true, OldContractReleased: true);
        var residentPlan = FollowerTransfers.Review(after, physical, [new("child", actualTown with { HasInhabitants = true, ResidentProof = new string('A', 64) }), new("deep", actualDeep)], "union", now);
        var residentPeace = UnionPeaceAdmission.Review("union", "player", "chief", now, 75, ["seat"], residentPlan.Moves);
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulFollowerAdmission("resident", politics, command, residentPlan, physical, closure, residentPeace), "saved residents require independently journaled medical reset evidence");
        var residentSet = ResidentMoveSet.Capture([new("pawn-a", Hash("preserved-a"), 4), new("pawn-b", Hash("preserved-b"), null)]);
        var residentTown = actualTown with { HasInhabitants = true, ResidentProof = residentSet.Proof };
        var liveResidentPlan = FollowerTransfers.Review(after, physical, [new("child", residentTown), new("deep", actualDeep)], "union", now);
        var liveResidentPeace = UnionPeaceAdmission.Review("union", "player", "chief", now, 75, ["seat"], liveResidentPlan.Moves);
        var census = ResidentTransferCensus.Prepare("town", "realm", "union", 7, residentSet);
        var residentJournal = new AllegianceTransitionLedger();
        residentJournal.PrepareLawfulResidentAdmission("residents", politics, command, liveResidentPlan, physical, closure, liveResidentPeace, [new("child", census)]);
        Check(residentJournal.Current.LawfulResidents != null && AllegianceTransitionLedger.Restore(residentJournal.Export()).Export() == residentJournal.Export(), "original residents and medical census survive full political save/restore");
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulResidentAdmission("missing", politics, command, liveResidentPlan, physical, closure, liveResidentPeace, []), "missing original resident town cannot be admitted");
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulResidentAdmission("wrong", politics, command, liveResidentPlan, physical, closure, liveResidentPeace,
            [new("child", ResidentTransferCensus.Prepare("town", "realm", "foreign", 7, residentSet))]), "original census destination cannot differ from council admission");
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulResidentAdmission("profile", politics, command, liveResidentPlan, physical, closure, liveResidentPeace,
            [new("child", ResidentTransferCensus.Prepare("town", "realm", "union", 6, residentSet))]), "new legal mode requires exact complete medical preservation profile");
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulResidentAdmission("duplicate", politics, command, liveResidentPlan, physical, closure, liveResidentPeace,
            [new("child", census), new("child", census)]), "original town cannot be duplicated");
        var duplicatePlan = FollowerTransfers.Review(after, physical, [new("child", residentTown), new("deep", actualDeep with { HasInhabitants = true, ResidentProof = residentSet.Proof })], "union", now);
        var duplicatePeace = UnionPeaceAdmission.Review("union", "player", "chief", now, 75, ["seat"], duplicatePlan.Moves);
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulResidentAdmission("shared", politics, command, duplicatePlan, physical, closure, duplicatePeace,
            [new("child", census), new("deep", ResidentTransferCensus.Prepare("town2", "realm", "union", 7, residentSet))]), "a pawn cannot be assigned to two independently reviewed follower towns");
        var changedCare = ResidentMoveSet.Capture([new("pawn-a", Hash("preserved-a"), 1), new("pawn-b", Hash("preserved-b"), null)]);
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulResidentAdmission("care", politics, command, liveResidentPlan, physical, closure, liveResidentPeace,
            [new("child", ResidentTransferCensus.Prepare("town", "realm", "union", 7, changedCare))]), "original medical settings cannot be replaced while preserving prior council review");
        var residentSave = JsonSerializer.Deserialize<AllegianceTransitionSave>(residentJournal.Export());
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(residentSave with { Current = residentSave.Current with { LawfulResidents = null } })), "new resident authority cannot masquerade as legacy empty-town mode");
        var residentPlayer = PlayerAllegiance.Restore(politics.Export()); residentPlayer.Apply(command); residentJournal.ConfirmAllegiance(residentPlayer, now);
        var residentPhysical = TerritoryTransferLedger.Restore(physical.Export());
        residentPhysical.Prepare("resident-move", after, "child", residentTown, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, now);
        LawfulAdmissions.ValidateCurrentPhysical(residentJournal.Current, residentPhysical);
        LawfulAdmissions.ValidateArchive([residentJournal.Export()], after, afterPayments, protection);
        var residentPhysicalSave = JsonSerializer.Deserialize<TerritoryTransfersSave>(residentPhysical.Export());
        var wrongResidents = TerritoryTransferLedger.Restore(JsonSerializer.Serialize(residentPhysicalSave with { Transfers = [residentPhysicalSave.Transfers[0] with { ResidentProof = Hash("other-residents") }] }));
        Reject(() => LawfulAdmissions.ValidateCurrentPhysical(residentJournal.Current, wrongResidents), "prepared custody cannot swap original resident evidence");
        residentPhysical.Confirm("resident-move", residentTown with { Owner = "union" }, now);
        LawfulAdmissions.ValidateCurrentPhysical(residentJournal.Current, residentPhysical);
        residentPhysical.Prepare("resident-deep", after, "deep", actualDeep with { SourceSettlements = 2 }, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, now);
        residentPhysical.Confirm("resident-deep", actualDeep with { Owner = "union", SourceSettlements = 2 }, now);
        var residentFinal = FollowerTransfers.Review(after, residentPhysical, [new("child", residentTown with { Owner = "union", SourceSettlements = 2 }), new("deep", actualDeep with { Owner = "union", SourceSettlements = 2 })], "union", now);
        residentJournal.ReaffirmPeace(liveResidentPeace, now); residentJournal.Complete(residentPlayer, residentFinal, now);
        LawfulAdmissions.ValidateArchive([AllegianceTransitionLedger.Restore(residentJournal.Export()).Export()], after, afterPayments, protection);
        Check(residentJournal.Current.Phase == AllegianceTransitionPhase.Completed && residentJournal.Current.LawfulResidents == residentSave.Current.LawfulResidents && afterPayments.Account("c").ClosedTick == now,
            "actual resident migration completion retains original census, full descendant scope and debt cutoff across restore");
        var clinicalSet = ResidentMoveSet.Capture([new("patient", ResidentMoveProfiles.BindMobility(Hash("original-health-gear-needs"), Hash("original-status"), true), 4)]);
        var clinicalTown = actualTown with { HasInhabitants = true, ResidentProof = clinicalSet.Proof };
        var clinicalPlan = FollowerTransfers.Review(after, physical, [new("child", clinicalTown), new("deep", actualDeep)], "union", now);
        var clinicalPeace = UnionPeaceAdmission.Review("union", "player", "chief", now, 75, ["seat"], clinicalPlan.Moves);
        var clinicalJournal = new AllegianceTransitionLedger();
        clinicalJournal.PrepareLawfulResidentAdmission("clinical", politics, command, clinicalPlan, physical, closure, clinicalPeace,
            [new("child", ResidentTransferCensus.Prepare("town", "realm", "union", 8, clinicalSet))]);
        var clinicalRestored = AllegianceTransitionLedger.Restore(clinicalJournal.Export());
        Check(clinicalRestored.Export() == clinicalJournal.Export() && JsonSerializer.Deserialize<LawfulResidentScope[]>(clinicalRestored.Current.LawfulResidents).Length == 1,
            "new clinical profile binds original patient/medical evidence to complete lawful scope and survives political restore");
        var originalAreas = ResidentAreas.Prepare("restricted", [new("map-original", "area-original")]);
        var restrictedSet = ResidentMoveSet.Capture([new("restricted", ResidentMoveProfiles.BindAreas(Hash("clinical-original"), ResidentAreas.Proof(originalAreas)), 4)]);
        var restrictedTown = actualTown with { HasInhabitants = true, ResidentProof = restrictedSet.Proof };
        var restrictedPlan = FollowerTransfers.Review(after, physical, [new("child", restrictedTown), new("deep", actualDeep)], "union", now);
        var restrictedPeace = UnionPeaceAdmission.Review("union", "player", "chief", now, 75, ["seat"], restrictedPlan.Moves);
        var restrictedJournal = new AllegianceTransitionLedger();
        restrictedJournal.PrepareLawfulResidentAdmission("restricted", politics, command, restrictedPlan, physical, closure, restrictedPeace,
            [new("child", ResidentTransferCensus.Prepare("town", "realm", "union", 9, restrictedSet))]);
        Check(AllegianceTransitionLedger.Restore(restrictedJournal.Export()).Export() == restrictedJournal.Export(), "area-bound original clinical census survives lawful admission save/restore");
        var changedAreas = ResidentMoveSet.Capture([new("restricted", ResidentMoveProfiles.BindAreas(Hash("clinical-original"), ResidentAreas.Proof(ResidentAreas.Prepare("restricted", [new("map-new", "area-new")]))), 4)]);
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulResidentAdmission("new-areas", politics, command, restrictedPlan, physical, closure, restrictedPeace,
            [new("child", ResidentTransferCensus.Prepare("town", "realm", "union", 9, changedAreas))]), "new map/area rights cannot replace original resident restrictions in prior consent");
        var peace = UnionPeaceAdmission.Review("union", "player", "chief", now, 75, ["seat"], plan.Moves);
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulAdmission("old-api", politics, command, plan, physical, closure, peace), "old empty-scope API cannot acquire follower authority");
        var journal = new AllegianceTransitionLedger(); journal.PrepareLawfulFollowerAdmission("follow", politics, command, plan, physical, closure, peace);
        Check(!journal.Export().Contains("LawfulResidents"), "legacy empty-town mode preserves omitted null bytes");
        Check(journal.Current.LawfulFollowerMigration && journal.Current.Followers.SequenceEqual(["child", "deep"]) && plan.Moves.SequenceEqual(["child", "deep"]), "original complete direct and deeper followers bind the new mode");
        var player = PlayerAllegiance.Restore(politics.Export()); player.Apply(command); journal.ConfirmAllegiance(player, now);
        LawfulAdmissions.ValidateArchive([journal.Export()], after, afterPayments, protection);
        var active = TerritoryTransferLedger.Restore(physical.Export());
        active.Prepare("move", after, "child", actualTown, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, now);
        LawfulAdmissions.ValidateCurrentPhysical(journal.Current, active);
        Check(active.Transfer("move").Phase == TerritoryTransferPhase.Prepared, "prepared original-source call is retained for explicit physical recovery");
        var cancelled = TerritoryTransferLedger.Restore(active.Export()); cancelled.CancelUnmoved("move", actualTown, now);
        cancelled.Prepare("retry", after, "child", actualTown, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, now);
        LawfulAdmissions.ValidateCurrentPhysical(journal.Current, cancelled);
        Check(cancelled.Transfers[0].Phase == TerritoryTransferPhase.Cancelled && cancelled.Transfers[1].Phase == TerritoryTransferPhase.Prepared,
            "a positively unchanged cancellation remains in the journal before a separately reviewed retry");
        var bad = JsonSerializer.Deserialize<TerritoryTransfersSave>(active.Export());
        var changed = TerritoryTransferLedger.Restore(JsonSerializer.Serialize(bad with { Transfers = [bad.Transfers[0] with { StockProof = new string('A', 64) }] }));
        Reject(() => LawfulAdmissions.ValidateCurrentPhysical(journal.Current, changed), "altered original stock evidence blocks execution");
        changed = TerritoryTransferLedger.Restore(JsonSerializer.Serialize(bad with { Transfers = [bad.Transfers[0] with { TargetOwner = "other" }] }));
        Reject(() => LawfulAdmissions.ValidateCurrentPhysical(journal.Current, changed), "different destination is not authorized by current union consent");
        changed = TerritoryTransferLedger.Restore(JsonSerializer.Serialize(bad with { LastTick = now - 1, Transfers = [bad.Transfers[0] with { PreparedTick = now - 1 }] }));
        Reject(() => LawfulAdmissions.ValidateCurrentPhysical(journal.Current, changed), "a transfer predating the original admission cannot be relabeled as its outcome");
        active.Confirm("move", actualTown with { Owner = "union" }, now); LawfulAdmissions.ValidateCurrentPhysical(journal.Current, active);
        journal = AllegianceTransitionLedger.Restore(journal.Export());
        var remaining = FollowerTransfers.Review(after, active, [new("child", actualTown with { Owner = "union", SourceSettlements = 1 }), new("deep", actualDeep with { SourceSettlements = 2 })], "union", now);
        Check(remaining.Moves.SequenceEqual(["deep"]) && journal.MatchesSurvivingFollowers(remaining.Entries.Select(e => e.Charter)),
            "explicit recovery retains the full graph but does not replay the already applied city");
        active.Prepare("move-deep", after, "deep", actualDeep with { SourceSettlements = 2 }, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, now);
        LawfulAdmissions.ValidateCurrentPhysical(journal.Current, active);
        active.Confirm("move-deep", actualDeep with { Owner = "union", SourceSettlements = 2 }, now); LawfulAdmissions.ValidateCurrentPhysical(journal.Current, active);
        var completedPlan = FollowerTransfers.Review(after, active, [new("child", actualTown with { Owner = "union", SourceSettlements = 2 }), new("deep", actualDeep with { Owner = "union", SourceSettlements = 2 })], "union", now);
        journal.ReaffirmPeace(peace, now); journal.Complete(player, completedPlan, now);
        LawfulAdmissions.ValidateArchive([journal.Export()], after, afterPayments, protection);
        Check(journal.Current.Phase == AllegianceTransitionPhase.Completed && active.Transfer("move").Phase == TerritoryTransferPhase.Applied &&
            active.Transfer("move-deep").Phase == TerritoryTransferPhase.Applied && after.Charter("child") == book.Charter("child") &&
            after.Charter("deep") == book.Charter("deep") && physical.Transfers.Count == 0, "completion keeps original direct/deeper charters and physical ledger while retaining both actual new outcomes");
        var save = JsonSerializer.Deserialize<AllegianceTransitionSave>(journal.Export());
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(save with { Current = save.Current with { LawfulFollowerMigration = false } })), "nonempty full follower scope cannot be relabeled as the no-move mode");
        var foreignSave = JsonSerializer.Deserialize<CharterBookSave>(book.Export());
        var foreignBook = CharterBook.Restore(JsonSerializer.Serialize(foreignSave with { Entities = foreignSave.Entities.Select(e => e.Id == "town" ? e with { NativeOwner = "foreign" } : e).ToArray() }));
        var foreignObs = foreignBook.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        var foreignAid = aid with { Id = "foreign-refusal", BookProof = Hash(foreignBook.Export()) };
        var foreignProtection = new CharterProtection().Agree(foreignBook, "c", d, 75, true, 1).Refuse(foreignBook, foreignAid, d, true, "Honor_Vile", 1, 120, now);
        var foreignClosure = LawfulExitPreparation.Prepare(foreignBook, payments, politics, physical, foreignProtection, foreignAid.Id, now, foreignObs);
        var foreignFollowers = FollowerTransfers.Review(CharterBook.Restore(foreignClosure.AfterBook), physical,
            [new("child", actualTown with { Owner = "foreign", SourceSettlements = 2 }), new("deep", actualDeep with { SourceSettlements = 2 })], "union", now);
        var foreignPeace = UnionPeaceAdmission.Review("union", "player", "chief", now, 75, ["seat"], foreignFollowers.Moves);
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulFollowerAdmission("foreign", politics, command, foreignFollowers, physical, foreignClosure, foreignPeace),
            "a separately verified city from another source realm is not authority to move that realm's resident or stock");
        Check(book.Charter("c").Phase == RecordedCharterPhase.Active && politics.State.Mode == PlayerAllegianceMode.Vassal, "original models are unchanged until separately coordinated commit");
        return count;
    }
}
