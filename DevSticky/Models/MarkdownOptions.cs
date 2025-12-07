namespace DevSticky.Models;

/// <summary>
/// Options for configuring markdown rendering
/// </summary>
public class MarkdownOptions
{
    /// <summary>
    /// Enable syntax highlighting for code blocks
    /// </summary>
    public bool EnableSyntaxHighlighting { get; set; } = true;

    /// <summary>
    /// Enable table rendering
    /// </summary>
    public bool EnableTables { get; set; } = true;

    /// <summary>
    /// Enable task list rendering (checkboxes)
    /// </summary>
    public bool EnableTaskLists { get; set; } = true;

    /// <summary>
    /// Base path for resolving relative image paths
    /// </summary>
    public string BaseImagePath { get; set; } = string.Empty;

    /// <summary>
    /// Current theme for styling the rendered output
    /// </summary>
    public Theme? CurrentTheme { get; set; }
}
