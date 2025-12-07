namespace DevSticky.Models;

/// <summary>
/// Represents a placeholder within a snippet for tab-stop navigation
/// Uses syntax ${index:name} (e.g., ${1:variableName})
/// </summary>
public class SnippetPlaceholder
{
    /// <summary>
    /// The tab-stop index (1-based) for navigation order
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// The name/identifier of the placeholder
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The default value to insert if no replacement is provided
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// The starting position of the placeholder in the content
    /// </summary>
    public int StartPosition { get; set; }

    /// <summary>
    /// The length of the placeholder syntax in the original content
    /// </summary>
    public int Length { get; set; }
}
