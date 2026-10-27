namespace Honor.Contracts.Rules;

public static class LeaderPreservationProfiles
{
    public const int Current = 7;
    // Zero is the old, omitted marker. Never reinterpret historical leader fingerprints as profile7.
    public static int Resolve(int saved) => saved == 0 ? 5 : saved == Current ? Current : throw new ArgumentException("Unsupported saved leader preservation profile.");
}
