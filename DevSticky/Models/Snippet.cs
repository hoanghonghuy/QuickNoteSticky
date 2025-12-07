namespace DevSticky.Models;

/// <summary>
/// Represents a reusable code snippet with optional placeholders
/// </summary>
public class Snippet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Language { get; set; } = "PlainText";
    public string Category { get; set; } = "General";
    public List<string> Tags { get; set; } = new();
    public List<SnippetPlaceholder> Placeholders { get; set; } = new();
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
}
