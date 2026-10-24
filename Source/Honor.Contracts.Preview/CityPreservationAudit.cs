using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

/// <summary>Observations only. Never grants transfer authority or mutates a pawn, inventory or journal.</summary>
public sealed class CityPreservationAuditWindow : Window
{
    private readonly Settlement city;
    private readonly List<string> lines = [];
    private Vector2 scroll;
    private float height = 1000;
    public override Vector2 InitialSize => new(850, 760);
    public CityPreservationAuditWindow(Settlement settlement)
    {
        city = settlement;
        forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
        Refresh();
    }
    private void Refresh()
    {
        lines.Clear();
        if (city == null || city.Destroyed || !Find.WorldObjects.AllWorldObjects.Contains(city))
        { lines.Add("CA_Missing".Translate()); return; }
        lines.Add("CA_City".Translate(city.LabelCap, city.Faction?.Name ?? "CT_Missing".Translate()));
        lines.Add((city.HasMap ? "CA_MapLoaded" : "CA_MapUnloaded").Translate());
        if (city.Faction != null)
        {
            int bases = Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Count(s => !s.Destroyed && s.Faction == city.Faction);
            lines.Add("CA_SourceBases".Translate(bases));
        }
        if (city.GetType() != typeof(Settlement) || city.trader?.GetType() != typeof(Settlement_TraderTracker) || city.trader.settlement != city)
            lines.Add("CA_Custom".Translate());
        else
        {
            // Do not read Goods/StockListForReading: they may create new goods during inspection.
            var stock = city.trader.GetDirectlyHeldThings();
            lines.Add("CA_Stock".Translate(stock?.Count ?? 0, city.EverVisited ? "CA_Yes".Translate() : "CA_No".Translate()));
            if (stock == null) lines.Add("CA_NoStock".Translate());
            else
            {
                var excluded = stock.Select(CityStockPreservation.StockExclusion).Where(reason => reason != null).GroupBy(reason => reason);
                foreach (var group in excluded) lines.Add("CA_Excluded".Translate(group.Key.Translate(), group.Count()));
            }
        }
        var inhabitants = city.previouslyGeneratedInhabitants;
        if (inhabitants == null) { lines.Add("CA_RosterMissing".Translate()); return; }
        lines.Add("CA_Roster".Translate(inhabitants.Count));
        if (inhabitants.Count > 0) lines.Add("CA_ResidentsBlocked".Translate());
        var seen = new HashSet<Pawn>();
        // Summarize every reference; bound rendered detail separately so a large roster cannot break the window.
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        var details = new List<string>();
        foreach (var pawn in inhabitants)
        {
            var flags = new List<string>();
            if (pawn == null) flags.Add("CA_Invalid");
            else
            {
                if (!seen.Add(pawn)) flags.Add("CA_Duplicate");
                if (pawn.Destroyed || pawn.Dead) flags.Add("CA_Dead");
                if (pawn.Spawned || pawn.holdingOwner != null || !pawn.IsWorldPawn()) flags.Add("CA_Elsewhere");
                if (pawn.Faction?.IsPlayer == true) flags.Add("CA_Player");
                else if (pawn.Faction != city.Faction) flags.Add("CA_Foreign");
                if (PawnUtility.IsFactionLeader(pawn)) flags.Add("CA_Leader");
                if (pawn.IsPrisoner || pawn.IsSlave) flags.Add("CA_Captive");
            }
            foreach (string flag in flags) totals[flag] = totals.GetValueOrDefault(flag) + 1;
            if (details.Count < 20) details.Add("CA_Resident".Translate(pawn?.LabelShortCap.ToString() ?? "CT_Missing".Translate(),
                pawn?.Faction?.Name ?? "CT_Missing".Translate(), flags.Count == 0 ? "CA_Unverified".Translate() : string.Join(", ", flags.Select(f => f.Translate().ToString()))));
        }
        foreach (var pair in totals) lines.Add("CA_Excluded".Translate(pair.Key.Translate(), pair.Value));
        lines.AddRange(details);
        if (inhabitants.Count > details.Count) lines.Add("CA_Truncated".Translate(inhabitants.Count - details.Count));
    }
    public override void DoWindowContents(Rect rect)
    {
        if (Widgets.ButtonText(new Rect(0, 0, rect.width, 32), "CA_Refresh".Translate())) Refresh();
        var area = new Rect(0, 42, rect.width, rect.height - 85);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 24, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 28, 20000));
        list.Label("CA_Note".Translate());
        foreach (string line in lines) list.Label(line);
        height = list.CurHeight + 20; list.End(); Widgets.EndScrollView();
    }
}
