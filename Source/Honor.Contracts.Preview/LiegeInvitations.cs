using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Honor.Contracts.Preview;

public sealed partial class LiveCharterLedger
{
    public bool ReceiveLiegeInvitations = true;
    private int lastInvitationCheck = -1;
    private bool invitationClockInvalid;
    private void ExposeInvitations()
    {
        Scribe_Values.Look(ref ReceiveLiegeInvitations, "honorReceiveLiegeInvitations", true);
        Scribe_Values.Look(ref lastInvitationCheck, "honorLastLiegeInvitationCheck", -1);
    }
    public override void GameComponentTick()
    {
        int now = Find.TickManager.TicksGame;
        if (now % 250 != 0 || RestoreFailed || invitationClockInvalid) return;
        if (lastInvitationCheck < -1 || lastInvitationCheck > now)
        {
            invitationClockInvalid = true;
            Log.Warning("[Honor.Contracts.Preview] Invalid invitation clock retained; automatic invitations disabled.");
            return;
        }
        // No missed-offer backfill, including when enabled after a long disabled period.
        if (lastInvitationCheck == -1 || !ReceiveLiegeInvitations) { lastInvitationCheck = now; return; }
        if (now - lastInvitationCheck < LiegeInvitations.Interval) return;
        lastInvitationCheck = now;
        var payments = Current.Game.GetComponent<TributePaymentLedger>();
        if (Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges ||
            payments.RestoreFailed || payments.Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) ||
            !Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Any(s => !s.Destroyed && s.Faction == Faction.OfPlayer)) return;
        try { SendLiegeInvitation(now); }
        catch (ArgumentException) { /* Unavailable connected identities never authorize a partial offer. */ }
    }
    private void SendLiegeInvitation(int now)
    {
        var towns = Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s => !s.Destroyed && s.Faction != null)
            .OrderBy(s => s.GetUniqueLoadID(), StringComparer.Ordinal).ToArray();
        var options = towns.Select(s => s.Faction).Distinct().Where(f => !f.IsPlayer && !f.Hidden && !f.temporary &&
            !f.defeated && f.def.humanlikeFaction && f.HasGoodwill && !f.def.permanentEnemy && !f.HostileTo(Faction.OfPlayer) &&
            f.def.defName != RealmAdministrationLedger.DefName && f.def.defName != UnionPoliticsLedger.UnionDefName &&
            f.leader != null && !f.leader.Dead && f.leader.Faction == f && Find.FactionManager.AllFactionsListForReading.Contains(f)).ToArray();
        // Registration is staged. Even a failed ranking/topology check leaves the real book untouched.
        var copy = CharterBook.Restore(Book.Export()); var registry = bindings.ToList();
        string player = Register(copy, registry, null, true, false);
        var representatives = new Dictionary<string, Faction>(StringComparer.Ordinal);
        foreach (var f in options)
        {
            try
            {
                string id = Register(copy, registry, towns.First(s => s.Faction == f), false, true);
                representatives.Add(id, f);
            }
            catch (ArgumentException) { /* A stale faction binding cannot silence unrelated current candidates. */ }
        }
        foreach (string id in LiegeInvitations.Rank(copy, player, representatives.Select(p => new LiegeInvitationCandidate(p.Key, p.Value.GoodwillWith(Faction.OfPlayer))), now))
        {
            var faction = representatives[id];
            CharterOfferResult result;
            try { result = CharterOffers.Propose(copy, Guid.NewGuid().ToString("N"), player, id, true, new(),
                faction.GoodwillWith(Faction.OfPlayer), now, ObserveForMigration(null, copy, registry), Current.Game.GetComponent<TributePaymentLedger>().Ledger); }
            catch (ArgumentException) { continue; } // A cycle/depth blocker on one candidate need not silence all others.
            if (!result.Assessment.Accepted) continue;
            // Only the selected counterparty and player need registration. Discard unrelated candidate registrations.
            var kept = new HashSet<string>(Book.Entities.Select(e => e.Id), StringComparer.Ordinal) { player, id };
            var selected = System.Text.Json.JsonSerializer.Deserialize<CharterBookSave>(result.Book.Export());
            var finalBook = CharterBook.Restore(System.Text.Json.JsonSerializer.Serialize(selected with { Entities = selected.Entities.Where(e => kept.Contains(e.Id)).ToArray() }));
            Book = finalBook; bindings = registry.Where(b => kept.Contains(b.Identity)).ToList();
            Find.LetterStack.ReceiveLetter("LI_Title".Translate(), "LI_Arrived".Translate(faction.Name), LetterDefOf.NeutralEvent);
            return;
        }
    }
}
