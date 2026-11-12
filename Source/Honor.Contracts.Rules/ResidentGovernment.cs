using System.Text.Json;
namespace Honor.Contracts.Rules;
/// <summary>Action-specific registration/wait consent; never clinical reset, faction or job restoration permission.</summary>
public static class ResidentGovernment
{
    public static void Validate(NpcRealmDecisionEvidence authority, string decision, string scope, long tick)
    {
        if (decision == null) { _ = NpcRealmDecision.Consent(authority); return; }
        if (authority == null || authority.Regime == NpcDecisionRegime.Ordinary || decision.Length > 100000) throw new ArgumentException("Separate current collective/royal decision required.");
        var actual = JsonSerializer.Deserialize<ScopedNpcContractDecision>(decision);
        if (actual == null || JsonSerializer.Serialize(actual) != decision) throw new ArgumentException("Canonical original government decision required.");
        ScopedNpcTerminationAuthority.ValidateDecisions([authority], [actual], scope, tick);
    }
    public static void Current(string original, string current)
    { if (original != current) throw new ArgumentException("Current seats/title/ruler decision changed; original authority is not reusable."); }
}
