namespace Honor.Contracts.Preview;

/// <summary>A localized explanation, never a transfer permission or a persisted recovery record.</summary>
internal sealed class ResidentMoveBlocked(string key) : ArgumentException(key)
{
    internal string Key { get; } = key;
}
