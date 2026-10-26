using System.Text.Json;
using Honor.Contracts.Rules;

internal static class UnionPeaceAdmissionTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Peace admission: " + name); }
        void Reject(Action a, string name) { bool bad = false; try { a(); } catch (ArgumentException) { bad = true; } Check(bad, name); }
        UnionPeaceConsent Vote(int goodwill = 50, string chief = "chief", string[] seats = null, long tick = 10) =>
            UnionPeaceAdmission.Review("union", "player", chief, tick, goodwill, seats ?? ["town/b", "town/a"], []);
        var vote = Vote();
        Check(vote.Accepted && vote.TotalWeight == 2 && vote.ApprovedWeight == 2 && !vote.Founding && vote.Votes[0].Seat == "town/a", "actual seats have ordered base votes and strict-majority acceptance");
        Check(!Vote(49).Accepted && Vote(49).Votes.All(v => v.Reason == UnionAdmissionVoteReason.Relationship), "current NPC relationship policy can decline actual membership");
        Check(Vote(-50, seats: []).Accepted && Vote(-50, seats: []).Founding && !Vote(-51, seats: []).Accepted, "only a genuinely empty electorate uses founding representative policy");
        Reject(() => Vote(seats: ["town/a", "town/a"]), "duplicate actual town cannot create extra votes");
        Reject(() => UnionPeaceAdmission.Validate(vote with { Votes = [vote.Votes[0] with { Weight = 100 }] }), "unimplemented size/contribution weights cannot be invented");
        Reject(() => UnionPeaceAdmission.Validate(vote with { Votes = [vote.Votes[0] with { Approve = false }] }), "saved votes must replay actual NPC policy");
        Check(UnionPeaceAdmission.SameReview(vote, Vote(100, tick: 11)), "normal admission goodwill projection does not revoke the same positive vote");
        Check(!UnionPeaceAdmission.SameReview(vote, Vote(chief: "successor")), "new current representative needs explicit recovery review");
        Check(!UnionPeaceAdmission.SameReview(vote, Vote(seats: ["town/a"])), "unrelated member loss or arrival changes reviewed electorate");
        var polity = new PlayerAllegiance("player"); var transfers = new TerritoryTransferLedger();
        var book = new CharterBook(); book.Register(new("p", "player", true), 0);
        var followers = FollowerTransfers.Review(book, transfers, [], "union", 10);
        var command = new AllegianceCommand(AllegianceAction.JoinUnion, 10, "union", Consent: true, NoActiveFeudalContract: true);
        var old = new AllegianceTransitionLedger(); old.Prepare("legacy", polity, command, followers, transfers);
        string legacy = old.Export();
        Check(!legacy.Contains("Peace") && AllegianceTransitionLedger.Restore(legacy).Export() == legacy, "null optional council properties retain legacy JSON and public constructor behavior");
        using (var sha = System.Security.Cryptography.SHA256.Create())
            Check(BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(legacy))).Replace("-", "") ==
                "68873B0338C87DDEA58BFA02FCF761B434C32F2B96C8D48AA975CFC4A09618CD", "published0.3.51 pure-rule fixture retains exactly the same legacy bytes on both hosts");
        var journal = new AllegianceTransitionLedger(); journal.PreparePeace("new", polity, command, followers, transfers, vote);
        vote.Votes[0] = vote.Votes[0] with { Seat = "mutated" };
        Check(journal.Current.Peace.Votes[0].Seat == "town/a", "original consent owns an independent immutable snapshot of caller inputs");
        Reject(() => journal.ReaffirmPeace(Vote(), 10), "cannot claim applied council recovery before allegiance application");
        polity.Apply(command); journal.ConfirmAllegiance(polity, 10);
        Reject(() => journal.Complete(polity, followers, 10), "actual admission cannot complete without current applied council re-review");
        var original = JsonSerializer.Serialize(journal.Current.Peace); journal.ReaffirmPeace(Vote(chief: "successor"), 10);
        Check(JsonSerializer.Serialize(journal.Current.Peace) == original && journal.Current.PeaceReaffirmations[0].Representative == "successor", "successor consent is appended without replacing original electorate/representative");
        journal.Complete(polity, followers, 10);
        Check(AllegianceTransitionLedger.Restore(journal.Export()).Export() == journal.Export() && !journal.Pending && journal.Current.Wars == null && journal.Current.WarResults == null,
            "completed membership restores without any war authority or fabricated native outcomes");
        var pending = new AllegianceTransitionLedger(); pending.PreparePeace("pending", new("player"), command, followers, transfers, Vote());
        var prepared = JsonSerializer.Deserialize<AllegianceTransitionSave>(pending.Export());
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(prepared with { Current = prepared.Current with { Peace = prepared.Current.Peace with { Player = "foreign" } } })), "restore pairs vote to exact native applicant");
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(prepared with { Current = prepared.Current with { Peace = prepared.Current.Peace with { Tick = 9 } } })), "restore pairs original vote to preparation clock");
        Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(prepared with { Current = prepared.Current with { Peace = null, PeaceReaffirmations = [Vote()] } })), "orphan council recovery cannot replace missing original consent");
        Reject(() => pending.PreparePeace("bad", new("player"), command, followers, transfers, Vote(49)), "declined NPC vote never starts a political command");
        Reject(() => new AllegianceTransitionLedger().PreparePeace("bad", new("player"), command, followers, transfers,
            Vote() with { IncomingCharters = ["unreviewed-city"] }), "incoming city exemptions bind exactly the original actual migration plan");
        var resumed = AllegianceTransitionLedger.Restore(journal.Export());
        var later = new AllegianceTransitionLedger();
        var bind = new AllegianceCommand(AllegianceAction.UnionExitNotice, 11, NoticeTicks: PoliticalWorld.Day); polity.Apply(bind);
        polity.Apply(new(AllegianceAction.BindLiege, PoliticalWorld.Day + 11, "liege", "contract", Consent: true, UnionDuesSettled: true));
        // A later journal must retain the real command prefix; archive validation does not invent departure or settlement evidence.
        var newer = new PlayerAllegiance("player"); newer.Apply(command); newer.Apply(bind);
        var bindCommand = new AllegianceCommand(AllegianceAction.BindLiege, PoliticalWorld.Day + 11, "liege", "contract", Consent: true, UnionDuesSettled: true);
        var nextFollowers = FollowerTransfers.Review(book, transfers, [], "liege", PoliticalWorld.Day + 11);
        later.Prepare("later", newer, bindCommand, nextFollowers, transfers);
        UnionWarAdmission.ValidateHistory([resumed.Export()], later.Current);
        Check(later.Current.Wars == null && later.Current.Peace == null, "original completed council consent can be retained in later exact command lineage without changing later authority");
        var unconfirmed = AllegianceTransitionLedger.Restore(pending.Export()); unconfirmed.CancelUnchanged(new("player"), transfers, 10);
        Check(unconfirmed.Current.Phase == AllegianceTransitionPhase.Cancelled && unconfirmed.Current.Peace != null, "unchanged cancellation preserves actual original vote without applying allegiance");
        var blocked = AllegianceTransitionLedger.Restore(pending.Export()); blocked.ConfirmAllegiance(PlayerAllegiance.Restore(blocked.Current.After), 10);
        Reject(() => blocked.ReaffirmPeace(Vote() with { IncomingCharters = ["extra"] }, 10), "recovery cannot invent new pending town exclusions");
        for (int i = 0; i < 64; i++) blocked.ReaffirmPeace(Vote(tick: 10 + i), 10 + i);
        string full = blocked.Export(); Reject(() => blocked.ReaffirmPeace(Vote(tick: 74), 74), "bounded recovery history never evicts original decisions");
        blocked.ReaffirmPeace(Vote(tick: 73), 73);
        Check(blocked.Export() == full, "same exact current re-review does not consume a new slot even at capacity");
        Check(blocked.Export() == full && AllegianceTransitionLedger.Restore(full).Export() == full, "full recovery archive remains unchanged and restorable");
        var family = new CharterBook(); family.Register(new("p", "player", true), 0); family.Register(new("a", "source", false), 0);
        var obs = family.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        family.Offer("child", new("p", "a", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0), 0, PoliticalWorld.Day, obs);
        family.Sign("child", true, 0, obs); family.Sign("child", false, 0, obs);
        var custody = new TerritoryTransferLedger(); var originalFollowers = FollowerTransfers.Review(family, custody, [new("child", new("a", "source", 3))], "union", 10);
        var incoming = UnionPeaceAdmission.Review("union", "player", "chief", 10, 50, ["town/a"], ["child"]);
        var migration = new AllegianceTransitionLedger(); migration.PreparePeace("migration", new("player"), command, originalFollowers, custody, incoming);
        migration.ConfirmAllegiance(PlayerAllegiance.Restore(migration.Current.After), 10);
        Reject(() => UnionPeaceAdmission.RequireIncomingReceipt(migration.Current, family, custody, "child", "union"), "native ownership alone cannot fabricate an incoming-city voting exclusion");
        custody.Prepare("physical", family, "child", new("a", "source", 3), "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 10);
        Reject(() => UnionPeaceAdmission.RequireIncomingReceipt(migration.Current, family, custody, "child", "union"), "unknown applying transfer cannot count as a completed incoming town");
        custody.Confirm("physical", new("a", "union", 3), 11);
        string originalPhysical = custody.Export(), originalFamily = family.Export();
        UnionPeaceAdmission.RequireIncomingReceipt(migration.Current, family, custody, "child", "union");
        Check(custody.Export() == originalPhysical && family.Export() == originalFamily, "positive exact migration proof excludes only pending votes without replaying city/stock/resident writes");
        Reject(() => UnionPeaceAdmission.RequireIncomingReceipt(migration.Current with { PreparedTick = 12 }, family, custody, "child", "union"), "an older transfer does not prove this admission's incoming city");
        Reject(() => UnionPeaceAdmission.RequireIncomingReceipt(migration.Current with { Peace = incoming with { IncomingCharters = [] } }, family, custody, "child", "union"), "a nonincoming original member cannot be omitted from council votes");
        Reject(() => UnionPeaceAdmission.RequireIncomingReceipt(migration.Current, family, custody, "child", "foreign"), "changed actual owner cannot retain incoming-city exclusion");
        var unchangedVote = incoming with { Tick = 11 }; migration.ReaffirmPeace(unchangedVote, 11);
        var finalFollowers = FollowerTransfers.Review(family, custody, [new("child", new("a", "union", 3))], "union", 11);
        migration.Complete(PlayerAllegiance.Restore(migration.Current.After), finalFollowers, 11);
        Reject(() => UnionPeaceAdmission.RequireIncomingReceipt(migration.Current, family, custody, "child", "union"), "completed admission no longer excludes the now full member town");
        var manySeats = Enumerable.Range(0, 1000).Select(i => "town/" + i + "/" + new string('x', 100)).ToArray();
        var largeVote = Vote(seats: manySeats); var large = new AllegianceTransitionLedger();
        large.PreparePeace("large", new("player"), command, followers, transfers, largeVote);
        large.ConfirmAllegiance(PlayerAllegiance.Restore(large.Current.After), 10);
        bool sizeStopped = false;
        for (int i = 0; i < 64; i++)
        {
            string beforeAttempt = large.Export();
            try { large.ReaffirmPeace(largeVote with { Tick = 10 + i }, 10 + i); }
            catch (ArgumentException) { sizeStopped = large.Export() == beforeAttempt; break; }
        }
        Check(sizeStopped && large.Current.PeaceReaffirmations.Length < 64 && large.Export().Length <= 7000000,
            "large valid electorate reserves room before count capacity and never partially appends an unsaveable decision");
        Check(AllegianceTransitionLedger.Restore(large.Export()).Export() == large.Export(), "size-limited original consent and every prior re-review stay restorable without eviction");
        return count;
    }
}
