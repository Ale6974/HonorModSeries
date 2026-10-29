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
        Check(!signed.Contains("PlayerLiege") && !signed.Contains("DeathWitnessV1") && !signed.Contains("PersonalRelease"));
        var npc = empty.Append(new PersonalOathEvent(PersonalOathAction.Swear, "npc", "lord", "player", "realm", 1, Decision()) { PlayerLiege = true });
        npc = npc.Append(new PersonalOathEvent(PersonalOathAction.DesignateHeir, "npc", "player", "heir", "realm", 2) { PlayerLiege = true });
        var original = npc.Export();
        Reject(() => npc.Append(new PersonalOathEvent(PersonalOathAction.DesignateHeir, "npc", "lord", "other", "realm", 3) { PlayerLiege = true }));
        Reject(() => npc.Append(new(PersonalOathAction.DesignateHeir, "npc", "player", "other", "realm", 3)));
        Reject(() => npc.Append(new PersonalOathEvent(PersonalOathAction.SubjectSuccession, "npc", "lord", "newlord", "realm", 3, Decision("newlord")) { PlayerLiege = true, DeathWitnessV1 = true }));
        Check(npc.Export() == original);
        npc = npc.Append(new PersonalOathEvent(PersonalOathAction.DeathObserved, "npc", "lord", "player", "realm", 3) { PlayerLiege = true });
        npc = npc.Append(new PersonalOathEvent(PersonalOathAction.DeathObserved, "npc", "player", "lord", "realm", 3) { PlayerLiege = true });
        npc = PersonalOathBook.Restore(npc.Export());
        Check(npc.Oaths.Single().SubjectDeathTick == 3 && npc.Oaths.Single().LiegeDeathTick == 3);
        Reject(() => npc.Append(new PersonalOathEvent(PersonalOathAction.DeathObserved, "npc", "player", "lord", "realm", 3) { PlayerLiege = true }));
        npc = npc.Append(new PersonalOathEvent(PersonalOathAction.SubjectSuccession, "npc", "lord", "newlord", "realm", 4, Decision("newlord")) { PlayerLiege = true, DeathWitnessV1 = true });
        Check(npc.Oaths.Single().Subject == "newlord" && npc.Oaths.Single().Heir == "heir" && npc.Oaths.Single().SubjectDeathTick == null && npc.Oaths.Single().LiegeDeathTick == 3);
        npc = npc.Append(new PersonalOathEvent(PersonalOathAction.LiegeSuccession, "npc", "player", "heir", "realm", 5, Decision("newlord")) { PlayerLiege = true, DeathWitnessV1 = true });
        var inherited = npc.Oaths.Single();
        Check(inherited.OriginalSubject == "lord" && inherited.OriginalLiege == "player" && inherited.Subject == "newlord" && inherited.Liege == "heir" && inherited.Heir == null && inherited.LiegeDeathTick == null);
        Check(npc.Events.Count == 6 && npc.Events[2].Action == PersonalOathAction.DeathObserved);
        var missingDeath = new PersonalOathSave(1, npc.Events.Where(e => e.Action != PersonalOathAction.DeathObserved).ToArray());
        Reject(() => PersonalOathBook.Restore(System.Text.Json.JsonSerializer.Serialize(missingDeath)));
        npc = PersonalOathBook.Restore(npc.Export());
        Reject(() => npc.Append(new PersonalOathEvent(PersonalOathAction.MutualRelease, "npc", "heir", "impostor", "realm", 6, Decision("newlord")) { PlayerLiege = true }));
        var released = npc.Append(new PersonalOathEvent(PersonalOathAction.MutualRelease, "npc", "heir", "newlord", "realm", 6, Decision("newlord")) { PlayerLiege = true });
        Check(released.Oaths.Single().EndedTick == 6 && npc.Oaths.Single().EndedTick == null);
        Check(PersonalOathBook.Restore(released.Export()).Export() == released.Export());
        Reject(() => released.Append(new PersonalOathEvent(PersonalOathAction.DesignateHeir, "npc", "heir", "next", "realm", 7) { PlayerLiege = true }));
        var renewed = released.Append(new PersonalOathEvent(PersonalOathAction.Swear, "new", "newlord", "heir", "realm", 7, Decision("newlord")) { PlayerLiege = true });
        Check(renewed.Oaths.Count == 2 && renewed.Events.Count == 8 && renewed.Oaths.Single(x => x.Id == "npc").EndedTick == 6);
        var children = graph.Append(new(PersonalOathAction.MutualRelease, "b", "b", "c", "realm", 4, Decision("c")));
        Check(children.Oaths.Single(x => x.Id == "a").Liege == "b" && children.Oaths.Single(x => x.Id == "a").EndedTick == null && children.Oaths.Single(x => x.Id == "c").EndedTick == null);
        Check(PersonalOathBook.Restore(children.Export()).Export() == children.Export());
        var individual = new PersonalReleaseDecision("newlord", "heir", "realm", 6, 1, true, true, true, true);
        var individualRelease = new PersonalOathEvent(PersonalOathAction.MutualRelease, "npc", "heir", "newlord", "realm", 6) { PlayerLiege = true, PersonalRelease = individual };
        var retired = npc.Append(individualRelease);
        Check(retired.Oaths.Single().EndedTick == 6 && retired.Events.Last().Decision == null && retired.Events.Last().PersonalRelease == individual);
        Check(PersonalOathBook.Restore(retired.Export()).Export() == retired.Export());
        Reject(() => npc.Append(individualRelease with { PersonalRelease = individual with { Actor = "substitute" } }));
        Reject(() => npc.Append(individualRelease with { PersonalRelease = individual with { Policy = 2 } }));
        Reject(() => npc.Append(individualRelease with { PersonalRelease = individual with { Willing = false } }));
        Reject(() => npc.Append(individualRelease with { PersonalRelease = individual with { Tick = 7 } }));
        Reject(() => npc.Append(individualRelease with { Decision = Decision("newlord") }));
        var migratedDecision = individual with { Policy = 2, CurrentRealm = "actual-new-realm" };
        var migratedRelease = npc.Append(individualRelease with { PersonalRelease = migratedDecision });
        Check(migratedRelease.Oaths.Single().Realm == "realm" && migratedRelease.Events.Last().PersonalRelease.CurrentRealm == "actual-new-realm");
        Check(PersonalOathBook.Restore(migratedRelease.Export()).Export() == migratedRelease.Export());
        Reject(() => npc.Append(individualRelease with { PersonalRelease = migratedDecision with { CurrentRealm = "realm" } }));
        Reject(() => npc.Append(individualRelease with { PersonalRelease = individual with { CurrentRealm = "foreign" } }));
        Reject(() => npc.Append(individualRelease with { PersonalRelease = migratedDecision with { RegisteredOrdinaryRealm = false } }));
        foreach (var regime in new[] { NpcDecisionRegime.Union, NpcDecisionRegime.Royal })
        {
            var personal = new PersonalOathBook();
            PersonalOathEvent WithMandate(PersonalOathEvent e)
            {
                var d = e.Decision with { Regime = regime }; e = e with { Decision = d };
                return e with { Mandate = new ScopedNpcContractDecision(d.Realm, d.Representative, regime, personal.DecisionScope(e), e.Tick, 1, 70,
                    regime == NpcDecisionRegime.Union ? [new("actual-city", true)] : [], regime == NpcDecisionRegime.Royal ? "Stellarch" : null, regime == NpcDecisionRegime.Royal ? 60 : 0) };
            }
            var swear = WithMandate(new(PersonalOathAction.Swear, "scoped", "player", "lord", "realm", 1, Decision("lord")));
            personal = personal.Append(swear); Check(personal.Oaths.Single().OriginalLiege == "lord");
            var raw = personal.Export(); if (personal.Events[0].Mandate.Ballots.Length > 0) personal.Events[0].Mandate.Ballots[0] = new("tampered", false); Check(personal.Export() == raw);
            personal = personal.Append(new(PersonalOathAction.DeathObserved, "scoped", "lord", "player", "realm", 2));
            var succession = WithMandate(new PersonalOathEvent(PersonalOathAction.LiegeSuccession, "scoped", "lord", "newlord", "realm", 3, Decision("newlord")) { DeathWitnessV1 = true });
            personal = personal.Append(succession); Check(personal.Oaths.Single().Liege == "newlord" && personal.Oaths.Single().OriginalLiege == "lord");
            var release = WithMandate(new(PersonalOathAction.MutualRelease, "scoped", "player", "newlord", "realm", 4, Decision("newlord")));
            Reject(() => personal.Append(release with { Mandate = swear.Mandate }));
            Reject(() => personal.Append(release with { Mandate = null }));
            Reject(() => personal.Append(release with { Other = "substitute" }));
            personal = personal.Append(release); Check(personal.Oaths.Single().EndedTick == 4);
            Check(PersonalOathBook.Restore(personal.Export()).Export() == personal.Export());
            var altered = new PersonalOathSave(1, personal.Events.Select(e => e.Action == PersonalOathAction.DeathObserved ? e with { Tick = 1 } : e).ToArray());
            Reject(() => PersonalOathBook.Restore(System.Text.Json.JsonSerializer.Serialize(altered)));
        }
        Check(!released.Export().Contains("Mandate"));
        return n;
    }
}
