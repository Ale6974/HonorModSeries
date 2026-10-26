using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;

internal static class CharterAidGenerationTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string why) { count++; if (!value) throw new Exception("Aid generation recovery: " + why); }
        void Reject(Action action, string why) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, why); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("n", "realm", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        book.Offer("c", new("n", "p", 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m), 0, PoliticalWorld.Day, obs);
        book.Sign("c", true, 0, obs); book.Sign("c", false, 0, obs); string original = book.Export();
        var d = new NpcRealmDecisionEvidence("realm", "ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var p = new CharterAidPlan("aid", "c", "realm", "ruler", "map", ["enemy"], ["colonist", "enemy"], 10, new string('A', 64), new string('B', 64), d);
        var legacy = new CharterAidReceipt(p, CharterAidOutcome.ArrivalsObserved, 11, ["new-a", "new-b"]);
        Check(RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(legacy)))) ==
            "CD8B539E35944B32D3F7B31C7C8BEA541A7CAA34034A4980E3D2BA866739BA05", "published0.3.48 receipt bytes keep their exact hash with null new fields");
        Check((int)CharterAidOutcome.Applying == 0 && (int)CharterAidOutcome.ArrivalsObserved == 1 && (int)CharterAidOutcome.ReturnedFalse == 2 &&
            (int)CharterAidOutcome.ReturnedUnverified == 3 && typeof(CharterAidReceipt).GetConstructors().Any(c => c.GetParameters().Length == 4), "legacy enum values and original constructor retained");
        var pending = new CharterProtection().Agree(book, "c", d, 75, true, 1).Begin(book, p, d, true, 10);
        string pendingBytes = pending.Export();
        Reject(() => pending.AcknowledgeGeneration(book, "aid", "realm", "map", ["later-army"], 12), "a later army cannot fill an old unknown call's missing native generation receipt");
        Reject(() => pending.RecordGeneration("aid", "foreign", "map", ["new-a"], 12), "actual source must match original plan");
        Reject(() => pending.RecordGeneration("aid", "realm", "elsewhere", ["new-a"], 12), "exact original target map required");
        Reject(() => pending.RecordGeneration("aid", "realm", "map", ["colonist"], 12), "original known pawn cannot be new native reinforcement");
        Reject(() => pending.RecordGeneration("aid", "realm", "map", ["new-a", "new-a"], 12), "duplicate generated IDs rejected");
        Reject(() => pending.RecordGeneration("aid", "realm", "map", [], 12), "empty native output gives no positive recovery authority");
        var generated = pending.RecordGeneration("aid", "realm", "map", ["new-a", "new-b"], 12);
        var row = generated.Requests.Single();
        Check(generated.Pending && row.Outcome == CharterAidOutcome.Applying && row.Generation.Tick == 12 && row.ReturnedTick == null && row.Arrivals == null,
            "inner normal generation return is saved separately while full native call stays unknown");
        Check(pending.Export() == pendingBytes && !pendingBytes.Contains("\"Generation\"") && !pendingBytes.Contains("\"AcknowledgedTick\""), "no backfill or legacy JSON rewrite");
        var reloaded = CharterProtection.Restore(generated.Export()); reloaded.ValidateBook(book);
        Check(reloaded.Export() == generated.Export(), "original inner return survives a saved interrupted outer dispatch");
        Reject(() => generated.RecordGeneration("aid", "realm", "map", ["new-c"], 13), "first positive generation output cannot be overwritten");
        Reject(() => reloaded.AcknowledgeGeneration(book, "aid", "foreign", "map", ["new-a", "new-b"], 13), "changed actual source blocks acknowledgement");
        Reject(() => reloaded.AcknowledgeGeneration(book, "aid", "realm", "other-map", ["new-a", "new-b"], 13), "changed current map blocks acknowledgement");
        Reject(() => reloaded.AcknowledgeGeneration(book, "aid", "realm", "map", ["new-a"], 13), "missing original identity stays blocked rather than inferred dead or repaired");
        Reject(() => reloaded.AcknowledgeGeneration(book, "aid", "realm", "map", ["new-a", "unrelated"], 13), "other current arrivals cannot replace a missing original");
        var repeated = generated.MarkRepeatedGenerationReturn("aid");
        Check(repeated.Pending && repeated.Requests.Single().Generation.RepeatedReturn && repeated.Requests.Single().Generation.PlanProof == row.Generation.PlanProof &&
            repeated.Requests.Single().Generation.Pawns.SequenceEqual(row.Generation.Pawns), "repeated matching native returns preserve original evidence and persist ambiguity");
        Reject(() => CharterProtection.Restore(repeated.Export()).AcknowledgeGeneration(book, "aid", "realm", "map", ["new-a", "new-b"], 13), "a repeated native scope cannot authorize recovery after load");
        Reject(() => repeated.Returned("aid", true, ["new-a"], 13), "ambiguous repeated generation cannot become a claimed full success");
        string beforeReview = reloaded.Export(); reloaded.ReviewGenerationAcknowledgement(book, "aid", "realm", "map", ["new-b", "new-a"], 13);
        Check(reloaded.Export() == beforeReview, "current identity review is read-only and order independent");
        var acknowledged = reloaded.AcknowledgeGeneration(book, "aid", "realm", "map", ["new-a", "new-b"], 13);
        var receipt = acknowledged.Requests.Single();
        Check(!acknowledged.Pending && receipt.Outcome == CharterAidOutcome.GenerationAcknowledged && receipt.ReturnedTick == null && receipt.AcknowledgedTick == 13 &&
            receipt.Plan.Tick == 10 && receipt.Generation.Tick == 12 && receipt.Generation.PlanProof == row.Generation.PlanProof,
            "explicit partial acknowledgement clears only pending aid and never fabricates full dispatch return or resets original quota clock");
        Check(CharterProtection.Restore(acknowledged.Export()).Export() == acknowledged.Export(), "unknown-full-call acknowledgement retains exact saved attribution");
        Reject(() => acknowledged.AcknowledgeGeneration(book, "aid", "realm", "map", ["new-a", "new-b"], 14), "acknowledgement cannot replay");
        Reject(() => acknowledged.Returned("aid", true, ["new-a", "new-b"], 14), "acknowledged uncertainty cannot later be relabelled as a full normal return");
        Reject(() => acknowledged.Begin(book, p with { Id = "too-soon", Tick = 14 }, d, true, 14), "old source-wide daily quota still applies");
        var next = acknowledged.Begin(book, p with { Id = "next-request", Tick = PoliticalWorld.Day + 10 }, d, true, PoliticalWorld.Day + 10);
        Check(next.Pending && next.Requests.Count == 2 && next.Requests.First().Outcome == CharterAidOutcome.GenerationAcknowledged,
            "a later distinct request can start only after explicit acknowledgement and original daily allowance");
        Reject(() => generated.Returned("aid", true, ["new-unrelated"], 13), "full normal return cannot claim unrelated generation identities");
        var finished = generated.Returned("aid", true, ["new-a"], 13);
        Check(finished.Requests.Single().Generation.Pawns.Length == 2 && finished.Requests.Single().Arrivals.SequenceEqual(new[] { "new-a" }),
            "normal full return retains original inner output and separately observed surviving arrivals");
        var exposed = generated.Requests.Single(); exposed.Generation.Pawns[0] = "changed";
        Check(generated.Requests.Single().Generation.Pawns[0] == "new-a", "UI arrays cannot mutate original positive native evidence");
        var closed = CharterBook.Restore(original); closed.ConsentToRelease("c", true, 20, obs); closed.ConsentToRelease("c", false, 20, obs);
        var historical = generated.AcknowledgeGeneration(closed, "aid", "realm", "map", ["new-a", "new-b"], 21); historical.ValidateBook(closed);
        Check(historical.Requests.Single().ReturnedTick == null && closed.Charter("c").Phase == RecordedCharterPhase.Released,
            "read-only historical acknowledgement works after lawful closure without reviving contract or requiring new NPC consent");
        var save = JsonSerializer.Deserialize<CharterProtectionSave>(acknowledged.Export()); var saved = save.Requests.Single();
        void Bad(CharterAidReceipt bad, string why) => Reject(() => CharterProtection.Restore(JsonSerializer.Serialize(save with { Requests = [bad] })), why);
        Bad(saved with { Generation = null }, "acknowledged state requires original generation evidence");
        Bad(saved with { ReturnedTick = 13 }, "partial acknowledgement cannot fake a normal full native return");
        Bad(saved with { AcknowledgedTick = 11 }, "acknowledgement cannot precede original generation return");
        Bad(saved with { Generation = saved.Generation with { PlanProof = new string('C', 64) } }, "changed original request scope digest rejected");
        Bad(saved with { Generation = saved.Generation with { RepeatedReturn = true } }, "repeated native scope cannot pair with an acknowledgement");
        Bad(saved with { Generation = saved.Generation with { Pawns = ["colonist", "new-b"] } }, "existing original pawn cannot be inserted into archived generation");
        Bad(saved with { Arrivals = ["new-a"] }, "partial current ID set cannot complete an exact-generation acknowledgement");
        Check(book.Export() == original && pending.Export() == pendingBytes && reloaded.Export() == beforeReview, "all paths retain original contracts, debt basis, native intent and proof on copies");
        return count;
    }
}
