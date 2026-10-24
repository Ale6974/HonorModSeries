using Honor.Contracts.Rules;

static class RescuePledgeTests
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
        ContractTerms Charter(string liege = "rescuer", string subject = "city", string town = "town") =>
            new(ContractKind.SettlementCharter, F(liege), F(subject), 1000,
                new[] { new ObligationClause("protect", ObligationKind.Protection, ContractSide.Liege, 0, 5, maximumRequests: 1, responseTicks: 10) },
                town, new(true, false, true));
        RescuePledgeTerms Terms(string id = "offer", string incident = "attack", DefenseIncidentKind kind = DefenseIncidentKind.Attack,
            string town = "town", string aggressor = "enemy") =>
            new(id, incident, $"pledge/{id}", aggressor, kind, Charter(town: town), 0, 10, 20, 30);
        ContractLedger Active(ContractTerms terms = null)
        {
            var c = new ContractLedger("old", terms ?? Charter("old-liege"));
            c.Sign(ContractSide.Liege, 0); c.Sign(ContractSide.Subject, 0);
            return c;
        }
        var none = Array.Empty<ContractLedger>();
        var noOffers = Array.Empty<RescuePledge>();
        RescuePledge Free(RescuePledgeTerms terms = null) => RescuePledge.TryOffer(terms ?? Terms(), none, noOffers);

        foreach (var kind in Enum.GetValues<DefenseIncidentKind>())
        {
            var offer = Free(Terms(kind: kind));
            Check(offer != null && offer.OriginalContractId == null, "unbound city can offer vassalage for relief");
            Check(offer.Contract == null && offer.Phase == RescuePledgePhase.Offered, "offer creates no feudal contract");
            Check(!offer.RecordVerifiedRelief("unaccepted", 0) && offer.TryRatifyVerified(none, 0) == null, "acceptance required before relief and ratification");
            Check(!offer.Accept("pledge/offer", 1), "representative pledge cannot double as player acceptance");
            Check(offer.Accept("player-acceptance", 1), "player accepts exact offered terms");
            Check(!offer.Accept("duplicate-acceptance", 1), "duplicate acceptance");
            Check(offer.Contract == null && offer.TryRatifyVerified(none, 1) == null, "accepting or arriving is not victory");
            Check(!offer.RecordVerifiedRelief("player-acceptance", 2), "acceptance proof cannot double as battle victory");
            Check(offer.RecordVerifiedRelief("actual-relief", 2), "verified relief satisfies rescue condition");
            Check(!offer.RecordVerifiedRelief("repeat-victory", 2), "same pledge cannot complete relief twice");
            var contract = offer.TryRatifyVerified(none, 3);
            Check(contract != null && contract.Phase == ContractPhase.Active && offer.Phase == RescuePledgePhase.Completed, "relief plus valid authority ratifies charter");
            Check(ReferenceEquals(contract.Terms, offer.Terms.Contract) && contract.StartedTick == 3, "promised terms unchanged and obligations start on ratification");
            Check(contract.HasSigned(ContractSide.Liege) && contract.HasSigned(ContractSide.Subject), "actual conditional pledge and acceptance recorded");
            Check(offer.TryRatifyVerified(none, 3) == null && !offer.RecordVerifiedFailure("late-failure", 3), "completed pledge is terminal");
            Check(RescuePledge.TryOffer(Terms("new-id", kind: kind), none, new[] { offer }) == null, "completed incident cannot generate another pledge");
        }

        var old = Active();
        var lowLord = new FactionHonorLedger(F("old-liege"), .2);
        var vassal = RescuePledge.TryOffer(Terms(), new[] { old }, noOffers, old, lowLord);
        Check(vassal != null && vassal.OriginalContractId == old.Id && vassal.OriginalLiegeId == "old-liege", "low-honor liege's territory can seek rescue");
        Check(RescuePledge.TryOffer(Terms(), new[] { old }, noOffers) == null, "bound city cannot masquerade as a free city");
        Check(RescuePledge.TryOffer(Terms(), new[] { old }, noOffers, old, new(F("old-liege"), .4)) == null, "honor threshold is strict");
        Check(RescuePledge.TryOffer(Terms(), new[] { old }, noOffers, old, new(F("other"), .1)) == null, "wrong faction honor cannot justify poaching");
        var unreliableSubject = RescuePledge.TryOffer(Terms(), new[] { old }, noOffers, old, new(F("city"), .2));
        Check(unreliableSubject != null && unreliableSubject.MotivationFactionId == "city", "low-honor subject may opportunistically offer allegiance");
        Check(RescuePledge.TryOffer(Terms(), new[] { old }, noOffers, old, new(F("rescuer"), .1)) == null,
            "rescuer's own low honor is not a qualifying territorial grievance");
        Check(RescuePledge.TryOffer(Terms(), none, noOffers, old, lowLord) == null, "departing contract must be in authoritative registry");
        Check(RescuePledge.TryOffer(Terms(town: "other-town"), new[] { old }, noOffers, old, lowLord) == null, "local old charter must concern the requesting city");
        var ownVassal = Active(Charter());
        Check(RescuePledge.TryOffer(Terms(), new[] { ownVassal }, noOffers, ownVassal, new(F("rescuer"), .1)) == null,
            "existing own vassal uses ordinary defense, not a new vassalage reward");
        vassal.Accept("accept", 1); vassal.RecordVerifiedRelief("rescue", 2);
        Check(vassal.TryRatifyVerified(new[] { old }, 2) == null && vassal.Phase == RescuePledgePhase.AwaitingRelease, "rescue cannot overwrite prior feudal obligation");
        Check(old.Phase == ContractPhase.Active && old.BreachingSide == null, "offer never automatically repudiates old lord");
        Check(vassal.TryRatifyVerified(none, 2) == null, "omitting old history is not proof of release");
        old.ConsentToRelease(ContractSide.Liege, 3); old.ConsentToRelease(ContractSide.Subject, 3);
        Check(vassal.TryRatifyVerified(new[] { old }, 4) != null, "valid release permits new promised charter");

        var changed = Free(); changed.Accept("accept", 1); changed.RecordVerifiedRelief("rescue", 2);
        var intervening = Active(Charter("third-liege"));
        Check(changed.TryRatifyVerified(new[] { intervening }, 3) == null && changed.Phase == RescuePledgePhase.AwaitingRelease,
            "new competing contract is rechecked after rescue");
        changed.Advance(31);
        Check(changed.Phase == RescuePledgePhase.RatificationLapsed && changed.TryRatifyVerified(none, 31) == null,
            "completed rescue cannot leave an unlimited stale annexation promise");
        var duplicate = Free();
        Check(RescuePledge.TryOffer(Terms("other", "other-attack"), none, new[] { duplicate }) == null, "one active rescue offer per city");
        Check(RescuePledge.TryOffer(Terms("other", "attack", town: "other-town"), none, new[] { duplicate }) == null, "one incident cannot target another city for duplicate pledge");
        duplicate.Decline(0);
        Check(duplicate.Phase == RescuePledgePhase.Declined && !duplicate.Accept("late", 1), "player may decline without forming obligations");
        Check(RescuePledge.TryOffer(Terms("other", "attack"), none, new[] { duplicate }) == null, "declined incident does not repeatedly spam offers");
        Check(RescuePledge.TryOffer(Terms("other", "new-attack"), none, new[] { duplicate }) != null, "later genuinely different incident may qualify");

        var boundary = Free();
        Check(boundary.Accept("accept", 10) && boundary.RecordVerifiedRelief("victory", 20) && boundary.TryRatifyVerified(none, 30) != null,
            "all deadline endpoints are inclusive");
        var offerExpired = Free();
        Check(!offerExpired.Accept("late", 11) && offerExpired.Phase == RescuePledgePhase.Expired, "offer deadline cannot be skipped");
        var rescueExpired = Free(); rescueExpired.Accept("accept", 1);
        Check(!rescueExpired.RecordVerifiedRelief("late", 21) && rescueExpired.Phase == RescuePledgePhase.Failed, "relief deadline cannot be skipped");
        var targetLost = Free(); targetLost.Accept("accept", 1);
        Check(!targetLost.RecordVerifiedFailure("accept", 2), "acceptance is not target-loss evidence");
        Check(targetLost.RecordVerifiedFailure("verified-city-lost", 2) && targetLost.TryRatifyVerified(none, 2) == null, "lost target cannot ratify");
        var priorResult = new ContractLedger("rescue/offer", Charter());
        Check(RescuePledge.TryOffer(Terms(), new[] { priorResult }, noOffers) == null, "persisted result ID blocks fresh offer clone");
        Reject(() => Terms(aggressor: "rescuer"), "rescuer cannot stage its own qualifying attack");
        Reject(() => Terms(aggressor: "city"), "victim cannot attack itself for pledge reward");
        Reject(() => new RescuePledgeTerms("id", "incident", "pledge", "enemy", DefenseIncidentKind.Attack, Charter(), 0, 20, 10, 30), "ordered deadlines");
        Reject(() => RescuePledge.TryOffer(Terms(), none, noOffers, lowHonorThreshold: double.NaN), "finite eligibility threshold");
        Reject(() => targetLost.Advance(0), "pledge time cannot run backward");
        Console.WriteLine($"PASS: {count} rescue pledge assertions (conditional terms, free cities, disaffected vassals, verified relief, prior rights, deadlines).");
        return count;
    }
}
