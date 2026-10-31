using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

public sealed class ResidentAreaRecord : IExposable
{
    public Pawn Pawn;
    public List<Map> Maps = [];
    public List<Area> Areas = [];
    public string Journal;
    public string Previous;
    public void ExposeData()
    {
        Scribe_References.Look(ref Pawn, "pawn");
        Scribe_Collections.Look(ref Maps, "originalMaps", LookMode.Reference);
        Scribe_Collections.Look(ref Areas, "originalAreas", LookMode.Reference);
        Scribe_Values.Look(ref Journal, "originalAreaResetV1");
        Scribe_Values.Look(ref Previous, "originalClinicalProofV1");
    }
}

internal static class ResidentAreaRecords
{
    private static readonly System.Reflection.FieldInfo Field = AccessTools.Field(typeof(Pawn_PlayerSettings), "allowedAreas");
    private static readonly System.Reflection.FieldInfo Owner = AccessTools.Field(typeof(Pawn_PlayerSettings), "pawn");
    internal static Dictionary<Map, Area> Read(Pawn p)
    {
        if (p?.playerSettings?.GetType() != typeof(Pawn_PlayerSettings) || Field == null || Field.IsStatic || Field.FieldType != typeof(Dictionary<Map, Area>) ||
            Owner == null || Owner.IsStatic || Owner.FieldType != typeof(Pawn) || Owner.GetValue(p.playerSettings) != p ||
            Field.GetValue(p.playerSettings) is not Dictionary<Map, Area> areas || areas.Count > 128)
            throw new ResidentMoveBlocked("RE_AreasUnavailable");
        foreach (var method in new[] {
            AccessTools.Method(typeof(Pawn_PlayerSettings), nameof(Pawn_PlayerSettings.Notify_FactionChanged), []),
            AccessTools.Method(typeof(Pawn), nameof(Pawn.SetFaction), [typeof(Faction), typeof(Pawn)]),
            AccessTools.PropertyGetter(typeof(Area), nameof(Area.Map)) })
            if (method == null || Harmony.GetPatchInfo(method)?.Owners.Any() == true) throw new ResidentMoveBlocked("RE_AreasUnavailable");
        foreach (var pair in areas)
            CheckReference(pair.Key, pair.Value);
        return areas;
    }
    private static void CheckReference(Map map, Area area)
    {
        if (map?.GetType() != typeof(Map) || !Find.Maps.Contains(map) || area == null || area.GetType().Assembly != typeof(Pawn).Assembly ||
            area.Map != map || !map.areaManager.AllAreas.Contains(area)) throw new ResidentMoveBlocked("RE_AreasUnavailable");
    }
    internal static ResidentAreaEntry[] Entries(Pawn p) => Read(p).Select(e => new ResidentAreaEntry(e.Key.GetUniqueLoadID(), e.Value.GetUniqueLoadID()))
        .OrderBy(e => e.Map, StringComparer.Ordinal).ToArray();
    internal static bool HasRestrictions(Pawn p)
        => p?.playerSettings != null && Field?.GetValue(p.playerSettings) is Dictionary<Map, Area> areas && areas.Count > 0;
    internal static ResidentAreaRecord Capture(Pawn p)
    {
        var areas = Read(p).OrderBy(e => e.Key.GetUniqueLoadID(), StringComparer.Ordinal).ToArray();
        return new() { Pawn = p, Maps = areas.Select(e => e.Key).ToList(), Areas = areas.Select(e => e.Value).ToList(), Journal = ResidentAreas.Prepare(p.GetUniqueLoadID(), Entries(p)) };
    }
    internal static void References(ResidentAreaRecord r, string id)
    {
        var state = ResidentAreas.Restore(r?.Journal);
        if (r.Pawn == null || r.Pawn.GetUniqueLoadID() != id || state.Id != id || r.Maps == null || r.Areas == null || r.Maps.Count != state.Original.Length || r.Areas.Count != r.Maps.Count)
            throw new ResidentMoveBlocked("RE_AreasUnavailable");
        for (int i = 0; i < r.Maps.Count; i++)
        {
            CheckReference(r.Maps[i], r.Areas[i]);
            if (state.Original[i] != new ResidentAreaEntry(r.Maps[i].GetUniqueLoadID(), r.Areas[i].GetUniqueLoadID())) throw new ResidentMoveBlocked("RE_AreasUnavailable");
        }
    }
    internal static string Proof(Pawn p, ResidentAreaRecord r = null)
    {
        if (r == null) return ResidentAreas.Proof(ResidentAreas.Prepare(p.GetUniqueLoadID(), Entries(p)));
        if (r.Pawn != p) throw new ResidentMoveBlocked("RE_AreasUnavailable");
        References(r, p.GetUniqueLoadID()); ResidentAreas.CheckCurrent(r.Journal, Entries(p)); return ResidentAreas.Proof(r.Journal);
    }
    internal static void Returned(ResidentAreaRecord r) { References(r, r.Pawn.GetUniqueLoadID()); r.Journal = ResidentAreas.Returned(r.Journal, Entries(r.Pawn)); }
    internal static void Restore(ResidentAreaRecord r)
    {
        References(r, r.Pawn.GetUniqueLoadID()); var actual = Entries(r.Pawn); ResidentAreas.CheckCurrent(r.Journal, actual);
        var state = ResidentAreas.Restore(r.Journal);
        if (state.Consumed) return;
        if (!state.Returned) throw new ResidentMoveBlocked("RE_AreasUnavailable");
        if (state.Cleared)
        {
            r.Journal = ResidentAreas.BeginRestore(r.Journal, actual);
            // Exact original Dictionary and original Map/Area references only. No game callbacks or area/grid changes.
            var dictionary = Read(r.Pawn);
            for (int i = actual.Length; i < state.Original.Length; i++) dictionary.Add(r.Maps[i], r.Areas[i]);
        }
        r.Journal = ResidentAreas.Complete(r.Journal, Entries(r.Pawn));
    }
}
