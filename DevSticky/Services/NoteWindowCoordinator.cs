using System.Windows;
using System.Windows.Input;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.ViewModels;
using DevSticky.Views;

namespace DevSticky.Services;

/// <summary>
/// Coordinator for NoteWindow operations (Requirements 1.1, 8.3)
/// Implements Single Responsibility Principle by extracting coordination logic
/// </summary>
public class NoteWindowCoordinator
{
    private readonly IMonitorService _monitorService;
    private readonly IExportService _exportService;
    private readonly ITemplateService _templateService;
    private readonly IDialogService _dialogService;

    public NoteWindowCoordinator(
        IMonitorService monitorService,
        IExportService exportService,
        ITemplateService templateService,
        IDialogService dialogService)
    {
        _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    /// <summary>
    /// Handle moving window to a specific monitor (Requirements 1.1)
    /// </summary>
    public void MoveWindowToMonitor(Window window, string targetDeviceId, NoteViewModel viewModel)
    {
        if (string.IsNullOrEmpty(targetDeviceId))
            return;

        var targetMonitor = _monitorService.GetMonitorById(targetDeviceId);
        if (targetMonitor == null)
            return;

        // Calculate relative position within current monitor
        var currentMonitor = _monitorService.GetMonitorAt(window.Left, window.Top);
        
        if (currentMonitor != null)
        {
            // Calculate relative position and apply to target monitor
            var (relativeX, relativeY) = MonitorBoundsHelper.CalculateRelativePosition(window, currentMonitor);
            MonitorBoundsHelper.ApplyRelativePosition(window, targetMonitor, relativeX, relativeY);
        }
        else
        {
            // Window is off-screen, center on target monitor
            MonitorBoundsHelper.CenterWindowOnMonitor(window, targetMonitor);
        }

        // Ensure window is within bounds
        MonitorBoundsHelper.EnsureWindowInBounds(window, targetMonitor);

        // Update the note's monitor assignment
        viewModel.MonitorDeviceId = targetDeviceId;
    }



    /// <summary>
    /// Handle exporting note as HTML (Requirements 4.9)
    /// </summary>
    public async Task ExportAsHtmlAsync(string content, string title)
    {
        await _exportService.ExportAsHtmlAsync(content, title);
    }

    /// <summary>
    /// Handle exporting note as PDF (Requirements 4.9)
    /// </summary>
    public async Task ExportAsPdfAsync(string content, string title)
    {
        await _exportService.ExportAsPdfAsync(content, title);
    }

    /// <summary>
    /// Handle exporting note as Markdown (Requirements 4.9)
    /// </summary>
    public async Task ExportAsMarkdownAsync(string content, string title)
    {
        await _exportService.ExportAsMarkdownAsync(content, title);
    }

    /// <summary>
    /// Handle saving note as template (Requirements 6.6, 6.7)
    /// </summary>
    public async Task<bool> SaveAsTemplateAsync(Window owner, Note note)
    {
        var dialog = new SaveAsTemplateDialog(note)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() == true && dialog.CreatedTemplate != null)
        {
            await _dialogService.ShowSuccessAsync(
                "Template Saved",
                $"Template '{dialog.CreatedTemplate.Name}' has been saved.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handle keyboard shortcuts (Requirements 1.1)
    /// </summary>
    public bool HandleKeyDown(System.Windows.Input.KeyEventArgs e, NoteViewModel? viewModel, Action<string, string> onSaveSnippet, Action onOpenSnippetBrowser, Action onToggleSearch, Action onClose)
    {
        if (viewModel == null) return false;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.S:
                    viewModel.SaveCommand.Execute(null);
                    return true;
                case Key.F:
                    onToggleSearch();
                    return true;
                case Key.W:
                    onClose();
                    return true;
            }
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            switch (e.Key)
            {
                case Key.F:
                    viewModel.FormatCommand.Execute(null);
                    return true;
                case Key.S:
                    onSaveSnippet(viewModel.Content, viewModel.Language);
                    return true;
                case Key.I:
                    onOpenSnippetBrowser();
                    return true;
            }
        }

        return false;
    }
}
