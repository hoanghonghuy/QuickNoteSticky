using System.Text.RegularExpressions;

namespace DevSticky.Helpers;

/// <summary>
/// Helper class for common validation operations.
/// Provides utilities for validating various data types and formats.
/// </summary>
public static partial class ValidationHelper
{
    /// <summary>
    /// Regex pattern for validating email addresses.
    /// </summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    /// <summary>
    /// Regex pattern for validating URLs.
    /// </summary>
    [GeneratedRegex(@"^https?://[^\s/$.?#].[^\s]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    /// <summary>
    /// Regex pattern for validating hex color codes (#RGB or #RRGGBB).
    /// </summary>
    [GeneratedRegex(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$", RegexOptions.Compiled)]
    private static partial Regex HexColorRegex();

    /// <summary>
    /// Validates if a string is a valid email address.
    /// </summary>
    /// <param name="email">The email address to validate</param>
    /// <returns>True if the email is valid; otherwise false</returns>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return EmailRegex().IsMatch(email);
    }

    /// <summary>
    /// Validates if a string is a valid URL.
    /// </summary>
    /// <param name="url">The URL to validate</param>
    /// <returns>True if the URL is valid; otherwise false</returns>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return UrlRegex().IsMatch(url) && Uri.TryCreate(url, UriKind.Absolute, out _);
    }

    /// <summary>
    /// Validates if a string is a valid hex color code.
    /// </summary>
    /// <param name="color">The color code to validate</param>
    /// <returns>True if the color code is valid; otherwise false</returns>
    public static bool IsValidHexColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return false;

        return HexColorRegex().IsMatch(color);
    }

    /// <summary>
    /// Validates if a value is within a specified range.
    /// </summary>
    /// <typeparam name="T">The type of value to validate</typeparam>
    /// <param name="value">The value to validate</param>
    /// <param name="min">The minimum allowed value</param>
    /// <param name="max">The maximum allowed value</param>
    /// <returns>True if the value is within range; otherwise false</returns>
    public static bool IsInRange<T>(T value, T min, T max) where T : IComparable<T>
    {
        return value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
    }

    /// <summary>
    /// Validates if a string length is within a specified range.
    /// </summary>
    /// <param name="value">The string to validate</param>
    /// <param name="minLength">The minimum allowed length</param>
    /// <param name="maxLength">The maximum allowed length</param>
    /// <returns>True if the string length is within range; otherwise false</returns>
    public static bool IsLengthInRange(string? value, int minLength, int maxLength)
    {
        if (value == null)
            return minLength == 0;

        return value.Length >= minLength && value.Length <= maxLength;
    }

    /// <summary>
    /// Validates if a collection count is within a specified range.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection</typeparam>
    /// <param name="collection">The collection to validate</param>
    /// <param name="minCount">The minimum allowed count</param>
    /// <param name="maxCount">The maximum allowed count</param>
    /// <returns>True if the collection count is within range; otherwise false</returns>
    public static bool IsCountInRange<T>(IEnumerable<T>? collection, int minCount, int maxCount)
    {
        if (collection == null)
            return minCount == 0;

        var count = collection.Count();
        return count >= minCount && count <= maxCount;
    }

    /// <summary>
    /// Validates if a string contains only alphanumeric characters.
    /// </summary>
    /// <param name="value">The string to validate</param>
    /// <returns>True if the string contains only alphanumeric characters; otherwise false</returns>
    public static bool IsAlphanumeric(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.All(char.IsLetterOrDigit);
    }

    /// <summary>
    /// Validates if a string contains only letters.
    /// </summary>
    /// <param name="value">The string to validate</param>
    /// <returns>True if the string contains only letters; otherwise false</returns>
    public static bool IsAlpha(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.All(char.IsLetter);
    }

    /// <summary>
    /// Validates if a string contains only digits.
    /// </summary>
    /// <param name="value">The string to validate</param>
    /// <returns>True if the string contains only digits; otherwise false</returns>
    public static bool IsNumeric(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.All(char.IsDigit);
    }

    /// <summary>
    /// Validates if a file path has a valid format.
    /// </summary>
    /// <param name="path">The file path to validate</param>
    /// <returns>True if the path format is valid; otherwise false</returns>
    public static bool IsValidFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var fileInfo = new System.IO.FileInfo(path);
            return !string.IsNullOrEmpty(fileInfo.Name);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates if a directory path has a valid format.
    /// </summary>
    /// <param name="path">The directory path to validate</param>
    /// <returns>True if the path format is valid; otherwise false</returns>
    public static bool IsValidDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var directoryInfo = new System.IO.DirectoryInfo(path);
            return !string.IsNullOrEmpty(directoryInfo.Name);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates if a value is not null.
    /// </summary>
    /// <typeparam name="T">The type of value to validate</typeparam>
    /// <param name="value">The value to validate</param>
    /// <returns>True if the value is not null; otherwise false</returns>
    public static bool IsNotNull<T>(T? value) where T : class
    {
        return value != null;
    }

    /// <summary>
    /// Validates if a nullable value has a value.
    /// </summary>
    /// <typeparam name="T">The type of value to validate</typeparam>
    /// <param name="value">The nullable value to validate</param>
    /// <returns>True if the value has a value; otherwise false</returns>
    public static bool HasValue<T>(T? value) where T : struct
    {
        return value.HasValue;
    }

    /// <summary>
    /// Validates multiple conditions and returns true only if all are true.
    /// </summary>
    /// <param name="conditions">The conditions to validate</param>
    /// <returns>True if all conditions are true; otherwise false</returns>
    public static bool All(params bool[] conditions)
    {
        return conditions.All(c => c);
    }

    /// <summary>
    /// Validates multiple conditions and returns true if any is true.
    /// </summary>
    /// <param name="conditions">The conditions to validate</param>
    /// <returns>True if any condition is true; otherwise false</returns>
    public static bool Any(params bool[] conditions)
    {
        return conditions.Any(c => c);
    }
}
