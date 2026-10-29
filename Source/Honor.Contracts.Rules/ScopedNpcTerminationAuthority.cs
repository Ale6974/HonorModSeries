using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record NpcTerminationBallot(string Seat, bool Approve);
public sealed record ScopedNpcContractDecision(string Realm, string Representative, NpcDecisionRegime Regime, string Scope,
    long Tick, int Policy, int Goodwill, NpcTerminationBallot[] Ballots, string RoyalTitle = null, int Seniority = 0);

/// <summary>Action-specific mod decisions, never membership/war/city authority or a disguised ordinary ruler.</summary>
public static class ScopedNpcTerminationAuthority
{
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    public static string Scope(NpcTerminationWitness w) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
        Action = "NpcIncomingTermination/v1", w.Root, w.Before, w.Tick, w.Transfers, w.Payments, w.Observations,
        Parties = w.Parties.Select(p => new { p.Party, p.ActualOwner }).OrderBy(p => p.Party, StringComparer.Ordinal).ToArray(),
        Children = w.Children.Select(p => new { p.Party, p.ActualOwner }).OrderBy(p => p.Party, StringComparer.Ordinal).ToArray()
        , Claims = w.OwnershipClaims?.Select(c => new { c.Party, c.PreviousKnownOwner, c.ActualOwner, c.NoRecordedLoss }).OrderBy(c => c.Party, StringComparer.Ordinal).ToArray()
    }))));
    public static void Validate(NpcTerminationWitness w)
    {
        var all = w.Parties.Concat(w.Children).Select(p => p.Decision).Concat((w.OwnershipClaims ?? []).Select(c => c.PreviousClaimant)).Append(w.Authority).ToArray();
        ValidateDecisions(all, w.Regimes, Scope(w), w.Tick);
    }
    internal static void ValidateDecisions(NpcRealmDecisionEvidence[] all, ScopedNpcContractDecision[] decisions, string scope, long tick)
    {
        if (all.Any(d => d == null)) throw new ArgumentException("Missing current termination decision.");
        var required = all.Where(d => d.Regime != NpcDecisionRegime.Ordinary).Select(d => d.Realm).Distinct(StringComparer.Ordinal).ToArray();
        var scoped = decisions ?? [];
        if (scoped.Length != required.Length || scoped.Any(s => s == null) || scoped.Select(s => s.Realm).Distinct().Count() != scoped.Length ||
            !new HashSet<string>(required, StringComparer.Ordinal).SetEquals(scoped.Select(s => s.Realm)) || scoped.Length == 0 && decisions != null)
            throw new ArgumentException("Exact action-specific collective/royal decision scope required.");
        foreach (var d in all)
        {
            if (d.Regime == NpcDecisionRegime.Ordinary) { NpcRealmDecision.Consent(d); continue; }
            if (!Key(d.Realm) || !Key(d.Representative) || !d.Registered || !d.Eligible || !d.DecisionCapable || !d.UniqueOffice || !d.Willing || !d.IndependentAuthority)
                throw new ArgumentException("Current registered capable representative required.");
            var s = scoped.Single(s => s.Realm == d.Realm);
            if (s.Regime != d.Regime || s.Representative != d.Representative || s.Scope != scope || s.Tick != tick || s.Policy != 1 || s.Goodwill is < 50 or > 100 || s.Ballots == null)
                throw new ArgumentException("Decision cannot be reused for another action, ruler, tick or original scope.");
            if (d.Regime == NpcDecisionRegime.Union)
            {
                if (s.RoyalTitle != null || s.Seniority != 0 || s.Ballots.Length is < 1 or > 1000 || s.Ballots.Any(v => v == null || !Key(v.Seat)) ||
                    s.Ballots.Select(v => v.Seat).Distinct().Count() != s.Ballots.Length || s.Ballots.Any(v => v.Approve != (s.Goodwill >= 50)) ||
                    s.Ballots.Count(v => v.Approve) * 2 <= s.Ballots.Length)
                    throw new ArgumentException("Actual member-seat majority for this termination required.");
            }
            else if (d.Regime != NpcDecisionRegime.Royal || !Key(s.RoyalTitle) || s.Seniority < 1 || s.Ballots.Length != 0)
                throw new ArgumentException("Named current titled ruler mandate required; no noble votes are invented.");
        }
    }
}
