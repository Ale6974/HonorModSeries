using System.Text.Json;
using Honor.Contracts.Rules;

internal static class UnionWarIncomingTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string label) { count++; if (!ok) throw new Exception("War incoming: " + label); }
        void Reject(Action a, string label) { bool bad = false; try { a(); } catch (ArgumentException) { bad = true; } Check(bad, label); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("a", "source", false), 0); book.Register(new("b", "source", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        var terms = new RecordedCharterTerms("p", "a", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0);
        book.Offer("first", terms, 0, PoliticalWorld.Day, obs); book.Sign("first", true, 0, obs); book.Sign("first", false, 0, obs);
        book.OfferNpc("second", terms with { Liege = "a", Subject = "b" }, new("historic", "historic"), 0, PoliticalWorld.Day, obs);
        book.SignNpc("second", "a", "historic", 0, obs); book.SignNpc("second", "b", "historic", 0, obs);
        var transfers = new TerritoryTransferLedger(); var entries = new[] { new FollowerCandidate("first", new("a", "source", 3)), new FollowerCandidate("second", new("b", "source", 3)) };
        var followers = FollowerTransfers.Review(book, transfers, entries, "union", 10);
        var target = new UnionWarTarget("enemy", 0, SharedRelationKind.Neutral, 0, SharedRelationKind.Neutral, true, true);
        UnionWarConsent Vote(string[] seats = null, long tick = 10) => UnionWarAdmission.Review("union", "player", "chief", tick, seats ?? ["member"], [target], 75);
        var old = Vote(); string oldBytes = JsonSerializer.Serialize(old);
        Check(!oldBytes.Contains("IncomingCharters") && JsonSerializer.Serialize(JsonSerializer.Deserialize<UnionWarConsent>(oldBytes)) == oldBytes, "legacy null consent has unchanged constructor and JSON shape");
        using (var sha = System.Security.Cryptography.SHA256.Create())
            Check(BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(oldBytes))).Replace("-", "") ==
                "54C016DA55B4FDB9CDA73FB891B043B6CFC0A7D82230147E68C5FB6767DEDD27", "published0.3.52 old war consent keeps its exact bytes on both rule hosts");
        var modern = UnionWarAdmission.BindIncoming(old, followers);
        Check(modern.IncomingCharters.SequenceEqual(new[] { "first", "second" }) && old.IncomingCharters == null && JsonSerializer.Serialize(old) == oldBytes,
            "original actual full migration set is bound without modifying old consent");
        Reject(() => UnionWarAdmission.BindIncoming(modern, followers), "already scoped consent cannot be silently rebound");
        Reject(() => UnionWarAdmission.BindIncoming(old, followers with { Target = "other" }), "same-party destination must match the actual follower plan");
        var feudal = UnionWarAdmission.ReviewFeudal("union", "player", "chief", 10, [target], 75);
        Reject(() => UnionWarAdmission.BindIncoming(feudal, followers), "individual feudal consent is not a union pending-membership council");
        Reject(() => UnionWarAdmission.Validate(feudal with { IncomingCharters = [] }), "restore cannot relabel individual authority as council exclusions");
        Reject(() => UnionWarAdmission.Validate(modern with { IncomingCharters = ["first", "first"] }), "duplicate incoming identities rejected");
        var polity = new PlayerAllegiance("player"); var command = new AllegianceCommand(AllegianceAction.JoinUnion, 10, "union", Consent: true, NoActiveFeudalContract: true);
        var journal = new AllegianceTransitionLedger(); journal.Prepare("war", polity, command, followers, transfers, modern);
        Reject(() => new AllegianceTransitionLedger().Prepare("bad", polity, command, followers, transfers, modern with { IncomingCharters = ["first"] }), "preparation cannot omit an actual incoming follower");
        Reject(() => new AllegianceTransitionLedger().Prepare("bad", polity, command, followers, transfers, modern with { IncomingCharters = ["first", "second", "extra"] }), "preparation cannot exempt an unreviewed town");
        polity.Apply(command); journal.ConfirmAllegiance(polity, 10); journal.BeginWar("enemy", 10); journal.ObserveWar("enemy", true, -100, -100, true, 10);
        string declarations = JsonSerializer.Serialize(journal.Current.WarResults), original = JsonSerializer.Serialize(journal.Current.Wars);
        Reject(() => journal.ReaffirmWars(Vote(tick: 11), 11), "legacy unscoped review cannot authorize a scoped war recovery");
        var fresh = Vote(tick: 11) with { Representative = "successor", IncomingCharters = modern.IncomingCharters.ToArray() };
        journal.ReaffirmWars(fresh, 11);
        Check(JsonSerializer.Serialize(journal.Current.Wars) == original && JsonSerializer.Serialize(journal.Current.WarResults) == declarations && journal.Current.WarReaffirmations[0].Representative == "successor",
            "fresh current council appends consent without rewriting original war vote or replaying a verified declaration");
        Check(UnionWarAdmission.SameScope(modern, Vote() with { IncomingCharters = modern.IncomingCharters }) &&
            !UnionWarAdmission.SameScope(modern, Vote(["member", "new-town"]) with { IncomingCharters = modern.IncomingCharters }),
            "only this operation's positively migrated incoming towns stay outside the fixed admission electorate");
        Reject(() => UnionPeaceAdmission.RequireWarIncomingReceipt(journal.Current, book, transfers, "first", "union"), "mere reported ownership cannot omit a vote");
        transfers.Prepare("move-a", book, "first", entries[0].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 11);
        Reject(() => UnionPeaceAdmission.RequireWarIncomingReceipt(journal.Current, book, transfers, "first", "union"), "uncertain prepared physical move cannot count as complete");
        transfers.Confirm("move-a", entries[0].Observation with { Owner = "union" }, 11);
        UnionPeaceAdmission.RequireWarIncomingReceipt(journal.Current, book, transfers, "first", "union");
        Check(transfers.Transfers.Single().Phase == TerritoryTransferPhase.Applied && journal.Current.Followers.Count == 2, "actual first move proves only its pending seat, preserving complete original follower set");
        Reject(() => UnionPeaceAdmission.RequireWarIncomingReceipt(journal.Current with { PreparedTick = 12 }, book, transfers, "first", "union"), "old physical move cannot supply a later admission's seat proof");
        Reject(() => UnionPeaceAdmission.RequireWarIncomingReceipt(journal.Current with { Wars = old }, book, transfers, "first", "union"), "old null consent never acquires incoming exclusions by inference");
        Reject(() => UnionPeaceAdmission.RequireWarIncomingReceipt(journal.Current, book, transfers, "first", "foreign"), "changed actual realm cannot retain seat exclusion");
        transfers.Prepare("move-b", book, "second", entries[1].Observation with { SourceSettlements = 2 }, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 12);
        transfers.Confirm("move-b", entries[1].Observation with { Owner = "union" }, 12);
        UnionPeaceAdmission.RequireWarIncomingReceipt(journal.Current, book, transfers, "second", "union");
        var final = FollowerTransfers.Review(book, transfers, entries.Select(e => e with { Observation = e.Observation with { Owner = "union", SourceSettlements = 3 } }), "union", 12);
        journal.Complete(polity, final, 12);
        Check(AllegianceTransitionLedger.Restore(journal.Export()).Export() == journal.Export() && journal.Current.WarResults.Single().FromCompletedCall,
            "whole migration completes and restores with original declaration attribution and current council history intact");
        Reject(() => UnionPeaceAdmission.RequireWarIncomingReceipt(journal.Current, book, transfers, "first", "union"), "completed incoming towns are full members for future votes");
        var saved = JsonSerializer.Deserialize<AllegianceTransitionSave>(journal.Export());
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(saved with { Current = saved.Current with { Wars = modern with { IncomingCharters = ["unrelated"] } } })), "restored incoming scope must stay inside original followers");
        Check(book.Charter("first").Terms == terms && book.Charter("second").Terms.Subject == "b", "original economic terms and deeper contract survive coordinated war admission");
        var emptyBook = new CharterBook(); emptyBook.Register(new("p", "player", true), 0);
        var emptyFollowers = FollowerTransfers.Review(emptyBook, new(), [], "union", 10);
        var largeVote = UnionWarAdmission.BindIncoming(Vote(Enumerable.Range(0, 1000).Select(i => "member/" + i + new string('x', 100)).ToArray()), emptyFollowers);
        var large = new AllegianceTransitionLedger(); large.Prepare("large", new("player"), command, emptyFollowers, new(), largeVote);
        large.ConfirmAllegiance(PlayerAllegiance.Restore(large.Current.After), 10);
        bool stopped = false;
        for (int i = 1; i <= 64; i++)
        {
            string prior = large.Export();
            try { large.ReaffirmWars(largeVote with { Tick = 10 + i }, 10 + i); }
            catch (ArgumentException) { stopped = large.Export() == prior; break; }
        }
        Check(stopped && large.Current.WarReaffirmations.Length < 64 && large.Export().Length <= 7000000,
            "large current council history reserves result/loss space before capacity without partially appending");
        Check(AllegianceTransitionLedger.Restore(large.Export()).Export() == large.Export(), "size-limited original war vote and every earlier decision remain restorable");
        return count;
    }
}
