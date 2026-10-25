using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record RecruitmentInteraction(string Initiator, float ResistanceReduction, float Negotiation, float Mood, float Opinion);

/// <summary>Read-only evidence of an existing record or intended target. Never advances recruitment or converts anyone.</summary>
public static class PrisonerHistory
{
    private static string Hash(object value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    public static string Interaction(RecruitmentInteraction value)
    {
        if (value == null || value.Initiator?.Length > 4096 || value.Initiator?.Any(char.IsControl) == true ||
            !RuntimeCompat.IsFinite(value.ResistanceReduction) || !RuntimeCompat.IsFinite(value.Negotiation) ||
            !RuntimeCompat.IsFinite(value.Mood) || !RuntimeCompat.IsFinite(value.Opinion))
            throw new ArgumentException("Unsupported recruitment interaction evidence.");
        return Hash(new { Version = 1, Interaction = value });
    }
    public static string ConversionTarget(string id, bool registered, bool nativeType)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 250 || id.Any(char.IsControl) || !registered || !nativeType)
            throw new ArgumentException("Unverified conversion target reference.");
        return Hash(new { Version = 1, ConversionTarget = id });
    }
}
