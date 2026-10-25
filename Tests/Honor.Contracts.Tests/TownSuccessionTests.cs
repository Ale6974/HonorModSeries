using System.Text.Json;
using Honor.Contracts.Rules;

internal static class TownSuccessionTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception("Town succession: " + name); }
        void Reject(CharterBook book, Action action, string name)
        { string before = book.Export(); bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected && book.Export() == before, name); }
        RecordedCharterTerms Terms(string parent, string child) => new(parent, child, 137, PoliticalWorld.Day * 7, PoliticalWorld.Day * 2, TributeTransport.SubjectDelivery, .23m);
        var authority = new NpcCharterAuthority("current-leader", "current-leader");
        (CharterBook Book, TerritoryObservation[] Observed) Seed(bool higher = false)
        {
            var book = new CharterBook(); book.Register(new("p", "native-player", true), 0);
            foreach (string id in new[] { "upper", "a", "b", "g", "higher", "extra" }) book.Register(new(id, "realm", false), 0);
            var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
            void Player(string id, string child)
            { book.Offer(id, Terms("p", child), 0, PoliticalWorld.Day, obs); book.Sign(id, true, 0, obs); book.Sign(id, false, 0, obs); }
            void Npc(string id, string parent, string child)
            { book.OfferNpc(id, Terms(parent, child), new("old-leader", "old-leader"), 0, PoliticalWorld.Day, obs); book.SignNpc(id, parent, "old-leader", 0, obs); book.SignNpc(id, child, "old-leader", 0, obs); }
            if (higher) { Player("top", "higher"); Npc("root", "higher", "upper"); }
            else Player("root", "upper");
            Npc("child-a", "upper", "a"); Npc("child-b", "upper", "b");
            if (!higher) Npc("grandchild", "a", "g");
            return (book, obs);
        }
        (TownSuccessionArchive Archive, CharterBook After) Lose(CharterBook book, string root, long tick, TownSuccessionArchive existing = null)
        {
            var entity = book.Entity(book.Charter(root).Terms.Subject); string before = book.Export();
            var after = CharterBook.Restore(before); after.CloseDestroyedTown(root, entity.Id, entity.NativeOwner, tick, tick, true, true);
            var archive = (existing ?? new()).Observe(new(root, entity.Id, entity.NativeOwner, "realm", "Lost town", tick, tick, before), after);
            return (archive, after);
        }
        var seed = Seed(); string legacy = seed.Book.Export();
        Check(!legacy.Contains("Replacement") && !legacy.Contains("Replaces"), "new null fields retain legacy book bytes");
        Reject(seed.Book, () => seed.Book.CloseDestroyedLeaf("root", "upper", "realm", 100, 100, true), "legacy leaf API does not silently gain upper-town authority");
        Reject(seed.Book, () => seed.Book.CloseDestroyedTown("root", "upper", "realm", 100, 100, false, true), "missing native destruction proof");
        var lost = Lose(seed.Book, "root", 100); var source = lost.After;
        Check(source.Charter("root").Phase == RecordedCharterPhase.TerritoryLost && source.Charter("child-a").Phase == RecordedCharterPhase.Active,
            "only destroyed-town bond ends before explicit review");
        Check(!source.PlayerManaged(source.Charter("child-a")), "orphaned chain is not silently treated as normal player authority");
        var observations = seed.Observed.Select(o => o.Id == "upper" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var beforeChild = source.Charter("child-a"); var deep = source.Charter("grandchild");
        var plan = lost.Archive.Review("root", source, "successors", authority, 200, observations);
        Check(source.Charter("child-a") == beforeChild && source.Charter("grandchild") == deep, "review is read-only");
        Check(plan.Replacements.Count == 2 && plan.Descendants.Length == 3 && plan.Parent == "p", "only direct children replaced; full deeper subtree reviewed");
        var completed = lost.Archive.Commit(source, plan, 200, observations);
        Check(completed.Book.Charter("child-a").Phase == RecordedCharterPhase.Replaced && completed.Book.Charter("child-b").Phase == RecordedCharterPhase.Replaced,
            "old direct contracts remain as explicit replaced history");
        Check(completed.Book.Charter("child-a").Terms == beforeChild.Terms && completed.Book.Charter("child-a").NpcAuthority == beforeChild.NpcAuthority &&
            completed.Book.Charter("child-a").PlayerReleaseTick == null && completed.Book.Charter("child-a").NpcReleaseTick == null,
            "old terms, actors and signatures are retained without fabricated release consent");
        Check(completed.Book.Charter(plan.Replacements["child-a"]).Terms == beforeChild.Terms with { Liege = "p" }, "only immediate liege changes in new contract");
        Check(completed.Book.Charter("grandchild") == deep && completed.Book.Root("g") == "p", "deep terms and history preserved while chain reconnects");
        Check(completed.Book.PlayerFollowers().Length == 3 && completed.Book.Charter("root").Phase == RecordedCharterPhase.TerritoryLost, "all surviving followers return without reviving destroyed-town bond");
        Check(completed.Book.Charter(plan.Replacements["child-a"]).PlayerSignedTick == 200 && completed.Book.Charter(plan.Replacements["child-a"]).NpcSignedTick == 200,
            "replacement receives actual newly reviewed bilateral consent time");
        Check(completed.Archive.Record("root").Resolution.Authority == authority, "current decision representatives saved separately from historical actors");
        var reload = TownSuccessionArchive.Restore(completed.Archive.Export()); reload.ValidateBook(CharterBook.Restore(completed.Book.Export()));
        Check(reload.Export() == completed.Archive.Export(), "native-loss scope and resolution survive reload");
        Reject(completed.Book, () => completed.Archive.Review("root", completed.Book, "again", authority, 201, observations), "completed resolution cannot repeat");
        Reject(source, () => lost.Archive.Commit(source, plan with { Parent = "higher" }, 200, observations), "caller cannot substitute another lord");
        Reject(source, () => lost.Archive.Commit(source, plan with { After = source.Export() }, 200, observations), "altered after-book rejected");
        var invalidIds = new Dictionary<string, string>(plan.Replacements) { ["child-a"] = null };
        Reject(source, () => lost.Archive.Commit(source, plan with { Replacements = invalidIds }, 200, observations), "malformed successor identities reject before parsing or changes");
        Reject(source, () => lost.Archive.Commit(source, plan, 201, observations), "stale confirmation time");
        var stale = observations.Select(o => o.Id == "g" ? o with { NativeOwner = "foreign" } : o).ToArray();
        Reject(source, () => lost.Archive.Review("root", source, "wrong-owner", authority, 200, stale), "a deeper stale owner blocks entire plan atomically");
        var missing = observations.Where(o => o.Id != "a").ToArray();
        Reject(source, () => lost.Archive.Review("root", source, "missing", authority, 200, missing), "missing survivor cannot be silently skipped");
        var changed = CharterBook.Restore(source.Export()); changed.Register(new("new-town", "realm", false), 200);
        Reject(changed, () => lost.Archive.Commit(changed, plan, 200, observations), "intervening book edits invalidate review");
        var partial = Seed(); partial.Book.ConsentNpcRelease("child-a", "upper", 1, partial.Observed); var partialLoss = Lose(partial.Book, "root", 100);
        Reject(partialLoss.After, () => partialLoss.Archive.Review("root", partialLoss.After, "partial", authority, 200, observations), "pending release signatures are not overwritten by succession");
        var higher = Seed(true); var higherLoss = Lose(higher.Book, "root", 100);
        var higherObs = higher.Observed.Select(o => o.Id == "upper" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var higherPlan = higherLoss.Archive.Review("root", higherLoss.After, "higher-plan", authority, 200, higherObs);
        var higherDone = higherLoss.Archive.Commit(higherLoss.After, higherPlan, 200, higherObs);
        Check(higherDone.Book.Charter(higherPlan.Replacements["child-a"]).Terms.Liege == "higher" &&
            higherDone.Book.Charter(higherPlan.Replacements["child-a"]).NpcAuthority == authority, "surviving NPC higher liege gets newly authorized NPC-only contracts");
        var saved = JsonSerializer.Deserialize<CharterBookSave>(completed.Book.Export());
        void Bad(CharterBookSave value, string reason)
        { bool rejected = false; try { CharterBook.Restore(JsonSerializer.Serialize(value)); } catch (ArgumentException) { rejected = true; } Check(rejected, reason); }
        Bad(saved with { Charters = saved.Charters.Select(c => c.Id == "child-a" ? c with { Replacement = null } : c).ToArray() }, "replacement phase cannot lose its provenance");
        Bad(saved with { Charters = saved.Charters.Select(c => c.Id == plan.Replacements["child-a"] ? c with { Replaces = null } : c).ToArray() }, "reciprocal successor link required");
        Bad(saved with { Charters = saved.Charters.Select(c => c.Id == plan.Replacements["child-a"] ? c with { Terms = c.Terms with { TributeValue = 138 } } : c).ToArray() }, "succession cannot silently renegotiate tribute");
        Bad(saved with { Charters = saved.Charters.Select(c => c.Id == "child-a" ? c with { PlayerReleaseTick = 200 } : c).ToArray() }, "no forged historical release slot");
        var empty = new TownSuccessionArchive();
        Reject(completed.Book, () => empty.ValidateBook(completed.Book), "replacement book requires its saved actual-loss archive");
        // A later loss of a promoted town can be resolved without rewriting an earlier succession receipt.
        string promoted = plan.Replacements["child-a"]; var later = Lose(completed.Book, promoted, 300, completed.Archive);
        var laterObs = observations.Select(o => o.Id == "a" ? o with { Exists = false, Eligible = false } : o).ToArray();
        var laterPlan = later.Archive.Review(promoted, later.After, "later", authority, 400, laterObs);
        var laterDone = later.Archive.Commit(later.After, laterPlan, 400, laterObs); laterDone.Archive.ValidateBook(laterDone.Book);
        Check(laterDone.Book.Root("g") == "p" && laterDone.Archive.Records.Count == 2, "successive real town losses keep both histories and reconnect the surviving grandchild");
        Check(source.Export() == lost.After.Export() && seed.Book.Export() == legacy, "source books remain unchanged throughout staged operations");
        var unmanaged = Seed(true); unmanaged.Book.ConsentToRelease("top", true, 1, unmanaged.Observed); unmanaged.Book.ConsentToRelease("top", false, 1, unmanaged.Observed);
        Reject(unmanaged.Book, () => unmanaged.Book.CloseDestroyedTown("root", "upper", "realm", 100, 100, true, true), "foreign unmanaged hierarchy is not given player succession authority");
        var pending = Seed();
        pending.Book.OfferNpc("open-root", Terms("upper", "extra"), authority, 1, PoliticalWorld.Day, pending.Observed);
        Reject(pending.Book, () => pending.Book.CloseDestroyedTown("root", "upper", "realm", 100, 100, true, true), "root-touching offer retains existing native-loss guard");
        pending = Seed(); pending.Book.OfferNpc("open-deep", Terms("a", "extra"), authority, 1, PoliticalWorld.Day, pending.Observed);
        var pendingLoss = Lose(pending.Book, "root", 100);
        Reject(pendingLoss.After, () => pendingLoss.Archive.Review("root", pendingLoss.After, "pending", authority, 200, observations), "deeper pending proposal blocks succession without being silently declined");
        pendingLoss.After.Decline("open-deep", 200);
        Check(pendingLoss.Archive.Review("root", pendingLoss.After, "cleared", authority, 200, observations).Replacements.Count == 2,
            "resolved proposal permits a fresh review without changing original loss evidence");
        var archiveSave = JsonSerializer.Deserialize<TownLossSave>(completed.Archive.Export());
        void BadArchive(TownLossSave value, string name)
        { bool rejected = false; try { var a = TownSuccessionArchive.Restore(JsonSerializer.Serialize(value)); a.ValidateBook(completed.Book); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        BadArchive(archiveSave with { Version = 99 }, "unknown archive version");
        BadArchive(archiveSave with { Records = new[] { archiveSave.Records[0], archiveSave.Records[0] } }, "duplicate native-loss identity");
        BadArchive(archiveSave with { Records = archiveSave.Records.Select(r => r with { Territory = "b" }).ToArray() }, "loss territory scope cannot be substituted");
        BadArchive(archiveSave with { Records = archiveSave.Records.Select(r => r with { Resolution = null }).ToArray() }, "completed replacement requires its reviewed receipt");
        var full = CharterBook.Restore(source.Export());
        for (int i = full.Charters.Count; i < CharterBook.MaxCharters - 1; i++)
        {
            full.Offer("capacity-" + i, Terms("p", "extra"), 200, PoliticalWorld.Day, observations); full.Decline("capacity-" + i, 200);
        }
        Reject(full, () => lost.Archive.Review("root", full, "full", authority, 200, observations), "insufficient capacity for whole sibling batch cannot partially reparent");
        return count;
    }
}
