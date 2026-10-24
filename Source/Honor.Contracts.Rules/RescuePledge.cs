namespace Honor.Contracts.Rules;

public enum RescuePledgePhase { Offered, Accepted, Rescued, AwaitingRelease, Completed, Declined, Expired, Failed, RatificationLapsed }

/// <summary>Exact conditional pledge made by a verified representative; no quest, combat, or item generation.</summary>
public sealed class RescuePledgeTerms
{
    public string Id { get; }
    public string IncidentId { get; }
    public string PledgeEvidenceId { get; }
    public string AggressorFactionId { get; }
    public DefenseIncidentKind ThreatKind { get; }
    public ContractTerms Contract { get; }
    public long OfferedTick { get; }
    public long AcceptUntil { get; }
    public long RescueUntil { get; }
    public long RatifyUntil { get; }

    public RescuePledgeTerms(string id, string incidentId, string pledgeEvidenceId, string aggressorFactionId,
        DefenseIncidentKind threatKind, ContractTerms contract, long offeredTick, long acceptUntil, long rescueUntil, long ratifyUntil)
    {
        RuntimeCompat.NotBlank(id);
        RuntimeCompat.NotBlank(incidentId);
        RuntimeCompat.NotBlank(pledgeEvidenceId);
        RuntimeCompat.NotBlank(aggressorFactionId);
        RuntimeCompat.NotNull(contract);
        if (!RuntimeCompat.IsDefined(threatKind)) throw new ArgumentOutOfRangeException(nameof(threatKind));
        if (contract.Kind != ContractKind.SettlementCharter || contract.Origin != ContractOrigin.Negotiated)
            throw new ArgumentException("Rescue pledges promise a negotiated charter over one settlement.");
        if (aggressorFactionId == contract.Liege.Id || aggressorFactionId == contract.Subject.Id)
            throw new ArgumentException("Neither the rescuer nor the victim can stage the qualifying attack.");
        if (offeredTick < 0 || acceptUntil < offeredTick || rescueUntil < acceptUntil || ratifyUntil <= rescueUntil ||
            ratifyUntil - offeredTick > int.MaxValue)
            throw new ArgumentException("Offer, rescue and ratification need finite ordered deadlines.");
        // Reserve enough room to activate the complete promised contract at the last legal moment.
        long end = checked(ratifyUntil + contract.DurationTicks);
        foreach (var clause in contract.Clauses) _ = checked(end + clause.ResponseTicks + clause.GraceTicks);
        Id = id; IncidentId = incidentId; PledgeEvidenceId = pledgeEvidenceId; AggressorFactionId = aggressorFactionId;
        ThreatKind = threatKind; Contract = contract; OfferedTick = offeredTick;
        AcceptUntil = acceptUntil; RescueUntil = rescueUntil; RatifyUntil = ratifyUntil;
    }
}

/// <summary>
/// Rescue-for-vassalage protocol. The future game adapter verifies authority, the hostile incident,
/// actual relief, current ownership and legal release, and supplies complete fresh registries.
/// A successful rescue alone never erases an earlier contract or proves the city can change rulers.
/// </summary>
public sealed class RescuePledge
{
    long lastTick;
    readonly HashSet<string> evidenceIds = new(StringComparer.Ordinal);
    public RescuePledgeTerms Terms { get; }
    public string OriginalContractId { get; }
    public string OriginalLiegeId { get; }
    public string MotivationFactionId { get; }
    public RescuePledgePhase Phase { get; private set; } = RescuePledgePhase.Offered;
    public string AcceptanceEvidenceId { get; private set; }
    public string ReliefEvidenceId { get; private set; }
    public string FailureEvidenceId { get; private set; }
    public ContractLedger Contract { get; private set; }
    public bool Terminal => Phase is RescuePledgePhase.Completed or RescuePledgePhase.Declined or RescuePledgePhase.Expired
        or RescuePledgePhase.Failed or RescuePledgePhase.RatificationLapsed;

    RescuePledge(RescuePledgeTerms terms, ContractLedger departing, PartyRef motivation = null)
    {
        Terms = terms; lastTick = terms.OfferedTick;
        evidenceIds.Add(terms.PledgeEvidenceId);
        OriginalContractId = departing?.Id; OriginalLiegeId = departing?.Terms.Liege.Id;
        MotivationFactionId = motivation?.Id;
    }

