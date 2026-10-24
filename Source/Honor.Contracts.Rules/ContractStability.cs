namespace Honor.Contracts.Rules;

public enum ContractStabilityPhase { Stable, Restless, WarningNeeded, NoticeGiven, SecessionReady, Closed }

/// <summary>Preview balance parameters; no game settings UI exists yet.</summary>
public sealed class ContractStabilityPolicy
{
    public double LowHonorThreshold { get; }
    public double LowTrustThreshold { get; }
    public double PressurePerDay { get; }
    public double RecoveryPerDay { get; }
    public double WarningThreshold { get; }
    public double ExitThreshold { get; }
    public double ResetThreshold { get; }
    public long NoticeGraceTicks { get; }
    public double LossTrustMultiplier { get; }
    public double RepairTrustMultiplier { get; }

    public ContractStabilityPolicy(double lowHonorThreshold = .4, double lowTrustThreshold = .4,
        double pressurePerDay = .04, double recoveryPerDay = .02, double warningThreshold = .4,
        double exitThreshold = .8, double resetThreshold = .2, long noticeGraceTicks = 180000,
        double lossTrustMultiplier = 1, double repairTrustMultiplier = .25)
    {
        double[] values = [lowHonorThreshold, lowTrustThreshold, pressurePerDay, recoveryPerDay,
            warningThreshold, exitThreshold, resetThreshold, lossTrustMultiplier, repairTrustMultiplier];
        if (values.Any(v => !double.IsFinite(v))) throw new ArgumentException("Stability parameters must be finite.");
        if (lowHonorThreshold is <= 0 or > 1 || lowTrustThreshold is <= 0 or > 1 ||
            pressurePerDay is < 0 or > 1 || recoveryPerDay is < 0 or > 1 ||
            resetThreshold < 0 || resetThreshold >= warningThreshold || warningThreshold >= exitThreshold || exitThreshold > 1 ||
            noticeGraceTicks is < 1 or > int.MaxValue || lossTrustMultiplier is < 0 or > 10 || repairTrustMultiplier is < 0 or > 10)
            throw new ArgumentException("Invalid stability thresholds, rates, multipliers, or grace period.");
        LowHonorThreshold = lowHonorThreshold; LowTrustThreshold = lowTrustThreshold;
        PressurePerDay = pressurePerDay; RecoveryPerDay = recoveryPerDay;
        WarningThreshold = warningThreshold; ExitThreshold = exitThreshold; ResetThreshold = resetThreshold;
        NoticeGraceTicks = noticeGraceTicks; LossTrustMultiplier = lossTrustMultiplier; RepairTrustMultiplier = repairTrustMultiplier;
    }
}

/// <summary>
/// Direct-party trust and secession pressure for one faction/settlement contract.
/// Apply honor events in revision/time order BEFORE advancing past their ticks.
/// Readiness is a decision input, never automatic repudiation or a personal dishonor finding.
/// </summary>
public sealed class ContractStability
{
    readonly ContractLedger contract;
    readonly ContractStabilityPolicy policy;
    readonly HashSet<string> noticeEvidence = new(StringComparer.Ordinal);
    long lastTick, liegeRevision, subjectRevision;
    public string ContractId => contract.Id;
    public double LiegeHonor { get; private set; }
    public double SubjectHonor { get; private set; }
    public double SubjectTrustInLiege { get; private set; }
    public double LiegeTrustInSubject { get; private set; }
    public double SecessionPressure { get; private set; }
    public ContractStabilityPhase Phase { get; private set; }
    public long? NoticeDeliveredTick { get; private set; }
    public string NoticeEvidenceId { get; private set; }

