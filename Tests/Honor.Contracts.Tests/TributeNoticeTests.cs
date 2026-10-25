using System.Text.Json;
using Honor.Contracts.Rules;

internal static class TributeNoticeTests
{
    public static int Run()
    {
        int count = 0; long day = PoliticalWorld.Day;
        void Check(bool value, string name) { count++; if (!value) throw new Exception("Notice: " + name); }
        void Reject(TributeNotices model, Action action, string name)
        { string before = model.Export(); bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected && model.Export() == before, name); }
        var notices = new TributeNotices(); var bill = new TributeBill("charter", 1, 0, 1, 100, 90);
        Check(notices.Enabled && notices.LastScan == -1 && notices.Notices.Count == 0, "new save has no historical reminder or obligation");
        Check(!notices.BeginScan(0) && notices.LastScan == 0, "first observation initializes without a backlog letter");
        Check(!notices.BeginScan(day - 1), "daily cadence lower bound");
        Check(notices.BeginScan(day), "daily cadence inclusive bound");
        var batch = notices.RecordBatch(new[] { bill }, day);
        Check(batch.Count == 1 && batch[0].RequiredValue == 90 && batch[0].NotifiedTick == day, "actual agreed value and first-observed time saved");
        string before = notices.Export();
        Check(notices.RecordBatch(new[] { bill }, day).Count == 0 && notices.Export() == before, "retries cannot send the same obligation twice");
        Reject(notices, () => notices.RecordBatch(new[] { bill, bill }, day), "duplicate input fails before mutations");
        Reject(notices, () => notices.RecordBatch(new[] { bill with { Period = 0 } }, day), "invalid bill number");
        Reject(notices, () => notices.RecordBatch(new[] { bill with { GraceUntil = day } }, day), "last grace tick is not overdue");
        Reject(notices, () => notices.RecordBatch(new[] { bill with { RequiredValue = 0 } }, day), "zero value is not a payable obligation");
        Reject(notices, () => notices.RecordBatch(new[] { bill with { CharterId = "bad\nidentity" } }, day), "malformed identity");
        Reject(notices, () => notices.RecordBatch(new[] { bill with { Period = 2 } }, day + 1), "batch must use current scan");
        Reject(notices, () => notices.BeginScan(day - 1), "reversed reminder clock");
        Reject(notices, () => notices.BeginScan((long)int.MaxValue + 1), "engine clock bound");
        var restored = TributeNotices.Restore(notices.Export());
        Check(restored.Export() == notices.Export(), "notice identity and timestamp roundtrip");
        Check(restored.BeginScan(day * 2) && restored.RecordBatch(new[] { bill }, day * 2).Count == 0, "loading cannot duplicate an old letter");
        restored.SetEnabled(false, day * 2);
        Check(!restored.BeginScan(day * 20), "disabled periods do not queue daily notices");
        restored.SetEnabled(true, day * 20);
        Check(!restored.BeginScan(day * 20 + 1) && restored.BeginScan(day * 21), "re-enable starts one new interval");
        var more = restored.RecordBatch(new[] { bill with { Period = 3 }, bill with { CharterId = "another", Period = 2 } }, day * 21);
        Check(more.Count == 2 && more[0].CharterId == "another", "one deterministic batch can summarize several obligations");
        bool readOnly = false;
        try { ((IList<TributeNotice>)more)[0] = more[1]; } catch (NotSupportedException) { readOnly = true; }
        Check(readOnly, "returned batch cannot be edited");
        var save = JsonSerializer.Deserialize<TributeNoticeSave>(restored.Export());
        void Bad(TributeNoticeSave value, string name)
        { bool rejected = false; try { TributeNotices.Restore(JsonSerializer.Serialize(value)); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        Bad(save with { Version = 99 }, "unsupported notice version");
        Bad(save with { LastScan = -2 }, "invalid saved scan clock");
        Bad(save with { Notices = null }, "missing saved notice array");
        Bad(save with { Notices = new[] { save.Notices[0], save.Notices[0] } }, "duplicate saved identity");
        Bad(save with { Notices = new[] { save.Notices[0] with { NotifiedTick = save.LastScan + 1 } } }, "future saved notification");
        Bad(save with { Notices = new[] { save.Notices[0] with { GraceUntil = save.Notices[0].NotifiedTick } } }, "saved notice cannot invent overdue status at grace boundary");
        restored.SetEnabled(false, day * 21);
        Check(!TributeNotices.Restore(restored.Export()).Enabled, "receive preference survives reload without dropping history");
        var full = new TributeNotices(); full.BeginScan(0); full.BeginScan(day);
        var limit = Enumerable.Range(0, TributeNotices.MaxNotices).Select(i => bill with { CharterId = "actual-" + i }).ToArray();
        Check(full.RecordBatch(limit, day).Count == TributeNotices.MaxNotices, "bounded archive accepts exactly its declared capacity");
        Check(full.RecordBatch(new[] { bill }, day).Count == 0 && full.Notices.Count == TributeNotices.MaxNotices, "full archive never evicts or replays earlier notices");
        Check(TributeNotices.Restore(full.Export()).Notices.Count == TributeNotices.MaxNotices, "full archive roundtrip");
        return count;
    }
}
