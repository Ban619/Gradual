namespace Gradual.Infrastructure;

/// <summary>
/// Lightweight string extension helpers used across service and UI layers.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Returns <paramref name="fallback"/> when the string is null or whitespace.
    /// Equivalent to: string.IsNullOrWhiteSpace(s) ? fallback : s
    /// </summary>
    public static string IfEmpty(this string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value!;

    /// <summary>
    /// Truncates the string to at most <paramref name="maxLength"/> characters,
    /// appending "…" if truncated.
    /// </summary>
    public static string Truncate(this string? value, int maxLength)
    {
        if (value == null) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }

    /// <summary>
    /// Returns true if the string contains <paramref name="search"/> using
    /// OrdinalIgnoreCase comparison.
    /// </summary>
    public static bool ContainsI(this string? value, string search) =>
        value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Fluent alias for string.IsNullOrWhiteSpace.</summary>
    public static bool IsNullOrWhiteSpace(this string? value) =>
        string.IsNullOrWhiteSpace(value);

    /// <summary>Returns true if the string starts with <paramref name="prefix"/> (case-insensitive).</summary>
    public static bool StartsWithI(this string? value, string prefix) =>
        value?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true;
}
