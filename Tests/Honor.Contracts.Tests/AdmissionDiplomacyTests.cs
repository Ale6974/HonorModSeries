using Honor.Contracts.Rules;

internal static class AdmissionDiplomacyTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool v, string name) { count++; if (!v) throw new Exception("Admission: " + name); }
        void Reject(Action action, string name)
        { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
        SharedRelation Relation(string id, SharedRelationKind kind, int goodwill = 0) => new(id, goodwill, kind);
        IReadOnlyList<string> Review(SharedRelation[] player, SharedRelation[] root) => SharedDiplomacyPlan.AdmissionConflicts("player", "union", player, root);
        var hostile = Relation("enemy", SharedRelationKind.Hostile, -80);
        var neutral = Relation("enemy", SharedRelationKind.Neutral);
        var ally = Relation("enemy", SharedRelationKind.Ally, 80);
        Check(Review([], []).Count == 0, "empty external world is valid");
        Check(Review([neutral], [neutral]).Count == 0, "peaceful applicant and destination");
        Check(Review([hostile], [hostile]).Count == 0, "already shared enemy introduces no unaccepted war");
        Check(Review([hostile], [neutral]).SequenceEqual(new[] { "enemy" }), "joining cannot silently erase applicant hostility");
        Check(Review([hostile], [ally]).SequenceEqual(new[] { "enemy" }), "destination alliance cannot erase applicant war");
        Check(Review([neutral], [hostile]).Count == 0, "destination enemies remain visible in separate consent review");
        Check(Review([ally], [neutral]).Count == 0, "peaceful relation changes are not invented war evidence");
        Check(Review([hostile with { BaseGoodwill = 20 }], [neutral]).Count == 1, "actual relation kind overrides numeric goodwill inference");
        Check(Review([neutral with { BaseGoodwill = -80 }], [neutral]).Count == 0, "negative goodwill alone is not hostility evidence");
        var source = new[] { hostile, Relation("another", SharedRelationKind.Hostile, -100) };
        var target = new[] { neutral, Relation("another", SharedRelationKind.Neutral) };
        var result = Review(source, target);
        Check(result.SequenceEqual(new[] { "another", "enemy" }), "all unsupported wars listed in deterministic order");
        Check(Review(source.AsEnumerable().Reverse().ToArray(), target.AsEnumerable().Reverse().ToArray()).SequenceEqual(result), "enumeration order does not change conflicts");
        source[0] = neutral; target[0] = hostile;
        Check(result.SequenceEqual(new[] { "another", "enemy" }), "result is a stable independent review snapshot");
        bool immutable = false;
        try { ((IList<string>)result)[0] = "replacement"; } catch (NotSupportedException) { immutable = true; }
        Check(immutable, "conflict list is read-only");
        Reject(() => Review([hostile], []), "missing destination counterpart cannot imply peace");
        Reject(() => Review([], [neutral]), "missing applicant counterpart cannot imply no war");
        Reject(() => Review([hostile], [Relation("other", SharedRelationKind.Hostile)]), "matching counts do not waive exact identity scope");
        Reject(() => Review([hostile, hostile], [neutral]), "duplicate applicant identity");
        Reject(() => Review([hostile], [neutral, neutral]), "duplicate destination identity");
        Reject(() => Review([null], [neutral]), "missing applicant entry");
        Reject(() => Review([hostile], [null]), "missing destination entry");
        Reject(() => Review([hostile with { Kind = (SharedRelationKind)99 }], [neutral]), "unknown relation kind");
        Reject(() => Review([hostile], [neutral with { BaseGoodwill = 101 }]), "out-of-range destination snapshot");
        Reject(() => SharedDiplomacyPlan.AdmissionConflicts("player", "player", [], []), "native player cannot be own external authority");
        Check(Review([Relation("union", SharedRelationKind.Hostile), hostile], [Relation("player", SharedRelationKind.Neutral), hostile]).Count == 0,
            "internal admission relationship remains separate from external war review");
        var book = new CharterBook(); book.Register(new("p", "player", true), 0); book.Register(new("old", "old-owner", false), 0);
        var observations = book.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        book.Offer("old-bond", new("old", "p", 100, PoliticalWorld.Day * 5, PoliticalWorld.Day, TributeTransport.SubjectDelivery, .1m), 0, PoliticalWorld.Day, observations);
        book.Sign("old-bond", false, 0, observations); book.Sign("old-bond", true, 0, observations);
        var dues = new TributeLedger(); dues.Enable(book, "old-bond", 0);
        var before = new PlayerAllegianceState(PlayerAllegianceMode.Vassal, "old-owner", "old-bond");
        var command = new AllegianceCommand(AllegianceAction.JoinUnion, PoliticalWorld.Day * 6);
        Reject(() => AllegianceChangeSource.Validate(book, before, command, dues), "preparing a union does not authorize unilateral release");
        book.ConsentToRelease("old-bond", true, 1, observations);
        Reject(() => AllegianceChangeSource.Validate(book, before, command, dues), "one release signature still blocks preparation");
        book.ConsentToRelease("old-bond", false, 2, observations);
        string debtSnapshot = dues.Export(), bookSnapshot = book.Export();
        AllegianceChangeSource.Validate(book, before, command, dues);
        Check(dues.Export() == debtSnapshot && book.Export() == bookSnapshot, "released admission preparation preserves debts and all charter history");
        return count;
    }
}
