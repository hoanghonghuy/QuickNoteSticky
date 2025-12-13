using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for performing fuzzy search operations on notes
/// </summary>
public interface IFuzzySearchService
{
    /// <summary>
    /// Searches notes using fuzzy matching with configurable Levenshtein distance
    /// </summary>
    /// <param name="notes">Collection of notes to search</param>
    /// <param name="query">Search query string</param>
    /// <param name="maxLevenshteinDistance">Maximum allowed Levenshtein distance for fuzzy matches (default: 2)</param>
    /// <returns>Ranked list of search results</returns>
    IReadOnlyList<FuzzySearchResult> Search(
        IEnumerable<Note> notes, 
        string query, 
        int maxLevenshteinDistance = 2);
    
    /// <summary>
    /// Calculates the Levenshtein distance between two strings
    /// </summary>
    /// <param name="source">Source string</param>
    /// <param name="target">Target string</param>
    /// <returns>Levenshtein distance</returns>
    int CalculateLevenshteinDistance(string source, string target);
    
    /// <summary>
    /// Calculates relevance score for a text against a query
    /// </summary>
    /// <param name="text">Text to score</param>
    /// <param name="query">Search query</param>
    /// <returns>Relevance score (higher is better)</returns>
    double CalculateRelevanceScore(string text, string query);
}