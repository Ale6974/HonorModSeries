using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

internal sealed class NpcDecisionBlocked(string key) : ArgumentException(key)
{ internal string Key { get; } = key; }

internal static class NpcDecisionAuthority
{
    internal static NpcRealmDecisionEvidence Read(Faction faction)
    {
        var evidence = Observe(faction);
        try { NpcRealmDecision.Consent(evidence); }
        catch (ArgumentException) { throw new NpcDecisionBlocked(evidence.Regime != NpcDecisionRegime.Ordinary || !evidence.IndependentAuthority ? "ND_Unsupported" : "ND_Unavailable"); }
        return evidence;
    }
    // Observation is not consent. Nonordinary callers must validate their own exact action-specific mandate.
    internal static NpcRealmDecisionEvidence Observe(Faction faction)
    {
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null || faction?.def == null || Find.FactionManager == null || Faction.OfPlayer == null)
            throw new NpcDecisionBlocked("ND_Unavailable");
        if (faction.GetType() != typeof(Faction) || faction.def.GetType() != typeof(FactionDef) || faction.leader != null && faction.leader.GetType() != typeof(Pawn))
            throw new NpcDecisionBlocked("ND_Unsupported");
        var leader = faction.leader; var factions = Find.FactionManager.AllFactionsListForReading;
        if (factions == null || factions.Any(f => f == null)) throw new NpcDecisionBlocked("ND_Unavailable");
        var regime = faction.def.defName == UnionPoliticsLedger.UnionDefName ? NpcDecisionRegime.Union :
            faction.def.royalTitleTags?.Count > 0 ? NpcDecisionRegime.Royal : NpcDecisionRegime.Ordinary;
        var evidence = new NpcRealmDecisionEvidence(faction.GetUniqueLoadID(), leader?.GetUniqueLoadID(), regime,
            factions.Contains(faction) && factions.Count(f => f.GetUniqueLoadID() == faction.GetUniqueLoadID()) == 1,
            faction.GetType() == typeof(Faction) && faction.def.GetType() == typeof(FactionDef) && !faction.IsPlayer && !faction.Hidden && !faction.temporary &&
                !faction.defeated && !faction.def.permanentEnemy && faction.def.humanlikeFaction && faction.HasGoodwill,
            leader != null && leader.GetType() == typeof(Pawn) && !leader.Dead && !leader.Destroyed && !leader.Downed && leader.Awake() &&
                !leader.InMentalState && !leader.IsPrisoner && !leader.IsSlave && leader.HostFaction == null && leader.Faction == faction &&
                !leader.HasExtraHomeFaction() && !leader.HasExtraMiniFaction() && leader.GetExtraHostFaction() == null &&
                PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Contains(leader) && Find.QuestManager != null &&
                !Find.QuestManager.IsReservedByAnyQuest(faction) && !Find.QuestManager.IsReservedByAnyQuest(leader),
            leader != null && factions.Count(f => f.leader == leader) == 1,
            faction.HasGoodwill && !faction.HostileTo(Faction.OfPlayer) && !Faction.OfPlayer.HostileTo(faction) && faction.GoodwillWith(Faction.OfPlayer) >= 50,
            !UnionWarCouncil.HasExternalProvider);
        return evidence;
    }
}
