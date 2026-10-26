namespace Honor.Contracts.Rules;

/// <summary>Restore only the recorded original higher party; physical player allegiance is not NPC town ownership.</summary>
public static class TownSuccessionAuthority
{
    private static bool Key(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 120 && !value.Any(char.IsControl);
    public static NpcCharterAuthority Review(NpcRealmDecisionEvidence realm, string recordedOwner, string parent,
        bool playerParent, string playerParty, string nativePlayer, string actualParentOwner)
    {
        var authority = NpcRealmDecision.Consent(realm);
        if (realm.Realm != recordedOwner || !Key(parent) ||
            (playerParent ? parent != playerParty || !Key(nativePlayer) || actualParentOwner != nativePlayer : actualParentOwner != realm.Realm))
            throw new ArgumentException("Original loss realm and original higher party must be verified separately from player allegiance.");
        return playerParent ? new(parent, authority.SubjectRepresentative) : authority;
    }
}
