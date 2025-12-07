namespace DevSticky.Models;

/// <summary>
/// Represents a search match in note content
/// </summary>
public class SearchMatch
{
    public int StartIndex { get; set; }
    public int Length { get; set; }
    
    public SearchMatch(int startIndex, int length)
    {
        StartIndex = startIndex;
        Length = length;
    }
}
