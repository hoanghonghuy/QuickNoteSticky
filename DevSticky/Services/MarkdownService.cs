using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DevSticky.Interfaces;
using DevSticky.Models;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Renderers;
using Markdig.Syntax;

namespace DevSticky.Services;

/// <summary>
/// Service for rendering markdown content to HTML and extracting note links
/// </summary>
public partial class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _defaultPipeline;
    private readonly string _appDataPath;

    // Regex pattern for note links: [[note-id|display-text]] or [[note-id]]
    [GeneratedRegex(@"\[\[([a-fA-F0-9\-]{36})(?:\|([^\]]+))?\]\]", RegexOptions.Compiled)]
    private static partial Regex NoteLinkRegex();

    public MarkdownService() : this(GetDefaultAppDataPath())
    {
    }

    public MarkdownService(string appDataPath)
    {
        _appDataPath = appDataPath;
        _defaultPipeline = CreatePipeline(new MarkdownOptions());
    }

    /// <inheritdoc />
    public string RenderToHtml(string markdown)
    {
        return RenderToHtml(markdown, new MarkdownOptions());
    }

    /// <inheritdoc />
    public string RenderToHtml(string markdown, MarkdownOptions options)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        // Convert note links [[guid|text]] to clickable links before rendering
        var processedMarkdown = ConvertNoteLinksToHtml(markdown);

        var pipeline = CreatePipeline(options);
        var html = Markdown.ToHtml(processedMarkdown, pipeline);

        // Wrap in styled container
        return WrapInHtmlDocument(html, options);
    }

    /// <summary>
    /// Convert note links in format [[guid|text]] or [[guid]] to HTML anchor tags
    /// </summary>
    private static string ConvertNoteLinksToHtml(string markdown)
    {
        return NoteLinkRegex().Replace(markdown, match =>
        {
            var noteId = match.Groups[1].Value;
            var displayText = match.Groups[2].Success ? match.Groups[2].Value : noteId;
            return $"<a href=\"devsticky://note/{noteId}\" class=\"note-link\">{displayText}</a>";
        });
    }


    /// <inheritdoc />
    public Task<byte[]> RenderToPdfAsync(string markdown)
    {
        // PDF rendering is a placeholder - would require additional library like iTextSharp or PdfSharp
        // For now, return HTML as bytes which can be used with a PDF converter
        var html = RenderToHtml(markdown);
        return Task.FromResult(Encoding.UTF8.GetBytes(html));
    }

    /// <inheritdoc />
    public IReadOnlyList<NoteLink> ExtractNoteLinks(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return Array.Empty<NoteLink>();

        var links = new List<NoteLink>();
        var matches = NoteLinkRegex().Matches(markdown);

        foreach (Match match in matches)
        {
            if (Guid.TryParse(match.Groups[1].Value, out var targetNoteId))
            {
                var displayText = match.Groups[2].Success 
                    ? match.Groups[2].Value 
                    : match.Groups[1].Value;

                links.Add(new NoteLink
                {
                    TargetNoteId = targetNoteId,
                    DisplayText = displayText,
                    StartPosition = match.Index,
                    Length = match.Length,
                    IsBroken = false // Will be determined by the caller
                });
            }
        }

        return links;
    }

    /// <inheritdoc />
    public string ResolveImagePath(string relativePath, string basePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return string.Empty;

        // If already absolute, return as-is
        if (Path.IsPathRooted(relativePath))
            return relativePath;

        // Use provided base path or default app data path
        var effectiveBasePath = string.IsNullOrEmpty(basePath) ? _appDataPath : basePath;

        // Resolve the path
        var resolvedPath = Path.GetFullPath(Path.Combine(effectiveBasePath, relativePath));

        // Security check: ensure resolved path is within the base path
        if (!resolvedPath.StartsWith(effectiveBasePath, StringComparison.OrdinalIgnoreCase))
        {
            // Path traversal attempt - return empty or throw
            return string.Empty;
        }

        return resolvedPath;
    }


    private static MarkdownPipeline CreatePipeline(MarkdownOptions options)
    {
        var builder = new MarkdownPipelineBuilder();

        if (options.EnableTables)
        {
            builder.UseAdvancedExtensions();
        }

        if (options.EnableTaskLists)
        {
            builder.UseTaskLists();
        }

        // Add other common extensions
        builder.UseAutoLinks();
        builder.UseEmphasisExtras();

        return builder.Build();
    }

    private string WrapInHtmlDocument(string bodyHtml, MarkdownOptions options)
    {
        var isDark = options.CurrentTheme == Theme.Dark;
        var css = GetStylesheet(isDark);
        
        // Get Highlight.js resources for syntax highlighting
        var highlightJs = Resources.HighlightJsResources.HighlightJs;
        var highlightCss = Resources.HighlightJsResources.GetCssForTheme(isDark);

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>{css}</style>
    <style>{highlightCss}</style>
</head>
<body class=""markdown-body"">
{bodyHtml}
<script>{highlightJs}</script>
<script>hljs.highlightAll();</script>
</body>
</html>";
    }

    private static string GetStylesheet(bool isDark)
    {
        var bgColor = isDark ? "#1e1e1e" : "#ffffff";
        var textColor = isDark ? "#d4d4d4" : "#24292e";
        var codeBackground = isDark ? "#2d2d2d" : "#f6f8fa";
        var borderColor = isDark ? "#444444" : "#e1e4e8";
        var linkColor = isDark ? "#58a6ff" : "#0366d6";

        return $@"
            body {{
                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
                font-size: 14px;
                line-height: 1.6;
                color: {textColor};
                background-color: {bgColor};
                padding: 16px;
                margin: 0;
            }}
            h1, h2, h3, h4, h5, h6 {{
                margin-top: 24px;
                margin-bottom: 16px;
                font-weight: 600;
                line-height: 1.25;
            }}
            h1 {{ font-size: 2em; border-bottom: 1px solid {borderColor}; padding-bottom: 0.3em; }}
            h2 {{ font-size: 1.5em; border-bottom: 1px solid {borderColor}; padding-bottom: 0.3em; }}
            h3 {{ font-size: 1.25em; }}
            code {{
                background-color: {codeBackground};
                padding: 0.2em 0.4em;
                border-radius: 3px;
                font-family: 'Consolas', 'Monaco', monospace;
                font-size: 85%;
            }}
            pre {{
                background-color: {codeBackground};
                padding: 16px;
                overflow: auto;
                border-radius: 6px;
                margin: 16px 0;
            }}
            pre code {{
                background-color: transparent;
                padding: 0;
                font-size: 13px;
                line-height: 1.5;
            }}
            /* Override highlight.js background to match theme */
            pre code.hljs {{
                background-color: transparent;
                padding: 0;
            }}
            blockquote {{
                padding: 0 1em;
                color: {(isDark ? "#8b949e" : "#6a737d")};
                border-left: 0.25em solid {borderColor};
                margin: 0 0 16px 0;
            }}
            table {{
                border-collapse: collapse;
                width: 100%;
                margin-bottom: 16px;
            }}
            th, td {{
                padding: 6px 13px;
                border: 1px solid {borderColor};
            }}
            th {{
                font-weight: 600;
                background-color: {(isDark ? "#2d2d2d" : "#f6f8fa")};
            }}
            a {{
                color: {linkColor};
                text-decoration: none;
            }}
            a:hover {{
                text-decoration: underline;
            }}
            a.note-link {{
                color: {(isDark ? "#a78bfa" : "#7c3aed")};
                background-color: {(isDark ? "rgba(167, 139, 250, 0.1)" : "rgba(124, 58, 237, 0.1)")};
                padding: 0 4px;
                border-radius: 3px;
            }}
            a.note-link:hover {{
                background-color: {(isDark ? "rgba(167, 139, 250, 0.2)" : "rgba(124, 58, 237, 0.2)")};
            }}
            ul, ol {{
                padding-left: 2em;
                margin-bottom: 16px;
            }}
            li {{
                margin-bottom: 0.25em;
            }}
            input[type='checkbox'] {{
                margin-right: 0.5em;
            }}
            img {{
                max-width: 100%;
                height: auto;
            }}
        ";
    }

    private static string GetDefaultAppDataPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DevSticky");
    }
}
