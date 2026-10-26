using System.Text.Json;
using Honor.Contracts.Rules;

internal static class NpcRealmDecisionTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string message) { count++; if (!ok) throw new Exception("Realm decision: " + message); }
        var book = new CharterBook(); book.Register(new("player", "native-player", true), 0);
        foreach (string id in new[] { "a", "b", "c" }) book.Register(new(id, "realm", false), 0);
        TerritoryObservation[] Observe() => book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        foreach (string id in new[] { "a", "b", "c" })
        {
            book.Offer("bond-" + id, new("player", id, 135, PoliticalWorld.Day, 200, TributeTransport.SubjectDelivery, .13m), 0, PoliticalWorld.Day, Observe());
            book.Sign("bond-" + id, true, 0, Observe()); book.Sign("bond-" + id, false, 0, Observe());
        }
        var current = new NpcRealmDecisionEvidence("realm", "ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var plan = NpcSubcharters.ReviewAuthorized(book, "bond-b", "a", "a-b", current, 1, Observe());
        string original = book.Export();
        void Reject(Action call, string name)
        {
            bool rejected = false; try { call(); } catch (ArgumentException) { rejected = true; }
            Check(rejected && book.Export() == original, name + ": no partial release/signature or debt/history mutation.");
        }
        Check(plan.Decision == current && book.Export() == original, "Review records current verified decision facts without mutating the common book.");
        foreach (var changed in new[] {
            current with { Regime = NpcDecisionRegime.Union }, current with { Regime = NpcDecisionRegime.Royal }, current with { Regime = (NpcDecisionRegime)99 },
            current with { Registered = false }, current with { Eligible = false }, current with { DecisionCapable = false }, current with { UniqueOffice = false },
            current with { Willing = false }, current with { IndependentAuthority = false }, current with { Representative = null } })
        {
            Reject(() => NpcSubcharters.ReviewAuthorized(book, "bond-b", "a", "other", changed, 1, Observe()), "No decision is manufactured from unsupported current authority");
            Reject(() => NpcSubcharters.CommitAuthorized(book, plan, changed, 1, Observe()), "An old review cannot replace changed or unavailable current consent");
        }
        Reject(() => NpcSubcharters.CommitAuthorized(book, plan, current with { Representative = "new-ruler" }, 1, Observe()), "A later legitimate ruler needs a new reviewed decision");
        Reject(() => NpcSubcharters.CommitAuthorized(book, plan, current with { Realm = "other-realm" }, 1, Observe()), "Matching actor text does not authorize a different realm");
        var legacy = NpcSubcharters.Review(book, "bond-b", "a", "legacy", new("ruler", "ruler"), 1, Observe());
        Check(!JsonSerializer.Serialize(legacy).Contains("Decision"), "Old plan JSON shape omits absent decision evidence.");
        Reject(() => NpcSubcharters.CommitAuthorized(book, legacy, current, 1, Observe()), "Legacy actor IDs are not backfilled as current ordinary decision proof");
        Reject(() => NpcSubcharters.CommitAuthorized(book, plan with { Authority = new("other", "other") }, current, 1, Observe()), "The signing actors must match both reviewed and current evidence");
        var restored = JsonSerializer.Deserialize<NpcSubcharterPlan>(JsonSerializer.Serialize(plan));
        var after = NpcSubcharters.CommitAuthorized(book, restored, current, 1, Observe());
        Check(after.Charter("a-b").Phase == RecordedCharterPhase.Active && after.Charter("bond-b").Phase == RecordedCharterPhase.Released,
            "Supported ordinary final consent atomically applies the actual replacement.");
        Check(after.Charter("a-b").Terms.TributeValue == 135 && after.Charter("a-b").Terms.TransportRate == .13m &&
            after.Charter("bond-c") == book.Charter("bond-c") && book.Export() == original, "Terms, unrelated contracts and original source history are retained.");
        Check(CharterBook.Restore(after.Export()).Export() == after.Export(), "The resulting actual book retains its existing persistent schema.");
        return count;
    }
}
