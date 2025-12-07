namespace DevSticky.Models;

/// <summary>
/// Represents a predefined note template with placeholders
/// </summary>
public class NoteTemplate
{
    /// <summary>
    /// Unique identifier for the template
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name of the template
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what the template is for
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Category for organizing templates (e.g., "Meeting", "Development", "Personal")
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// The template content with placeholder syntax (e.g., {{date}}, {{author}})
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Default language/syntax highlighting for notes created from this template
    /// </summary>
    public string DefaultLanguage { get; set; } = "PlainText";

    /// <summary>
    /// Default tags to apply to notes created from this template
    /// </summary>
    public List<string> DefaultTags { get; set; } = new();

    /// <summary>
    /// List of placeholders defined in the template
    /// </summary>
    public List<TemplatePlaceholder> Placeholders { get; set; } = new();

    /// <summary>
    /// Indicates if this is a built-in template that cannot be deleted
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Date when the template was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
