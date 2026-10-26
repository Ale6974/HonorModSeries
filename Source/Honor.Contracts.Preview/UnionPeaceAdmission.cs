using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

internal static class UnionPeaceCouncil
{
    internal static UnionPeaceConsent Review(Faction union, IEnumerable<string> incoming, AllegianceTransition intent = null)
    {
        _ = UnionWarCouncil.Review(union, Array.Empty<string>()); // Actual registered union and its current representative/registry.
        var leader = union.leader; var factions = Find.FactionManager.AllFactionsListForReading;
        if (UnionWarCouncil.HasExternalProvider || union.GetType() != typeof(Faction) || union.def.GetType() != typeof(FactionDef) ||
            leader.GetType() != typeof(Pawn) || !leader.Awake() || leader.HostFaction != null || leader.HasExtraHomeFaction() || leader.HasExtraMiniFaction() ||
            leader.GetExtraHostFaction() != null || factions.Any(f => f == null) || factions.Count(f => f.GetUniqueLoadID() == union.GetUniqueLoadID()) != 1 || factions.Count(f => f.leader == leader) != 1 ||
            !PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Contains(leader) || Find.QuestManager == null ||
            Find.QuestManager.IsReservedByAnyQuest(union) || Find.QuestManager.IsReservedByAnyQuest(leader) ||
            PlayerPoliticsLedger.AdmissionConflicts(union).Count != 0 || Faction.OfPlayer.HostileTo(union))
            throw new ArgumentException("Current independent membership council unavailable; wars require separate consent.");
        var actual = Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => !s.Destroyed && s.Faction == union).ToArray();
        if (actual.Any(s => s.GetType() != typeof(Settlement))) throw new ArgumentException("Unsupported union member town.");
        var pendingSeats = new HashSet<string>(StringComparer.Ordinal);
        if (intent?.Peace != null)
        {
            var charters = Current.Game.GetComponent<LiveCharterLedger>(); var towns = Current.Game.GetComponent<TerritoryAdministrationLedger>();
            foreach (var id in intent.Peace.IncomingCharters.Where(id => !Current.Game.GetComponent<PoliticalTransitionLedger>().Journal.LostCharters.Contains(id)))
            {
                var c = charters.Book.Charter(id); var entity = charters.Book.Entity(c.Terms.Subject); var city = charters.Binding(entity.Id).Settlement;
                if (city?.Faction != union) continue;
                UnionPeaceAdmission.RequireIncomingReceipt(intent, charters.Book, towns.Ledger, id, union.GetUniqueLoadID());
                pendingSeats.Add(city.GetUniqueLoadID());
            }
        }
        return UnionPeaceAdmission.Review(union.GetUniqueLoadID(), Faction.OfPlayer.GetUniqueLoadID(), leader.GetUniqueLoadID(),
            Find.TickManager.TicksGame, union.GoodwillWith(Faction.OfPlayer), actual.Select(s => s.GetUniqueLoadID()).Where(s => !pendingSeats.Contains(s)), incoming);
    }
    internal static void Verify(Faction union, AllegianceTransition intent)
    {
        if (intent?.Peace == null) return;
        var accepted = intent.PeaceReaffirmations?.LastOrDefault() ?? intent.Peace;
        var fresh = Review(union, intent.Peace.IncomingCharters, intent);
        if (!fresh.Accepted || !UnionPeaceAdmission.SameReview(accepted, fresh))
            throw new ArgumentException("Current council membership, representative or decision changed; explicitly review recovery.");
    }
}
