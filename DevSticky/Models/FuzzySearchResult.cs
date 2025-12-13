namespace DevSticky.Models;

/// <summary>
/// Represents a fuzzy search result with relevance scoring and highlighting
/// </summary>
public record FuzzySearchResult(
    Note Note,
    double RelevanceScore,
    MatchType MatchType,
    IReadOnlyList<HighlightRange> Highlights);