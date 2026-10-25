using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

/// <summary>Unsaved draft. Opening, editing or dismissing this window never changes the charter book.</summary>
public sealed class CharterOfferWindow : Window
{
    private readonly Settlement target;
    private readonly Faction owner;
    private readonly Pawn representative;
    private readonly bool playerSubject;
    private int value = 100, interval = 5, grace = 1, allowance = 10;
    private string valueBuffer = "100", intervalBuffer = "5", graceBuffer = "1", allowanceBuffer = "10";
    private Vector2 scroll;
    private float height = 1200;
    public override Vector2 InitialSize => new(850, 760);
    public CharterOfferWindow(Settlement target, bool playerSubject)
    {
        this.target = target; this.playerSubject = playerSubject; owner = target?.Faction; representative = owner?.leader;
        forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
    }
    internal static string Reason(CharterOfferAssessment assessment) => assessment.Decision switch {
        CharterOfferDecision.Accepted => "CO_Acceptable".Translate(),
        CharterOfferDecision.GoodwillTooLow => "CO_Goodwill".Translate(assessment.MinimumGoodwill),
        CharterOfferDecision.ValueTooLow => "CO_ValueLow".Translate(assessment.Benchmark.ToString("0.##")),
        CharterOfferDecision.ValueTooHigh => "CO_ValueHigh".Translate(assessment.Benchmark.ToString("0.##")),
        CharterOfferDecision.GraceTooLong => "CO_GraceLong".Translate(),
        CharterOfferDecision.PriorTributeOverdue => "PH_OldDebt".Translate(),
        CharterOfferDecision.HistoryIncomplete => "PH_Incomplete".Translate(),
        _ => "HCL_Unavailable".Translate()
    };
    internal static void DrawTerms(Listing_Standard list, RecordedCharterTerms terms)
    {
        decimal rate = terms.TransportRate;
        decimal required = terms.Transport == TributeTransport.SubjectDelivery ? terms.TributeValue * (1 - rate) : terms.TributeValue * (1 + rate);
        list.Label("CO_Recorded".Translate(terms.TributeValue.ToString("0.##"), (terms.IntervalTicks / (double)PoliticalWorld.Day).ToString("0.##"),
            (terms.GraceTicks / (double)PoliticalWorld.Day).ToString("0.##"), (rate * 100).ToString("0.##"), required.ToString("0.##")));
        list.Label((terms.Transport == TributeTransport.SubjectDelivery ? "CO_SubjectDelivery" : "CO_LiegeCollection").Translate());
    }
    public override void DoWindowContents(Rect rect)
    {
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, 8000));
        list.Label("CO_Title".Translate());
        list.Label((playerSubject ? "CO_PlayerSubject" : "CO_PlayerLiege").Translate());
        list.Label("CO_Draft".Translate());
        list.Label("CO_Value".Translate(value)); list.TextFieldNumeric(ref value, ref valueBuffer, 1, 10000); value = Mathf.Clamp(value, 1, 10000);
        list.Label("CO_Interval".Translate(interval)); list.TextFieldNumeric(ref interval, ref intervalBuffer, 1, 60); interval = Mathf.Clamp(interval, 1, 60);
        list.Label("CO_Grace".Translate(grace)); list.TextFieldNumeric(ref grace, ref graceBuffer, 0, 30); grace = Mathf.Clamp(grace, 0, 30);
        list.Label("CO_Allowance".Translate(allowance)); list.TextFieldNumeric(ref allowance, ref allowanceBuffer, 0, 50); allowance = Mathf.Clamp(allowance, 0, 50);
        if (list.ButtonText("CO_Reset".Translate()))
        {
            value = 100; interval = 5; grace = 1; allowance = 10;
            valueBuffer = "100"; intervalBuffer = "5"; graceBuffer = "1"; allowanceBuffer = "10";
        }
        bool valid = int.TryParse(valueBuffer, out int v) && v == value && int.TryParse(intervalBuffer, out int i) && i == interval &&
            int.TryParse(graceBuffer, out int g) && g == grace && int.TryParse(allowanceBuffer, out int a) && a == allowance;
        var draft = new CharterOfferDraft(value, interval, grace, allowance);
        list.Label("CO_Net".Translate(draft.RequiredValue.ToString("0.##"), (draft.RequiredValue / interval).ToString("0.##")));
        bool current = target != null && !target.Destroyed && Find.WorldObjects.AllWorldObjects.Contains(target) && target.Faction == owner &&
            owner != null && !owner.IsPlayer && owner.HasGoodwill && !owner.defeated && !owner.HostileTo(Faction.OfPlayer) &&
            owner.leader == representative && representative != null && !representative.Dead && representative.Faction == owner;
        var ledger = Current.Game.GetComponent<LiveCharterLedger>();
        if (!valid) list.Label("CO_InvalidInput".Translate());
        else if (!current) list.Label("CO_Changed".Translate());
        else if (ledger.RestoreFailed || Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges) list.Label("PS_Pending".Translate());
        else
        {
            var payments = Current.Game.GetComponent<TributePaymentLedger>();
            if (payments.RestoreFailed) list.Label("PH_Unavailable".Translate());
            string npc = "faction/" + owner.GetUniqueLoadID();
            var assessment = draft.Assess(playerSubject, owner.GoodwillWith(Faction.OfPlayer));
            if (playerSubject && !payments.RestoreFailed && ledger.Book.Entities.Any(e => e.Id == npc) && ledger.Book.Entities.Any(e => e.Id == ledger.PlayerIdentity))
            {
                try
                {
                    var previous = ledger.Book.Charters.FirstOrDefault(c => c.Terms.Liege == npc && c.Terms.Subject == ledger.PlayerIdentity);
                    var history = previous == null ? new TributePerformance(0, 0, 0, 0, 0, 0, 0, 0) : ledger.Performance(previous.Id, true);
                    list.Label("PH_Pair".Translate()); LiveCharterLedger.DrawPerformance(list, history);
                    assessment = CharterOffers.AssessHistory(assessment, history);
                }
                catch (ArgumentException) { list.Label("PH_Unavailable".Translate()); valid = false; }
            }
            list.Label("CO_Policy".Translate(assessment.MinimumGoodwill, assessment.Benchmark.ToString("0.##")));
            list.Label("CO_Response".Translate(owner.Name, Reason(assessment)));
            if (valid && !payments.RestoreFailed && list.ButtonText("CO_Submit".Translate()) && ledger.Request(target, playerSubject, draft, owner, representative) && assessment.Accepted) Close();
        }
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
