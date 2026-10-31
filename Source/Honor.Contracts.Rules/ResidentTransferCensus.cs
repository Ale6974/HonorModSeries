using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record ResidentCensusState(string Town, string Source, string Target, int Profile, string Residents, string Proof);
public sealed record ResidentAuthorityState(string Id, bool Started = false, bool Returned = false);
public sealed record ResidentLocation(string Town, string Id);

/// <summary>Original whole resident scope and independently recorded native call facts, never permission to replay a call.</summary>
public static class ResidentTransferCensus
{
    public static void RequireUniqueLocations(string town, IEnumerable<string> residents, IEnumerable<ResidentLocation> locations)
    {
        var ids = residents.ToArray(); var all = locations.ToArray();
        if (!Key(town) || ids.Length is < 1 or > ResidentMoveSet.Capacity || ids.Any(id => !Key(id)) || ids.Distinct().Count() != ids.Length || all.Any(r => r == null || !Key(r.Town) || !Key(r.Id)))
            throw new ArgumentException("Invalid original resident location scope.");
        foreach (string id in ids)
            if (all.Count(r => r.Id == id) != 1 || !all.Any(r => r.Id == id && r.Town == town)) throw new ArgumentException("Resident appears in another town, is duplicated or missing.");
    }
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static string Hash(ResidentCensusState s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(s with { Proof = null }))));
    public static string Prepare(string town, string source, string target, int profile, ResidentMoveSet residents)
    {
        var s = new ResidentCensusState(town, source, target, profile, residents.Export(), null); s = s with { Proof = Hash(s) };
        return JsonSerializer.Serialize(Validate(s));
    }
    private static ResidentCensusState Validate(ResidentCensusState s)
    {
        if (s == null || !Key(s.Town) || !Key(s.Source) || !Key(s.Target) || s.Source == s.Target || s.Profile is < 1 or > 7 || s.Proof != Hash(s))
            throw new ArgumentException("Invalid original resident census.");
        _ = ResidentMoveSet.Restore(s.Residents); return s;
    }
    public static ResidentCensusState Restore(string json)
    {
        if (json == null || json.Length > 1000000) throw new ArgumentException("Invalid resident census size.");
        return Validate(JsonSerializer.Deserialize<ResidentCensusState>(json));
    }
    public static ResidentAuthorityState Call(string json)
    {
        if (json == null || json.Length > 4096) throw new ArgumentException("Invalid resident call journal size.");
        var s = JsonSerializer.Deserialize<ResidentAuthorityState>(json);
        if (s == null || !Key(s.Id) || s.Returned && !s.Started) throw new ArgumentException("Invalid resident call facts.");
        return s;
    }
    public static string Begin(string json)
    {
        var s = Call(json); if (s.Started) throw new ArgumentException("Never replay a started resident call.");
        return JsonSerializer.Serialize(s with { Started = true });
    }
    public static string Returned(string json)
    {
        var s = Call(json); if (!s.Started || s.Returned) throw new ArgumentException("Exact original normal return required.");
        return JsonSerializer.Serialize(s with { Returned = true });
    }
    public static void Check(string json, string proof, string town, string source, string target, int profile, ResidentMoveSet set,
        IReadOnlyList<string> calls, IReadOnlyList<string> care, bool executionStarted, bool complete = false)
    {
        var s = Restore(json);
        if (s.Proof != proof || s.Town != town || s.Source != source || s.Target != target || s.Profile != profile || s.Residents != set.Export() ||
            calls == null || care == null || calls.Count != set.Residents.Count || care.Count != set.Residents.Count) throw new ArgumentException("Original town/authority/resident census is unpaired.");
        for (int i = 0; i < calls.Count; i++)
        {
            var call = Call(calls[i]); var medical = ResidentCareJournal.Restore(care[i]).State; var r = set.Residents[i];
            if (call.Id != r.Id || medical.Id != r.Id || medical.Source != source || medical.Target != target || medical.OriginalCare != r.MedicalCare ||
                call.Started != medical.Started || medical.Returned && !call.Returned || !executionStarted && call.Started || complete && (!call.Returned || !medical.Returned || !medical.Consumed))
                throw new ArgumentException("Original resident call/medical evidence is incomplete or changed.");
        }
    }
    public static void RequireRecoverable(IReadOnlyList<string> calls, IReadOnlyList<string> care)
    {
        if (calls == null || care == null || calls.Count != care.Count) throw new ArgumentException("Missing original resident journals.");
        for (int i = 0; i < calls.Count; i++)
        {
            var c = Call(calls[i]); var m = ResidentCareJournal.Restore(care[i]).State;
            if (c.Started && (!c.Returned || !m.Returned)) throw new ArgumentException("Uncertain native call or missing original medical return observation; never replay or infer output.");
        }
    }
}
