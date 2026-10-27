using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;

internal static class LawfulAdmissionTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new Exception("Lawful admission: " + why); }
        void Reject(Action f, string why) { bool bad = false; try { f(); } catch (ArgumentException) { bad = true; } Check(bad, why); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("n", "realm", false), 0); book.Register(new("m", "other-realm", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        book.Offer("c", new("n", "p", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        book.Sign("c", true, 0, obs); book.Sign("c", false, 0, obs);
        book.OfferAlternative("candidate", new("m", "p", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        var politics = new PlayerAllegiance("player"); politics.Apply(new(AllegianceAction.BindLiege, 0, "realm", "c", Consent: true));
        var payments = new TributeLedger(); payments.Enable(book, "c", 0);
        payments.Prepare("paid", "c", 1, [new("silver", 100, 1)], PoliticalWorld.Day);
        payments.Commit("paid", [new("silver", 100, 1)], PoliticalWorld.Day);
        var physical = new TerritoryTransferLedger(); long tick = PoliticalWorld.Day * 2 + 10;
        var d = new NpcRealmDecisionEvidence("realm", "ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var plan = new CharterAidPlan("refusal", "c", "realm", "ruler", "map", ["enemy"], ["enemy"], tick, Hash(book.Export()), Hash(politics.Export()), d);
        var protection = new CharterProtection().Agree(book, "c", d, 75, true, 1).Refuse(book, plan, d, true, "Honor_Vile", 1, 120, tick);
        var closure = LawfulExitPreparation.Prepare(book, payments, politics, physical, protection, "refusal", tick, obs);
        var afterBook = CharterBook.Restore(closure.AfterBook); var afterPayments = TributeLedger.Restore(closure.AfterPayments);
        var command = new AllegianceCommand(AllegianceAction.JoinUnion, tick, "union", Consent: true, NoActiveFeudalContract: true, OldContractReleased: true);
        var followers = FollowerTransfers.Review(afterBook, physical, Array.Empty<FollowerCandidate>(), "union", tick);
        var peace = UnionPeaceAdmission.Review("union", "player", "chief", tick, 75, ["seat"], []);
        var journal = new AllegianceTransitionLedger();
        journal.PrepareLawfulAdmission("legal", politics, command, followers, physical, closure, peace);
        Check(journal.Current.Phase == AllegianceTransitionPhase.Prepared && journal.Current.LawfulExit == closure.Export(), "original refusal and accepted council are paired before execution");
        Reject(() => LawfulAdmissions.ValidateArchive([journal.Export()], afterBook, afterPayments, protection), "prepared-only intent cannot authorize a live closed book");
        var actual = PlayerAllegiance.Restore(politics.Export()); actual.Apply(command); journal.ConfirmAllegiance(actual, tick);
        LawfulAdmissions.ValidateArchive([journal.Export()], afterBook, afterPayments, protection);
        Check(actual.State.Mode == PlayerAllegianceMode.Union && actual.NativePlayer == "player" && afterBook.Charter("c").NpcReleaseTick == null,
            "applied journal retains the native player and never invents NPC release");
        var restored = AllegianceTransitionLedger.Restore(journal.Export());
        LawfulAdmissions.ValidatePair(restored.Current, afterBook, afterPayments, protection);
        restored.ReaffirmPeace(peace, tick); restored.Complete(actual, followers, tick);
        LawfulAdmissions.ValidateArchive([restored.Export()], afterBook, afterPayments, protection);
        Check(restored.Current.Phase == AllegianceTransitionPhase.Completed && afterPayments.Account("c").ClosedTick == tick &&
            TributePerformanceReview.ForCharter(afterBook, afterPayments, "c", tick).OutstandingValue == 100,
            "explicit completion preserves the original unpaid obligation and cutoff");
        Reject(() => LawfulAdmissions.ValidateArchive([], afterBook, afterPayments, protection), "closure without its execution record is blocked");
        Reject(() => LawfulAdmissions.ValidateArchive([restored.Export(), restored.Export()], afterBook, afterPayments, protection), "duplicate execution history rejected");
        Reject(() => LawfulAdmissions.ValidateArchive([restored.Export()], afterBook, payments, protection), "missing original cutoff cannot validate the completed history");
        var damaged = JsonSerializer.Deserialize<TributeLedgerSave>(afterPayments.Export());
        var missingReceipt = TributeLedger.Restore(JsonSerializer.Serialize(damaged with { Receipts = [] }));
        Reject(() => LawfulAdmissions.ValidateArchive([restored.Export()], afterBook, missingReceipt, protection), "deleting a pre-existing paid receipt is rejected");
        var oldBook = JsonSerializer.Deserialize<CharterBookSave>(afterBook.Export());
        var missingContract = CharterBook.Restore(JsonSerializer.Serialize(oldBook with { Charters = oldBook.Charters.Where(c => c.Id != "candidate").ToArray() }));
        Reject(() => LawfulAdmissions.ValidateArchive([restored.Export()], missingContract, afterPayments, protection), "an original third-party proposal cannot disappear from retained history");
        Reject(() => LawfulAdmissions.ValidateArchive([restored.Export()], afterBook, afterPayments, new CharterProtection()), "missing original refusal archive cannot validate the closure");
        Reject(() => new AllegianceTransitionLedger().PrepareLawfulAdmission("no-consent", politics, command, followers, physical, closure,
            UnionPeaceAdmission.Review("union", "player", "chief", tick, -100, ["seat"], [])), "declined union vote cannot authorize admission");
        var scope = JsonSerializer.Deserialize<AllegianceTransitionSave>(restored.Export());
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(scope with { Current = scope.Current with { LawfulExit = null } })),
            "a unilateral closure cannot be relabeled into an ordinary admission without its evidence");
        Check(book.Charter("c").Phase == RecordedCharterPhase.Active && politics.State.Mode == PlayerAllegianceMode.Vassal && payments.Account("c").ClosedTick == null,
            "copy staging leaves original ledgers unchanged until the coordinator swaps them");
        return count;
    }
}
