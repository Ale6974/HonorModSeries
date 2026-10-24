using Honor.Contracts.Rules;

static class CityTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool condition, string name) { count++; if (!condition) throw new Exception(name); }
        void Reject(Action action, string name)
        {
            bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, name);
        }
        PartyRef F(string id) => new(PartyKind.Faction, id);
        var player = F("player");
        var liege = F("liege");
        var none = Array.Empty<ContractLedger>();
        ContractLedger Draft(ContractKind kind = ContractKind.SettlementCharter, string town = "town", string lord = "liege", string subject = "player", bool tribute = false)
        {
            var partyKind = kind == ContractKind.PersonalVassalage ? PartyKind.Pawn : PartyKind.Faction;
            var clauses = new List<ObligationClause> { new("protect", ObligationKind.Protection, ContractSide.Liege,
                0, 5, maximumRequests: 1, responseTicks: 10) };
            if (tribute) clauses.Add(new("tax", ObligationKind.Tribute, ContractSide.Subject, 25, 5, "Silver", 10));
            return new("binding", new ContractTerms(kind, new(partyKind, lord), new(partyKind, subject), 100, clauses,
                kind == ContractKind.SettlementCharter ? town : null,
                kind == ContractKind.SettlementCharter ? new(true, true, true) : null));
        }
        ContractLedger Active(ContractLedger contract)
        {
            contract.Sign(ContractSide.Liege, 0); contract.Sign(ContractSide.Subject, 0);
            return contract;
        }
        CityRecognition Ready()
        {
            var city = new CityRecognition("town", player);
            city.Observe(100000, 100000, true, none, 0);
            return city;
        }

        Check(FreeCityRules.IsFreeCity("town", player, none), "unbound city is free");
        var draft = Draft();
        Check(FreeCityRules.IsFreeCity("town", player, new[] { draft }), "proposal is not a feudal binding");
        draft.Sign(ContractSide.Liege, 0);
        Check(FreeCityRules.IsFreeCity("town", player, new[] { draft }), "one signature does not remove independence");
        draft.Sign(ContractSide.Subject, 0);
        Check(!FreeCityRules.IsFreeCity("town", player, new[] { draft }), "active subject charter removes free status");
        Check(FreeCityRules.IsFreeCity("other", player, new[] { draft }), "another subject town's local charter does not bind this town");
        Check(FreeCityRules.IsFreeCity("town", F("outsider"), new[] { draft }), "unrelated faction remains free");
        Check(!FreeCityRules.IsFreeCity("town", player, new[] { Active(Draft(ContractKind.FactionVassalage)) }), "faction vassalage binds all its towns");
        Check(!FreeCityRules.IsFreeCity("town", player, new[] { Active(Draft(ContractKind.FactionVassalage, lord: "player", subject: "other")) }),
            "a feudal overlord is not a city with no feudal contracts");
        Check(!FreeCityRules.IsFreeCity("town", player, new[] { Active(Draft(lord: "player", subject: "other")) }),
            "faction-wide charter protection as liege also counts");
        Check(FreeCityRules.IsFreeCity("town", player, new[] { Active(Draft(ContractKind.PersonalVassalage)) }),
            "citizens' personal oaths do not bind the city");
        var disputed = Active(Draft(tribute: true)); disputed.Advance(31);
        Check(disputed.Phase == ContractPhase.Disputed && !FreeCityRules.IsFreeCity("town", player, new[] { disputed }),
            "unpaid tribute cannot be erased by declaring free status");
        disputed.Repudiate(ContractSide.Subject, 31);
        Check(FreeCityRules.IsFreeCity("town", player, new[] { disputed }), "ended binding no longer determines current status");
        draft.ConsentToRelease(ContractSide.Liege, 0); draft.ConsentToRelease(ContractSide.Subject, 0);
        Check(FreeCityRules.IsFreeCity("town", player, new[] { draft }), "mutually released city is free");
        var expired = Active(Draft()); expired.Advance(100);
        Check(FreeCityRules.IsFreeCity("town", player, new[] { expired }), "expired agreement is not a permanent feudal flag");

        var city = new CityRecognition("town", player);
        Check(!city.Observe(99999, 100000, true, none, 0), "below threshold");
        Check(!city.Observe(100000, 100000, false, none, 1), "non-home map cannot trigger recognition");
        Check(city.Observe(100000, 100000, true, none, 2), "exact threshold opens first decision");
        Check(city.FirstChoiceTick == 2 && city.WealthAtRecognition == 100000 && city.ThresholdAtRecognition == 100000, "recognition evidence");
        Check(!city.Observe(1, 100000, true, none, 3) && city.Phase == CityRecognitionPhase.ChoicePending, "wealth loss does not retract decision");
        Check(city.ChooseFreeCity(none, 4), "free city choice");
        Check(!city.ChooseFreeCity(none, 5), "duplicate choice does not resolve twice");
        for (int i = 0; i < 100; i++)
            Check(!city.Observe(i % 2 == 0 ? 0 : 1000000, i % 2 == 0 ? 1 : 100000, true, none, 6 + i),
                "wealth and configuration changes never spam completed recognition");
        Check(!city.SeekLiege(liege, Array.Empty<PartyRef>(), none, 106), "ineligible suzerain is not auto-selected");
        Check(!city.SeekLiege(player, new[] { player }, none, 106), "self suzerainty is not a choice");
        Check(!city.SeekLiege(new(PartyKind.Pawn, "lord"), new[] { new PartyRef(PartyKind.Pawn, "lord") }, none, 106), "city suzerainty requires faction");
        Check(city.SeekLiege(liege, new[] { liege }, none, 106), "free city may later voluntarily negotiate");
        Check(city.RequestedLiege == liege && city.Phase == CityRecognitionPhase.Negotiating && FreeCityRules.IsFreeCity("town", player, none),
            "selecting a liege does not create or sign a contract");
        Check(city.CancelNegotiation(107) && city.Phase == CityRecognitionPhase.ChoicePending, "rejected or cancelled talks restore options");
        Check(!city.Observe(1000000, 100000, true, none, 108), "cancelled talks do not resend threshold popup");
        Check(city.ChooseFreeCity(none, 109), "no available liege still permits independence");

        var ready = Ready();
        Check(ready.SeekLiege(liege, new[] { liege }, none, 1), "pending city begins talks");
        var agreement = Draft(); agreement.Sign(ContractSide.Liege, 0);
        Check(!ready.Observe(100000, 100000, true, new[] { agreement }, 2) && ready.Phase == CityRecognitionPhase.Negotiating,
            "proposal's first signature cannot settle the choice");
        agreement.Sign(ContractSide.Subject, 2);
        Check(!ready.ChooseFreeCity(new[] { agreement }, 3), "stale UI cannot nullify newly active contract");
        ready.Observe(100000, 100000, true, new[] { agreement }, 3);
        Check(ready.Phase == CityRecognitionPhase.FeudalAffiliationRecorded && ready.RequestedLiege == null,
            "actual active binding completes affiliation history");
        Check(!ready.ReconsiderAfterRelease(new[] { agreement }, 4), "cannot reconsider away a live binding");
        agreement.ConsentToRelease(ContractSide.Liege, 4); agreement.ConsentToRelease(ContractSide.Subject, 4);
        Check(!ready.Observe(0, 100000, true, new[] { agreement }, 5), "release does not cause automatic repeat popup");
        Check(FreeCityRules.IsFreeCity("town", player, new[] { agreement }), "live status derives from ledger, not old choice history");
        Check(ready.ReconsiderAfterRelease(new[] { agreement }, 6) && ready.ChooseFreeCity(new[] { agreement }, 7),
            "manual reconsideration after legitimate release");

        var alreadyBound = new CityRecognition("town", player);
        var prior = Active(Draft());
        Check(!alreadyBound.Observe(200000, 100000, true, new[] { prior }, 0) && alreadyBound.FirstChoiceTick == null,
            "already bound city does not receive a contradictory prompt");
        prior.ConsentToRelease(ContractSide.Liege, 1); prior.ConsentToRelease(ContractSide.Subject, 1);
        Check(alreadyBound.Observe(200000, 100000, true, new[] { prior }, 2), "unrecognized city can qualify after its prior binding ends");
        var noLongerOwned = Ready();
        noLongerOwned.Observe(100000, 100000, false, none, 1);
        Check(!noLongerOwned.ChooseFreeCity(none, 2) && !noLongerOwned.SeekLiege(liege, new[] { liege }, none, 2),
            "lost or non-home settlement cannot act on stale player choice");
        var custom = new CityRecognition("town", player);
        Check(custom.Observe(50000, 50000, true, none, 0), "threshold is configurable input, not hardcoded policy");
        Reject(() => custom.Observe(double.NaN, 100000, true, none, 1), "invalid wealth");
        Reject(() => custom.Observe(double.PositiveInfinity, 100000, true, none, 1), "infinite wealth");
        Reject(() => custom.Observe(-1, 100000, true, none, 1), "negative wealth");
        Reject(() => custom.Observe(100000, 0, true, none, 1), "zero threshold");
        Reject(() => custom.Observe(100000, double.NaN, true, none, 1), "invalid threshold");
        Reject(() => custom.Observe(100000, 100000, true, none, -1), "recognition time cannot reverse");
        Reject(() => new CityRecognition("town", new(PartyKind.Pawn, "owner")), "city ownership scope");
        Reject(() => FreeCityRules.IsFreeCity("", player, none), "empty settlement identity");
        Console.WriteLine($"PASS: {count} city assertions (free status, wealth threshold, one-time choice, liege negotiation, live bindings).");
        return count;
    }
}
