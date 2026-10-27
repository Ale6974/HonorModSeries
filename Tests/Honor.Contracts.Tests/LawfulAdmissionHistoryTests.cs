using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;

internal static class LawfulAdmissionHistoryTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new Exception("Lawful historical admission: " + why); }
        void Reject(Action f, string why) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad, why); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("n", "realm", false), 0); book.Register(new("town", "old", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string l, string s) => new(l, s, 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0);
        book.Offer("old", Terms("p", "town"), 0, PoliticalWorld.Day, obs); book.Sign("old", true, 0, obs); book.Sign("old", false, 0, obs);
        var physical = new TerritoryTransferLedger(); var source = new TerritoryTransferObservation("town", "old", 2);
        physical.Prepare("past", book, "old", source, "target", "target", null, TerritoryTransferPurpose.ActiveCharter, 1);
        physical.Confirm("past", source with { Owner = "target" }, 2);
        book.ConsentToRelease("old", true, 3, obs); book.ConsentToRelease("old", false, 3, obs);
        book.Offer("c", Terms("n", "p"), 4, PoliticalWorld.Day, obs); book.Sign("c", true, 4, obs); book.Sign("c", false, 4, obs);
        var politics = new PlayerAllegiance("player"); politics.Apply(new(AllegianceAction.BindLiege, 4, "realm", "c", Consent: true));
        var payments = new TributeLedger(); payments.Enable(book, "c", 4); long now = PoliticalWorld.Day + 10;
        var d = new NpcRealmDecisionEvidence("realm", "ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var aid = new CharterAidPlan("refusal", "c", "realm", "ruler", "map", ["enemy"], ["enemy"], now, Hash(book.Export()), Hash(politics.Export()), d);
        var protection = new CharterProtection().Agree(book, "c", d, 75, true, 5).Refuse(book, aid, d, true, "Honor_Vile", 1, 120, now);
        string originalPhysical = physical.Export(); var closure = LawfulExitPreparation.Prepare(book, payments, politics, physical, protection, "refusal", now, obs);
        var afterBook = CharterBook.Restore(closure.AfterBook); var afterPayments = TributeLedger.Restore(closure.AfterPayments);
        var command = new AllegianceCommand(AllegianceAction.JoinUnion, now, "union", Consent: true, NoActiveFeudalContract: true, OldContractReleased: true);
        var followers = FollowerTransfers.Review(afterBook, physical, Array.Empty<FollowerCandidate>(), "union", now);
        var peace = UnionPeaceAdmission.Review("union", "player", "chief", now, 75, ["seat"], []);
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulAdmission("old-api", politics, command, followers, physical, closure, peace),
            "legacy zero-history signature cannot acquire historical-transfer authority");
        var journal = new AllegianceTransitionLedger(); journal.PrepareLawfulAdmissionWithHistory("legal", politics, command, followers, physical, closure, peace);
        Check(journal.Current.LawfulHistoricalTransfers && journal.Current.Followers.Count == 0 && journal.Current.FollowerReview != null,
            "explicit new marker binds original historical journal and complete empty follower graph");
        var player = PlayerAllegiance.Restore(politics.Export()); player.Apply(command); journal.ConfirmAllegiance(player, now);
        var loaded = AllegianceTransitionLedger.Restore(journal.Export());
        LawfulAdmissions.ValidateCurrentPhysical(loaded.Current, physical); LawfulAdmissions.ValidateArchive([loaded.Export()], afterBook, afterPayments, protection);
        loaded.ReaffirmPeace(peace, now); loaded.Complete(player, followers, now);
        LawfulAdmissions.ValidateArchive([loaded.Export()], afterBook, afterPayments, protection);
        Check(physical.Export() == originalPhysical && physical.Transfers.Single().Phase == TerritoryTransferPhase.Applied &&
            physical.Transfers.Single().FromOwner == "old" && physical.Transfers.Single().TargetOwner == "target" && afterBook.Charter("old") == book.Charter("old"),
            "old applied city ownership, original receipt and old released terms remain byte-identical with no native migration");
        var save = JsonSerializer.Deserialize<AllegianceTransitionSave>(loaded.Export());
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(save with { Current = save.Current with { LawfulHistoricalTransfers = false } })),
            "nonempty history cannot be relabeled as the legacy zero-history mode");
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(save with { Current = save.Current with { LawfulExit = null } })),
            "history marker cannot replace the original refusal and closure evidence");
        var raw = JsonSerializer.Deserialize<TerritoryTransfersSave>(originalPhysical);
        var unresolved = TerritoryTransferLedger.Restore(JsonSerializer.Serialize(raw with {
            LastTick = raw.Transfers[0].PreparedTick, Transfers = [raw.Transfers[0] with { Phase = TerritoryTransferPhase.Prepared, ResolvedTick = null }] }));
        Reject(() => LawfulExitPreparation.Prepare(book, payments, politics, unresolved, protection, "refusal", now, obs),
            "a Prepared physical operation cannot become historical completion or authorize a new exit");
        var changed = TerritoryTransferLedger.Restore(JsonSerializer.Serialize(raw with { LastTick = now + 1 }));
        Reject(() => LawfulAdmissions.ValidateCurrentPhysical(journal.Current, changed), "even a valid changed physical journal blocks in-progress recovery");
        LawfulAdmissions.ValidateArchive([loaded.Export()], afterBook, afterPayments, protection);
        Check(true, "completed historical claim does not require the unrelated current physical ledger to stay frozen forever");
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulAdmissionWithHistory("empty", politics, command, followers, new TerritoryTransferLedger(), closure, peace),
            "a different physical baseline cannot be substituted for the original historical evidence");
        return count;
    }
}