    public ContractStability(ContractLedger contract, FactionHonorLedger liege, FactionHonorLedger subject,
        long now, ContractStabilityPolicy policy = null)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(liege);
        ArgumentNullException.ThrowIfNull(subject);
        if (contract.Terms.Kind == ContractKind.PersonalVassalage || contract.Terms.Liege != liege.Faction || contract.Terms.Subject != subject.Faction)
            throw new ArgumentException("Stability needs the actual faction parties of a territorial contract.");
        if (now < 0 || !contract.StartedTick.HasValue || now < contract.StartedTick.Value ||
            liege.AsOfTick > now || subject.AsOfTick > now)
            throw new ArgumentOutOfRangeException(nameof(now));
        contract.Advance(now);
        if (contract.Phase is not (ContractPhase.Active or ContractPhase.Disputed))
            throw new ArgumentException("Only an active/disputed contract has territorial stability.");
        this.contract = contract; this.policy = policy ?? new(); lastTick = now;
        LiegeHonor = liege.Value; SubjectHonor = subject.Value;
        SubjectTrustInLiege = liege.Value; LiegeTrustInSubject = subject.Value;
        liegeRevision = liege.Revision; subjectRevision = subject.Revision;
        Recalculate(now);
    }

    bool Live => (contract?.Phase) is ContractPhase.Active or ContractPhase.Disputed;

    public bool ApplyFactionHonorChange(FactionHonorChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        bool isLiege = change.Faction == contract.Terms.Liege;
        bool isSubject = change.Faction == contract.Terms.Subject;
        if (!isLiege && !isSubject) return false;
        long revision = isLiege ? liegeRevision : subjectRevision;
        double honor = isLiege ? LiegeHonor : SubjectHonor;
        if (change.Revision != revision + 1 || change.Before != honor) return false;
        Advance(change.Tick);
        if (!Live) return false;
        // A distinct, verified repair deed can rebuild trust even when the honor score is already capped.
        double impact = change.AssessedDelta * (change.AssessedDelta < 0 ? policy.LossTrustMultiplier : policy.RepairTrustMultiplier);
        if (isLiege)
        {
            LiegeHonor = change.After; liegeRevision = change.Revision;
            SubjectTrustInLiege = Math.Clamp(SubjectTrustInLiege + impact, 0, 1);
        }
        else
        {
            SubjectHonor = change.After; subjectRevision = change.Revision;
            LiegeTrustInSubject = Math.Clamp(LiegeTrustInSubject + impact, 0, 1);
        }
        Recalculate(change.Tick);
        return true;
    }

    public void Advance(long now)
    {
        if (now < 0 || now < lastTick) throw new ArgumentOutOfRangeException(nameof(now), "Stability time cannot run backward.");
        if (Live)
        {
            double severity = Math.Max(0, (policy.LowHonorThreshold - LiegeHonor) / policy.LowHonorThreshold) +
                Math.Max(0, (policy.LowTrustThreshold - SubjectTrustInLiege) / policy.LowTrustThreshold);
            double rate = severity > 0 ? policy.PressurePerDay * severity : -policy.RecoveryPerDay;
            SecessionPressure = Math.Clamp(SecessionPressure + rate * ((now - lastTick) / 60000d), 0, 1);
        }
        lastTick = now;
        contract.Advance(now);
        Recalculate(now);
    }

    /// <summary>Only after the game has actually delivered and saved the warning; no backdated grace period.</summary>
    public bool RecordVerifiedNoticeDelivered(string evidenceId, long now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);
        Advance(now);
        if (Phase != ContractStabilityPhase.WarningNeeded || !noticeEvidence.Add(evidenceId)) return false;
        NoticeDeliveredTick = now; NoticeEvidenceId = evidenceId;
        Recalculate(now);
        return true;
    }

    void Recalculate(long now)
    {
        if (!Live) { Phase = ContractStabilityPhase.Closed; return; }
        if (SecessionPressure <= policy.ResetThreshold)
        {
            NoticeDeliveredTick = null; NoticeEvidenceId = null;
        }
        if (NoticeDeliveredTick.HasValue)
            Phase = SecessionPressure >= policy.ExitThreshold && now - NoticeDeliveredTick.Value >= policy.NoticeGraceTicks
                ? ContractStabilityPhase.SecessionReady : ContractStabilityPhase.NoticeGiven;
        else if (SecessionPressure >= policy.WarningThreshold) Phase = ContractStabilityPhase.WarningNeeded;
        else Phase = SecessionPressure > 0 ? ContractStabilityPhase.Restless : ContractStabilityPhase.Stable;
    }
}
