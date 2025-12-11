using System.IO;
using System.Linq;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Views;

namespace DevSticky.Services;

/// <summary>
/// Service for exporting notes to various formats (Requirements 1.1, 4.9)
/// Implements Single Responsibility Principle by extracting export logic
/// </summary>
public class ExportService : IExportService
{
    private readonly IMarkdownService _markdownService;
    private readonly IThemeService _themeService;
    private readonly IDialogService _dialogService;

    public ExportService(
        IMarkdownService markdownService,
        IThemeService themeService,
        IDialogService dialogService)
    {
        _markdownService = markdownService ?? throw new ArgumentNullException(nameof(markdownService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    /// <summary>
    /// Export note content as HTML (Requirements 4.9)
    /// </summary>
    public async Task<bool> ExportAsHtmlAsync(string content, string title)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = L.Get("FilterHtmlFiles"),
            DefaultExt = ".html",
            FileName = SanitizeFileName(title, "html")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var options = new MarkdownOptions
                {
                    EnableSyntaxHighlighting = true,
                    EnableTables = true,
                    EnableTaskLists = true,
                    CurrentTheme = _themeService.CurrentTheme
                };

                var html = _markdownService.RenderToHtml(content, options);
                await File.WriteAllTextAsync(dialog.FileName, html).ConfigureAwait(false);
                
                await _dialogService.ShowSuccessAsync(
                    L.Get("ExportComplete"), 
                    L.Get("HtmlExportedTo", dialog.FileName));
                
                return true;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync(
                    L.Get("ExportFailed"), 
                    L.Get("FailedToExportHtml", ex.Message));
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Export note content as PDF (Requirements 4.9)
    /// </summary>
    public async Task<bool> ExportAsPdfAsync(string content, string title)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = L.Get("FilterPdfFiles"),
            DefaultExt = ".pdf",
            FileName = SanitizeFileName(title, "pdf")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                // Note: RenderToPdfAsync currently returns HTML as bytes
                // A full PDF implementation would require a PDF library like iTextSharp or PdfSharp
                var pdfBytes = await _markdownService.RenderToPdfAsync(content);
                
                // For now, save as HTML with .pdf extension (placeholder)
                // In a production app, you'd use a proper PDF library
                await File.WriteAllBytesAsync(dialog.FileName, pdfBytes);
                
                await _dialogService.ShowInfoAsync(
                    L.Get("ExportNote"), 
                    L.Get("PdfExportPlaceholder"));
                
                return true;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync(
                    L.Get("ExportFailed"), 
                    L.Get("FailedToExportPdf", ex.Message));
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Export note content as Markdown (Requirements 4.9)
    /// </summary>
    public async Task<bool> ExportAsMarkdownAsync(string content, string title)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = L.Get("FilterMarkdownFiles"),
            DefaultExt = ".md",
            FileName = SanitizeFileName(title, "md")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await File.WriteAllTextAsync(dialog.FileName, content).ConfigureAwait(false);
                
                await _dialogService.ShowSuccessAsync(
                    L.Get("ExportComplete"), 
                    L.Get("MarkdownExportedTo", dialog.FileName));
                
                return true;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync(
                    L.Get("ExportFailed"), 
                    L.Get("FailedToExportMarkdown", ex.Message));
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Generate a safe filename for export based on note title
    /// </summary>
    private static string SanitizeFileName(string title, string extension)
    {
        // Sanitize filename
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(title.Where(c => !invalidChars.Contains(c)).ToArray());
        
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = L.Get("DefaultExportFileName");
        
        return $"{sanitized}.{extension}";
    }
}
