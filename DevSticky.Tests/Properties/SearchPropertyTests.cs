using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for SearchService
/// </summary>
public class SearchPropertyTests
{
    private readonly SearchService _search = new();

    /// <summary>
    /// **Feature: devsticky, Property 10: Search Match Completeness**
    /// **Validates: Requirements 10.2**
    /// For any content string C and search term T, the search function SHALL return 
    /// all non-overlapping occurrences of T in C, with correct start positions.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FindMatches_ShouldFindAllOccurrences()
    {
        var gen = from term in Gen.Elements("test", "abc", "xyz", "foo")
                  from count in Gen.Choose(0, 5)
                  from separator in Gen.Elements(" ", "-", "_", ".")
                  let content = string.Join(separator, Enumerable.Repeat(term, count))
                  select (content, term, count);

        return Prop.ForAll(
            Arb.From(gen),
            data =>
            {
                var matches = _search.FindMatches(data.content, data.term);
                
                // Should find exactly 'count' matches
                if (data.count == 0)
                    return matches.Count == 0;
                    
                return matches.Count == data.count &&
                       matches.All(m => data.content.Substring(m.StartIndex, m.Length)
                           .Equals(data.term, StringComparison.OrdinalIgnoreCase));
            });
    }

    /// <summary>
    /// **Feature: devsticky, Property 11: Search Navigation Wrap-Around**
    /// **Validates: Requirements 10.3, 10.4**
    /// For any current match index I and total matches N (where N > 0), 
    /// next match SHALL be (I + 1) % N, and previous match SHALL be (I - 1 + N) % N.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Navigation_ShouldWrapAround()
    {
        var gen = from total in Gen.Choose(1, 100)
                  from current in Gen.Choose(0, total - 1)
                  select (current, total);

        return Prop.ForAll(
            Arb.From(gen),
            data =>
            {
                var next = _search.GetNextMatchIndex(data.current, data.total);
                var prev = _search.GetPreviousMatchIndex(data.current, data.total);
                
                var expectedNext = (data.current + 1) % data.total;
                var expectedPrev = (data.current - 1 + data.total) % data.total;
                
                return next == expectedNext && prev == expectedPrev;
            });
    }

    [Property(MaxTest = 100)]
    public Property NextFromLast_ShouldWrapToFirst()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100)),
            (int total) =>
            {
                var next = _search.GetNextMatchIndex(total - 1, total);
                return next == 0;
            });
    }

    [Property(MaxTest = 100)]
    public Property PrevFromFirst_ShouldWrapToLast()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100)),
            (int total) =>
            {
                var prev = _search.GetPreviousMatchIndex(0, total);
                return prev == total - 1;
            });
    }

    [Property(MaxTest = 100)]
    public Property FindMatches_ShouldReturnNonOverlapping()
    {
        // Test with overlapping potential: "aaa" searching for "aa"
        var gen = from baseChar in Gen.Elements('a', 'b', 'x')
                  from repeatCount in Gen.Choose(2, 10)
                  let content = new string(baseChar, repeatCount)
                  let term = new string(baseChar, 2)
                  select (content, term, repeatCount);

        return Prop.ForAll(
            Arb.From(gen),
            data =>
            {
                var matches = _search.FindMatches(data.content, data.term);
                
                // Verify no overlapping
                for (int i = 1; i < matches.Count; i++)
                {
                    if (matches[i].StartIndex < matches[i - 1].StartIndex + matches[i - 1].Length)
                        return false;
                }
                return true;
            });
    }
}
