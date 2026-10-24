using System.Text.Json;
using Honor.Contracts.Rules;

internal static class AllegianceTransitionsTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        var book = new CharterBook(); book.Register(new("player", "native-player", true), 0);
        foreach (string id in new[] { "a", "b" }) book.Register(new(id, "old", false), 0);
        TerritoryObservation[] Observations() => book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        foreach (string id in new[] { "a", "b" })
        {
            book.Offer(id, new("player", id, 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m), 0, PoliticalWorld.Day, Observations());
            book.Sign(id, true, 0, Observations()); book.Sign(id, false, 0, Observations());
        }
        var transfers = new TerritoryTransferLedger(); var player = new PlayerAllegiance("native-player");
        var candidates = new[] { new FollowerCandidate("a", new("a", "old", 3)), new FollowerCandidate("b", new("b", "old", 3)) };
        FollowerTransferPlan Plan(long now) => FollowerTransfers.Review(book, transfers, candidates, "union", now);
        var command = new AllegianceCommand(AllegianceAction.JoinUnion, 10, "union", Consent: true, NoActiveFeudalContract: true);
        var journal = new AllegianceTransitionLedger();
        void Reject(Action action, string name)
        {
            string before = journal.Export(); bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected && journal.Export() == before, name + ": no partial transition mutation");
        }
        Check(AllegianceTransitionLedger.Restore(journal.Export()).Current == null, "old save without intent remains empty");
        Reject(() => journal.Prepare("bad", player, command with { Consent = false }, Plan(10), transfers), "preparation validates actual consent command");
        Reject(() => journal.Prepare("bad", player, command with { Tick = 11 }, Plan(10), transfers), "stale follower review tick cannot be reused");
        Reject(() => journal.Prepare("bad", player, command with { Authority = "other" }, Plan(10), transfers), "command and follower destinations must match");
        Reject(() => journal.Prepare("bad", player, new(AllegianceAction.Suspend, 10, "union"), Plan(10), transfers), "only membership changes can start a coordinated transfer");
        Reject(() => journal.Prepare("bad", player, command, Plan(10) with { Moves = ["unreviewed"] }, transfers), "cannot add unreviewed city");
        Reject(() => journal.Prepare("bad", player, command, Plan(10) with { Entries = [candidates[0], candidates[0]] }, transfers), "duplicate subjects rejected");
        journal.Prepare("first", player, command, Plan(10), transfers);
        Check(journal.Pending && journal.Current.Phase == AllegianceTransitionPhase.Prepared && journal.IsBefore(player) && !journal.IsAfter(player), "intent does not apply player allegiance");
        Check(transfers.Transfers.Count == 0 && player.State.Mode == PlayerAllegianceMode.Independent, "prepare has no physical or political success side effect");
        var pending = AllegianceTransitionLedger.Restore(journal.Export());
        Check(pending.Export() == journal.Export(), "prepared intent roundtrip");
        Reject(() => journal.Prepare("competing", player, command, Plan(10), transfers), "cannot overwrite pending transition");
        Reject(() => journal.ConfirmAllegiance(player, 10), "intended command is not actual execution proof");
        Reject(() => journal.CancelUnchanged(player, transfers, 9), "cancel cannot backdate");
        journal.CancelUnchanged(player, transfers, 11);
        Check(!journal.Pending && journal.Current.Phase == AllegianceTransitionPhase.Cancelled, "unchanged preparation can cancel");
        Reject(() => journal.Prepare("first", player, command with { Tick = 12 }, Plan(12), transfers), "current intent ID cannot be reused");
        journal.Prepare("second", player, command with { Tick = 12 }, Plan(12), transfers);
        bool immutable = false;
        try { ((IList<string>)journal.Current.Followers)[0] = "other"; } catch (NotSupportedException) { immutable = true; }
        Check(immutable, "intent follower set immutable");
        var alternative = new PlayerAllegiance("native-player"); alternative.Apply(command with { Tick = 11 });
        Check(alternative.State == PlayerAllegiance.Restore(journal.Current.After).State && !journal.IsAfter(alternative), "same political state with different command history is not completion proof");
        player.Apply(command with { Tick = 12 });
        Check(journal.IsAfter(player) && !journal.IsBefore(player), "recorded actual command recognized");
        Reject(() => journal.CancelUnchanged(player, transfers, 12), "cannot cancel after allegiance changed even before phase confirmation");
        journal.ConfirmAllegiance(player, 12);
        Check(journal.Current.Phase == AllegianceTransitionPhase.AllegianceApplied && journal.Pending, "allegiance alone is not all-city completion");
        Reject(() => journal.Complete(player, Plan(12), 12), "remaining city moves prevent completion");
        Reject(() => journal.ConfirmAllegiance(player, 12), "duplicate phase confirmation rejected");
        player.Apply(new(AllegianceAction.Suspend, 13));
        Check(journal.IsAfter(player), "later suspension retains recorded command head for recovery");
        player.Apply(new(AllegianceAction.Reaffirm, 13, "union", Consent: true));
        Check(journal.IsAfter(player), "same-authority reaffirmation retains command head");
        var travelled = PlayerAllegiance.Restore(journal.Current.After);
        travelled.Apply(new(AllegianceAction.UnionExitNotice, 13, NoticeTicks: PoliticalWorld.Day));
        travelled.Apply(new(AllegianceAction.BindLiege, 13 + PoliticalWorld.Day, "foreign", "foreign-bond", Consent: true, UnionDuesSettled: true));
        travelled.Apply(command with { Tick = 14 + PoliticalWorld.Day, OldContractReleased = true });
        Check(travelled.State == PlayerAllegiance.Restore(journal.Current.After).State && !journal.IsAfter(travelled),
            "leaving for another polity and returning to the same final state cannot masquerade as recovery");
        var applied = AllegianceTransitionLedger.Restore(journal.Export());
        Check(applied.IsAfter(player) && applied.Export() == journal.Export(), "applied intent survives load with extended actual history");
        foreach (var c in candidates)
        {
            int remaining = 3 - transfers.Transfers.Count;
            transfers.Prepare("move-" + c.Charter, book, c.Charter, c.Observation with { SourceSettlements = remaining }, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 14);
            transfers.Confirm("move-" + c.Charter, c.Observation with { Owner = "union" }, 14);
            if (c.Charter == "a") Check(journal.Pending, "one verified city does not complete the coordinated change");
        }
        var arrived = candidates.Select(c => c with { Observation = c.Observation with { Owner = "union", SourceSettlements = 2, HasMap = true, Visited = true } }).ToArray();
        var final = FollowerTransfers.Review(book, transfers, arrived, "union", 14);
        Reject(() => journal.Complete(player, final with { Entries = [arrived[0]] }, 14), "missing original follower cannot be called complete");
        Reject(() => journal.Complete(player, final with { Target = "other" }, 14), "wrong completion destination");
        Reject(() => journal.Complete(player, final with { ReviewedTick = 13 }, 14), "completion needs a current review");
        var suspendedAtEnd = PlayerAllegiance.Restore(player.Export()); suspendedAtEnd.Apply(new(AllegianceAction.Suspend, 14));
        Reject(() => journal.Complete(suspendedAtEnd, final, 14), "all cities arrived cannot hide suspended diplomacy");
        journal.Complete(player, final, 14);
        Check(!journal.Pending && journal.Current.Phase == AllegianceTransitionPhase.Completed && transfers.Transfers.Count == 2, "only verified whole-group completion ends transition");
        Check(AllegianceTransitionLedger.Restore(journal.Export()).Export() == journal.Export(), "completed intent roundtrip");

        var saved = JsonSerializer.Deserialize<AllegianceTransitionSave>(journal.Export());
        void Corrupt(AllegianceTransitionSave value, string name)
        {
            bool rejected = false;
            try { AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(value)); } catch (ArgumentException) { rejected = true; }
            Check(rejected, name);
        }
        Corrupt(saved with { Version = 99 }, "future schema rejected");
        Corrupt(saved with { Current = saved.Current with { Id = "" } }, "empty identity rejected");
        Corrupt(saved with { Current = saved.Current with { Followers = ["a", "a"] } }, "duplicate saved follower rejected");
        Corrupt(saved with { Current = saved.Current with { TransferBaseline = "not-a-hash" } }, "invalid baseline digest rejected");
        Corrupt(saved with { Current = saved.Current with { Phase = (AllegianceTransitionPhase)99 } }, "unknown phase rejected");
        Corrupt(saved with { Current = saved.Current with { Phase = AllegianceTransitionPhase.Prepared } }, "phase/time contradiction rejected");
        Corrupt(saved with { Current = saved.Current with { PreparedTick = 11 } }, "prepared tick must match recorded command");
        Corrupt(saved with { Current = saved.Current with { FinishedTick = 11 } }, "completion before allegiance confirmation rejected");
        Corrupt(saved with { Current = saved.Current with { After = player.Export() } }, "extra commands cannot be substituted for intended single transition");
        var wrongPlayer = new PlayerAllegiance("another-player"); wrongPlayer.Apply(command with { Tick = 12 });
        Corrupt(saved with { Current = saved.Current with { After = wrongPlayer.Export() } }, "native player identity cannot change");
        Check(!journal.IsAfter(wrongPlayer), "wrong native player cannot recover intent");

        var untouchedPlayer = new PlayerAllegiance("native-player"); var changingLedger = new TerritoryTransferLedger();
        var incomplete = new AllegianceTransitionLedger(); incomplete.Prepare("unchanged-test", untouchedPlayer, command,
            FollowerTransfers.Review(book, changingLedger, candidates, "union", 10), changingLedger);
        changingLedger.Prepare("outside", book, "a", candidates[0].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 10);
        bool cancellationRejected = false;
        try { incomplete.CancelUnchanged(untouchedPlayer, changingLedger, 11); } catch (ArgumentException) { cancellationRejected = true; }
        Check(cancellationRejected && incomplete.Pending, "unchanged player alone cannot cancel after physical journal changed");

        var source = new CharterBook(); source.Register(new("player", "native-player", true), 0);
        source.Register(new("lord", "old-lord", false), 0); source.Register(new("new-lord", "new-authority", false), 0);
        TerritoryObservation[] SourceObservations() => source.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        source.Offer("old-bond", new("lord", "player", 100, PoliticalWorld.Day, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m), 0, PoliticalWorld.Day, SourceObservations());
        source.Sign("old-bond", true, 0, SourceObservations()); source.Sign("old-bond", false, 0, SourceObservations());
        var payments = new TributeLedger(); payments.Enable(source, "old-bond", 0);
        var vassal = new PlayerAllegianceState(PlayerAllegianceMode.Vassal, "old-lord", "old-bond");
        void RejectSource(Action action, string name)
        {
            string b = source.Export(), p = payments.Export(); bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected && source.Export() == b && payments.Export() == p, name + ": source and money unchanged");
        }
        RejectSource(() => AllegianceChangeSource.Validate(source, vassal, command, payments), "actual active old contract blocks union return despite a claimed release flag");
        source.ConsentToRelease("old-bond", true, PoliticalWorld.Day * 2, SourceObservations());
        RejectSource(() => AllegianceChangeSource.Validate(source, vassal, command, payments), "one release consent cannot return player to union");
        source.ConsentToRelease("old-bond", false, PoliticalWorld.Day * 2, SourceObservations()); payments.SynchronizeClosure(source, "old-bond", PoliticalWorld.Day * 2);
        string debtBefore = payments.Export(); AllegianceChangeSource.Validate(source, vassal, command, payments);
        Check(payments.Export() == debtBefore && payments.Bills("old-bond", PoliticalWorld.Day * 2).Count() == 2 && !payments.IsPaid("old-bond", 1), "mutual release permits reviewed return while preserving both unpaid obligations");
        RejectSource(() => AllegianceChangeSource.Validate(source, vassal with { Authority = "forged-lord" }, command, payments), "old contract must belong to actual recorded authority");
        payments.Prepare("reserved", "old-bond", 1, [new("stack", 100, 1m)], PoliticalWorld.Day * 2);
        RejectSource(() => AllegianceChangeSource.Validate(source, vassal, command, payments), "reserved real goods block allegiance transition");
        payments.CancelAfterVerifiedReturn("reserved", true, PoliticalWorld.Day * 2);
        source.Offer("new-bond", new("new-lord", "player", 100, PoliticalWorld.Day, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m), PoliticalWorld.Day * 2, PoliticalWorld.Day, SourceObservations());
        source.Sign("new-bond", true, PoliticalWorld.Day * 2, SourceObservations()); source.Sign("new-bond", false, PoliticalWorld.Day * 2, SourceObservations());
        RejectSource(() => AllegianceChangeSource.Validate(source, vassal, command, payments), "new active liege contract blocks old-release union shortcut");
        var bind = new AllegianceCommand(AllegianceAction.BindLiege, PoliticalWorld.Day * 2, "new-authority", "new-bond", Consent: true, OldContractReleased: true);
        AllegianceChangeSource.Validate(source, vassal, bind, payments); Check(!payments.IsPaid("old-bond", 1), "new valid liege still does not erase old creditor's debt");
        RejectSource(() => AllegianceChangeSource.Validate(source, vassal, bind with { Authority = "wrong" }, payments), "new contract and authority must agree");
        RejectSource(() => AllegianceChangeSource.Validate(source, vassal, new(AllegianceAction.Reaffirm, 10, "old-lord"), payments), "source checker does not accept unrelated political commands");
        return count;
    }
}
