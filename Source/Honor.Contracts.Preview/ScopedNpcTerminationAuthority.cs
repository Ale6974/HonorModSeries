using Honor.Contracts.Rules;
using RimWorld;
using Verse;
using HarmonyLib;

namespace Honor.Contracts.Preview;

internal static class NativeNpcContractAuthority
{
    internal static ScopedNpcContractDecision ReviewRelease(ScopedCharterReleaseWitness w, Faction realm)
        => Build(ScopedCharterReleases.Scope(w), w.Tick, [realm])?.Single();
    internal static ScopedNpcContractDecision ReviewAmendment(CharterAmendmentWitness w, Faction realm)
        => Build(CharterAmendments.GovernmentScope(w), w.Tick, [realm])?.Single();
    internal static ScopedNpcContractDecision[] Review(NpcTerminationWitness witness, IEnumerable<Faction> actualRealms)
        => Build(ScopedNpcTerminationAuthority.Scope(witness), witness.Tick, actualRealms);
    internal static ScopedNpcContractDecision[] ReviewReparent(NpcReparentWitness witness, IEnumerable<Faction> actualRealms)
        => Build(ScopedNpcReparentAuthority.Scope(witness), witness.Tick, actualRealms);
    internal static ScopedNpcContractDecision ReviewPersonal(PersonalOathBook book, PersonalOathEvent e, Faction realm)
        => Build(book.DecisionScope(e), e.Tick, [realm])?.Single();
    internal static ScopedNpcContractDecision ReviewInitial(InitialCharterMandate r, Faction realm)
        => Build(InitialCharterMandates.Scope(r), r.Tick, [realm])?.Single();
    internal static ScopedNpcContractDecision ReviewSuccession(TownSuccessionMandate w, Faction realm)
        => Build(TownSuccessionMandates.Scope(w), w.Plan.Tick, [realm])?.Single();
    internal static ScopedNpcContractDecision[] ReviewSuccessionParties(TownSuccessionMandate w, IEnumerable<Faction> realms)
        => Build(TownSuccessionMandates.Scope(w), w.Plan.Tick, realms);
    private static ScopedNpcContractDecision[] Build(string scope, long tick, IEnumerable<Faction> actualRealms)
    {
        var decisions = new List<ScopedNpcContractDecision>();
        foreach (var realm in actualRealms.OrderBy(f => f.GetUniqueLoadID(), StringComparer.Ordinal))
        {
            var observed = NpcDecisionAuthority.Observe(realm);
            if (observed.Regime == NpcDecisionRegime.Ordinary) continue;
            if (observed.Regime == NpcDecisionRegime.Union)
            {
                UnionPeaceCouncil.CheckRepresentative(realm);
                var seats = UnionPeaceCouncil.Seats(realm).OrderBy(s => s, StringComparer.Ordinal).ToArray();
                int goodwill = realm.GoodwillWith(Faction.OfPlayer);
                decisions.Add(new(realm.GetUniqueLoadID(), realm.leader.GetUniqueLoadID(), observed.Regime, scope, tick, 1, goodwill,
                    seats.Select(s => new NpcTerminationBallot(s, goodwill >= 50)).ToArray()));
            }
            else if (observed.Regime == NpcDecisionRegime.Royal)
            {
                var getter = AccessTools.Method(typeof(Pawn_RoyaltyTracker), nameof(Pawn_RoyaltyTracker.GetCurrentTitle), [typeof(Faction)]);
                if (!ModsConfig.RoyaltyActive || realm.leader?.royalty == null || realm.leader.royalty.GetType() != typeof(Pawn_RoyaltyTracker) ||
                    getter == null || Harmony.GetPatchInfo(getter)?.Owners.Any() == true) throw new ArgumentException("Current unpatched titled ruler mandate unavailable.");
                var title = realm.leader.royalty.GetCurrentTitle(realm);
                if (title == null || title.GetType() != typeof(RoyalTitleDef) || title.seniority < 1) throw new ArgumentException("Current ruler's own-realm royal title required.");
                decisions.Add(new(realm.GetUniqueLoadID(), realm.leader.GetUniqueLoadID(), observed.Regime, scope, tick, 1,
                    realm.GoodwillWith(Faction.OfPlayer), [], title.defName, title.seniority));
            }
            else throw new ArgumentException("Unsupported actual termination government.");
        }
        return decisions.Count == 0 ? null : decisions.ToArray();
    }
}
