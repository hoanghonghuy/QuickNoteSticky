namespace DevSticky.Helpers;

/// <summary>
/// Helper class for GUID operations and validation.
/// Provides utilities for generating, parsing, and validating GUIDs.
/// </summary>
public static class GuidHelper
{
    /// <summary>
    /// Generates a new GUID.
    /// </summary>
    /// <returns>A new GUID</returns>
    public static Guid NewGuid()
    {
        return Guid.NewGuid();
    }

    /// <summary>
    /// Checks if a GUID is empty (all zeros).
    /// </summary>
    /// <param name="guid">The GUID to check</param>
    /// <returns>True if the GUID is empty; otherwise false</returns>
    public static bool IsEmpty(Guid guid)
    {
        return guid == Guid.Empty;
    }

    /// <summary>
    /// Checks if a GUID is not empty (not all zeros).
    /// </summary>
    /// <param name="guid">The GUID to check</param>
    /// <returns>True if the GUID is not empty; otherwise false</returns>
    public static bool IsNotEmpty(Guid guid)
    {
        return guid != Guid.Empty;
    }

    /// <summary>
    /// Tries to parse a string as a GUID.
    /// </summary>
    /// <param name="value">The string to parse</param>
    /// <param name="result">The parsed GUID if successful</param>
    /// <returns>True if parsing was successful; otherwise false</returns>
    public static bool TryParse(string? value, out Guid result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = Guid.Empty;
            return false;
        }

        return Guid.TryParse(value, out result);
    }

    /// <summary>
    /// Parses a string as a GUID, returning Guid.Empty if parsing fails.
    /// </summary>
    /// <param name="value">The string to parse</param>
    /// <returns>The parsed GUID or Guid.Empty if parsing fails</returns>
    public static Guid ParseOrEmpty(string? value)
    {
        return TryParse(value, out var result) ? result : Guid.Empty;
    }

    /// <summary>
    /// Validates if a string is a valid GUID format.
    /// </summary>
    /// <param name="value">The string to validate</param>
    /// <returns>True if the string is a valid GUID; otherwise false</returns>
    public static bool IsValidGuid(string? value)
    {
        return TryParse(value, out _);
    }

    /// <summary>
    /// Converts a GUID to a short string representation (first 8 characters).
    /// Useful for display purposes or logging.
    /// </summary>
    /// <param name="guid">The GUID to convert</param>
    /// <returns>A short string representation of the GUID</returns>
    public static string ToShortString(Guid guid)
    {
        return guid.ToString()[..8];
    }

    /// <summary>
    /// Converts a GUID to uppercase string format.
    /// </summary>
    /// <param name="guid">The GUID to convert</param>
    /// <returns>The GUID as an uppercase string</returns>
    public static string ToUpperString(Guid guid)
    {
        return guid.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// Converts a GUID to lowercase string format.
    /// </summary>
    /// <param name="guid">The GUID to convert</param>
    /// <returns>The GUID as a lowercase string</returns>
    public static string ToLowerString(Guid guid)
    {
        return guid.ToString().ToLowerInvariant();
    }
}
