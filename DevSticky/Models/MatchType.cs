namespace DevSticky.Models;

/// <summary>
/// Represents the type of match found during fuzzy search
/// </summary>
public enum MatchType
{
    /// <summary>
    /// Exact string match
    /// </summary>
    Exact,
    
    /// <summary>
    /// Partial string match (substring)
    /// </summary>
    Partial,
    
    /// <summary>
    /// Fuzzy match within Levenshtein distance threshold
    /// </summary>
    Fuzzy
}