using System.Text.Json;

namespace Honor.Contracts.Rules;

/// <summary>Unconnected groundwork: the caller must prove RecordReturn is invoked inside the original normal native call.
/// This journal alone never proves a native return or grants authority to start a transfer.</summary>
public sealed record ResidentCareState(string Id, string Source, string Target, int? OriginalCare, bool Started,
    bool Returned, int? ReturnedCare, bool Consumed);

public sealed class ResidentCareJournal
{
    public ResidentCareState State { get; private set; }
    private ResidentCareJournal(ResidentCareState state) { State = state; Validate(); }
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static bool Care(int? c) => c == null || c is >= 0 and <= 4;
    private void Validate()
    {
        var s = State;
        if (s == null || !Key(s.Id) || !Key(s.Source) || !Key(s.Target) || s.Source == s.Target || !Care(s.OriginalCare) ||
            !Care(s.ReturnedCare) || s.Returned && (!s.Started || s.ReturnedCare.HasValue != s.OriginalCare.HasValue) ||
            !s.Returned && s.ReturnedCare != null || s.Consumed && !s.Returned)
            throw new ArgumentException("Invalid resident care journal.");
    }
    public static ResidentCareJournal Prepare(string id, string source, string target, int? care)
        => new(new(id, source, target, care, false, false, null, false));
    public void Begin(string faction, int? care)
    {
        if (State.Started || faction != State.Source || care != State.OriginalCare) throw new ArgumentException("Original resident state required.");
        State = State with { Started = true };
    }
    public void RecordReturn(string faction, int? care)
    {
        if (!State.Started || State.Returned || faction != State.Target || !Care(care) || care.HasValue != State.OriginalCare.HasValue)
            throw new ArgumentException("Exact normal native return required.");
        State = State with { Returned = true, ReturnedCare = care };
    }
    public bool MayRestore(string faction, int? care)
    {
        if (!State.Started || faction != State.Target || !Care(care)) throw new ArgumentException("Destination observation required.");
        if (care == State.OriginalCare) return false;
        if (!State.Returned || State.Consumed || care != State.ReturnedCare) throw new ArgumentException("Unobserved or consumed care change.");
        return true;
    }
    public void Consume(string faction, int? care)
    {
        if (!MayRestore(faction, care)) throw new ArgumentException("No observed reset to restore.");
        State = State with { Consumed = true };
    }
    public void SealUnchanged(string faction, int? care)
    {
        if (!State.Returned || State.Consumed || faction != State.Target || care != State.OriginalCare)
            throw new ArgumentException("Exact unchanged destination care required.");
        State = State with { Consumed = true };
    }
    public string Export() => JsonSerializer.Serialize(State);
    public static ResidentCareJournal Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 4096) throw new ArgumentException("Invalid resident care journal size.");
        return new(JsonSerializer.Deserialize<ResidentCareState>(json));
    }
}
