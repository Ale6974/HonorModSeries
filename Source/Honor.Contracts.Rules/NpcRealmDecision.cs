namespace Honor.Contracts.Rules;

public enum NpcDecisionRegime { Ordinary, Union, Royal, Unsupported }
public sealed record NpcRealmDecisionEvidence(string Realm, string Representative, NpcDecisionRegime Regime,
    bool Registered, bool Eligible, bool DecisionCapable, bool UniqueOffice, bool Willing, bool IndependentAuthority);

/// <summary>Existing ordinary-realm decision policy, not a collective vote or a native permission API.</summary>
public static class NpcRealmDecision
{
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    public static NpcCharterAuthority Consent(NpcRealmDecisionEvidence current)
    {
        if (current == null || !Key(current.Realm) || !Key(current.Representative) || current.Regime != NpcDecisionRegime.Ordinary ||
            !current.Registered || !current.Eligible || !current.DecisionCapable || !current.UniqueOffice || !current.Willing || !current.IndependentAuthority)
            throw new ArgumentException("Current ordinary independent realm decision authority is required; no collective decision is inferred.");
        return new(current.Representative, current.Representative);
    }
}
