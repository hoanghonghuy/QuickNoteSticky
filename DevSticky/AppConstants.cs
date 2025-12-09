namespace DevSticky;

/// <summary>
/// Centralized constants for the DevSticky application.
/// Consolidates magic strings and numbers for maintainability.
/// </summary>
public static class AppConstants
{
    #region Storage

    /// <summary>
    /// Application data folder name in AppData/Roaming
    /// </summary>
    public const string AppDataFolderName = "DevSticky";

    /// <summary>
    /// File name for storing notes data
    /// </summary>
    public const string NotesFileName = "notes.json";

    /// <summary>
    /// File name for storing snippets data
    /// </summary>
    public const string SnippetsFileName = "snippets.json";

    /// <summary>
    /// File name for storing templates data
    /// </summary>
    public const string TemplatesFileName = "templates.json";

    /// <summary>
    /// File name for storing application settings
    /// </summary>
    public const string SettingsFileName = "settings.json";

    #endregion

    #region Debounce Delays

    /// <summary>
    /// Debounce delay for auto-save operations in milliseconds
    /// </summary>
    public const int AutoSaveDebounceMs = 500;

    /// <summary>
    /// Debounce delay for markdown preview updates in milliseconds
    /// </summary>
    public const int MarkdownPreviewDebounceMs = 300;

    /// <summary>
    /// Debounce delay for search operations in milliseconds
    /// </summary>
    public const int SearchDebounceMs = 200;

    #endregion

    #region UI Limits

    /// <summary>
    /// Maximum length for note titles
    /// </summary>
    public const int MaxTitleLength = 50;

    /// <summary>
    /// Maximum length for tag names
    /// </summary>
    public const int MaxTagNameLength = 20;

    /// <summary>
    /// Maximum length for group names
    /// </summary>
    public const int MaxGroupNameLength = 30;

    /// <summary>
    /// Maximum length for snippet names
    /// </summary>
    public const int MaxSnippetNameLength = 50;

    /// <summary>
    /// Maximum length for template names
    /// </summary>
    public const int MaxTemplateNameLength = 50;

    #endregion

    #region Debounce Keys

    /// <summary>
    /// Debounce key for markdown preview updates
    /// </summary>
    public const string PreviewDebounceKey = "MarkdownPreview";

    /// <summary>
    /// Debounce key prefix for note save operations
    /// </summary>
    public const string SaveDebounceKeyPrefix = "save_";

    #endregion
}
