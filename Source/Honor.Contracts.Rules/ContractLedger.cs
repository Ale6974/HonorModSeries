namespace Honor.Contracts.Rules;

public sealed record ObligationRecord(string Id, string ClauseId, ContractSide Debtor, long DueTick,
    long DeadlineTick, ObligationPhase Phase, string EvidenceId = null);

/// <summary>
/// Engine-independent protocol. Evidence must be verified by the game adapter before submitting it.
/// This class never creates items, changes goodwill, or decides whether a missed duty was culpable.
/// </summary>
public sealed class ContractLedger
{
    readonly List<ObligationRecord> obligations = new();
    readonly HashSet<string> evidence = new(StringComparer.Ordinal);
    readonly HashSet<string> requestIds = new(StringComparer.Ordinal);
    readonly HashSet<ContractSide> signatures = new();
    readonly HashSet<ContractSide> releaseSignatures = new();
    long lastObservedTick = -1;

    public string Id { get; }
    public ContractTerms Terms { get; }
    public ContractPhase Phase { get; private set; } = ContractPhase.Proposed;
    public long? StartedTick { get; private set; }
    public long? EndTick { get; private set; }
    public ContractSide? BreachingSide { get; private set; }
    public IReadOnlyList<ObligationRecord> Obligations => obligations.AsReadOnly();
    public bool IsTerminal => Phase is ContractPhase.Expired or ContractPhase.Released or ContractPhase.Breached;
    public bool HasSigned(ContractSide side) => signatures.Contains(side);

    public ContractLedger(string id, ContractTerms terms)
    {
        RuntimeCompat.NotBlank(id);
        RuntimeCompat.NotNull(terms);
        Id = id; Terms = terms;
    }

    public bool Sign(ContractSide side, long now)
    {
        ValidateSide(side);
        Advance(now);
        if (Terms.Origin == ContractOrigin.Occupation || Phase != ContractPhase.Proposed || signatures.Contains(side)) return false;
        // Check all time arithmetic before recording either the signature or activation.
        long end = checked(now + Terms.DurationTicks);
        foreach (var clause in Terms.Clauses) _ = checked(end + clause.ResponseTicks + clause.GraceTicks);
        signatures.Add(side);
        if (signatures.Count != 2) return true;
        Activate(now, end);
        return true;
    }

    // Only the verified-conquest protocol can establish an imposed charter. No defender signature is fabricated.
    internal bool EstablishOccupation(long now)
    {
        Advance(now);
        if (Terms.Origin != ContractOrigin.Occupation || Phase != ContractPhase.Proposed) return false;
        long end = checked(now + Terms.DurationTicks);
        foreach (var clause in Terms.Clauses) _ = checked(end + clause.ResponseTicks + clause.GraceTicks);
        Activate(now, end);
        return true;
    }

    void Activate(long now, long end)
    {
        StartedTick = now; EndTick = end; Phase = ContractPhase.Active;
        foreach (var clause in Terms.Clauses.Where(c => c.Kind == ObligationKind.Tribute))
        {
            long count = Terms.DurationTicks / clause.IntervalTicks;
            for (long n = 1; n <= count; n++)
            {
                long due = now + n * clause.IntervalTicks;
                obligations.Add(new($"period/{clause.Id}/{n}", clause.Id, clause.Debtor,
                    due, due + clause.GraceTicks, ObligationPhase.Pending));
            }
        }
    }

    public void Advance(long now)
    {
        if (now < 0 || now < lastObservedTick) throw new ArgumentOutOfRangeException(nameof(now), "Time cannot run backward.");
        lastObservedTick = now;
        if (Phase == ContractPhase.Proposed || IsTerminal) return;
        for (int i = 0; i < obligations.Count; i++)
            if (obligations[i].Phase == ObligationPhase.Pending && now > obligations[i].DeadlineTick)
                obligations[i] = obligations[i] with { Phase = ObligationPhase.Overdue };
        Recalculate(now);
    }

