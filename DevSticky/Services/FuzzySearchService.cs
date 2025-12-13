using DevSticky.Interfaces;
using DevSticky.Models;
using FuzzySharp;
using System.Text.RegularExpressions;

namespace DevSticky.Services;

/// <summary>
/// Implementation of fuzzy search service using FuzzySharp library
/// </summary>
public class FuzzySearchService : IFuzzySearchService
{
    public IReadOnlyList<FuzzySearchResult> Search(
        IEnumerable<Note> notes, 
        string query, 
        int maxLevenshteinDistance = 2)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<FuzzySearchResult>();

        var results = new List<FuzzySearchResult>();
        var queryLower = query.ToLowerInvariant();

        foreach (var note in notes)
        {
            var searchableText = $"{note.Title} {note.Content}";
            var searchableTextLower = searchableText.ToLowerInvariant();
            
            // Check for exact match first
            if (searchableTextLower.Contains(queryLower))
            {
                var highlights = FindHighlights(searchableText, query, MatchType.Exact);
                var relevanceScore = CalculateRelevanceScore(searchableText, query);
                
                results.Add(new FuzzySearchResult(
                    note, 
                    relevanceScore + 100, // Boost exact matches
                    MatchType.Exact, 
                    highlights));
                continue;
            }

            // Check for partial matches (individual words)
            var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var partialMatches = new List<HighlightRange>();
            var hasPartialMatch = false;

            foreach (var word in queryWords)
            {
                if (searchableTextLower.Contains(word.ToLowerInvariant()))
                {
                    hasPartialMatch = true;
                    var wordHighlights = FindHighlights(searchableText, word, MatchType.Partial);
                    partialMatches.AddRange(wordHighlights);
                }
            }

            if (hasPartialMatch)
            {
                var relevanceScore = CalculateRelevanceScore(searchableText, query);
                results.Add(new FuzzySearchResult(
                    note, 
                    relevanceScore + 50, // Boost partial matches
                    MatchType.Partial, 
                    partialMatches));
                continue;
            }

            // Check for fuzzy matches
            var fuzzyScore = Fuzz.PartialRatio(queryLower, searchableTextLower);
            var levenshteinDistance = CalculateLevenshteinDistance(queryLower, searchableTextLower);
            
            if (levenshteinDistance <= maxLevenshteinDistance || fuzzyScore >= 70)
            {
                var highlights = FindFuzzyHighlights(searchableText, query);
                var relevanceScore = CalculateRelevanceScore(searchableText, query);
                
                results.Add(new FuzzySearchResult(
                    note, 
                    relevanceScore, 
                    MatchType.Fuzzy, 
                    highlights));
            }
        }

        // Sort by relevance score (descending)
        return results.OrderByDescending(r => r.RelevanceScore).ToList();
    }

    public int CalculateLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return target?.Length ?? 0;
        
        if (string.IsNullOrEmpty(target))
            return source.Length;

        var sourceLength = source.Length;
        var targetLength = target.Length;
        var matrix = new int[sourceLength + 1, targetLength + 1];

        // Initialize first row and column
        for (int i = 0; i <= sourceLength; i++)
            matrix[i, 0] = i;
        
        for (int j = 0; j <= targetLength; j++)
            matrix[0, j] = j;

        // Fill the matrix
        for (int i = 1; i <= sourceLength; i++)
        {
            for (int j = 1; j <= targetLength; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                
                matrix[i, j] = Math.Min(
                    Math.Min(
                        matrix[i - 1, j] + 1,     // deletion
                        matrix[i, j - 1] + 1),    // insertion
                    matrix[i - 1, j - 1] + cost  // substitution
                );
            }
        }

        return matrix[sourceLength, targetLength];
    }

    public double CalculateRelevanceScore(string text, string query)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(query))
            return 0;

        var textLower = text.ToLowerInvariant();
        var queryLower = query.ToLowerInvariant();
        
        // Base score from fuzzy matching
        var fuzzyScore = Fuzz.PartialRatio(queryLower, textLower);
        
        // Boost for title matches (assuming first part of text is title)
        var titleEndIndex = Math.Min(text.Length, 100);
        var titlePart = text.Substring(0, titleEndIndex).ToLowerInvariant();
        var titleBoost = titlePart.Contains(queryLower) ? 20 : 0;
        
        // Boost for exact word matches
        var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var exactWordMatches = queryWords.Count(word => 
            textLower.Contains($" {word.ToLowerInvariant()} ") || 
            textLower.StartsWith($"{word.ToLowerInvariant()} ") ||
            textLower.EndsWith($" {word.ToLowerInvariant()}"));
        
        var wordMatchBoost = exactWordMatches * 10;
        
        return fuzzyScore + titleBoost + wordMatchBoost;
    }

    private List<HighlightRange> FindHighlights(string text, string query, MatchType matchType)
    {
        var highlights = new List<HighlightRange>();
        var textLower = text.ToLowerInvariant();
        var queryLower = query.ToLowerInvariant();
        
        var startIndex = 0;
        while (startIndex < text.Length)
        {
            var index = textLower.IndexOf(queryLower, startIndex, StringComparison.OrdinalIgnoreCase);
            if (index == -1)
                break;
                
            highlights.Add(new HighlightRange(index, query.Length));
            startIndex = index + query.Length;
        }
        
        return highlights;
    }

    private List<HighlightRange> FindFuzzyHighlights(string text, string query)
    {
        // For fuzzy matches, we'll highlight the best matching substring
        var highlights = new List<HighlightRange>();
        var textLower = text.ToLowerInvariant();
        var queryLower = query.ToLowerInvariant();
        
        // Find the best matching substring using a sliding window
        var bestScore = 0;
        var bestStart = 0;
        var bestLength = query.Length;
        
        for (int i = 0; i <= text.Length - query.Length; i++)
        {
            var substring = textLower.Substring(i, query.Length);
            var score = Fuzz.Ratio(queryLower, substring);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestStart = i;
                bestLength = query.Length;
            }
        }
        
        if (bestScore >= 60) // Threshold for highlighting fuzzy matches
        {
            highlights.Add(new HighlightRange(bestStart, bestLength));
        }
        
        return highlights;
    }
}