namespace Honor.Contracts.Rules;

public static class FreeCityRules
{
    /// <summary>
    /// Free cities have no applicable active/disputed feudal contract in either role.
    /// Personal oaths do not bind their citizens' city; another subject settlement's charter is local.
    /// The adapter must supply the authoritative owner and complete contract ledger.
    /// </summary>
    public static bool IsFreeCity(string settlementId, PartyRef owner, IEnumerable<ContractLedger> contracts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementId);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(contracts);
        if (owner.Kind != PartyKind.Faction) throw new ArgumentException("A city needs a faction owner.", nameof(owner));
        return !contracts.Any(c => (c.Phase is ContractPhase.Active or ContractPhase.Disputed) && Applies(c.Terms, settlementId, owner));
    }

    static bool Applies(ContractTerms terms, string settlementId, PartyRef owner) => terms.Kind switch
    {
        ContractKind.FactionVassalage => terms.Liege == owner || terms.Subject == owner,
        ContractKind.SettlementCharter => terms.Liege == owner || (terms.Subject == owner && terms.SettlementId == settlementId),
        _ => false
    };
}

public enum CityRecognitionPhase { Growing, ChoicePending, Negotiating, FreeCityChosen, FeudalAffiliationRecorded }

/// <summary>
/// One wealth-triggered decision per player settlement. This records a choice, not diplomatic status.
/// FreeCityRules and actual contracts remain authoritative; selecting a liege never signs a contract.
/// Persistence, eligibility, candidate diplomacy, and translated UI belong to the future game adapter.
/// </summary>
public sealed class CityRecognition
{
    public const double ProposedWealthThreshold = 100_000;
    long lastObservedTick = -1;
    bool ownedPlayerHome;
    public string SettlementId { get; }
    public PartyRef Owner { get; }
    public CityRecognitionPhase Phase { get; private set; }
    public long? FirstChoiceTick { get; private set; }
    public double? ThresholdAtRecognition { get; private set; }
    public double? WealthAtRecognition { get; private set; }
    public PartyRef RequestedLiege { get; private set; }

    public CityRecognition(string settlementId, PartyRef owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementId);
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.Kind != PartyKind.Faction) throw new ArgumentException("A city needs a faction owner.", nameof(owner));
        SettlementId = settlementId; Owner = owner;
    }

    /// <returns>True only for the first automatic wealth-triggered prompt. Existing bindings suppress it.</returns>
    public bool Observe(double wealth, double threshold, bool isOwnedPlayerHome, IEnumerable<ContractLedger> contracts, long now)
    {
        if (!double.IsFinite(wealth) || wealth < 0) throw new ArgumentOutOfRangeException(nameof(wealth));
        if (!double.IsFinite(threshold) || threshold <= 0) throw new ArgumentOutOfRangeException(nameof(threshold));
        ArgumentNullException.ThrowIfNull(contracts);
        ObserveTime(now);
        ownedPlayerHome = isOwnedPlayerHome;
        if (!ownedPlayerHome) return false;
        if (!FreeCityRules.IsFreeCity(SettlementId, Owner, contracts))
        {
            if (Phase != CityRecognitionPhase.Growing)
            {
                Phase = CityRecognitionPhase.FeudalAffiliationRecorded;
                RequestedLiege = null;
            }
            return false;
        }
        if (Phase != CityRecognitionPhase.Growing || wealth < threshold) return false;
        FirstChoiceTick = now; ThresholdAtRecognition = threshold; WealthAtRecognition = wealth;
        Phase = CityRecognitionPhase.ChoicePending;
        return true;
    }

    public bool ChooseFreeCity(IEnumerable<ContractLedger> contracts, long now)
    {
        ObserveTime(now);
        if (!ownedPlayerHome || Phase is not (CityRecognitionPhase.ChoicePending or CityRecognitionPhase.Negotiating) ||
            !FreeCityRules.IsFreeCity(SettlementId, Owner, contracts)) return false;
        RequestedLiege = null;
        Phase = CityRecognitionPhase.FreeCityChosen;
        return true;
    }

    /// <summary>The adapter revalidates actual diplomacy and legal representatives before every signature.</summary>
    public bool SeekLiege(PartyRef liege, IEnumerable<PartyRef> eligibleLieges, IEnumerable<ContractLedger> contracts, long now)
    {
        ArgumentNullException.ThrowIfNull(liege);
        ArgumentNullException.ThrowIfNull(eligibleLieges);
        ObserveTime(now);
        if (!ownedPlayerHome || Phase is not (CityRecognitionPhase.ChoicePending or CityRecognitionPhase.FreeCityChosen) ||
            liege.Kind != PartyKind.Faction || liege == Owner || !eligibleLieges.Contains(liege) ||
            !FreeCityRules.IsFreeCity(SettlementId, Owner, contracts)) return false;
        RequestedLiege = liege;
        Phase = CityRecognitionPhase.Negotiating;
        return true;
    }

    public bool CancelNegotiation(long now)
    {
        ObserveTime(now);
        if (!ownedPlayerHome || Phase != CityRecognitionPhase.Negotiating) return false;
        RequestedLiege = null;
        Phase = CityRecognitionPhase.ChoicePending;
        return true;
    }

    /// <summary>Manual reconsideration after a real binding has ended; never an automatic repeat popup.</summary>
    public bool ReconsiderAfterRelease(IEnumerable<ContractLedger> contracts, long now)
    {
        ObserveTime(now);
        if (!ownedPlayerHome || Phase != CityRecognitionPhase.FeudalAffiliationRecorded ||
            !FirstChoiceTick.HasValue || !FreeCityRules.IsFreeCity(SettlementId, Owner, contracts)) return false;
        Phase = CityRecognitionPhase.ChoicePending;
        return true;
    }

    void ObserveTime(long now)
    {
        if (now < 0 || now < lastObservedTick) throw new ArgumentOutOfRangeException(nameof(now), "Recognition time cannot run backward.");
        lastObservedTick = now;
    }
}
