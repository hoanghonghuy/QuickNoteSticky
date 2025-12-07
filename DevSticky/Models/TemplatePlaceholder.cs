namespace DevSticky.Models;

/// <summary>
/// Represents a placeholder within a note template
/// Uses syntax {{name}} (e.g., {{date}}, {{author}}, {{custom:title}})
/// </summary>
public class TemplatePlaceholder
{
    /// <summary>
    /// The name/identifier of the placeholder (e.g., "date", "author", "title")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The display name shown to users when filling in the placeholder
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The type of placeholder determining how it's filled
    /// </summary>
    public PlaceholderType Type { get; set; } = PlaceholderType.Text;

    /// <summary>
    /// The default value to use if no replacement is provided
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;
}
