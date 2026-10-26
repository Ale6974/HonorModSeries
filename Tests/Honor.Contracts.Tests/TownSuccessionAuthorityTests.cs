using Honor.Contracts.Rules;

internal static class TownSuccessionAuthorityTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Loss authority: " + name); }
        foreach (bool playerParent in new[] { true, false })
        {
            var book = new CharterBook(); book.Register(new("p", "native-player", true), 0);
            foreach (string id in new[] { "upper", "child", "grandchild", "higher" }) book.Register(new(id, "npc-realm", false), 0);
            var observed = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
            RecordedCharterTerms Terms(string parent, string child) => new(parent, child, 175, PoliticalWorld.Day * 7, 100, TributeTransport.SubjectDelivery, .17m);
            void Player(string id, string child)
            { book.Offer(id, Terms("p", child), 0, PoliticalWorld.Day, observed); book.Sign(id, true, 0, observed); book.Sign(id, false, 0, observed); }
            void Npc(string id, string parent, string child)
            {
                book.OfferNpc(id, Terms(parent, child), new("old-chief", "old-chief"), 0, PoliticalWorld.Day, observed);
                book.SignNpc(id, parent, "old-chief", 0, observed); book.SignNpc(id, child, "old-chief", 0, observed);
            }
            string parent = playerParent ? "p" : "higher";
            if (playerParent) Player("root", "upper"); else { Player("top", "higher"); Npc("root", "higher", "upper"); }
            Npc("child-bond", "upper", "child");
            if (playerParent) Npc("deep", "child", "grandchild"); else Player("side", "grandchild");
            string original = book.Export();
            var loss = new TownLossProof("root", "upper", "npc-realm", "npc-realm", "Lost upper", 10, 10, original);
            var archive = new TownSuccessionArchive(); var closed = archive.CloseForObservation(loss); archive = archive.Observe(loss, closed);
            string unchangedBook = closed.Export(), unchangedArchive = archive.Export();
            var decision = new NpcRealmDecisionEvidence("npc-realm", "current-chief", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
            NpcCharterAuthority Authority(NpcRealmDecisionEvidence facts = null, string owner = "npc-realm", string higher = null, string actualHigher = null)
                => TownSuccessionAuthority.Review(facts ?? decision, owner, higher ?? parent, playerParent, "p", "native-player", actualHigher ?? (playerParent ? "native-player" : "npc-realm"));
            void Reject(Action action, string message)
            {
                bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; }
                Check(rejected && closed.Export() == unchangedBook && archive.Export() == unchangedArchive, message);
            }
            var authority = Authority();
            Check(authority.SubjectRepresentative == "current-chief" && authority.LiegeRepresentative == (playerParent ? "p" : "current-chief"),
                "Current actual NPC owner and original higher player/NPC party have distinct legitimate roles.");
            Reject(() => Authority(owner: "different-owner"), "Do not substitute the player's different physical realm as the recorded NPC loss owner.");
            Reject(() => Authority(actualHigher: "foreign-realm"), "A changed original higher owner cannot inherit the scope.");
            if (playerParent) Reject(() => Authority(higher: "other-player"), "Another party cannot impersonate the actual original human player.");
            foreach (var changed in new[] { decision with { Regime = NpcDecisionRegime.Union }, decision with { Regime = NpcDecisionRegime.Royal },
                decision with { DecisionCapable = false }, decision with { IndependentAuthority = false }, decision with { UniqueOffice = false } })
                Reject(() => Authority(changed), "No unavailable, collective or provider consent is manufactured for ownership succession.");
            var actual = observed.Select(o => o.Id == "upper" ? o with { Exists = false, Eligible = false } : o).ToArray();
            var plan = archive.Review("root", closed, "reconnected", authority, 20, actual);
            Check(plan.Parent == parent && plan.Authority == authority && plan.Descendants.Length == (playerParent ? 2 : 1), "Actual review preserves the original higher party and full surviving subtree.");
            var foreign = actual.Select(o => o.Id == "child" ? o with { NativeOwner = "other" } : o).ToArray();
            Reject(() => archive.Commit(closed, plan, 20, foreign), "Current survivor ownership remains mandatory; no new transfer is inferred.");
            var completed = archive.Commit(closed, plan, 20, actual);
            string successor = plan.Replacements["child-bond"];
            Check(completed.Book.Charter(successor).Terms == book.Charter("child-bond").Terms with { Liege = parent } &&
                completed.Book.Charter("child-bond").Terms == book.Charter("child-bond").Terms &&
                completed.Book.Charter(playerParent ? "deep" : "side") == book.Charter(playerParent ? "deep" : "side"),
                "Real atomic succession retains original economic terms, source history and exact deeper contracts.");
            Check(completed.Book.Entity("child").NativeOwner == "npc-realm" && completed.Book.Entity("grandchild").NativeOwner == "npc-realm" &&
                completed.Book.Root("child") == "p", "Original NPC town ownership is separate from restored player contractual authority.");
            Check(!completed.Archive.HasUnresolved && TownSuccessionArchive.Restore(completed.Archive.Export()).Export() == completed.Archive.Export() &&
                CharterBook.Restore(completed.Book.Export()).Export() == completed.Book.Export(), "Committed actual book and loss resolution survive existing save schemas.");
            Check(completed.Archive.Record("root").Resolution.Authority == authority && book.Export() == original, "Audit records the new actual roles without rewriting old representatives/signatures.");
        }
        return count;
    }
}
