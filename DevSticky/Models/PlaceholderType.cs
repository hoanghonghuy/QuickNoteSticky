namespace DevSticky.Models;

/// <summary>
/// Defines the type of placeholder in a template
/// </summary>
public enum PlaceholderType
{
    /// <summary>
    /// Plain text placeholder that requires user input
    /// </summary>
    Text,

    /// <summary>
    /// Date placeholder that auto-fills with current date
    /// </summary>
    Date,

    /// <summary>
    /// DateTime placeholder that auto-fills with current date and time
    /// </summary>
    DateTime,

    /// <summary>
    /// User placeholder that auto-fills with configured user information
    /// </summary>
    User,

    /// <summary>
    /// Custom placeholder with user-defined behavior
    /// </summary>
    Custom
}
