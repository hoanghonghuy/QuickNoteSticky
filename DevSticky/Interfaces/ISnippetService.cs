using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Conflict resolution strategy for snippet import
/// </summary>
public enum ConflictResolution
{
    /// <summary>
    /// Skip importing snippets that conflict with existing ones
    /// </summary>
    Skip,

    /// <summary>
    /// Replace existing snippets with imported ones
    /// </summary>
    Replace,

    /// <summary>
    /// Keep both by renaming the imported snippet
    /// </summary>
    KeepBoth
}

/// <summary>
/// Service for managing code snippets (CRUD operations, search, import/export)
/// </summary>
public interface ISnippetService
{
    /// <summary>
    /// Gets all snippets
    /// </summary>
    Task<IReadOnlyList<Snippet>> GetAllSnippetsAsync();

    /// <summary>
    /// Gets a snippet by its ID
    /// </summary>
    Task<Snippet?> GetSnippetByIdAsync(Guid id);

    /// <summary>
    /// Searches snippets by query across name, description, content, and tags
    /// </summary>
    Task<IReadOnlyList<Snippet>> SearchSnippetsAsync(string query);

    /// <summary>
    /// Creates a new snippet
    /// </summary>
    Task<Snippet> CreateSnippetAsync(Snippet snippet);

    /// <summary>
    /// Updates an existing snippet
    /// </summary>
    Task UpdateSnippetAsync(Snippet snippet);

    /// <summary>
    /// Deletes a snippet by its ID
    /// </summary>
    Task DeleteSnippetAsync(Guid id);

    /// <summary>
    /// Expands a snippet by replacing placeholders with provided values
    /// </summary>
    /// <param name="snippet">The snippet to expand</param>
    /// <param name="variables">Optional dictionary of placeholder name to value mappings</param>
    /// <returns>The expanded snippet content with placeholders replaced</returns>
    Task<string> ExpandSnippetAsync(Snippet snippet, Dictionary<string, string>? variables = null);

    /// <summary>
    /// Exports all snippets to a JSON file
    /// </summary>
    Task ExportSnippetsAsync(string filePath);

    /// <summary>
    /// Imports snippets from a JSON file with conflict resolution
    /// </summary>
    Task ImportSnippetsAsync(string filePath, ConflictResolution resolution);

    /// <summary>
    /// Parses placeholder syntax from content and returns list of placeholders
    /// </summary>
    IReadOnlyList<SnippetPlaceholder> ParsePlaceholders(string content);
}