    /// <summary>Call only after verifying a real protection/service trigger, not arbitrary player requests.</summary>
    public bool RequestVerifiedDuty(string clauseId, string triggerId, long now)
    {
        RuntimeCompat.NotBlank(triggerId);
        Advance(now);
        if (Phase is not (ContractPhase.Active or ContractPhase.Disputed) || now >= EndTick.Value) return false;
        var clause = Terms.Clauses.SingleOrDefault(c => c.Id == clauseId);
        if (clause == null || clause.Kind == ObligationKind.Tribute || requestIds.Contains(triggerId)) return false;
        var existing = obligations.Where(o => o.ClauseId == clause.Id).ToArray();
        if (existing.Length >= clause.MaximumRequests || existing.Any(o => o.Phase is ObligationPhase.Pending or ObligationPhase.Overdue))
            return false;
        int index = existing.Length + 1;
        long due = checked(now + clause.ResponseTicks);
        requestIds.Add(triggerId);
        obligations.Add(new($"request/{clause.Id}/{index}", clause.Id, clause.Debtor,
            due, checked(due + clause.GraceTicks), ObligationPhase.Pending));
        return true;
    }

    /// <summary>Transfer/quest completion receipt; a receipt can settle at most one obligation.</summary>
    public bool RecordVerifiedFulfillment(string obligationId, string receiptId, long now)
    {
        RuntimeCompat.NotBlank(receiptId);
        Advance(now);
        if (IsTerminal || Phase == ContractPhase.Proposed || evidence.Contains(receiptId)) return false;
        int index = obligations.FindIndex(o => o.Id == obligationId);
        if (index < 0) return false;
        var item = obligations[index];
        var clause = Terms.Clauses.Single(c => c.Id == item.ClauseId);
        // Tribute cannot be prepaid to farm honor. Requested assistance can be completed early.
        if (item.Phase != ObligationPhase.Pending || (clause.Kind == ObligationKind.Tribute && now < item.DueTick)) return false;
        evidence.Add(receiptId);
        obligations[index] = item with { Phase = ObligationPhase.Fulfilled, EvidenceId = receiptId };
        Recalculate(now);
        return true;
    }

    /// <summary>A late delivery cannot silently erase a dispute; the adapter must resolve it explicitly.</summary>
    public bool ResolveMissedDuty(string obligationId, bool culpable, string findingId, long now)
    {
        RuntimeCompat.NotBlank(findingId);
        Advance(now);
        if (Phase != ContractPhase.Disputed || evidence.Contains(findingId)) return false;
        int index = obligations.FindIndex(o => o.Id == obligationId && o.Phase == ObligationPhase.Overdue);
        if (index < 0) return false;
        evidence.Add(findingId);
        var item = obligations[index];
        if (culpable)
        {
            Phase = ContractPhase.Breached;
            BreachingSide = item.Debtor;
            obligations[index] = item with { EvidenceId = findingId };
            DischargeUnresolved();
        }
        else
        {
            obligations[index] = item with { Phase = ObligationPhase.Excused, EvidenceId = findingId };
            Recalculate(now);
        }
        return true;
    }

    public bool ConsentToRelease(ContractSide side, long now)
    {
        ValidateSide(side);
        Advance(now);
        if (Phase is not (ContractPhase.Active or ContractPhase.Disputed) || !releaseSignatures.Add(side)) return false;
        if (releaseSignatures.Count == 2)
        {
            Phase = ContractPhase.Released;
            DischargeUnresolved();
        }
        return true;
    }

    /// <summary>Deliberate unilateral repudiation; peaceful expiry and mutual release use other transitions.</summary>
    public bool Repudiate(ContractSide side, long now)
    {
        ValidateSide(side);
        Advance(now);
        if (Phase is not (ContractPhase.Active or ContractPhase.Disputed)) return false;
        BreachingSide = side; Phase = ContractPhase.Breached;
        DischargeUnresolved();
        return true;
    }

    void Recalculate(long now)
    {
        if (obligations.Any(o => o.Phase == ObligationPhase.Overdue)) Phase = ContractPhase.Disputed;
        else if (now >= EndTick.Value && obligations.All(o => o.Phase != ObligationPhase.Pending)) Phase = ContractPhase.Expired;
        else Phase = ContractPhase.Active;
    }

    void DischargeUnresolved()
    {
        for (int i = 0; i < obligations.Count; i++)
            if (obligations[i].Phase is ObligationPhase.Pending or ObligationPhase.Overdue)
                obligations[i] = obligations[i] with { Phase = ObligationPhase.Discharged };
    }

    static void ValidateSide(ContractSide side)
    {
        if (!RuntimeCompat.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
    }
}
