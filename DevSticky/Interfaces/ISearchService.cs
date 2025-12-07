using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for searching within note content
/// </summary>
public interface ISearchService
{
    IReadOnlyList<SearchMatch> FindMatches(string content, string searchTerm);
    int GetNextMatchIndex(int currentIndex, int totalMatches);
    int GetPreviousMatchIndex(int currentIndex, int totalMatches);
}
