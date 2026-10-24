namespace Honor.Contracts.Rules;

public enum ContractKind { FactionVassalage, PersonalVassalage, SettlementCharter }
public enum PartyKind { Faction, Pawn }
public enum ContractSide { Liege, Subject }
public enum ObligationKind { Tribute, Protection, Service }
public enum ContractPhase { Proposed, Active, Disputed, Expired, Released, Breached }
public enum ObligationPhase { Pending, Overdue, Fulfilled, Excused, Discharged }

public sealed record PartyRef
{
    public PartyKind Kind { get; }
    public string Id { get; }
    public PartyRef(PartyKind kind, string id)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Kind = kind;
        Id = id;
    }
}

/// <summary>Negotiated rights; the engine adapter must enforce each supported right.</summary>
public sealed record CharterRights(bool LocalGovernment, bool OwnDiplomacy, bool CanRefuseExtraLevies);

public sealed record ObligationClause
{
    public string Id { get; }
    public ObligationKind Kind { get; }
    public ContractSide Debtor { get; }
    public long IntervalTicks { get; }
    public long GraceTicks { get; }
    public string ResourceDef { get; }
    public int Amount { get; }
    public int MaximumRequests { get; }
    public long ResponseTicks { get; }

    public ObligationClause(string id, ObligationKind kind, ContractSide debtor, long intervalTicks,
        long graceTicks, string resourceDef = null, int amount = 0, int maximumRequests = 0, long responseTicks = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(debtor)) throw new ArgumentException("Unknown clause type or side.");
        if (graceTicks < 0 || graceTicks > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(graceTicks));
        if (kind == ObligationKind.Tribute)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceDef);
            if (intervalTicks <= 0 || intervalTicks > int.MaxValue || amount <= 0 || maximumRequests != 0 || responseTicks != 0)
                throw new ArgumentException("Tribute needs a positive interval and amount, with no request quota.");
        }
        else if (intervalTicks != 0 || resourceDef != null || amount != 0 || maximumRequests is < 1 or > 120 ||
            responseTicks <= 0 || responseTicks > int.MaxValue)
            throw new ArgumentException("Requested obligations need a finite quota and no recurring resource charge.");
        if (kind == ObligationKind.Protection && debtor != ContractSide.Liege)
            throw new ArgumentException("The liege owes protection.");
        if (kind == ObligationKind.Service && debtor != ContractSide.Subject)
            throw new ArgumentException("The subject owes service.");
        Id = id; Kind = kind; Debtor = debtor; IntervalTicks = intervalTicks; GraceTicks = graceTicks;
        ResourceDef = resourceDef; Amount = amount; MaximumRequests = maximumRequests;
        ResponseTicks = responseTicks;
    }
}

/// <summary>Immutable proposal snapshot. Changing terms requires a new proposal and both signatures.</summary>
public sealed class ContractTerms
{
    public ContractKind Kind { get; }
    public PartyRef Liege { get; }
    public PartyRef Subject { get; }
    public string SettlementId { get; }
    public long DurationTicks { get; }
    public CharterRights Rights { get; }
    public IReadOnlyList<ObligationClause> Clauses { get; }

    public ContractTerms(ContractKind kind, PartyRef liege, PartyRef subject, long durationTicks,
        IEnumerable<ObligationClause> clauses, string settlementId = null, CharterRights rights = null)
    {
        ArgumentNullException.ThrowIfNull(liege);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(clauses);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (liege == subject) throw new ArgumentException("A party cannot bind itself.");
        PartyKind required = kind == ContractKind.PersonalVassalage ? PartyKind.Pawn : PartyKind.Faction;
        if (liege.Kind != required || subject.Kind != required) throw new ArgumentException("Wrong party scope.");
        if (durationTicks <= 0 || durationTicks > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(durationTicks));
        if (kind == ContractKind.SettlementCharter)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settlementId);
            ArgumentNullException.ThrowIfNull(rights);
        }
        else if (settlementId != null || rights != null) throw new ArgumentException("Only a charter names a settlement and rights.");
        var copy = clauses.ToArray();
        if (copy.Length is < 1 or > 16 || copy.Any(c => c == null) ||
            copy.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("Clauses must have distinct IDs and a bounded nonempty list.");
        if (copy.Any(c => c.Kind == ObligationKind.Tribute &&
            (durationTicks / c.IntervalTicks is < 1 or > 120)))
            throw new ArgumentException("A tribute clause needs between one and 120 scheduled deliveries.");
        if (!copy.Any(c => c.Kind == ObligationKind.Protection))
            throw new ArgumentException("Vassalage must include the liege's protection duty.");
        Kind = kind; Liege = liege; Subject = subject; DurationTicks = durationTicks;
        SettlementId = settlementId; Rights = rights; Clauses = Array.AsReadOnly(copy);
    }
}
