using System.Text.Json;
using Honor.Contracts.Rules;

internal static class FactionMergerProposalTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Merger proposal: " + name); }
        void Reject(Action action, string name) { bool failed = false; try { action(); } catch (ArgumentException) { failed = true; } Check(failed, name); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0);
        foreach (string id in new[] { "a", "b" }) book.Register(new("territory/" + id, "source", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        foreach (string id in new[] { "a", "b" })
        {
            book.Offer(id, new("p", "territory/" + id, 137, PoliticalWorld.Day * 7, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .23m), 0, PoliticalWorld.Day, obs);
            book.Sign(id, true, 0, obs); book.Sign(id, false, 0, obs);
        }
        var transfers = new TerritoryTransferLedger();
        var scope = new FactionMergerScope("source", "administration", "player", "leader", "Source", "Target", "Leader", book.Export(), transfers.Export(),
            new[] { new MergerTown("territory/a", "a"), new MergerTown("territory/b", "b") }, new[] { "a", "b" }, new[] { "leader", "resident" }, new string('A', 64), 75, true, true);
        var proposals = new FactionMergerProposals(); string originalBook = book.Export(), originalTransfers = transfers.Export();
        Check(proposals.Proposals.Count == 0, "signed city charters do not create whole-faction merger consent");
        Check(FactionMergerProposals.Assess(scope) == FactionMergerResponse.Accepted, "explicit ordinary proposal boundary at75");
        Check(FactionMergerProposals.Assess(scope with { Goodwill = 74 }) == FactionMergerResponse.Relationship, "lower goodwill is a real NPC decline, not acceptance");
        Check(FactionMergerProposals.Assess(scope with { MutualAlly = false }) == FactionMergerResponse.InternalAlliance, "no alliance is invented");
        Check(FactionMergerProposals.Assess(scope with { SameExternalWars = false }) == FactionMergerResponse.ExternalWars, "proposal does not assume another existing war");
        proposals.Offer("first", scope, 10);
        Check(proposals.Proposals.Single().Phase == FactionMergerPhase.NpcAccepted && proposals.Proposals.Single().PlayerTick == null && proposals.HasOpen(10),
            "NPC response needs a separate player signature");
        string accepted = proposals.Export();
        Reject(() => proposals.Offer("other", scope, 11), "cannot overwrite open merger scope");
        foreach (var changed in new[] { scope with { Representative = "successor", NativePawns = new[] { "successor", "resident" } },
            scope with { Towns = scope.Towns.Take(1).ToArray() }, scope with { NativePawns = new[] { "leader", "resident", "new-pawn" } },
            scope with { NativeWorldObjects = new[] { "a", "b", "new-camp" } }, scope with { Diplomacy = new string('B', 64) }, scope with { Target = "other-authority" }, scope with { Goodwill = 74 } })
            Reject(() => proposals.Confirm("first", changed, 11), "changed ruler, complete scope, population, diplomacy or authority requires a new proposal");
        Check(proposals.Export() == accepted && book.Export() == originalBook && transfers.Export() == originalTransfers, "failed reviews preserve original consent and physical book/journal");
        bool immutable = false;
        try { ((IList<string>)proposals.Proposals[0].Scope.NativePawns)[0] = "other"; } catch (NotSupportedException) { immutable = true; }
        Check(immutable, "native affiliation evidence cannot be rewritten through public proposal arrays");
        var confirmed = FactionMergerProposals.Restore(accepted); confirmed.Confirm("first", scope, 12);
        Check(confirmed.Proposals[0].Phase == FactionMergerPhase.PlayerConfirmed && confirmed.Proposals[0].PlayerTick == 12 &&
            confirmed.Proposals[0].OfferedTick == 10 && confirmed.Proposals[0].Scope.Representative == "leader", "separate final signature retains original NPC response and representative");
        Check(FactionMergerProposals.Restore(confirmed.Export()).Export() == confirmed.Export(), "confirmed complete scope roundtrip");
        Reject(() => confirmed.Confirm("first", scope, 13), "player signature cannot replay");
        Check(book.Export() == originalBook && transfers.Export() == originalTransfers && transfers.Transfers.Count == 0, "consent never moves towns or creates a last-base exception");
        var declined = new FactionMergerProposals(); declined.Offer("no", scope with { Goodwill = 74 }, 10);
        Check(declined.Proposals[0].Phase == FactionMergerPhase.NpcDeclined && declined.Proposals[0].PlayerTick == null, "decline is retained without a fabricated player signature");
        Reject(() => declined.Confirm("no", scope, 11), "better later relations cannot turn a historical NPC decline into consent");
        var expired = FactionMergerProposals.Restore(accepted); long later = 11 + PoliticalWorld.Day * 3;
        Reject(() => expired.Confirm("first", scope, later), "expired scope cannot be signed");
        expired.Offer("second", scope, later); expired.Decline("first", later + 1);
        Check(FactionMergerProposals.Restore(expired.Export()).Export() == expired.Export(), "late decline of an expired earlier proposal preserves valid later-offer chronology");
        var saved = JsonSerializer.Deserialize<FactionMergerProposalSave>(confirmed.Export());
        void Corrupt(FactionMergerProposal alter, string name) => Reject(() => FactionMergerProposals.Restore(JsonSerializer.Serialize(saved with { Proposals = new[] { alter } })), name);
        Corrupt(saved.Proposals[0] with { Response = FactionMergerResponse.Relationship }, "response cannot contradict original signed scope");
        Corrupt(saved.Proposals[0] with { Phase = FactionMergerPhase.NpcAccepted }, "player time cannot survive a reverted phase");
        Corrupt(saved.Proposals[0] with { ExpiresTick = 13 }, "response window cannot be rewritten");
        Corrupt(saved.Proposals[0] with { PlayerTick = 9 }, "signature cannot precede original offer");
        Corrupt(saved.Proposals[0] with { Scope = scope with { Target = "player" } }, "native player target is never merger authority");
        Corrupt(saved.Proposals[0] with { Scope = scope with { NativePawns = new[] { "resident" } } }, "original representative cannot be omitted from faction scope");
        Corrupt(saved.Proposals[0] with { Scope = scope with { Towns = new[] { scope.Towns[0], scope.Towns[0] } } }, "duplicate town rights cannot be counted twice");
        Corrupt(saved.Proposals[0] with { Scope = scope with { Source = "foreign" } }, "unverified actual city owner is not source-wide consent");
        Reject(() => FactionMergerProposals.Restore(JsonSerializer.Serialize(saved with { Proposals = new[] { saved.Proposals[0], saved.Proposals[0] } })), "duplicate saved merger identity");
        var original = FactionMergerProposals.Restore(accepted); original.Decline("first", 12); original.Offer("second", scope, 13);
        var overlap = JsonSerializer.Deserialize<FactionMergerProposalSave>(original.Export());
        Reject(() => FactionMergerProposals.Restore(JsonSerializer.Serialize(overlap with { Proposals = overlap.Proposals.Select(p => p.Id == "first" ? p with { PlayerTick = 14 } : p).ToArray(), LastTick = 14 })),
            "historical late answer cannot conceal an overlapping earlier open proposal");
        var releasePending = CharterBook.Restore(originalBook); releasePending.ConsentToRelease("a", true, 1, obs);
        Reject(() => new FactionMergerProposals().Offer("release", scope with { Book = releasePending.Export() }, 10), "partial city release is not settled faction-wide consent");
        var pending = CharterBook.Restore(originalBook); pending.Register(new("territory/historic-c", "source", false), 1);
        pending.OfferNpc("open", new("territory/a", "territory/historic-c", 50, PoliticalWorld.Day, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m),
            new("leader", "leader"), 1, PoliticalWorld.Day, pending.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)));
        var withOffer = scope with { Book = pending.Export() };
        Reject(() => new FactionMergerProposals().Offer("live", withOffer, 10), "live touching NPC proposal blocks new merger consent");
        var afterExpiry = new FactionMergerProposals(); afterExpiry.Offer("after", withOffer, PoliticalWorld.Day + 2);
        Check(FactionMergerProposals.Restore(afterExpiry.Export()).Export() == afterExpiry.Export(), "expired old proposals remain archived without permanently blocking a later merger proposal");
        return count;
    }
}
