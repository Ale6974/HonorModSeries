using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public static class ScopedNpcReparentAuthority
{
    public static string Scope(NpcReparentWitness w) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
        Action = "NpcSubcharterReparent/v1", w.Previous, w.Parent, w.Created, w.Before, w.Tick, w.Transfers, w.Payments, w.Observations,
        Parties = w.Parties?.Select(p => new { p.Party, p.ActualOwner }).OrderBy(p => p.Party, StringComparer.Ordinal).ToArray(),
        Retained = w.RetainedRealms?.Select(p => new { p.Party, p.ActualOwner }).OrderBy(p => p.Party, StringComparer.Ordinal).ToArray()
    }))));
    public static void Validate(NpcReparentWitness w) => ScopedNpcTerminationAuthority.ValidateDecisions(
        (w.Parties ?? []).Concat(w.RetainedRealms ?? []).Select(p => p.Decision).Append(w.Decision).ToArray(), w.Regimes, Scope(w), w.Tick);
}
