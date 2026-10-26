namespace Honor.Contracts.Rules;

public enum LeaderNativeCall { Succession, Affiliation, LegacyCombined }

/// <summary>Process-local exclusion during native callbacks; never saved or treated as outcome evidence.</summary>
public static class LeaderNativeCallScope<T> where T : class
{
    public static T Owner { get; private set; }
    public static bool AllowsSuccession { get; private set; }
    public static void Run(T owner, LeaderNativeCall purpose, Action call)
    {
        if (owner == null || call == null || !RuntimeCompat.IsDefined(purpose)) throw new ArgumentException("Invalid native leader call scope.");
        if (Owner != null) throw new ArgumentException("Nested native leader mutation is not supported.");
        Owner = owner; AllowsSuccession = purpose != LeaderNativeCall.Affiliation;
        try { call(); }
        finally { AllowsSuccession = false; Owner = null; }
    }
}
