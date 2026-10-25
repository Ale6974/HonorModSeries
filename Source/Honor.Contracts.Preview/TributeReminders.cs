using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

public sealed class TributeReminderLedger : GameComponent
{
    private string snapshot;
    public TributeNotices Model { get; private set; } = new();
    public bool RestoreFailed { get; private set; }
    public TributeReminderLedger(Game game) { }
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) snapshot = Model.Export();
        Scribe_Values.Look(ref snapshot, "honorTributeRemindersV1");
        if (Scribe.mode != LoadSaveMode.PostLoadInit || snapshot == null) return;
        try { Model = TributeNotices.Restore(snapshot); }
        catch (Exception e) when (e is ArgumentException or JsonException)
        { RestoreFailed = true; Log.Warning("[Honor.Contracts.Preview] Invalid tribute reminder data retained; reminders disabled."); }
    }
    public override void FinalizeInit()
    {
        if (Model.LastScan > Find.TickManager.TicksGame)
        { RestoreFailed = true; Log.Warning("[Honor.Contracts.Preview] Future reminder clock retained; reminders disabled."); }
    }
    public override void GameComponentTick()
    {
        long now = Find.TickManager.TicksGame;
        if (now % 250 != 0 || RestoreFailed) return;
        var charters = Current.Game.GetComponent<LiveCharterLedger>();
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (charters.RestoreFailed || payments.RestoreFailed || Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges) return;
        try
        {
            if (!Model.BeginScan(now) || Model.Notices.Count >= TributeNotices.MaxNotices) return;
            var overdue = new List<TributeBill>();
            var reserved = new HashSet<(string, int)>(payments.Ledger.Receipts.Where(r => r.Phase == TributeReceiptPhase.Prepared).Select(r => (r.CharterId, r.Period)));
            var paid = new HashSet<(string, int)>(payments.Ledger.Receipts.Where(r => r.Phase == TributeReceiptPhase.Committed).Select(r => (r.CharterId, r.Period)));
            var known = new HashSet<(string, int)>(Model.Notices.Select(n => (n.CharterId, n.Period)));
            foreach (var account in payments.Ledger.Accounts)
            {
                var performance = TributePerformanceReview.ForCharter(charters.Book, payments.Ledger, account.CharterId, now);
                if (performance.Overdue == 0) continue;
                var c = charters.Book.Charter(account.CharterId);
                long end = c.Phase == RecordedCharterPhase.Released ? Math.Max(c.PlayerReleaseTick.Value, c.NpcReleaseTick.Value) : now;
                foreach (var bill in payments.Ledger.Bills(c.Id, now))
                {
                    var key = (c.Id, bill.Period);
                    if (bill.DueTick <= end && now > bill.GraceUntil && !reserved.Contains(key) && !paid.Contains(key) && !known.Contains(key)) overdue.Add(bill);
                    if (overdue.Count >= TributeNotices.MaxNotices - known.Count) break;
                }
                if (overdue.Count >= TributeNotices.MaxNotices - known.Count) break;
            }
            var notices = Model.RecordBatch(overdue, now);
            if (notices.Count == 0) return;
            string detail = string.Join("\n", notices.Take(10).Select(n => "PH_NoticeLine".Translate(
                charters.Label(charters.Book.Charter(n.CharterId).Terms.Liege), n.Period, n.RequiredValue.ToString("0.##"))));
            Find.LetterStack.ReceiveLetter("PH_NoticeTitle".Translate(), "PH_NoticeBody".Translate(notices.Count, detail), LetterDefOf.NeutralEvent);
        }
        catch (ArgumentException)
        { Log.Warning("[Honor.Contracts.Preview] Tribute reminders paused for this scan; actual obligations and payment evidence unchanged."); }
    }
}
