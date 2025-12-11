namespace DevSticky.Helpers;

/// <summary>
/// Helper class for common string operations and validation.
/// Provides utilities for string manipulation, validation, and formatting.
/// </summary>
public static class StringHelper
{
    /// <summary>
    /// Checks if a string is null, empty, or contains only whitespace characters.
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <returns>True if the string is null, empty, or whitespace; otherwise false</returns>
    public static bool IsNullOrWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Checks if a string is null or empty.
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <returns>True if the string is null or empty; otherwise false</returns>
    public static bool IsNullOrEmpty(string? value)
    {
        return string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Truncates a string to a maximum length, optionally adding an ellipsis.
    /// </summary>
    /// <param name="value">The string to truncate</param>
    /// <param name="maxLength">The maximum length</param>
    /// <param name="addEllipsis">Whether to add "..." at the end</param>
    /// <returns>The truncated string</returns>
    public static string Truncate(string? value, int maxLength, bool addEllipsis = false)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Length <= maxLength)
            return value;

        if (addEllipsis && maxLength > 3)
        {
            return value[..(maxLength - 3)] + "...";
        }

        return value[..maxLength];
    }

    /// <summary>
    /// Ensures a string is not null by returning an empty string if it is null.
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <returns>The original string or empty string if null</returns>
    public static string EnsureNotNull(string? value)
    {
        return value ?? string.Empty;
    }

    /// <summary>
    /// Normalizes line endings to the specified format.
    /// </summary>
    /// <param name="value">The string to normalize</param>
    /// <param name="lineEnding">The line ending to use (default: Environment.NewLine)</param>
    /// <returns>The string with normalized line endings</returns>
    public static string NormalizeLineEndings(string? value, string? lineEnding = null)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        lineEnding ??= Environment.NewLine;
        
        // Replace all line ending variations with the specified one
        return value
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Replace("\n", lineEnding);
    }

    /// <summary>
    /// Removes all whitespace from a string.
    /// </summary>
    /// <param name="value">The string to process</param>
    /// <returns>The string with all whitespace removed</returns>
    public static string RemoveWhitespace(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    /// <summary>
    /// Capitalizes the first letter of a string.
    /// </summary>
    /// <param name="value">The string to capitalize</param>
    /// <returns>The string with the first letter capitalized</returns>
    public static string Capitalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Length == 1)
            return value.ToUpper();

        return char.ToUpper(value[0]) + value[1..];
    }

    /// <summary>
    /// Converts a string to Title Case (each word capitalized).
    /// </summary>
    /// <param name="value">The string to convert</param>
    /// <returns>The string in Title Case</returns>
    public static string ToTitleCase(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
    }

    /// <summary>
    /// Counts the number of occurrences of a substring in a string.
    /// </summary>
    /// <param name="value">The string to search</param>
    /// <param name="substring">The substring to count</param>
    /// <param name="comparison">The string comparison type</param>
    /// <returns>The number of occurrences</returns>
    public static int CountOccurrences(string? value, string substring, StringComparison comparison = StringComparison.Ordinal)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(substring))
            return 0;

        int count = 0;
        int index = 0;

        while ((index = value.IndexOf(substring, index, comparison)) != -1)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }
}
