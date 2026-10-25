using System.Text.Json;
using Honor.Contracts.Rules;

internal static class UnionWarAdmissionTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool v, string name) { count++; if (!v) throw new Exception("Union war admission: " + name); }
        void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        var targets = new[] { new UnionWarTarget("enemy1", 0, SharedRelationKind.Neutral, 0, SharedRelationKind.Neutral, true, true),
            new UnionWarTarget("enemy2", -10, SharedRelationKind.Neutral, -10, SharedRelationKind.Neutral, true, true) };
        UnionWarConsent Vote(int goodwill, string[] seats = null, UnionWarTarget[] wars = null, long tick = 10, string leader = "leader")
            => UnionWarAdmission.Review("union", "native-player", leader, tick, seats ?? [], wars ?? targets, goodwill);
        Check(Vote(-50).Accepted && Vote(-50).Founding, "founding representative and explicit applicant consent need no invented member towns");
        Check(!Vote(-51).Accepted && Vote(-51).Votes[0].Reason == UnionAdmissionVoteReason.Relationship, "founding relationship boundary");
        var consent = Vote(50, new[] { "town-b", "town-a", "town-c" });
        Check(consent.Accepted && consent.TotalWeight == 3 && consent.ApprovedWeight == 3 && !consent.Founding, "all actual town seats have one base vote");
        Check(consent.Votes.Select(v => v.Seat).SequenceEqual(new[] { "town-a", "town-b", "town-c" }), "stable council scope order");
        Check(!Vote(49, new[] { "town-a" }).Accepted, "ordinary council relationship threshold");
        var ally = targets.Select(t => t.Enemy == "enemy1" ? t with { Kind = SharedRelationKind.Ally, ReciprocalKind = SharedRelationKind.Ally } : t).ToArray();
        Check(!Vote(100, wars: ally).Accepted && Vote(100, wars: ally).Votes[0].Reason == UnionAdmissionVoteReason.ProtectedAlly, "high applicant goodwill cannot erase a protected union alliance");
        Check(!Vote(100, wars: targets.Select(t => t with { NativeRoute = false }).ToArray()).Accepted, "no vote bypasses a missing native permission route");
        Check(!Vote(100, wars: targets.Select(t => t with { ApplicantHostile = false }).ToArray()).Accepted, "admission cannot invent a new applicant war");
        var existing = targets.Select(t => t with { Kind = SharedRelationKind.Hostile, ReciprocalKind = SharedRelationKind.Hostile, ApplicantHostile = false }).ToArray();
        Check(Vote(100, wars: existing).Accepted, "an already established union war can be reviewed without another declaration");
        Reject(() => Vote(100, new[] { "same", "same" }), "duplicate council seats");
        Reject(() => Vote(100, wars: new[] { targets[0], targets[0] }), "duplicate enemy scope");
        Reject(() => Vote(100, wars: new[] { targets[0] with { Enemy = "union" } }), "self-war");
        Reject(() => Vote(100, wars: new[] { targets[0] with { Kind = (SharedRelationKind)99 } }), "unknown native relation kind");
        Reject(() => Vote(100, wars: new[] { targets[0] with { ReciprocalKind = SharedRelationKind.Hostile } }), "asymmetric kind cannot be silently repaired");
        Reject(() => Vote(101), "invalid current goodwill");
        Reject(() => Vote(100, wars: []), "peaceful entry does not fabricate a war consent");
        Reject(() => UnionWarAdmission.Validate(consent with { Votes = consent.Votes.Select(v => v with { Weight = 2 }).ToArray() }), "unknown contribution or size supplies no invented influence bonus");
        var tie = consent with { Votes = new[] { consent.Votes[0], consent.Votes[1] with { Approve = false, Reason = UnionAdmissionVoteReason.Relationship } } };
        UnionWarAdmission.Validate(tie); Check(!tie.Accepted, "tie fails strict majority");
        var changedMembers = Vote(100, new[] { "new-town" }, leader: "successor");
        Check(UnionWarAdmission.SameWars(consent, changedMembers) && !UnionWarAdmission.SameScope(consent, changedMembers), "fresh changed electorate can be reviewed without altering original war scope");
        Check(!UnionWarAdmission.SameWars(consent, Vote(100, wars: new[] { targets[0] })), "recovery cannot quietly drop an enemy");
        var book = new CharterBook(); book.Register(new("p", "native-player", true), 0);
        var transfers = new TerritoryTransferLedger(); var player = new PlayerAllegiance("native-player");
        FollowerTransferPlan Plan(long now) => FollowerTransfers.Review(book, transfers, [], "union", now);
        var command = new AllegianceCommand(AllegianceAction.JoinUnion, 10, "union", Consent: true, NoActiveFeudalContract: true);
        var journal = new AllegianceTransitionLedger();
        void AtomicReject(Action action, string name)
        { string before = journal.Export(); bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected && journal.Export() == before, name); }
        AtomicReject(() => journal.Prepare("refused", player, command, Plan(10), transfers, Vote(49, new[] { "town" })), "rejected council creates no intent");
        AtomicReject(() => journal.Prepare("wrong", player, command, Plan(10), transfers, consent with { Union = "another" }), "consent must bind actual destination");
        AtomicReject(() => journal.Prepare("old", player, command, Plan(10), transfers, consent with { Tick = 9 }), "consent cannot be backdated");
        journal.Prepare("entry", player, command, Plan(10), transfers, consent);
        Check(journal.Pending && !journal.WarsReady && player.State.Mode == PlayerAllegianceMode.Independent, "saved war preparation is not native or membership execution");
        AtomicReject(() => journal.BeginWar("enemy1", 10), "no native declaration phase before recorded allegiance application");
        var copy = AllegianceTransitionLedger.Restore(journal.Export());
        Check(copy.Export() == journal.Export(), "prepared war consent roundtrip");
        copy.CancelUnchanged(player, transfers, 10); Check(!copy.Pending, "wholly unchanged war preparation can cancel");
        player.Apply(command); journal.ConfirmAllegiance(player, 10);
        AtomicReject(() => journal.BeginWar("unreviewed", 10), "no unreviewed enemy action");
        journal.BeginWar("enemy1", 10);
        Check(journal.Current.WarResults[0].Step == UnionWarStep.Applying && !journal.WarsReady, "applying intent retained before external callback");
        AtomicReject(() => journal.ObserveWar("enemy1", false, -75, -75, true, 10), "a return value is not reciprocal hostility proof");
        AtomicReject(() => journal.ObserveWar("enemy1", true, -101, -75, true, 10), "invalid native post-state");
        var interrupted = AllegianceTransitionLedger.Restore(journal.Export());
        interrupted.ObserveWar("enemy1", true, -75, -75, false, 10);
        Check(interrupted.Current.WarResults[0].Step == UnionWarStep.Verified && !interrupted.Current.WarResults[0].FromCompletedCall,
            "uncertain callback can be adopted only as observed hostility, without causal attribution or replay");
        journal = interrupted;
        AtomicReject(() => journal.Complete(player, Plan(10), 10), "membership cannot finish before every native war outcome");
        var freshTargets = targets.Select(t => t.Enemy == "enemy1" ? t with { Kind = SharedRelationKind.Hostile, ReciprocalKind = SharedRelationKind.Hostile, Goodwill = -75, ReciprocalGoodwill = -75 } : t).ToArray();
        var fresh = Vote(100, new[] { "new-town" }, freshTargets, 11, "successor");
        journal.ReaffirmWars(fresh, 11);
        Check(journal.Current.Wars.Representative == "leader" && journal.Current.WarReaffirmations.Last().Representative == "successor", "new current council and representative do not overwrite first consent");
        string reaffirmed = journal.Export(); journal.ReaffirmWars(fresh, 11);
        Check(journal.Export() == reaffirmed, "same current review does not duplicate reaffirmations");
        AtomicReject(() => journal.ReaffirmWars(fresh with { Targets = new[] { fresh.Targets[0] } }, 11), "cannot shorten admitted war scope during recovery");
        journal.BeginWar("enemy2", 11); journal.ObserveWar("enemy2", true, -75, -75, true, 12);
        Check(journal.WarsReady && journal.Current.WarResults[1].FromCompletedCall, "all actual reciprocal results are required");
        AtomicReject(() => journal.ReaffirmWars(fresh, 11), "recovery cannot precede a later native observation");
        AtomicReject(() => journal.CancelUnchanged(player, transfers, 12), "native or allegiance writes cannot be cancelled as unchanged preparation");
        journal.Complete(player, Plan(12), 12);
        Check(!journal.Pending && journal.Current.Phase == AllegianceTransitionPhase.Completed, "coordinated membership completes after wars and exact follower plan");
        Check(AllegianceTransitionLedger.Restore(journal.Export()).Export() == journal.Export(), "completed native observations and separate consents roundtrip");
        var saved = JsonSerializer.Deserialize<AllegianceTransitionSave>(journal.Export());
        void Bad(AllegianceTransition value, string name)
        { Reject(() => AllegianceTransitionLedger.Restore(JsonSerializer.Serialize(saved with { Current = value })), name); }
        Bad(saved.Current with { WarResults = null }, "war result scope missing");
        Bad(saved.Current with { Wars = null }, "observations cannot exist without original consent");
        Bad(saved.Current with { WarResults = new[] { saved.Current.WarResults[0], saved.Current.WarResults[0] } }, "duplicate result enemy");
        Bad(saved.Current with { WarResults = saved.Current.WarResults.Select(r => r with { ObservedTick = 9 }).ToArray() }, "observation cannot precede actual allegiance phase");
        Bad(saved.Current with { WarResults = saved.Current.WarResults.Select(r => new UnionWarResult(r.Enemy)).ToArray() }, "completed intent cannot contain unexecuted native stages");
        Bad(saved.Current with { Wars = saved.Current.Wars with { Union = "other" } }, "saved destination binding");
        var peace = new AllegianceTransitionLedger(); var independent = new PlayerAllegiance("native-player");
        peace.Prepare("peaceful", independent, command, Plan(10), transfers);
        Check(!peace.Export().Contains("Wars") && !peace.Export().Contains("WarResults"), "legacy peaceful intent JSON omits every new optional war field");
        var nextCommand = new AllegianceCommand(AllegianceAction.BindLiege, PoliticalWorld.Day * 4, "liege", "new-bond", Consent: true, UnionDuesSettled: true);
        player.Apply(new(AllegianceAction.UnionExitNotice, 13, NoticeTicks: PoliticalWorld.Day));
        var later = new AllegianceTransitionLedger();
        later.Prepare("later", player, nextCommand, Plan(PoliticalWorld.Day * 4) with { Target = "liege" }, transfers);
        UnionWarAdmission.ValidateHistory(new[] { journal.Export() }, later.Current);
        Check(true, "next membership retains prior war consent and native results in validated history");
        Reject(() => UnionWarAdmission.ValidateHistory(new[] { journal.Export(), journal.Export() }, later.Current), "duplicate historical intent");
        Reject(() => UnionWarAdmission.ValidateHistory(new[] { journal.Export() }, null), "history cannot lose current command lineage");
        Reject(() => UnionWarAdmission.ValidateHistory(new[] { peace.Export() }, later.Current), "peaceful unfinished records cannot masquerade as completed war history");
        Reject(() => UnionWarAdmission.ValidateHistory(new[] { journal.Export() }, peace.Current), "unrelated command head cannot adopt old wars");
        return count;
    }
}
