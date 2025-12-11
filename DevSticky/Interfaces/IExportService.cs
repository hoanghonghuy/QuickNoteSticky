using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for exporting notes to various formats (Requirements 1.1, 4.9)
/// Extracted from NoteWindow to follow Single Responsibility Principle
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export note content as HTML
    /// </summary>
    /// <param name="content">The note content to export</param>
    /// <param name="title">The note title for filename generation</param>
    /// <returns>True if export was successful</returns>
    Task<bool> ExportAsHtmlAsync(string content, string title);
    
    /// <summary>
    /// Export note content as PDF
    /// </summary>
    /// <param name="content">The note content to export</param>
    /// <param name="title">The note title for filename generation</param>
    /// <returns>True if export was successful</returns>
    Task<bool> ExportAsPdfAsync(string content, string title);
    
    /// <summary>
    /// Export note content as Markdown
    /// </summary>
    /// <param name="content">The note content to export</param>
    /// <param name="title">The note title for filename generation</param>
    /// <returns>True if export was successful</returns>
    Task<bool> ExportAsMarkdownAsync(string content, string title);
}
