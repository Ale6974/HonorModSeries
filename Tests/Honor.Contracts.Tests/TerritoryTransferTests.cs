using System.Text.Json;
using Honor.Contracts.Rules;

internal static class TerritoryTransferTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        void Reject(TerritoryTransferLedger ledger, Action action, string name)
        {
            string before = ledger.Export(); bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected && before == ledger.Export(), name + ": no partial journal mutation");
        }
        TerritoryObservation[] Observations() => [new("player", "native-player", true, true, true), new("town", "old", true, false, true)];
        CharterBook Seed(bool sign = true)
        {
            var book = new CharterBook(); book.Register(new("player", "native-player", true), 0); book.Register(new("town", "old", false), 0);
            book.Offer("bond", new("player", "town", 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m), 0, PoliticalWorld.Day * 3, Observations());
            if (sign) { book.Sign("bond", false, 1, Observations()); book.Sign("bond", true, 2, Observations()); }
            return book;
        }
        var book = Seed(); var ledger = new TerritoryTransferLedger();
        var source = new TerritoryTransferObservation("town", "old", 2);
        void Prepare(TerritoryTransferObservation observation, string target = "liege", string authority = "liege", TerritoryTransferPurpose purpose = TerritoryTransferPurpose.ActiveCharter,
            string id = "move", CharterBook fromBook = null, long now = 3) => ledger.Prepare(id, fromBook ?? book, "bond", observation, target, authority, "union", purpose, now);
        Check(ledger.VerifiedOwner("town", "old", "old"), "unchanged baseline initially current");
        Check(!ledger.VerifiedOwner("town", "old", "conqueror"), "foreign ownership does not become consent proof");
        Reject(ledger, () => Prepare(source, fromBook: Seed(false)), "unsigned offer cannot move a town");
        var oneSignature = Seed(false); oneSignature.Sign("bond", false, 1, Observations());
        Reject(ledger, () => Prepare(source, fromBook: oneSignature), "NPC signature alone insufficient");
        Reject(ledger, () => Prepare(source, now: 1), "movement cannot precede bilateral signature");
        Reject(ledger, () => Prepare(source, authority: null), "independent player lacks administrative NPC authority");
        Reject(ledger, () => Prepare(source, target: "native-player", authority: "native-player"), "native player destination forbidden");
        Reject(ledger, () => Prepare(source, target: "wrong"), "destination must be verified authority");
        Reject(ledger, () => Prepare(source, purpose: TerritoryTransferPurpose.ReleasedToUnion, target: "union"), "active charter cannot claim release");
        var blocked = new[] { source with { Territory = "other" }, source with { Owner = "conqueror" }, source with { SourceSettlements = 1 },
            source with { Exists = false }, source with { Player = true }, source with { HasMap = true }, source with { Visited = true },
            source with { Eligible = false }, source with { TargetEligible = false }, source with { TargetPlayer = true }, source with { PendingDelivery = true } };
        foreach (var observation in blocked) Reject(ledger, () => Prepare(observation), "every unsafe live observation rejected");
        Prepare(source);
        Check(ledger.Transfer("move").Phase == TerritoryTransferPhase.Prepared, "intent is not applied ownership");
        Check(!ledger.VerifiedOwner("town", "old", "old") && !ledger.VerifiedOwner("town", "old", "liege"), "pending transfer blocks old and new consent observations");
        Check(book.Availability("town", [ledger.ObserveIdentity(Observations()[1], "old")]) == CharterAvailability.Ineligible, "pending execution blocks consent even before any owner change");
        Reject(ledger, () => Prepare(source, id: "parallel"), "pending transfer cannot be repeated");
        Reject(ledger, () => ledger.Confirm("move", source, 4), "old ownership is not execution proof");
        Reject(ledger, () => ledger.Confirm("move", source with { Owner = "third" }, 4), "third party conquest is not completion");
        Reject(ledger, () => ledger.Confirm("move", source with { Owner = "liege", HasMap = true }, 4), "loaded map after transfer suspends confirmation");
        Reject(ledger, () => ledger.Confirm("move", source with { Owner = "liege", Visited = true }, 4), "generated inhabitants or stock prevents confirmation");
        Reject(ledger, () => ledger.Confirm("move", source with { Owner = "liege", Player = true }, 4), "player ownership cannot be confirmed as NPC");
        Reject(ledger, () => ledger.Confirm("move", source with { Owner = "liege", Territory = "different" }, 4), "wrong town proof");
        Reject(ledger, () => ledger.Confirm("move", source with { Owner = "liege", Exists = false }, 4), "destroyed town proof");
        Reject(ledger, () => ledger.CancelUnmoved("move", source with { Owner = "third" }, 4), "cannot cancel conquest as unmoved");
        var pending = TerritoryTransferLedger.Restore(ledger.Export());
        Check(pending.Export() == ledger.Export() && pending.Pending("town"), "prepared transfer roundtrip");
        pending.CancelUnmoved("move", source, 4);
        Check(pending.VerifiedOwner("town", "old", "old"), "verified no-op cancellation restores availability");
        Reject(pending, () => pending.Prepare("move", book, "bond", source, "liege", "liege", "union", TerritoryTransferPurpose.ActiveCharter, 5), "cancelled IDs cannot be reused");
        ledger.Confirm("move", source with { Owner = "liege" }, 4);
        Check(ledger.ExpectedOwner("town", "old") == "liege" && ledger.VerifiedOwner("town", "old", "liege"), "only completed expected ownership normalizes original identity");
        var normalized = ledger.ObserveIdentity(new("town", "liege", true, false, true), "old");
        Check(book.Availability("town", [normalized]) == CharterAvailability.Current, "authorized execution remains usable by immutable charter identity");
        book.ValidateCurrent("bond", [Observations()[0], normalized]);
        Check(normalized.NativeOwner == "old" && ledger.ExpectedOwner("town", "old") == "liege", "consent identity and actual expected owner stay distinct");
        Check(book.Availability("town", [ledger.ObserveIdentity(new("town", "third", true, false, true), "old")]) == CharterAvailability.OwnershipChanged, "normalization cannot hide foreign conquest");
        Check(!ledger.ObserveIdentity(new("town", "liege", true, true, true), "old").Eligible, "player ownership cannot inherit NPC normalization");
        Check(!ledger.ObserveIdentity(new("town", "liege", false, false, true), "old").Eligible, "missing town cannot inherit NPC normalization");
        Check(!ledger.ObserveIdentity(new("town", "liege", true, false, false), "old").Eligible, "ineligible current owner is not normalized into eligibility");
        Check(!ledger.VerifiedOwner("town", "old", "third") && !ledger.VerifiedOwner("town", "old", "old"), "later conquest or old owner return is not silently legitimized");
        Reject(ledger, () => ledger.Confirm("move", source with { Owner = "liege" }, 5), "confirmation is not replayable");
        Reject(ledger, () => ledger.CancelUnmoved("move", source, 5), "cannot cancel completed execution");
        Reject(ledger, () => Prepare(source with { Owner = "liege" }, id: "same"), "no duplicate movement into same owner");
        book.ConsentToRelease("bond", true, 5, Observations());
        Reject(ledger, () => Prepare(source with { Owner = "liege" }, purpose: TerritoryTransferPurpose.ReleasedToUnion, target: "union", id: "release", now: 5), "one release signature insufficient");
        book.ConsentToRelease("bond", false, 6, Observations());
        Reject(ledger, () => Prepare(source with { Owner = "liege" }, id: "late", target: "new-liege", authority: "new-liege", now: 7), "released charter cannot move as active");
        Reject(ledger, () => Prepare(source with { Owner = "liege" }, purpose: TerritoryTransferPurpose.ReleasedToUnion, target: "union", id: "release", now: 5), "no release execution before final release consent");
        Reject(ledger, () => Prepare(source with { Owner = "liege" }, purpose: TerritoryTransferPurpose.ReleasedToUnion, target: "other", id: "release", now: 7), "release target must be actual union");
        Prepare(source with { Owner = "liege" }, purpose: TerritoryTransferPurpose.ReleasedToUnion, target: "union", id: "release", now: 7);
        ledger.Confirm("release", source with { Owner = "union" }, 8);
        Check(ledger.ExpectedOwner("town", "old") == "union" && ledger.Transfers.Count == 2, "release keeps immutable baseline and chained ownership history");
        Check(book.Entity("town").NativeOwner == "old" && book.Charter("bond").Phase == RecordedCharterPhase.Released, "execution cannot rewrite consent or native identity");
        Check(TerritoryTransferLedger.Restore(ledger.Export()).Export() == ledger.Export(), "active and release history roundtrip");
        book.Offer("new-bond", new("player", "town", 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m), 9, PoliticalWorld.Day, Observations());
        var unexecutedRelease = new TerritoryTransferLedger();
        Reject(unexecutedRelease, () => unexecutedRelease.Prepare("old-release", book, "bond", source, "union", null, "union", TerritoryTransferPurpose.ReleasedToUnion, 10),
            "movement cannot carry an open proposal's old-owner consent into a new faction");
        book.Sign("new-bond", true, 9, Observations()); book.Sign("new-bond", false, 9, Observations());
        Reject(unexecutedRelease, () => unexecutedRelease.Prepare("old-release", book, "bond", source, "union", null, "union", TerritoryTransferPurpose.ReleasedToUnion, 10),
            "new active contract blocks a genuinely different union destination");
        Reject(ledger, () => Prepare(source with { Owner = "union" }, target: "union", purpose: TerritoryTransferPurpose.ReleasedToUnion, id: "old-release", now: 10), "old release cannot bypass newer active contract");

        var saved = JsonSerializer.Deserialize<TerritoryTransfersSave>(ledger.Export());
        void Corrupt(TerritoryTransfersSave invalid, string name)
        {
            bool rejected = false;
            try { TerritoryTransferLedger.Restore(JsonSerializer.Serialize(invalid)); } catch (ArgumentException) { rejected = true; }
            Check(rejected, name);
        }
        Corrupt(saved with { Version = 99 }, "future schema rejected");
        Corrupt(saved with { LastTick = 7 }, "future resolution rejected");
        Corrupt(saved with { Transfers = null }, "missing history");
        Corrupt(saved with { Transfers = [null] }, "missing record");
        Corrupt(saved with { Transfers = [saved.Transfers[0] with { BaselineOwner = "fake" }] }, "forged baseline chain");
        Corrupt(saved with { Transfers = [saved.Transfers[0] with { ResolvedTick = null }] }, "applied intent needs proof time");
        Corrupt(saved with { Transfers = [saved.Transfers[0] with { Phase = (TerritoryTransferPhase)99 }] }, "unknown phase");
        Corrupt(saved with { Transfers = [saved.Transfers[0] with { Purpose = (TerritoryTransferPurpose)99 }] }, "unknown purpose");
        Corrupt(saved with { Transfers = [saved.Transfers[0], saved.Transfers[1] with { FromOwner = "third" }] }, "broken ownership chain");
        Corrupt(saved with { Transfers = [saved.Transfers[0], saved.Transfers[1] with { Id = "move" }] }, "duplicate execution ID");
        Corrupt(saved with { Transfers = [saved.Transfers[0] with { Phase = TerritoryTransferPhase.Prepared, ResolvedTick = null }, saved.Transfers[1]] }, "cannot skip unresolved intent");
        Corrupt(saved with { Transfers = [saved.Transfers[0] with { TargetOwner = "old" }] }, "same-owner forged transfer");
        Reject(ledger, () => ledger.CancelUnmoved("release", source, -1), "negative or reversed clock");
        return count;
    }
}
