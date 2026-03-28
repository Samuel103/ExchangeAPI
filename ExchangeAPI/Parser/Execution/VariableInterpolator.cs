using System.Text.RegularExpressions;

namespace ExchangeAPI.Parser.Execution;

public static class VariableInterpolator
{
    private static readonly Regex Pattern = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    /// <summary>
    /// If the template is exactly "{{varName}}", returns the variable object directly.
    /// Otherwise performs string substitution for every {{varName}} occurrence.
    /// Returns null when the template is null or empty.
    /// </summary>
    public static object? Interpolate(string? template, IReadOnlyDictionary<string, object?> variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var trimmed = template.Trim();

        // Full object replacement: "{{varName}}" → return the object directly (e.g. a List from SQL)
        var fullMatch = Regex.Match(trimmed, @"^\{\{(\w+)\}\}$");
        if (fullMatch.Success && variables.TryGetValue(fullMatch.Groups[1].Value, out var value))
        {
            return value;
        }

        // Partial string replacement: embed variables as their string representation
        return Pattern.Replace(template, m =>
            variables.TryGetValue(m.Groups[1].Value, out var v)
                ? v?.ToString() ?? string.Empty
                : m.Value);
    }

    /// <summary>
    /// Always returns a string, substituting every {{varName}} with its string representation.
    /// Used when a string result is always expected (e.g. SQL query text before parameterization).
    /// </summary>
    public static string InterpolateString(string? template, IReadOnlyDictionary<string, object?> variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return Pattern.Replace(template, m =>
            variables.TryGetValue(m.Groups[1].Value, out var v)
                ? v?.ToString() ?? string.Empty
                : m.Value);
    }
}
