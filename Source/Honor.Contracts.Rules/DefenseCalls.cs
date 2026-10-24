namespace Honor.Contracts.Rules;

public enum DefenseIncidentKind { Attack, Siege }

public sealed record DefenseCall(string ContractId, string IncidentId, string SettlementId,
    string AggressorFactionId, DefenseIncidentKind Kind, long OccurredTick, string ObligationId);

public static class DefenseCalls
{
    /// <summary>
    /// Routes an engine-verified hostile attack to an agreed protection clause. The adapter must verify
    /// settlement ownership, hostility, incident uniqueness, and the continuing validity of the contract.
    /// It must create/save a real quest or report failure, rather than treating the call itself as success.
    /// </summary>
    public static DefenseCall TryOpen(ContractLedger contract, string clauseId, string incidentId,
        string settlementId, string defenderFactionId, string aggressorFactionId, DefenseIncidentKind kind, long incidentTick, long now)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(incidentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementId);
        ArgumentException.ThrowIfNullOrWhiteSpace(defenderFactionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggressorFactionId);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (incidentTick < 0 || incidentTick > now) throw new ArgumentOutOfRangeException(nameof(incidentTick));
        if (contract.StartedTick == null || incidentTick < contract.StartedTick.Value) return null;
        var terms = contract.Terms;
        if (terms.Kind == ContractKind.PersonalVassalage || terms.Subject.Id != defenderFactionId ||
            aggressorFactionId == defenderFactionId || aggressorFactionId == terms.Liege.Id ||
            (terms.Kind == ContractKind.SettlementCharter && terms.SettlementId != settlementId)) return null;
        var clause = terms.Clauses.SingleOrDefault(c => c.Id == clauseId);
        if (clause == null || clause.Kind != ObligationKind.Protection || clause.Debtor != ContractSide.Liege) return null;
        int before = contract.Obligations.Count;
        if (!contract.RequestVerifiedDuty(clauseId, incidentId, now)) return null;
        var duty = contract.Obligations[before];
        return new(contract.Id, incidentId, settlementId, aggressorFactionId, kind, incidentTick, duty.Id);
    }
}
