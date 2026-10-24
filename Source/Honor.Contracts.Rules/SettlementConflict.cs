namespace Honor.Contracts.Rules;

public enum SettlementConflictPhase { Assaulting, Besieging, Occupied, Contracted, Withdrawn, Razed }

/// <summary>
/// Protocol for one foreign settlement. Inputs are facts verified by the game adapter;
/// elapsed time alone never proves surrender or occupation. This does not simulate combat.
/// </summary>
public sealed class SettlementConflict
{
    long lastObservedTick;
    public string Id { get; }
    public string SettlementId { get; }
    public PartyRef Attacker { get; }
    public PartyRef Defender { get; }
    public SettlementConflictPhase Phase { get; private set; } = SettlementConflictPhase.Assaulting;
    public string OccupationEvidenceId { get; private set; }
    public string SettlementEvidenceId { get; private set; }
    public ContractLedger Contract { get; private set; }

    public SettlementConflict(string id, string settlementId, PartyRef attacker, PartyRef defender, long now)
    {
        RuntimeCompat.NotBlank(id);
        RuntimeCompat.NotBlank(settlementId);
        RuntimeCompat.NotNull(attacker);
        RuntimeCompat.NotNull(defender);
        if (attacker.Kind != PartyKind.Faction || defender.Kind != PartyKind.Faction || attacker == defender)
            throw new ArgumentException("Conquest requires different attacking and defending factions.");
        if (now < 0) throw new ArgumentOutOfRangeException(nameof(now));
        Id = id; SettlementId = settlementId; Attacker = attacker; Defender = defender;
        lastObservedTick = now;
    }

    public bool BeginVerifiedSiege(long now)
    {
        Observe(now);
        if (Phase != SettlementConflictPhase.Assaulting) return false;
        Phase = SettlementConflictPhase.Besieging;
        return true;
    }

    /// <summary>The adapter must verify defeated defenders AND surviving settlement AND actual attacker control.</summary>
    public bool RecordVerifiedOccupation(string evidenceId, long now)
    {
        RuntimeCompat.NotBlank(evidenceId);
        Observe(now);
        if (Phase is not (SettlementConflictPhase.Assaulting or SettlementConflictPhase.Besieging)) return false;
        OccupationEvidenceId = evidenceId;
        Phase = SettlementConflictPhase.Occupied;
        return true;
    }

    /// <summary>Records BOTH sides' verified acceptance of this exact capitulation text, never a mere surrender offer.</summary>
    public ContractLedger AcceptVerifiedCapitulation(ContractTerms acceptedTerms, string evidenceId,
        IEnumerable<ContractLedger> existing, long now)
    {
        RuntimeCompat.NotBlank(evidenceId);
        Observe(now);
        if (Phase is not (SettlementConflictPhase.Assaulting or SettlementConflictPhase.Besieging or SettlementConflictPhase.Occupied)) return null;
        if (!Matches(acceptedTerms, ContractOrigin.Capitulation) || !CanBind(acceptedTerms, existing)) return null;
        var result = new ContractLedger($"conquest/{Id}", acceptedTerms);
        result.Sign(ContractSide.Liege, now);
        result.Sign(ContractSide.Subject, now);
        Contract = result; SettlementEvidenceId = evidenceId; Phase = SettlementConflictPhase.Contracted;
        return result;
    }

    public ContractLedger EstablishOccupationCharter(ContractTerms imposedTerms, string evidenceId,
        IEnumerable<ContractLedger> existing, long now)
    {
        RuntimeCompat.NotBlank(evidenceId);
        Observe(now);
        if (Phase != SettlementConflictPhase.Occupied || !Matches(imposedTerms, ContractOrigin.Occupation) ||
            !CanBind(imposedTerms, existing)) return null;
        var result = new ContractLedger($"conquest/{Id}", imposedTerms);
        result.EstablishOccupation(now);
        Contract = result; SettlementEvidenceId = evidenceId; Phase = SettlementConflictPhase.Contracted;
        return result;
    }

    public bool Withdraw(long now)
    {
        Observe(now);
        if (Phase is not (SettlementConflictPhase.Assaulting or SettlementConflictPhase.Besieging or SettlementConflictPhase.Occupied)) return false;
        Phase = SettlementConflictPhase.Withdrawn;
        return true;
    }

    public bool RecordVerifiedRazing(long now)
    {
        Observe(now);
        if (Phase is not (SettlementConflictPhase.Assaulting or SettlementConflictPhase.Besieging or SettlementConflictPhase.Occupied)) return false;
        Phase = SettlementConflictPhase.Razed;
        return true;
    }

    bool Matches(ContractTerms terms, ContractOrigin origin) => terms != null && terms.Kind == ContractKind.SettlementCharter &&
        terms.Origin == origin && terms.SettlementId == SettlementId && terms.Liege == Attacker && terms.Subject == Defender;

    bool CanBind(ContractTerms terms, IEnumerable<ContractLedger> existing)
    {
        RuntimeCompat.NotNull(existing);
        var all = existing.ToArray();
        return !all.Any(c => c.Id == $"conquest/{Id}") && BindingRules.Check(terms, all) == BindingConflict.None;
    }

    void Observe(long now)
    {
        if (now < 0 || now < lastObservedTick) throw new ArgumentOutOfRangeException(nameof(now), "Conflict time cannot run backward.");
        lastObservedTick = now;
    }
}
