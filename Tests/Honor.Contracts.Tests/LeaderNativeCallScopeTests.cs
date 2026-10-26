using Honor.Contracts.Rules;

internal static class LeaderNativeCallScopeTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string message) { count++; if (!ok) throw new Exception(message); }
        void Reject(Action call) { bool rejected = false; try { call(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Nested/invalid leader callback must be rejected."); }
        var owner = new object(); var other = new object(); bool nested = false;
        foreach (var purpose in new[] { LeaderNativeCall.Succession, LeaderNativeCall.Affiliation, LeaderNativeCall.LegacyCombined })
        {
            LeaderNativeCallScope<object>.Run(owner, purpose, () => {
                Check(ReferenceEquals(LeaderNativeCallScope<object>.Owner, owner), "Callback has exact active owner.");
                Check(LeaderNativeCallScope<object>.AllowsSuccession == (purpose != LeaderNativeCall.Affiliation), "Affiliation blocks recovery without claiming successor-generation scope.");
                Reject(() => LeaderNativeCallScope<object>.Run(owner, purpose, () => nested = true));
                Reject(() => LeaderNativeCallScope<object>.Run(other, LeaderNativeCall.Succession, () => nested = true));
                Check(ReferenceEquals(LeaderNativeCallScope<object>.Owner, owner) && !nested, "Rejected callbacks retain the outer scope and never run.");
            });
            Check(LeaderNativeCallScope<object>.Owner == null && !LeaderNativeCallScope<object>.AllowsSuccession, "Normal return releases all process-local state.");
            bool threw = false;
            try { LeaderNativeCallScope<object>.Run(owner, purpose, () => throw new InvalidOperationException("native failure")); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw && LeaderNativeCallScope<object>.Owner == null && !LeaderNativeCallScope<object>.AllowsSuccession, "Native exception propagates and releases scope without an outcome receipt.");
        }
        Reject(() => LeaderNativeCallScope<object>.Run(null, LeaderNativeCall.Affiliation, () => nested = true));
        Reject(() => LeaderNativeCallScope<object>.Run(owner, (LeaderNativeCall)99, () => nested = true));
        Reject(() => LeaderNativeCallScope<object>.Run(owner, LeaderNativeCall.Succession, null));
        Check(!nested && LeaderNativeCallScope<object>.Owner == null, "Invalid entry never acquires authority or executes callbacks.");
        return count;
    }
}
