namespace Honor.Contracts.Rules;

/// <summary>New city calls record their start before execution; an unknown call is never replayed.</summary>
public static class TerritoryCallGuard
{
    public static void Validate(bool enabled, bool started, bool returned)
    {
        if ((!enabled && (started || returned)) || (returned && !started))
            throw new ArgumentException("Invalid city-call evidence.");
    }
    public static void RequireNewCall(bool enabled, bool started, bool returned)
    {
        Validate(enabled, started, returned);
        if (enabled && started) throw new ArgumentException("An already-started city call must not be replayed.");
    }
    public static void RequireStartedForTarget(bool enabled, bool started, bool returned)
    {
        Validate(enabled, started, returned);
        if (enabled && !started) throw new ArgumentException("Target ownership alone does not prove this city call started.");
    }
    public static void RequireVerifiedUnmoved(bool enabled, bool started, bool returned)
    {
        Validate(enabled, started, returned);
        if (enabled && started && !returned) throw new ArgumentException("An unreturned call is not a verified no-op.");
    }
}
