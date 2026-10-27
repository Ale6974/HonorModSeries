using System.Text.Json;
using Honor.Contracts.Rules;

internal static class AdmissionFollowerReviewTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception("Admission source: " + name); }
        void Reject(Action a, string name) { bool bad = false; try { a(); } catch (ArgumentException) { bad = true; } Check(bad, name); }
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("a", "source", false), 0); book.Register(new("b", "source", false), 0);
        var obs = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        var terms = new RecordedCharterTerms("p", "a", 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0);
        book.Offer("upper", terms, 0, PoliticalWorld.Day, obs); book.Sign("upper", true, 0, obs); book.Sign("upper", false, 0, obs);
        book.OfferNpc("lower", terms with { Liege = "a", Subject = "b" }, new("historic", "historic"), 0, PoliticalWorld.Day, obs);
        book.SignNpc("lower", "a", "historic", 0, obs); book.SignNpc("lower", "b", "historic", 0, obs);
        var physical = new TerritoryTransferLedger(); var candidates = new[] { new FollowerCandidate("upper", new("a", "source", 3)), new FollowerCandidate("lower", new("b", "source", 3)) };
        var plan = FollowerTransfers.Review(book, physical, candidates, "union", 10);
        var decision = UnionPeaceAdmission.Review("union", "player", "chief", 10, 75, ["member"], plan.Moves);
        var polity = new PlayerAllegiance("player"); var command = new AllegianceCommand(AllegianceAction.JoinUnion, 10, "union", Consent: true, NoActiveFeudalContract: true);
        var journal = new AllegianceTransitionLedger(); journal.PrepareReviewedAdmission("peace", polity, command, plan, physical, peace: decision);
        string original = journal.Export(), originalBook = book.Export(), originalPhysical = physical.Export();
        Check(journal.Current.FollowerReview.Original == plan.Stamp && journal.Current.FollowerReview.Proof.Length == 64, "actual original complete source review is saved before applying allegiance");
        Check(AllegianceTransitionLedger.Restore(original).Export() == original, "original graph, physical ledger, actual complete followers and moves replay on restore");
        var source = journal.Current.FollowerReview;
        Reject(() => AdmissionFollowerReviews.Validate(journal.Current with { FollowerReview = source with { Original = source.Original + " " } }), "changed original source fails its digest before use");
        Reject(() => AdmissionFollowerReviews.Validate(journal.Current with { FollowerReview = source with { Proof = new string('A', 64) } }), "unpaired original source digest rejected");
        Reject(() => AdmissionFollowerReviews.Validate(journal.Current with { TransferBaseline = new string('B', 64) }), "physical baseline must match the original captured full ledger");
        Reject(() => AdmissionFollowerReviews.Validate(journal.Current with { PreparedTick = 11 }), "later clock cannot relabel an earlier review");
        Reject(() => AdmissionFollowerReviews.Validate(journal.Current with { Followers = ["upper"] }), "original deeper follower cannot disappear from scope");
        Reject(() => AdmissionFollowerReviews.Validate(journal.Current with { Peace = decision with { IncomingCharters = ["upper"] } }), "original actual incoming move cannot become a pre-existing member by omission");
        var changed = plan with { Stamp = plan.Stamp.Replace("\"SourceSettlements\":3", "\"SourceSettlements\":2") };
        Reject(() => new AllegianceTransitionLedger().PrepareReviewedAdmission("bad", polity, command, changed, physical, peace: decision), "cumulative source-base guard is replayed from the preserved actual input");
        var shortened = System.Text.Json.Nodes.JsonNode.Parse(plan.Stamp); shortened["Entries"].AsArray().RemoveAt(0);
        Reject(() => new AllegianceTransitionLedger().PrepareReviewedAdmission("bad", polity, command, plan with { Stamp = shortened.ToJsonString() }, physical, peace: decision),
            "a freshly hashed but shortened original snapshot still fails complete graph reconstruction");
        var foreignTarget = System.Text.Json.Nodes.JsonNode.Parse(plan.Stamp); foreignTarget["Target"] = "other";
        Reject(() => new AllegianceTransitionLedger().PrepareReviewedAdmission("bad", polity, command, plan with { Stamp = foreignTarget.ToJsonString() }, physical, peace: decision),
            "a fresh digest cannot relabel another destination as the original admission");
        Check(journal.Export() == original && book.Export() == originalBook && physical.Export() == originalPhysical && polity.State.Mode == PlayerAllegianceMode.Independent,
            "all invalid staging leaves the current political and actual source models unchanged");
        var legacy = new AllegianceTransitionLedger(); legacy.PreparePeace("legacy", polity, command, plan, physical, decision);
        Check(legacy.Current.FollowerReview == null && !legacy.Export().Contains("FollowerReview") && AllegianceTransitionLedger.Restore(legacy.Export()).Export() == legacy.Export(), "older null source records stay unchanged without inferred original inputs");
        var war = UnionWarAdmission.BindIncoming(UnionWarAdmission.Review("union", "player", "chief", 10, ["member"],
            [new("enemy", 0, SharedRelationKind.Neutral, 0, SharedRelationKind.Neutral, true, true)], 75), plan);
        var warJournal = new AllegianceTransitionLedger(); warJournal.PrepareReviewedAdmission("war", polity, command, plan, physical, wars: war);
        Check(warJournal.Current.FollowerReview.Original == plan.Stamp && warJournal.Current.Peace == null, "same captured original input binds actual wartime admission without granting peace or extra war consent");
        polity.Apply(command); journal.ConfirmAllegiance(polity, 10);
        physical.Prepare("one", book, "upper", candidates[0].Observation, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 11);
        physical.Confirm("one", candidates[0].Observation with { Owner = "union" }, 11);
        var partial = FollowerTransfers.Review(book, physical, candidates.Select(c => c with { Observation = c.Observation with { Owner = c.Charter == "upper" ? "union" : "source", SourceSettlements = 2 } }), "union", 11);
        Reject(() => AdmissionFollowerReviews.Capture(journal.Current, partial), "later partial ownership is not a substitute original review");
        AdmissionFollowerReviews.Validate(journal.Current);
        Check(journal.Current.FollowerReview == source && AllegianceTransitionLedger.Restore(journal.Export()).Export() == journal.Export(), "real partial migration retains the untouched original full source for explicit recovery");
        var renamed = new PlayerAllegiance("foreign");
        Reject(() => new AllegianceTransitionLedger().PrepareReviewedAdmission("bad", renamed, command, plan, new(), peace: decision with { Player = "foreign" }), "foreign native player cannot adopt another colony's original scope");
        Reject(() => new AllegianceTransitionLedger().PrepareReviewedAdmission("bad", new("player"), command, plan, new(), wars: war, peace: decision), "one actual admission cannot pretend to have both distinct consent procedures");
        physical.Prepare("two", book, "lower", candidates[1].Observation with { SourceSettlements = 2 }, "union", "union", null, TerritoryTransferPurpose.ActiveCharter, 12);
        physical.Confirm("two", candidates[1].Observation with { Owner = "union" }, 12);
        journal.ReaffirmPeace(decision with { Tick = 12 }, 12);
        var final = FollowerTransfers.Review(book, physical, candidates.Select(c => c with { Observation = c.Observation with { Owner = "union", SourceSettlements = 3 } }), "union", 12);
        journal.Complete(polity, final, 12);
        Check(journal.Current.FollowerReview == source && physical.Transfers.Count == 2 && book.Export() == originalBook &&
            AllegianceTransitionLedger.Restore(journal.Export()).Export() == journal.Export(), "completed actual follower migration retains original full source without replaying ownership or rewriting contracts");
        return count;
    }
}
