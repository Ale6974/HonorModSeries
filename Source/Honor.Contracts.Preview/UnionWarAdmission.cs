using System.Text.Json;
using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

internal sealed class UnionWarRejected(UnionAdmissionVoteReason reason) : ArgumentException("WU_Refused")
{ internal UnionAdmissionVoteReason Reason { get; } = reason; }

internal static class UnionWarCouncil
{
    internal static bool HasExternalProvider => ModLister.GetActiveModWithIdentifier("jaeger972.factionterritories2", true) != null ||
        ModLister.GetActiveModWithIdentifier("tsa.worlddominationexperimental", true) != null;
    internal static string Name(string id) => Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.GetUniqueLoadID() == id)?.Name ?? "HCL_Missing".Translate();
    private static Faction Enemy(string id) => Find.FactionManager.AllFactionsListForReading.SingleOrDefault(f => f.GetUniqueLoadID() == id)
        ?? throw new ArgumentException("Reviewed enemy no longer exists.");
    private static SharedRelationKind Kind(FactionRelationKind kind) => kind switch {
        FactionRelationKind.Hostile => SharedRelationKind.Hostile, FactionRelationKind.Ally => SharedRelationKind.Ally,
        FactionRelationKind.Neutral => SharedRelationKind.Neutral, _ => throw new ArgumentException("Unknown native relation.") };
    internal static UnionWarConsent Review(Faction union, IEnumerable<string> requiredEnemies = null,
        AdmissionAuthorityKind authorityKind = AdmissionAuthorityKind.UnionCouncil)
        => ReviewCore(union, requiredEnemies, authorityKind, null);
    internal static UnionWarConsent ReviewForTransition(Faction union, AllegianceTransition intent)
    {
        if (intent?.Wars == null) throw new ArgumentException("Original war admission required.");
        var original = intent.Wars;
        UnionWarAdmission.Validate(original);
        if (union?.GetUniqueLoadID() != original.Union) throw new ArgumentException("Re-review the exact recorded admission destination.");
        if (original.IncomingCharters == null) return Review(union, original.Targets.Select(t => t.Enemy), original.AuthorityKind);
        UnionPeaceCouncil.CheckRepresentative(union);
        var fresh = ReviewCore(union, original.Targets.Select(t => t.Enemy), original.AuthorityKind, intent)
            with { IncomingCharters = original.IncomingCharters.ToArray() };
        UnionWarAdmission.Validate(fresh); return fresh;
    }
    private static UnionWarConsent ReviewCore(Faction union, IEnumerable<string> requiredEnemies, AdmissionAuthorityKind authorityKind, AllegianceTransition intent)
    {
        if (!RuntimeCompat.IsDefined(authorityKind)) throw new ArgumentException("Unknown admission authority.");
        if (union == null || union.IsPlayer || union.def == null || union.Hidden || union.defeated || union.temporary || !union.HasGoodwill ||
            union.HostileTo(Faction.OfPlayer) || !Find.FactionManager.AllFactionsListForReading.Contains(union) || union.leader == null ||
            union.leader.Dead || union.leader.Destroyed || union.leader.Downed || union.leader.InMentalState || union.leader.IsPrisoner || union.leader.IsSlave ||
            union.leader.Faction != union) throw new ArgumentException("No eligible current admission representative.");
        if (authorityKind == AdmissionAuthorityKind.UnionCouncil)
        {
            var registry = Current.Game.GetComponent<UnionPoliticsLedger>();
            if (registry.RestoreFailed || union != registry.Union || union.def.defName != UnionPoliticsLedger.UnionDefName ||
                registry.Admissions.Admissions.Any(a => a.Phase == UnionAdmissionPhase.Prepared))
                throw new ArgumentException("Resolve pending union-town admission before counting members.");
        }
        else if (union.def.defName == UnionPoliticsLedger.UnionDefName || union == Current.Game.GetComponent<RealmAdministrationLedger>().OwnedFaction ||
            union.def.defName == RealmAdministrationLedger.DefName)
            throw new FeudalWarAuthorityUnavailable();
        var targets = (requiredEnemies ?? PlayerPoliticsLedger.AdmissionConflicts(union).Select(f => f.GetUniqueLoadID())).ToArray();
        if (targets.Length == 0) return null;
        if (authorityKind == AdmissionAuthorityKind.FeudalLeader && union.def.royalTitleTags?.Count > 0)
            throw new FeudalWarAuthorityUnavailable();
        if (HasExternalProvider) throw new UnionWarProviderUnavailable();
        var seats = intent == null ? Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => !s.Destroyed && s.Faction == union).Select(s => s.GetUniqueLoadID()).ToArray()
            : UnionPeaceCouncil.Seats(union, intent);
        var observations = targets.Select(id => {
            var enemy = Enemy(id);
            if (enemy == Faction.OfPlayer || enemy == union || enemy == Current.Game.GetComponent<RealmAdministrationLedger>().OwnedFaction ||
                enemy.Hidden || enemy.temporary || enemy.defeated || enemy.def == null)
                throw new ArgumentException("Unverifiable war admission target.");
            var left = union.RelationWith(enemy, true); var right = enemy.RelationWith(union, true);
            if (left == null || right == null) throw new ArgumentException("Missing reciprocal native relation.");
            bool hostile = left.kind == FactionRelationKind.Hostile && right.kind == FactionRelationKind.Hostile;
            bool route = hostile || union.CanChangeGoodwillFor(enemy, union.GoodwillToMakeHostile(enemy));
            return new UnionWarTarget(id, left.baseGoodwill, Kind(left.kind), right.baseGoodwill, Kind(right.kind), Faction.OfPlayer.HostileTo(enemy), route);
        }).ToArray();
        if (authorityKind == AdmissionAuthorityKind.FeudalLeader)
            return UnionWarAdmission.ReviewFeudal(union.GetUniqueLoadID(), Faction.OfPlayer.GetUniqueLoadID(), union.leader.GetUniqueLoadID(),
                Find.TickManager.TicksGame, observations, union.GoodwillWith(Faction.OfPlayer));
        return UnionWarAdmission.Review(union.GetUniqueLoadID(), Faction.OfPlayer.GetUniqueLoadID(), union.leader.GetUniqueLoadID(),
            Find.TickManager.TicksGame, seats, observations, union.GoodwillWith(Faction.OfPlayer));
    }
    internal static void ValidateAccepted(Faction union, UnionWarConsent accepted)
    {
        UnionWarAdmission.Validate(accepted);
        UnionWarConsent fresh;
        if (accepted.IncomingCharters == null) fresh = Review(union, accepted.Targets.Select(t => t.Enemy), accepted.AuthorityKind);
        else
        {
            var journal = Current.Game.GetComponent<PoliticalTransitionLedger>().Journal;
            if (!journal.Pending || journal.Current.Wars == null || !UnionWarAdmission.SameWars(journal.Current.Wars, accepted))
                throw new ArgumentException("Incoming-city war consent needs its exact current pending political intent.");
            fresh = ReviewForTransition(union, journal.Current);
        }
        if (!accepted.Accepted || fresh?.Accepted == false)
            throw new UnionWarRejected((fresh ?? accepted).Votes.First(v => !v.Approve).Reason);
        if (fresh == null || !UnionWarAdmission.SameScope(accepted, fresh) || accepted.Representative != fresh.Representative)
            throw new ArgumentException("WU_Refused".Translate());
        var unreviewed = PlayerPoliticsLedger.AdmissionConflicts(union).Select(f => f.GetUniqueLoadID()).Except(accepted.Targets.Select(t => t.Enemy)).Any();
        if (unreviewed) throw new ArgumentException("A new war needs a separate admission review.");
    }
    internal static void Apply(Faction union, AllegianceTransitionLedger journal, UnionWarConsent reviewed)
    {
        if (journal.Current.Wars == null) return;
        if (reviewed == null || !UnionWarAdmission.SameWars(journal.Current.Wars, reviewed)) throw new ArgumentException("Re-review war admission before recovery.");
        ValidateAccepted(union, reviewed); journal.ReaffirmWars(reviewed, Find.TickManager.TicksGame);
        foreach (string id in journal.Current.Wars.Targets.Select(t => t.Enemy))
        {
            ValidateAccepted(union, reviewed);
            var enemy = Enemy(id); var state = journal.Current.WarResults.Single(r => r.Enemy == id);
            bool hostile = union.RelationKindWith(enemy) == FactionRelationKind.Hostile && enemy.RelationKindWith(union) == FactionRelationKind.Hostile;
            if (state.Step == UnionWarStep.Verified)
            { if (!hostile) throw new ArgumentException("A later peace/third-party change requires reconciliation; never redeclare automatically."); continue; }
            if (hostile)
            {
                // A prior exception or another actor may have created this state. Observe it without attribution or another delta.
                journal.ObserveWar(id, true, union.BaseGoodwillWith(enemy), enemy.BaseGoodwillWith(union), false, Find.TickManager.TicksGame);
                continue;
            }
            var expected = reviewed.Targets.Single(t => t.Enemy == id);
            if (union.BaseGoodwillWith(enemy) != expected.Goodwill || enemy.BaseGoodwillWith(union) != expected.ReciprocalGoodwill ||
                Kind(union.RelationKindWith(enemy)) != expected.Kind || Kind(enemy.RelationKindWith(union)) != expected.ReciprocalKind ||
                !Faction.OfPlayer.HostileTo(enemy)) throw new ArgumentException("Reviewed war relation changed before native declaration.");
            journal.BeginWar(id, Find.TickManager.TicksGame);
            bool returned = union.TryAffectGoodwillWith(enemy, union.GoodwillToMakeHostile(enemy), false, false);
            hostile = union.RelationKindWith(enemy) == FactionRelationKind.Hostile && enemy.RelationKindWith(union) == FactionRelationKind.Hostile;
            if (!hostile) throw new ArgumentException("Native declaration did not establish reciprocal hostility; keep the intent.");
            journal.ObserveWar(id, true, union.BaseGoodwillWith(enemy), enemy.BaseGoodwillWith(union), returned, Find.TickManager.TicksGame);
        }
        ValidateAccepted(union, reviewed); // A native callback may have changed the ruler or electorate during the last declaration.
    }
    internal static void Verify(Faction union, AllegianceTransitionLedger journal)
    {
        if (journal.Current?.Wars == null) return;
        if (!journal.WarsReady || union.GetUniqueLoadID() != journal.Current.Wars.Union || journal.Current.Wars.Targets.Any(t => {
            var e = Enemy(t.Enemy); return union.RelationKindWith(e) != FactionRelationKind.Hostile || e.RelationKindWith(union) != FactionRelationKind.Hostile;
        })) throw new ArgumentException("Current reciprocal war outcomes are not verified.");
    }
    internal static void VerifyCurrentCouncil(Faction union, AllegianceTransition intent)
    {
        if (intent?.Wars?.IncomingCharters == null) return;
        ValidateAccepted(union, intent.WarReaffirmations?.LastOrDefault() ?? intent.Wars);
    }
}

internal sealed class UnionWarProviderUnavailable() : ArgumentException("WU_Provider");
internal sealed class FeudalWarAuthorityUnavailable() : ArgumentException("WF_Authority");
