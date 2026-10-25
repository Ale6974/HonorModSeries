using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed class TributePaymentWindow : Window
{
    private readonly string charterId;
    private readonly int period;
    private readonly Caravan caravan;
    private readonly Settlement receiver;
    private readonly QuotedTributeItem[] quote;
    private readonly Dictionary<Thing, int> selected = [];
    private Vector2 scroll;
    private float height = 1000;
    private TributePaymentLedger Payments => Current.Game.GetComponent<TributePaymentLedger>();
    public override Vector2 InitialSize => new(850, 760);
    private TributePaymentWindow(string id, int period, Caravan caravan, Settlement receiver, QuotedTributeItem[] quote)
    {
        charterId = id; this.period = period; this.caravan = caravan; this.receiver = receiver; this.quote = quote;
        forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
    }
    public static void DrawAccount(Listing_Standard list, LiveCharterLedger charters, RecordedCharter charter)
    {
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (payments.RestoreFailed) { list.Label("HCP_RestoreFailed".Translate()); return; }
        if (payments.Ledger.Accounts.All(a => a.CharterId != charter.Id))
        {
            list.Label("HT_OptIn".Translate());
            if (charter.Phase == RecordedCharterPhase.Active && list.ButtonText("HT_Enable".Translate())) payments.Enable(charter.Id);
            return;
        }
        payments.Synchronize(charter.Id);
        if (payments.RestoreFailed) { list.Label("HCP_RestoreFailed".Translate()); return; }
        list.Label("HT_Account".Translate());
        try
        {
            list.Label("PH_Charter".Translate()); LiveCharterLedger.DrawPerformance(list, charters.Performance(charter.Id, false));
            list.Label("PH_Pair".Translate()); LiveCharterLedger.DrawPerformance(list, charters.Performance(charter.Id, true));
        }
        catch (ArgumentException) { list.Label("PH_Unavailable".Translate()); }
        foreach (var bill in payments.Ledger.Bills(charter.Id, Find.TickManager.TicksGame).Where(b => !payments.Ledger.IsPaid(charter.Id, b.Period)).Take(20))
        {
            int now = Find.TickManager.TicksGame;
            list.Label("HT_Bill".Translate(bill.Period, bill.Principal.ToString("F2"), bill.RequiredValue.ToString("F2"),
                ((bill.DueTick - now) / (double)PoliticalWorld.Day).ToString("F1")));
            if (now > bill.GraceUntil) list.Label("HT_Overdue".Translate());
            var pending = payments.Ledger.Receipts.SingleOrDefault(r => r.CharterId == charter.Id && r.Period == bill.Period && r.Phase == TributeReceiptPhase.Prepared);
            if (pending != null)
            {
                if (list.ButtonText("HT_Recover".Translate())) payments.Recover(pending.Id);
            }
            else if (now >= bill.DueTick && list.ButtonText("HT_Choose".Translate())) OpenCaravanMenu(charters, charter.Id, bill.Period);
        }
        foreach (var paid in payments.Ledger.Receipts.Where(r => r.CharterId == charter.Id && r.Phase == TributeReceiptPhase.Committed).TakeLast(5))
            list.Label("HT_Receipt".Translate(paid.Period, paid.TotalValue.ToString("F2")));
    }
    private static void OpenCaravanMenu(LiveCharterLedger charters, string charterId, int period)
    {
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        var settlements = Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => s.Faction == charters.CounterpartyFaction(charterId) && !s.Destroyed).ToArray();
        var options = new List<FloatMenuOption>();
        foreach (var caravan in Find.WorldObjects.AllWorldObjects.OfType<Caravan>().Where(c => c.Faction == Faction.OfPlayer))
        foreach (var settlement in settlements.Where(s => s.Tile == caravan.Tile))
            options.Add(new FloatMenuOption("HT_Caravan".Translate(caravan.Label, settlement.Label), () => {
                try
                {
                    var quote = payments.Quote(charterId, caravan, settlement);
                    Find.WindowStack.Add(new TributePaymentWindow(charterId, period, caravan, settlement, quote));
                }
                catch (ArgumentException) { Messages.Message("HT_Unavailable".Translate(), MessageTypeDefOf.RejectInput); }
            }));
        if (options.Count == 0) Messages.Message("HT_NoCaravan".Translate(), MessageTypeDefOf.RejectInput);
        else Find.WindowStack.Add(new FloatMenu(options));
    }
    public override void DoWindowContents(Rect rect)
    {
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, 15000));
        list.Label("HT_RealWarning".Translate());
        var bill = Payments.Ledger.Bill(charterId, period);
        list.Label("HT_Required".Translate(receiver.Label, bill.RequiredValue.ToString("F2")));
        foreach (var item in quote.Where(q => !q.Thing.Destroyed && q.Thing.stackCount > 0))
        {
            int count = RuntimeCompat.Clamp(selected.GetValueOrDefault(item.Thing), 0, item.Thing.stackCount);
            list.Label("HT_Item".Translate(item.Thing.LabelNoCount, count, item.Thing.stackCount, item.Price.ToString("F2")));
            selected[item.Thing] = Mathf.RoundToInt(list.Slider(count, 0, item.Thing.stackCount));
        }
        var chosen = quote.Where(q => !q.Thing.Destroyed && q.Thing.stackCount > 0 && selected.GetValueOrDefault(q.Thing) > 0)
            .Select(q => (Item: q, Count: selected[q.Thing])).ToArray();
        decimal total = chosen.Sum(q => q.Count * q.Item.Price);
        list.Label("HT_Total".Translate(total.ToString("F2"), Math.Max(0, total - bill.RequiredValue).ToString("F2")));
        if (total >= bill.RequiredValue && list.ButtonText("HT_Deliver".Translate()))
        {
            Payments.Deliver(charterId, period, caravan, receiver, chosen);
            Close();
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
