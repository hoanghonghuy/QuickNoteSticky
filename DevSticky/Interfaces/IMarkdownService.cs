using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for rendering markdown content to HTML and extracting note links
/// </summary>
public interface IMarkdownService
{
    /// <summary>
    /// Renders markdown content to HTML using default options
    /// </summary>
    /// <param name="markdown">The markdown content to render</param>
    /// <returns>The rendered HTML string</returns>
    string RenderToHtml(string markdown);

    /// <summary>
    /// Renders markdown content to HTML with custom options
    /// </summary>
    /// <param name="markdown">The markdown content to render</param>
    /// <param name="options">Options for configuring the rendering</param>
    /// <returns>The rendered HTML string</returns>
    string RenderToHtml(string markdown, MarkdownOptions options);

    /// <summary>
    /// Renders markdown content to PDF format
    /// </summary>
    /// <param name="markdown">The markdown content to render</param>
    /// <returns>The PDF content as a byte array</returns>
    Task<byte[]> RenderToPdfAsync(string markdown);

    /// <summary>
    /// Extracts all note links from markdown content
    /// Note links are in the format [[note-id|display-text]] or [[note-id]]
    /// </summary>
    /// <param name="markdown">The markdown content to parse</param>
    /// <returns>A list of extracted note links</returns>
    IReadOnlyList<NoteLink> ExtractNoteLinks(string markdown);

    /// <summary>
    /// Resolves a relative image path to an absolute path
    /// </summary>
    /// <param name="relativePath">The relative path from the markdown</param>
    /// <param name="basePath">The base path for resolution</param>
    /// <returns>The resolved absolute path</returns>
    string ResolveImagePath(string relativePath, string basePath);
}
