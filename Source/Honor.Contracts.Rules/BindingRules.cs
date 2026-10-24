namespace Honor.Contracts.Rules;

public enum BindingConflict { None, SubjectAlreadyBound, SettlementAlreadyBound, Cycle }

public static class BindingRules
{
    /// <summary>
    /// Recheck against the authoritative active ledger immediately before activation.
    /// A settlement charter binds that settlement, never the owning faction's other settlements.
    /// </summary>
    public static BindingConflict Check(ContractTerms candidate, IEnumerable<ContractLedger> existing)
    {
        RuntimeCompat.NotNull(candidate);
        RuntimeCompat.NotNull(existing);
        var active = existing.Where(c => c.Phase is ContractPhase.Active or ContractPhase.Disputed).Select(c => c.Terms).ToArray();
        if (candidate.Kind == ContractKind.SettlementCharter)
            return active.Any(c => c.Kind == candidate.Kind && c.SettlementId == candidate.SettlementId)
                ? BindingConflict.SettlementAlreadyBound : BindingConflict.None;
        var sameScope = active.Where(c => c.Kind == candidate.Kind).ToArray();
        if (sameScope.Any(c => c.Subject == candidate.Subject)) return BindingConflict.SubjectAlreadyBound;
        // Follow the proposed liege's superiors. A repeated node also rejects already-corrupt cycles.
        var visited = new HashSet<PartyRef> { candidate.Subject };
        PartyRef cursor = candidate.Liege;
        while (true)
        {
            if (!visited.Add(cursor)) return BindingConflict.Cycle;
            var parents = sameScope.Where(c => c.Subject == cursor).ToArray();
            if (parents.Length > 1) return BindingConflict.SubjectAlreadyBound;
            if (parents.Length == 0) return BindingConflict.None;
            cursor = parents[0].Liege;
        }
    }
}
