using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for searching within note content
/// </summary>
public class SearchService : ISearchService
{
    /// <summary>
    /// Finds all non-overlapping matches of searchTerm in content.
    /// Returns empty list if content or searchTerm is null/empty.
    /// </summary>
    public IReadOnlyList<SearchMatch> FindMatches(string content, string searchTerm)
    {
        var matches = new List<SearchMatch>();
        
        if (StringHelper.IsNullOrEmpty(content) || StringHelper.IsNullOrEmpty(searchTerm))
            return matches;

        int index = 0;
        while ((index = content.IndexOf(searchTerm, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            matches.Add(new SearchMatch(index, searchTerm.Length));
            index += searchTerm.Length; // Move past this match to avoid overlapping
        }

        return matches;
    }

    /// <summary>
    /// Gets the next match index with wrap-around.
    /// Returns 0 if totalMatches is 0.
    /// </summary>
    public int GetNextMatchIndex(int currentIndex, int totalMatches)
    {
        if (totalMatches <= 0)
            return 0;
            
        return (currentIndex + 1) % totalMatches;
    }

    /// <summary>
    /// Gets the previous match index with wrap-around.
    /// Returns 0 if totalMatches is 0.
    /// </summary>
    public int GetPreviousMatchIndex(int currentIndex, int totalMatches)
    {
        if (totalMatches <= 0)
            return 0;
            
        return (currentIndex - 1 + totalMatches) % totalMatches;
    }
}
