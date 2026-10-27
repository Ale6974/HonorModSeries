using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

internal static class FeudalAdmissionAuthority
{
    internal static FeudalAdmissionConsent Review(Faction realm, string charter, IEnumerable<string> moves)
    {
        var book = Current.Game.GetComponent<LiveCharterLedger>().Book;
        return FeudalAdmissions.Review(book, Faction.OfPlayer.GetUniqueLoadID(), charter, Find.TickManager.TicksGame,
            NpcDecisionAuthority.Read(realm), moves);
    }
    internal static void Verify(Faction realm, AllegianceTransition intent)
    {
        if (intent?.Feudal == null) return;
        var accepted = intent.FeudalReaffirmations?.LastOrDefault() ?? intent.Feudal;
        var fresh = Review(realm, intent.Feudal.Charter, intent.Feudal.Moves);
        if (!FeudalAdmissions.SameReview(accepted, fresh)) throw new ArgumentException("Current ordinary liege changed; explicitly review recovery.");
    }
}
