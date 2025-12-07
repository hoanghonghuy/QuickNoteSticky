using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing note templates (CRUD operations, template application, import/export)
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Gets all templates including built-in and user-created
    /// </summary>
    Task<IReadOnlyList<NoteTemplate>> GetAllTemplatesAsync();

    /// <summary>
    /// Gets a template by its ID
    /// </summary>
    Task<NoteTemplate?> GetTemplateByIdAsync(Guid id);

    /// <summary>
    /// Creates a new user template
    /// </summary>
    Task<NoteTemplate> CreateTemplateAsync(NoteTemplate template);

    /// <summary>
    /// Updates an existing template (built-in templates cannot be updated)
    /// </summary>
    Task UpdateTemplateAsync(NoteTemplate template);

    /// <summary>
    /// Deletes a template by its ID (built-in templates cannot be deleted)
    /// </summary>
    Task DeleteTemplateAsync(Guid id);

    /// <summary>
    /// Creates a new note from a template, replacing placeholders with provided values
    /// </summary>
    /// <param name="templateId">The ID of the template to use</param>
    /// <param name="variables">Optional dictionary of placeholder name to value mappings</param>
    /// <returns>A new Note with template content and placeholders replaced</returns>
    Task<Note> CreateNoteFromTemplateAsync(Guid templateId, Dictionary<string, string>? variables = null);

    /// <summary>
    /// Exports all user templates to a JSON file
    /// </summary>
    Task ExportTemplatesAsync(string filePath);

    /// <summary>
    /// Imports templates from a JSON file, merging with existing templates
    /// </summary>
    Task ImportTemplatesAsync(string filePath);

    /// <summary>
    /// Gets the list of built-in templates
    /// </summary>
    IReadOnlyList<NoteTemplate> GetBuiltInTemplates();

    /// <summary>
    /// Creates a template from an existing note
    /// </summary>
    /// <param name="note">The note to convert to a template</param>
    /// <param name="templateName">Name for the new template</param>
    /// <param name="description">Description for the new template</param>
    /// <param name="category">Category for the new template</param>
    /// <returns>The created template</returns>
    Task<NoteTemplate> CreateTemplateFromNoteAsync(Note note, string templateName, string description, string category);

    /// <summary>
    /// Parses placeholder syntax from content and returns list of placeholders
    /// </summary>
    IReadOnlyList<TemplatePlaceholder> ParsePlaceholders(string content);

    /// <summary>
    /// Replaces placeholders in content with provided values
    /// </summary>
    /// <param name="content">The content with placeholders</param>
    /// <param name="variables">Dictionary of placeholder name to value mappings</param>
    /// <returns>Content with placeholders replaced</returns>
    string ReplacePlaceholders(string content, Dictionary<string, string>? variables = null);
}
