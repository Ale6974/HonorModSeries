using Honor.Contracts.Rules;

internal static class ReleasedMigrationTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool condition, string name) { count++; if (!condition) throw new Exception(name); }
        var book = new CharterBook();
        foreach (var e in new[] { new TerritoryIdentity("player", "native-player", true), new("sovereign", "old", false), new("a", "old", false), new("b", "old", false) }) book.Register(e, 0);
        TerritoryObservation[] Observe() => book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string liege, string subject) => new(liege, subject, 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m);
        foreach (var c in new[] { ("old-bond", Terms("sovereign", "player")), ("a-bond", Terms("player", "a")) })
        { book.Offer(c.Item1, c.Item2, 0, PoliticalWorld.Day, Observe()); book.Sign(c.Item1, true, 0, Observe()); book.Sign(c.Item1, false, 0, Observe()); }
        book.OfferNpc("b-bond", Terms("a", "b"), new("rep", "rep"), 0, PoliticalWorld.Day, Observe());
        book.SignNpc("b-bond", "a", "rep", 0, Observe()); book.SignNpc("b-bond", "b", "rep", 0, Observe());
        var player = new PlayerAllegiance("native-player");
        player.Apply(new(AllegianceAction.BindLiege, 0, "old", "old-bond", Consent: true, OldContractReleased: true));
        var before = player.State;
        Check(AllegianceChangeSource.ReleasedMigrationSource(book, before, "new", 0) == null, "active sovereignty cannot be bypassed by calling a wartime migration");
        book.ConsentToRelease("old-bond", true, 1, Observe());
        Check(AllegianceChangeSource.ReleasedMigrationSource(book, before, "new", 1) == null, "player release declaration alone grants no source exception");
        book.ConsentToRelease("old-bond", false, 2, Observe());
        Check(AllegianceChangeSource.ReleasedMigrationSource(book, before, "new", 2) == "old", "actual prior sovereign's mutual release covers the exact source faction");
        foreach (var input in new[] { (before, "old", 2L), (before, "native-player", 2L), (before, "", 2L), (before, "new", 1L),
            (before, "new", (long)int.MaxValue + 1), (before with { Authority = "unrelated" }, "new", 2L),
            (before with { Mode = PlayerAllegianceMode.Independent }, "new", 2L) })
            Check(AllegianceChangeSource.ReleasedMigrationSource(book, input.Item1, input.Item2, input.Item3) == null, "unrelated/source/native-player/invalid-clock exceptions denied");
        var transfers = new TerritoryTransferLedger();
        var candidates = book.PlayerFollowers().Select(c => new FollowerCandidate(c.Id, new(c.Terms.Subject, "old", 3))).ToArray();
        var plan = FollowerTransfers.Review(book, transfers, candidates, "new", 3);
        var change = new AllegianceCommand(AllegianceAction.JoinUnion, 3, "new", Consent: true, NoActiveFeudalContract: true, OldContractReleased: true);
        AllegianceChangeSource.Validate(book, before, change, new());
        var journal = new AllegianceTransitionLedger(); journal.Prepare("war-exit", player, change, plan, transfers);
        player.Apply(change); journal.ConfirmAllegiance(player, 3);
        var retainedBefore = PlayerAllegiance.Restore(journal.Current.Before).State;
        Check(AllegianceChangeSource.ReleasedMigrationSource(book, retainedBefore, "new", 3) == "old" &&
            AllegianceChangeSource.ReleasedMigrationSource(book, player.State, "new", 3) == null, "only the pending original intent retains the old source proof after new membership");
        int remaining = 3;
        foreach (var c in candidates)
        {
            var observed = c.Observation with { SourceSettlements = remaining-- };
            transfers.Prepare(c.Charter, book, c.Charter, observed, "new", "new", null, TerritoryTransferPurpose.ActiveCharter, 3);
            transfers.Confirm(c.Charter, observed with { Owner = "new" }, 3);
        }
        journal.Complete(player, FollowerTransfers.Review(book, transfers, candidates.Select(c => c with { Observation = c.Observation with { Owner = "new", SourceSettlements = 2 } }), "new", 3), 3);
        Check(!journal.Pending && player.NativePlayer == "native-player" && transfers.Transfers.Count == 2, "completed direct/indirect migration retains player control and individual ownership proof");
        Check(book.Charter("old-bond").Phase == RecordedCharterPhase.Released && book.Charter("b-bond").Phase == RecordedCharterPhase.Active,
            "prior release and subordinate contract remain intact after migration");
        book.Register(new("different-root", "different", false), 3);
        book.OfferNpc("root-drift", Terms("different-root", "sovereign"), new("rep2", "rep3"), 3, PoliticalWorld.Day, Observe());
        book.SignNpc("root-drift", "different-root", "rep2", 3, Observe()); book.SignNpc("root-drift", "sovereign", "rep3", 3, Observe());
        Check(AllegianceChangeSource.ReleasedMigrationSource(book, before, "new", 3) == null, "changed prior sovereign hierarchy cannot reuse old native ownership as departure authority");
        return count;
    }
}
