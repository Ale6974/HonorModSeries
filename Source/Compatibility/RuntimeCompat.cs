global using Honor.Compatibility;

using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Honor.Compatibility;

internal static class RuntimeCompat
{
    public static void NotNull(object value, [CallerArgumentExpression("value")] string name = null)
    { if (value == null) throw new ArgumentNullException(name); }
    public static void NotBlank(string value, [CallerArgumentExpression("value")] string name = null)
    { NotNull(value, name); if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value must not be blank.", name); }
    public static bool IsDefined<T>(T value) where T : struct, Enum => Enum.IsDefined(typeof(T), value);
    public static T[] EnumValues<T>() where T : struct, Enum => (T[])Enum.GetValues(typeof(T));
    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static int Clamp(int value, int min, int max) { if (min > max) throw new ArgumentException("Invalid clamp limits."); return value < min ? min : value > max ? max : value; }
    public static long Clamp(long value, long min, long max) { if (min > max) throw new ArgumentException("Invalid clamp limits."); return value < min ? min : value > max ? max : value; }
    public static float Clamp(float value, float min, float max) { if (min > max) throw new ArgumentException("Invalid clamp limits."); return value < min ? min : value > max ? max : value; }
    public static double Clamp(double value, double min, double max) { if (min > max) throw new ArgumentException("Invalid clamp limits."); return value < min ? min : value > max ? max : value; }
    public static decimal Clamp(decimal value, decimal min, decimal max) { if (min > max) throw new ArgumentException("Invalid clamp limits."); return value < min ? min : value > max ? max : value; }
    public static byte[] Sha256(byte[] data) { using var sha = SHA256.Create(); return sha.ComputeHash(data); }
    public static string Hex(byte[] data) => BitConverter.ToString(data).Replace("-", "");
}
