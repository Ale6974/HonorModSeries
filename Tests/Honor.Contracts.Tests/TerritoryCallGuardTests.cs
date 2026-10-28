using Honor.Contracts.Rules;

internal static class TerritoryCallGuardTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value) { count++; if (!value) throw new Exception("City call evidence regression."); }
        void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        TerritoryCallGuard.RequireNewCall(true, false, false); Check(true);
        Reject(() => TerritoryCallGuard.RequireNewCall(true, true, false));
        Reject(() => TerritoryCallGuard.RequireNewCall(true, true, true));
        Reject(() => TerritoryCallGuard.RequireStartedForTarget(true, false, false));
        TerritoryCallGuard.RequireStartedForTarget(true, true, false); Check(true);
        TerritoryCallGuard.RequireStartedForTarget(true, true, true); Check(true);
        Reject(() => TerritoryCallGuard.Validate(true, false, true));
        Reject(() => TerritoryCallGuard.Validate(false, true, false));
        Reject(() => TerritoryCallGuard.RequireVerifiedUnmoved(true, true, false));
        TerritoryCallGuard.RequireVerifiedUnmoved(true, false, false); Check(true);
        TerritoryCallGuard.RequireVerifiedUnmoved(true, true, true); Check(true);
        TerritoryCallGuard.RequireNewCall(false, false, false); Check(true);
        TerritoryCallGuard.RequireStartedForTarget(false, false, false); Check(true);
        return count;
    }
}