    public static RescuePledge TryOffer(RescuePledgeTerms terms, IEnumerable<ContractLedger> contracts,
        IEnumerable<RescuePledge> offers, ContractLedger departing = null, FactionHonorLedger lowHonorParty = null,
        double lowHonorThreshold = .4)
    {
        RuntimeCompat.NotNull(terms);
        RuntimeCompat.NotNull(contracts);
        RuntimeCompat.NotNull(offers);
        if (!RuntimeCompat.IsFinite(lowHonorThreshold) || lowHonorThreshold is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(lowHonorThreshold));
        var all = contracts.ToArray();
        if (all.Any(c => c.Id == $"rescue/{terms.Id}") || offers.Any(o => o.Terms.Id == terms.Id ||
            o.Terms.IncidentId == terms.IncidentId || (!o.Terminal && o.Terms.Contract.SettlementId == terms.Contract.SettlementId))) return null;
        if (departing == null)
            return FreeCityRules.IsFreeCity(terms.Contract.SettlementId, terms.Contract.Subject, all)
                ? new(terms, null) : null;
        if (!all.Contains(departing) || departing.Phase is not (ContractPhase.Active or ContractPhase.Disputed) ||
            departing.Terms.Subject != terms.Contract.Subject || departing.Terms.Liege == terms.Contract.Liege ||
            (departing.Terms.Kind != ContractKind.FactionVassalage &&
             !(departing.Terms.Kind == ContractKind.SettlementCharter && departing.Terms.SettlementId == terms.Contract.SettlementId)) ||
            lowHonorParty == null || (lowHonorParty.Faction != departing.Terms.Liege && lowHonorParty.Faction != departing.Terms.Subject) ||
            lowHonorParty.AsOfTick > terms.OfferedTick || lowHonorParty.Value >= lowHonorThreshold) return null;
        return new(terms, departing, lowHonorParty.Faction);
    }

    public bool Accept(string evidenceId, long now)
    {
        RuntimeCompat.NotBlank(evidenceId);
        Advance(now);
        if (Phase != RescuePledgePhase.Offered || !evidenceIds.Add(evidenceId)) return false;
        AcceptanceEvidenceId = evidenceId; Phase = RescuePledgePhase.Accepted;
        return true;
    }

    public bool Decline(long now)
    {
        Advance(now);
        if (Phase != RescuePledgePhase.Offered) return false;
        Phase = RescuePledgePhase.Declined;
        return true;
    }

    /// <summary>Call after actual verified relief by the agreed rescuer, never merely caravan arrival.</summary>
    public bool RecordVerifiedRelief(string evidenceId, long now)
    {
        RuntimeCompat.NotBlank(evidenceId);
        Advance(now);
        if (Phase != RescuePledgePhase.Accepted || !evidenceIds.Add(evidenceId)) return false;
        ReliefEvidenceId = evidenceId; Phase = RescuePledgePhase.Rescued;
        return true;
    }

    public ContractLedger TryRatifyVerified(IEnumerable<ContractLedger> contracts, long now)
    {
        RuntimeCompat.NotNull(contracts);
        Advance(now);
        if (Phase is not (RescuePledgePhase.Rescued or RescuePledgePhase.AwaitingRelease)) return null;
        var all = contracts.ToArray();
        if (all.Any(c => c.Id == $"rescue/{Terms.Id}")) return null;
        if (OriginalContractId != null && !all.Any(c => c.Id == OriginalContractId)) return null;
        if (!FreeCityRules.IsFreeCity(Terms.Contract.SettlementId, Terms.Contract.Subject, all) ||
            BindingRules.Check(Terms.Contract, all) != BindingConflict.None)
        {
            Phase = RescuePledgePhase.AwaitingRelease;
            return null;
        }
        // The representative's exact conditional pledge and player's acceptance are already recorded.
        // The adapter must reverify authority, target survival/ownership, and valid exit before this call.
        var result = new ContractLedger($"rescue/{Terms.Id}", Terms.Contract);
        result.Sign(ContractSide.Liege, now); result.Sign(ContractSide.Subject, now);
        Contract = result; Phase = RescuePledgePhase.Completed;
        return result;
    }

    public bool RecordVerifiedFailure(string evidenceId, long now)
    {
        RuntimeCompat.NotBlank(evidenceId);
        Advance(now);
        if (Terminal || !evidenceIds.Add(evidenceId)) return false;
        FailureEvidenceId = evidenceId; Phase = RescuePledgePhase.Failed;
        return true;
    }

    public void Advance(long now)
    {
        if (now < 0 || now < lastTick) throw new ArgumentOutOfRangeException(nameof(now), "Pledge time cannot run backward.");
        lastTick = now;
        if (Phase == RescuePledgePhase.Offered && now > Terms.AcceptUntil) Phase = RescuePledgePhase.Expired;
        else if (Phase == RescuePledgePhase.Accepted && now > Terms.RescueUntil) Phase = RescuePledgePhase.Failed;
        else if ((Phase is RescuePledgePhase.Rescued or RescuePledgePhase.AwaitingRelease) && now > Terms.RatifyUntil)
            Phase = RescuePledgePhase.RatificationLapsed;
    }
}
