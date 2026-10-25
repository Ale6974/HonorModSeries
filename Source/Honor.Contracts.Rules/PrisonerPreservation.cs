using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record PrisonerEvidence(string Id, string OriginalFaction, string State, float Resistance, float Will);
public sealed record PrisonerActual(string Id, string OriginalFaction, string Host, string State, float Resistance, float Will,
    bool Prisoner = true, bool Slave = false);
public sealed record PrisonerNativeReset(string Host, float Resistance, float Will);

/// <summary>The host may change; the captive's own faction and custody status may not.</summary>
public static class PrisonerPreservation
{
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static bool Amount(float v) => RuntimeCompat.IsFinite(v) && v >= -1;
    public static readonly string None = Hash(new { Version = 1, Prisoner = (string)null });
    private static string Hash(object value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    public static string Proof(PrisonerEvidence before)
    {
        if (before == null || !Key(before.Id) || !Key(before.OriginalFaction) || !CityStockProof.Valid(before.State) ||
            !Amount(before.Resistance) || !Amount(before.Will)) throw new ArgumentException("Invalid prisoner evidence.");
        return Hash(new { Version = 1, Prisoner = before });
    }
    /// <returns>True only when the observed, journaled native reset still needs restoring.</returns>
    public static bool NeedsRestore(PrisonerEvidence before, PrisonerActual actual, string source, string target, PrisonerNativeReset reset)
    {
        Proof(before);
        if (!Key(source) || !Key(target) || source == target || before.OriginalFaction == source || before.OriginalFaction == target ||
            actual == null || actual.Id != before.Id || actual.OriginalFaction != before.OriginalFaction || actual.State != before.State ||
            !actual.Prisoner || actual.Slave || actual.Host != source && actual.Host != target || !Amount(actual.Resistance) || !Amount(actual.Will) ||
            reset != null && (reset.Host != target || !Amount(reset.Resistance) || !Amount(reset.Will) || reset.Resistance < 0 || reset.Will < 0))
            throw new ArgumentException("Prisoner identity, status, settings or custody changed.");
        if (actual.Resistance == before.Resistance && actual.Will == before.Will) return false;
        if (actual.Host == target && reset != null && actual.Resistance == reset.Resistance && actual.Will == reset.Will) return true;
        throw new ArgumentException("Unobserved prisoner reset or subsequent progress: never overwrite it.");
    }
}
