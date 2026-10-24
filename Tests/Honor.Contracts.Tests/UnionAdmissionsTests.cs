using System.Text.Json;
using Honor.Contracts.Rules;

internal static class UnionAdmissionsTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        void Reject(UnionAdmissionLedger ledger, Action action, string name)
        {
            string before = ledger.Export(); bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected && ledger.Export() == before, name + ": rejected without partial mutation");
        }
        var city = new UnionCandidate("town", "source", 2);
        var forbidden = new[] {
            (city with { Player = true }, UnionExclusion.Player),
            (city with { AlreadyUnion = true }, UnionExclusion.AlreadyUnion),
            (city with { HasMap = true }, UnionExclusion.LoadedMap),
            (city with { Visited = true }, UnionExclusion.PreviouslyVisited),
            (city with { RecordedCharter = true }, UnionExclusion.RecordedCharter),
            (city with { Hostile = true }, UnionExclusion.Hostile),
            (city with { EligibleFaction = false }, UnionExclusion.IneligibleFaction),
            (city with { OwnerSettlementCount = 1 }, UnionExclusion.LastSettlement)
        };
        foreach (var (candidate, reason) in forbidden)
        {
            Check(UnionAdmissionRules.Exclusion(candidate) == reason, "explicit setup exclusion");
            var ledger = new UnionAdmissionLedger();
            Reject(ledger, () => ledger.Prepare("bad", candidate, "union", 0), "ineligible city cannot be admitted");
        }
        var admissions = new UnionAdmissionLedger();
        admissions.Prepare("admit", city, "union", 10);
        Check(admissions.Admission("admit").Phase == UnionAdmissionPhase.Prepared, "prepared intent is not applied ownership");
        Reject(admissions, () => admissions.Confirm("admit", "town", "source", false, false, 11), "unchanged source is not proof");
        Reject(admissions, () => admissions.Confirm("admit", "other-town", "union", false, false, 11), "wrong territory");
        Reject(admissions, () => admissions.Confirm("admit", "town", "conqueror", false, false, 11), "third party conquest cannot count as intended admission");
        Reject(admissions, () => admissions.Confirm("admit", "town", "union", true, false, 11), "loaded map cannot be claimed safe");
        Reject(admissions, () => admissions.Confirm("admit", "town", "union", false, true, 11), "player ownership cannot be repurposed");
        Reject(admissions, () => admissions.Prepare("parallel", city, "union", 11), "pending city cannot be admitted twice");
        var reload = UnionAdmissionLedger.Restore(admissions.Export());
        Check(reload.Export() == admissions.Export(), "pending ownership intent survives save roundtrip");
        reload.Confirm("admit", "town", "union", false, false, 11);
        Check(reload.Admission("admit").Phase == UnionAdmissionPhase.Applied, "verified union ownership resolves prepared admission");
        Reject(reload, () => reload.CancelUnmoved("admit", "town", "source", 12), "applied ownership cannot be silently undone");
        Reject(reload, () => reload.Confirm("admit", "town", "union", false, false, 12), "double confirmation");
        Reject(admissions, () => admissions.CancelUnmoved("admit", "town", "conqueror", 11), "third party change cannot be cancelled as unmoved");
        admissions.CancelUnmoved("admit", "town", "source", 11);
        Check(admissions.Admission("admit").Phase == UnionAdmissionPhase.Cancelled, "verified original owner permits cancellation");
        Reject(admissions, () => admissions.Prepare("admit", city, "union", 12), "historical IDs cannot be reused");
        admissions.Prepare("retry", city, "union", 12);
        Check(admissions.Admissions.Count == 2, "retry preserves cancelled history");
        Reject(admissions, () => admissions.Confirm("retry", "town", "union", false, false, 11), "clock reversal");
        var self = new UnionAdmissionLedger();
        Reject(self, () => self.Prepare("self", city, "source", 0), "same owner cannot create a new union admission");

        var candidates = Enumerable.Range(0, 30).Select(i => new UnionCandidate("town-" + i.ToString("D2"), "faction-" + (i / 3), 3)).ToArray();
        Check(UnionAdmissionRules.Plan(candidates, 0).Count == 0, "zero percentage makes no world changes");
        Check(UnionAdmissionRules.Plan(candidates, 10).Count == 3, "requested ratio determines bounded initial count");
        Check(UnionAdmissionRules.Plan(candidates, 30).Count == 9, "thirty percent configured count");
        Check(UnionAdmissionRules.Plan(candidates, 30, 2).Count == 2, "explicit setup cap");
        Check(UnionAdmissionRules.Plan(candidates, 30).Select(c => c.OwnerId).Distinct().Count() == 9, "at most one city per source faction");
        Check(UnionAdmissionRules.Plan(candidates.AsEnumerable().Reverse(), 30).SequenceEqual(UnionAdmissionRules.Plan(candidates, 30)), "plan independent of world enumeration order");

        // All combinations of exclusion flags remain excluded at every supported setup ratio.
        for (int mask = 1; mask < 128; mask++)
        foreach (int ratio in new[] { 1, 10, 30 })
        {
            var blocked = city with { Player = (mask & 1) != 0, HasMap = (mask & 2) != 0, Visited = (mask & 4) != 0,
                RecordedCharter = (mask & 8) != 0, Hostile = (mask & 16) != 0, EligibleFaction = (mask & 32) == 0,
                AlreadyUnion = (mask & 64) != 0 };
            Check(UnionAdmissionRules.Plan([blocked], ratio).Count == 0, "setup never overrides player/map/charter/hostility safeguards");
        }
        void Corrupt(UnionAdmissionSave save, string name)
        {
            bool rejected = false;
            try { UnionAdmissionLedger.Restore(JsonSerializer.Serialize(save)); } catch (ArgumentException) { rejected = true; }
            Check(rejected, name);
        }
        var saved = JsonSerializer.Deserialize<UnionAdmissionSave>(reload.Export());
        Corrupt(saved with { Version = 99 }, "future union schema rejected");
        Corrupt(saved with { Admissions = saved.Admissions.Append(saved.Admissions[0] with { Id = "duplicate" }).ToArray() }, "duplicate active city rejected");
        Corrupt(saved with { Admissions = [saved.Admissions[0] with { ResolvedTick = null }] }, "applied city needs confirmation time");
        Corrupt(saved with { LastTick = 9 }, "future admission cannot precede snapshot clock");
        Corrupt(saved with { Admissions = [saved.Admissions[0] with { OriginalOwner = "union" }] }, "same-owner corruption rejected");
        Check(UnionAdmissionLedger.Restore(admissions.Export()).Admissions.Count == 2, "cancelled and pending history survives reload together");
        return count;
    }
}
