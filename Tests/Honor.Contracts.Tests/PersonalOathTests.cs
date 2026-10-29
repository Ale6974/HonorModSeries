using Honor.Contracts.Rules;

internal static class PersonalOathTests
{
    public static int Run()
    {
        int n = 0;
        void Check(bool b) { n++; if (!b) throw new Exception("Personal oath regression."); }
        void Reject(Action a) { bool rejected = false; try { a(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        NpcRealmDecisionEvidence Decision(string ruler = "lord") => new("realm", ruler, NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var empty = new PersonalOathBook();
        var book = empty.Append(new(PersonalOathAction.Swear, "oath", "pawn", "lord", "realm", 1, Decision()));
        Check(empty.Oaths.Count == 0 && book.Oaths.Single().OriginalSubject == "pawn");
        var signed = book.Export();
        Reject(() => book.Append(new(PersonalOathAction.Swear, "second", "pawn", "lord", "realm", 2, Decision())));
        Reject(() => book.Append(new(PersonalOathAction.DesignateHeir, "oath", "foreign", "heir", "realm", 2)));
        Reject(() => book.Append(new(PersonalOathAction.SubjectSuccession, "oath", "pawn", "heir", "realm", 2, Decision())));
        Check(book.Export() == signed);
        book = book.Append(new(PersonalOathAction.DesignateHeir, "oath", "pawn", "heir", "realm", 2));
        book = PersonalOathBook.Restore(book.Export());
        Check(book.Oaths.Single().Heir == "heir");
        book = book.Append(new(PersonalOathAction.LiegeSuccession, "oath", "lord", "newlord", "realm", 3, Decision("newlord")));
        var afterLord = book.Export();
        Reject(() => book.Append(new(PersonalOathAction.SubjectSuccession, "oath", "pawn", "heir", "realm", 4, Decision())));
        book = book.Append(new(PersonalOathAction.SubjectSuccession, "oath", "pawn", "heir", "realm", 4, Decision("newlord")));
        var state = book.Oaths.Single();
        Check(state.Subject == "heir" && state.Liege == "newlord" && state.Heir == null && state.OriginalLiege == "lord" && state.OriginalSubject == "pawn");
        Check(book.Events.Count == 4 && book.Events[0].Decision == Decision());
        var restored = PersonalOathBook.Restore(book.Export());
        Check(restored.Export() == book.Export());
        Reject(() => restored.Append(new(PersonalOathAction.SubjectSuccession, "oath", "pawn", "heir", "realm", 5, Decision("newlord"))));
        Reject(() => PersonalOathBook.Restore(afterLord.Replace("\"Representative\":\"newlord\"", "\"Representative\":\"impostor\"")));
        Reject(() => restored.Append(new(PersonalOathAction.DesignateHeir, "oath", "heir", "future", "realm", 3)));
        Reject(() => empty.Append(new(PersonalOathAction.Swear, "bad", "a", "lord", "realm", 1)));
        Reject(() => empty.Append(new(PersonalOathAction.Swear, "bad", "a", "lord", "realm", 1, Decision() with { Regime = NpcDecisionRegime.Royal })));
        var graph = empty.Append(new(PersonalOathAction.Swear, "a", "a", "b", "realm", 1, Decision("b")))
            .Append(new(PersonalOathAction.Swear, "b", "b", "c", "realm", 2, Decision("c")))
            .Append(new(PersonalOathAction.Swear, "c", "c", "d", "realm", 3, Decision("d")));
        Check(graph.Oaths.Count == 3);
        Reject(() => graph.Append(new(PersonalOathAction.Swear, "d", "d", "e", "realm", 4, Decision("e"))));
        Reject(() => graph.Append(new(PersonalOathAction.Swear, "cycle", "d", "a", "realm", 4, Decision("a"))));
        return n;
    }
}
