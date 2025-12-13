namespace DevSticky.Models;

/// <summary>
/// Represents a range of text to highlight in search results
/// </summary>
public record HighlightRange(int Start, int Length);