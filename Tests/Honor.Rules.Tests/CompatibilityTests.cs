using System.Text;

internal static class CompatibilityTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        Check(RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes("abc"))) == "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", "SHA256 known vector on both frameworks");
        Check(RuntimeCompat.Hex(RuntimeCompat.Sha256([])) == "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", "empty SHA256 vector");
        Check(RuntimeCompat.Clamp(-2, 0, 1) == 0 && RuntimeCompat.Clamp(2, 0, 1) == 1, "integer clamping");
        Check(RuntimeCompat.Clamp(long.MaxValue, 0L, 10L) == 10 && RuntimeCompat.Clamp(long.MinValue, 0L, 10L) == 0, "long clamping without overflow");
        Check(RuntimeCompat.Clamp(.25f, 0f, 1f) == .25f && float.IsNaN(RuntimeCompat.Clamp(float.NaN, 0f, 1f)), "float value and NaN preserved");
        Check(RuntimeCompat.Clamp(double.PositiveInfinity, 0d, 1d) == 1 && double.IsNaN(RuntimeCompat.Clamp(double.NaN, 0d, 1d)), "double infinity and NaN semantics");
        Check(RuntimeCompat.Clamp(.15m, .1m, .2m) == .15m, "decimal precision preserved");
        Check(!RuntimeCompat.IsFinite(float.NaN) && !RuntimeCompat.IsFinite(float.NegativeInfinity) && RuntimeCompat.IsFinite(float.MaxValue), "float finite checks");
        Check(!RuntimeCompat.IsFinite(double.NaN) && !RuntimeCompat.IsFinite(double.PositiveInfinity) && RuntimeCompat.IsFinite(double.MaxValue), "double finite checks");
        bool rejected = false;
        try { RuntimeCompat.Clamp(0, 2, 1); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "reversed clamp bounds rejected");
        object absent = null; rejected = false;
        try { RuntimeCompat.NotNull(absent); } catch (ArgumentNullException e) { rejected = e.ParamName == "absent"; }
        Check(rejected, "caller argument name retained on legacy runtime");
        rejected = false;
        try { RuntimeCompat.NotBlank(" "); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "blank strings rejected");
        Check(RuntimeCompat.EnumValues<DayOfWeek>().Length == 7 && RuntimeCompat.IsDefined(DayOfWeek.Friday) && !RuntimeCompat.IsDefined((DayOfWeek)99), "enum helpers preserve validation");
        return count;
    }
}
