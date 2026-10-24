namespace Honor.Contracts.Rules;

/// <summary>A verified public deed, not a transferable balance or a change to every member's personal Need.</summary>
public sealed record FactionHonorChange
{
    public PartyRef Faction { get; }
    public string EventId { get; }
    public long Revision { get; }
    public long Tick { get; }
    public double Before { get; }
    public double After { get; }
    public double AssessedDelta { get; }
    public double ActualDelta => After - Before;

    internal FactionHonorChange(PartyRef faction, string eventId, long revision, long tick, double before, double after, double assessedDelta)
    {
        Faction = faction; EventId = eventId; Revision = revision; Tick = tick;
        Before = before; After = after; AssessedDelta = assessedDelta;
    }
}

/// <summary>Event-only faction honor. Attribution, evidence, reward caps, and Scribe persistence belong to the game adapter.</summary>
public sealed class FactionHonorLedger
{
    readonly HashSet<string> eventIds = new(StringComparer.Ordinal);
    readonly List<FactionHonorChange> history = new();
    long lastTick;
    public PartyRef Faction { get; }
    public double Value { get; private set; }
    public long Revision { get; private set; }
    public long AsOfTick => lastTick;
    public IReadOnlyList<FactionHonorChange> History => history.AsReadOnly();

    public FactionHonorLedger(PartyRef faction, double initialHonor = .5, long now = 0)
    {
        RuntimeCompat.NotNull(faction);
        if (faction.Kind != PartyKind.Faction) throw new ArgumentException("Faction honor needs a faction identity.");
        if (!RuntimeCompat.IsFinite(initialHonor) || initialHonor is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(initialHonor));
        if (now < 0) throw new ArgumentOutOfRangeException(nameof(now));
        Faction = faction; Value = initialHonor; lastTick = now;
    }

    public FactionHonorChange RecordVerifiedEvent(string eventId, double assessedDelta, long now)
    {
        RuntimeCompat.NotBlank(eventId);
        if (!RuntimeCompat.IsFinite(assessedDelta) || assessedDelta is < -1 or > 1) throw new ArgumentOutOfRangeException(nameof(assessedDelta));
        if (eventIds.Contains(eventId)) return null;
        if (now < 0 || now < lastTick) throw new ArgumentOutOfRangeException(nameof(now), "Honor events cannot run backward.");
        long revision = checked(Revision + 1);
        double next = RuntimeCompat.Clamp(Value + assessedDelta, 0, 1);
        var change = new FactionHonorChange(Faction, eventId, revision, now, Value, next, assessedDelta);
        eventIds.Add(eventId); history.Add(change);
        Value = next; Revision = revision; lastTick = now;
        return change;
    }
}
