using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevSticky.Handlers;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Resources;
using DevSticky.ViewModels;
using ICSharpCode.AvalonEdit.Highlighting;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DevSticky.Views;

public partial class NoteWindow : Window
{
    private NoteViewModel? _viewModel;
    private bool _isPinned = true;
    private readonly IThemeService _themeService;
    private readonly IMonitorService _monitorService;
    private readonly ISnippetService _snippetService;
    private readonly IDebounceService _debounceService;
    private readonly IMarkdownService _markdownService;
    private readonly ILinkService _linkService;
    private readonly INoteService _noteService;
    private string _currentLanguage = "PlainText";
    
    // Markdown preview state (managed by MarkdownPreviewHandler)
    // Note: _isPreviewVisible is kept for backward compatibility with existing code paths
    
    // Link autocomplete handler (Requirements 2.1, 7.1, 7.2)
    private LinkAutocompleteHandler? _linkAutocompleteHandler;
    
    // Markdown preview handler (Requirements 2.2)
    private MarkdownPreviewHandler? _markdownPreviewHandler;
    
    // Snippet handler (Requirements 2.3)
    private SnippetHandler? _snippetHandler;
    
    // Backlinks panel state (Requirements 7.6, 7.7)
    private bool _isBacklinksPanelVisible;

    /// <summary>
    /// Creates a new NoteWindow with all required services injected via NoteWindowContext.
    /// </summary>
    /// <param name="context">The context containing all required services</param>
    public NoteWindow(NoteWindowContext context)
    {
        // Initialize services from context (Requirements 4.1, 4.2, 4.3)
        _themeService = context.ThemeService;
        _monitorService = context.MonitorService;
        _snippetService = context.SnippetService;
        _debounceService = context.DebounceService;
        _markdownService = context.MarkdownService;
        _linkService = context.LinkService;
        _noteService = context.NoteService;
        
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        KeyDown += OnKeyDown;
        
        LanguageCombo.ItemsSource = new[] 
        { 
            "PlainText", "Markdown", "CSharp", "Java", "JavaScript", "TypeScript", 
            "Json", "Xml", "Sql", "Python", "Bash" 
        };
        LanguageCombo.SelectedIndex = 0;
        
        // Subscribe to theme changes for syntax highlighting
        _themeService.ThemeChanged += OnThemeChanged;
        
        // Subscribe to monitor changes for multi-monitor support
        _monitorService.MonitorsChanged += OnMonitorsChanged;
        
        // Initialize markdown preview handler (Requirements 2.2)
        _markdownPreviewHandler = new MarkdownPreviewHandler(_debounceService);
        _markdownPreviewHandler.Initialize(
            Editor,
            MarkdownPreview,
            BtnPreview,
            BtnExport,
            PreviewSplitter,
            EditorColumn,
            SplitterColumn,
            PreviewColumn);
        _markdownPreviewHandler.NoteLinkClicked += OnNoteLinkClicked;
        _markdownPreviewHandler.ExternalLinkClicked += OnExternalLinkClicked;
        
        // Initialize link autocomplete handler (Requirements 2.1, 7.1, 7.2)
        _linkAutocompleteHandler = new LinkAutocompleteHandler(_noteService, _linkService);
        _linkAutocompleteHandler.NoteNavigationRequested += OnLinkNavigationRequested;
        
        // Initialize snippet handler (Requirements 2.3)
        _snippetHandler = new SnippetHandler(_snippetService);
        _snippetHandler.Initialize(Editor, this);
        
        // Initialize backlinks panel (Requirements 7.6, 7.7)
        BacklinksPanel.BacklinkClicked += OnBacklinkClicked;
        
        // Populate monitor menu
        PopulateMonitorMenu();
    }
    
    #region Link Autocomplete (Requirements 2.1, 7.1, 7.2)
    
    /// <summary>
    /// Handle link navigation request from the handler
    /// </summary>
    private void OnLinkNavigationRequested(object? sender, Guid noteId)
    {
        _viewModel?.NavigateToNoteCommand?.Execute(noteId);
    }
    
    #endregion
    
    #region Backlinks Panel (Requirements 7.6, 7.7)
    
    /// <summary>
    /// Toggle backlinks panel visibility (Requirements 7.6)
    /// </summary>
    private void BtnBacklinks_Click(object sender, RoutedEventArgs e)
    {
        _isBacklinksPanelVisible = !_isBacklinksPanelVisible;
        UpdateBacklinksPanelVisibility();
        
        if (_isBacklinksPanelVisible && _viewModel != null)
        {
            BacklinksPanel.UpdateBacklinks(_viewModel.Id);
        }
    }
    
    /// <summary>
    /// Update the backlinks panel visibility and layout
    /// </summary>
    private void UpdateBacklinksPanelVisibility()
    {
        if (_isBacklinksPanelVisible)
        {
            BacklinksSplitterColumn.Width = new GridLength(4);
            BacklinksColumn.Width = new GridLength(200);
            BacklinksSplitter.Visibility = Visibility.Visible;
            BacklinksPanel.Visibility = Visibility.Visible;
            BtnBacklinks.ToolTip = "Hide Backlinks";
        }
        else
        {
            BacklinksSplitterColumn.Width = new GridLength(0);
            BacklinksColumn.Width = new GridLength(0);
            BacklinksSplitter.Visibility = Visibility.Collapsed;
            BacklinksPanel.Visibility = Visibility.Collapsed;
            BtnBacklinks.ToolTip = "Show Backlinks";
        }
    }
    
    /// <summary>
    /// Handle backlink click to navigate to the source note (Requirements 7.7)
    /// </summary>
    private void OnBacklinkClicked(object? sender, Guid noteId)
    {
        _viewModel?.NavigateToNoteCommand?.Execute(noteId);
    }
    
    /// <summary>
    /// Show backlinks panel from context menu (Requirements 7.6)
    /// </summary>
    private void ShowBacklinks_Click(object sender, RoutedEventArgs e)
    {
        if (!_isBacklinksPanelVisible)
        {
            _isBacklinksPanelVisible = true;
            UpdateBacklinksPanelVisibility();
            
            if (_viewModel != null)
            {
                BacklinksPanel.UpdateBacklinks(_viewModel.Id);
            }
        }
    }
    
    /// <summary>
    /// Open graph view window (Requirements 7.8, 7.9)
    /// </summary>
    private void OpenGraphView_Click(object sender, RoutedEventArgs e)
    {
        var graphWindow = new GraphViewWindow();
        graphWindow.NoteClicked += (_, noteId) =>
        {
            _viewModel?.NavigateToNoteCommand?.Execute(noteId);
        };
        graphWindow.Show();
    }
    
    #endregion
    
    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        // Re-apply syntax highlighting with new theme colors
        Dispatcher.Invoke(() => ApplySyntaxHighlighting(_currentLanguage));
    }

    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        // Update monitor menu when monitors change
        Dispatcher.Invoke(PopulateMonitorMenu);
    }

    /// <summary>
    /// Populate the "Move to Monitor" context menu with available monitors
    /// </summary>
    private void PopulateMonitorMenu()
    {

        MoveToMonitorMenuItem.Items.Clear();
        
        var monitors = _monitorService.GetAllMonitors();
        foreach (var monitor in monitors)
        {
            var menuItem = new MenuItem
            {
                Header = monitor.DisplayName,
                Tag = monitor.DeviceId
            };
            menuItem.Click += MoveToMonitor_Click;
            MoveToMonitorMenuItem.Items.Add(menuItem);
        }
    }

    /// <summary>
    /// Handle "Move to Monitor" menu item click
    /// </summary>
    private void MoveToMonitor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || _viewModel == null)
            return;

        var targetDeviceId = menuItem.Tag as string;
        if (string.IsNullOrEmpty(targetDeviceId))
            return;

        var targetMonitor = _monitorService.GetMonitorById(targetDeviceId);
        if (targetMonitor == null)
            return;

        // Calculate relative position within current monitor
        var currentMonitor = _monitorService.GetMonitorAt(Left, Top);
        
        if (currentMonitor != null)
        {
            // Calculate relative position and apply to target monitor
            double relativeX = (Left - currentMonitor.WorkingArea.Left) / currentMonitor.WorkingArea.Width;
            double relativeY = (Top - currentMonitor.WorkingArea.Top) / currentMonitor.WorkingArea.Height;
            
            Left = targetMonitor.WorkingArea.Left + relativeX * targetMonitor.WorkingArea.Width;
            Top = targetMonitor.WorkingArea.Top + relativeY * targetMonitor.WorkingArea.Height;
        }
        else
        {
            // Window is off-screen, center on target monitor
            Left = targetMonitor.WorkingArea.Left + (targetMonitor.WorkingArea.Width - Width) / 2;
            Top = targetMonitor.WorkingArea.Top + (targetMonitor.WorkingArea.Height - Height) / 2;
        }

        // Ensure window is within bounds
        EnsureWindowInMonitorBounds(targetMonitor);

        // Update the note's monitor assignment
        _viewModel.MonitorDeviceId = targetDeviceId;
    }

    /// <summary>
    /// Ensure the window is within the monitor's working area
    /// </summary>
    private void EnsureWindowInMonitorBounds(MonitorInfo monitor)
    {
        var workingArea = monitor.WorkingArea;
        
        if (Left < workingArea.Left)
            Left = workingArea.Left;
        if (Top < workingArea.Top)
            Top = workingArea.Top;
        if (Left + Width > workingArea.Right)
            Left = Math.Max(workingArea.Left, workingArea.Right - Width);
        if (Top + Height > workingArea.Bottom)
            Top = Math.Max(workingArea.Top, workingArea.Bottom - Height);
    }

    /// <summary>
    /// Handle right-click on header to show context menu
    /// </summary>
    private void Header_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Refresh monitor list before showing menu
        PopulateMonitorMenu();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is NoteViewModel vm)
        {
            _viewModel = vm;
            _isPinned = vm.IsPinned;
            
            // Initialize link autocomplete handler with editor and note ID (Requirements 2.1)
            _linkAutocompleteHandler?.Initialize(Editor, vm.Id);
            
            Editor.Text = vm.Content;
            Editor.TextChanged += (_, _) => 
            {
                if (_viewModel != null)
                    _viewModel.Content = Editor.Text;
                
                // Update markdown preview if visible (Requirements 4.3, 2.2)
                _markdownPreviewHandler?.RequestPreviewUpdate();
            };
            
            Opacity = vm.Opacity;
            Topmost = vm.IsPinned;
            OpacitySlider.Value = vm.Opacity;
            
            var langIndex = Array.IndexOf(
                new[] { "PlainText", "CSharp", "Java", "JavaScript", "TypeScript", "Json", "Xml", "Sql", "Python", "Bash" },
                vm.Language);
            if (langIndex >= 0) LanguageCombo.SelectedIndex = langIndex;
            
            ApplySyntaxHighlighting(vm.Language);
            
            LanguageCombo.SelectionChanged += (_, _) =>
            {
                if (LanguageCombo.SelectedItem is string lang && _viewModel != null)
                {
                    _viewModel.Language = lang;
                    ApplySyntaxHighlighting(lang);
                    // Update handlers with new language (Requirements 2.2, 2.3)
                    _markdownPreviewHandler?.SetLanguage(lang);
                    _snippetHandler?.SetLanguage(lang);
                }
            };
            
            // Update handlers with initial language (Requirements 2.2, 2.3)
            _markdownPreviewHandler?.SetLanguage(vm.Language);
            _snippetHandler?.SetLanguage(vm.Language);
            
            UpdatePinButton();
        }
    }


    private void ApplySyntaxHighlighting(string language)
    {
        _currentLanguage = language;
        
        var highlighting = language switch
        {
            "CSharp" => "C#",
            "JavaScript" or "TypeScript" => "JavaScript",
            "Json" => "Json",
            "Xml" => "XML",
            "Sql" => "TSQL",
            "Python" => "Python",
            "Java" => "Java",
            "Bash" => "Boo", // Use Boo as fallback for shell-like syntax
            _ => null
        };
        
        if (highlighting != null)
        {
            try
            {
                var definition = HighlightingManager.Instance.GetDefinition(highlighting);
                if (definition != null)
                {
                    // Apply theme-appropriate syntax highlighting colors
                    var isDarkTheme = _themeService.CurrentTheme == Models.Theme.Dark;
                    if (isDarkTheme)
                    {
                        VSCodeDarkTheme.ApplyTheme(definition);
                    }
                    else
                    {
                        VSCodeLightTheme.ApplyTheme(definition);
                    }
                    Editor.SyntaxHighlighting = definition;
                }
            }
            catch { Editor.SyntaxHighlighting = null; }
        }
        else
        {
            Editor.SyntaxHighlighting = null;
        }
    }

    private void UpdatePinButton()
    {
        BtnPin.Content = _isPinned ? "●" : "○";
        BtnPin.ToolTip = _isPinned ? "Unpin (on top)" : "Pin";
    }

    // Event Handlers
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void BtnPin_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        
        // Force Topmost change by toggling it
        Topmost = false;
        if (_isPinned)
        {
            Topmost = true;
        }
        
        if (_viewModel != null)
            _viewModel.IsPinned = _isPinned;
        
        UpdatePinButton();
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchPanel.Visibility = SearchPanel.Visibility == Visibility.Visible 
            ? Visibility.Collapsed 
            : Visibility.Visible;
        if (SearchPanel.Visibility == Visibility.Visible)
            SearchBox.Focus();
    }

    #region Markdown Preview (Requirements 2.2, 4.1, 4.2, 4.3, 4.6, 4.7)

    /// <summary>
    /// Toggle markdown preview visibility (Requirements 4.2, 2.2)
    /// Delegates to MarkdownPreviewHandler
    /// </summary>
    private void BtnPreview_Click(object sender, RoutedEventArgs e)
    {
        _markdownPreviewHandler?.TogglePreview();
    }

    /// <summary>
    /// Handle internal note link clicks (Requirements 4.7)
    /// </summary>
    private void OnNoteLinkClicked(object? sender, Guid noteId)
    {
        // Navigate to the linked note
        if (_viewModel != null)
        {
            _viewModel.NavigateToNoteCommand?.Execute(noteId);
        }
    }

    /// <summary>
    /// Handle external link clicks (Requirements 4.6)
    /// </summary>
    private void OnExternalLinkClicked(object? sender, string uri)
    {
        // External links are handled by the MarkdownPreviewControl/Handler
        // This event is for logging or additional handling if needed
    }

    #endregion

    #region Markdown Export (Requirements 4.9)

    /// <summary>
    /// Show export context menu
    /// </summary>
    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (BtnExport.ContextMenu != null)
        {
            BtnExport.ContextMenu.PlacementTarget = BtnExport;
            BtnExport.ContextMenu.IsOpen = true;
        }
    }

    /// <summary>
    /// Export as HTML (Requirements 4.9)
    /// </summary>
    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "HTML files (*.html)|*.html|All files (*.*)|*.*",
            DefaultExt = ".html",
            FileName = GetExportFileName("html")
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

                var html = _markdownService.RenderToHtml(Editor.Text, options);
                System.IO.File.WriteAllText(dialog.FileName, html);
                CustomDialog.ShowSuccess("Export Complete", $"HTML exported to:\n{dialog.FileName}");
            }
            catch (Exception ex)
            {
                CustomDialog.ShowError("Export Failed", $"Failed to export HTML: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Export as PDF (Requirements 4.9)
    /// </summary>
    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
            DefaultExt = ".pdf",
            FileName = GetExportFileName("pdf")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                // Note: RenderToPdfAsync currently returns HTML as bytes
                // A full PDF implementation would require a PDF library like iTextSharp or PdfSharp
                var pdfBytes = await _markdownService.RenderToPdfAsync(Editor.Text);
                
                // For now, save as HTML with .pdf extension (placeholder)
                // In a production app, you'd use a proper PDF library
                await System.IO.File.WriteAllBytesAsync(dialog.FileName, pdfBytes);
                
                CustomDialog.ShowInfo("Export Note", 
                    "PDF export is currently a placeholder. The file contains HTML content.\n" +
                    "For full PDF support, a PDF library would need to be integrated.");
            }
            catch (Exception ex)
            {
                CustomDialog.ShowError("Export Failed", $"Failed to export PDF: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Export as plain Markdown (Requirements 4.9)
    /// </summary>
    private void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".md",
            FileName = GetExportFileName("md")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                System.IO.File.WriteAllText(dialog.FileName, Editor.Text);
                CustomDialog.ShowSuccess("Export Complete", $"Markdown exported to:\n{dialog.FileName}");
            }
            catch (Exception ex)
            {
                CustomDialog.ShowError("Export Failed", $"Failed to export Markdown: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Generate a filename for export based on note title
    /// </summary>
    private string GetExportFileName(string extension)
    {
        var title = _viewModel?.Title ?? "note";
        // Sanitize filename
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        var sanitized = new string(title.Where(c => !invalidChars.Contains(c)).ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "note";
        return $"{sanitized}.{extension}";
    }

    #endregion

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CloseCommand.Execute(null);
        }
        else
        {
            Close();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.SearchTerm = SearchBox.Text;
    }

    private void BtnPrevMatch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.PreviousMatchCommand.Execute(null);
    }

    private void BtnNextMatch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.NextMatchCommand.Execute(null);
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Opacity = e.NewValue;
        if (_viewModel != null)
            _viewModel.Opacity = e.NewValue;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.S:
                    _viewModel.SaveCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F:
                    BtnSearch_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.W:
                    BtnClose_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            switch (e.Key)
            {
                case Key.F:
                    _viewModel.FormatCommand.Execute(null);
                    Editor.Text = _viewModel.Content;
                    e.Handled = true;
                    break;
                case Key.S:
                    // Save selection as snippet (Requirements 3.1)
                    SaveSelectionAsSnippet();
                    e.Handled = true;
                    break;
                case Key.I:
                    // Open snippet browser (Requirements 3.3)
                    OpenSnippetBrowser();
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Key == Key.Tab && _snippetHandler != null && _snippetHandler.IsPlaceholderNavigationActive)
        {
            // Tab navigation between placeholders (Requirements 3.6, 2.3)
            if (_snippetHandler.HandleKeyDown(e))
            {
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            if (SearchPanel.Visibility == Visibility.Visible)
                SearchPanel.Visibility = Visibility.Collapsed;
            else
                Hide();
            e.Handled = true;
        }
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (_viewModel != null)
        {
            _viewModel.WindowRect.Top = Top;
            _viewModel.WindowRect.Left = Left;
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (_viewModel != null)
        {
            _viewModel.WindowRect.Width = ActualWidth;
            _viewModel.WindowRect.Height = ActualHeight;
        }
    }

    private void ResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        var newWidth = Width + e.HorizontalChange;
        var newHeight = Height + e.VerticalChange;
        
        if (newWidth >= MinWidth)
            Width = newWidth;
        if (newHeight >= MinHeight)
            Height = newHeight;
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from theme changes
        _themeService.ThemeChanged -= OnThemeChanged;
        
        // Unsubscribe from monitor changes
        _monitorService.MonitorsChanged -= OnMonitorsChanged;
        
        // Clean up markdown preview handler (Requirements 2.2)
        if (_markdownPreviewHandler != null)
        {
            _markdownPreviewHandler.NoteLinkClicked -= OnNoteLinkClicked;
            _markdownPreviewHandler.ExternalLinkClicked -= OnExternalLinkClicked;
            _markdownPreviewHandler.Dispose();
        }
        
        // Clean up link autocomplete handler (Requirements 2.1, 7.1, 7.2)
        if (_linkAutocompleteHandler != null)
        {
            _linkAutocompleteHandler.NoteNavigationRequested -= OnLinkNavigationRequested;
            _linkAutocompleteHandler.Dispose();
        }
        
        // Clean up snippet handler (Requirements 2.3)
        _snippetHandler?.Dispose();
        
        // Unsubscribe from backlinks panel events (Requirements 7.6, 7.7)
        BacklinksPanel.BacklinkClicked -= OnBacklinkClicked;
        
        base.OnClosed(e);
    }

    #region Snippet Integration (Requirements 3.1, 3.3, 3.4, 3.6)

    /// <summary>
    /// Save selected text as a snippet (Requirements 3.1, 2.3)
    /// Delegates to SnippetHandler
    /// </summary>
    private void SaveSelectionAsSnippet()
    {
        _snippetHandler?.SaveSelectionAsSnippet();
    }

    /// <summary>
    /// Open snippet browser for insertion (Requirements 3.3, 2.3)
    /// Delegates to SnippetHandler
    /// </summary>
    private void OpenSnippetBrowser()
    {
        _snippetHandler?.OpenSnippetBrowser();
    }

    #endregion

    #region Save as Template (Requirements 6.6, 6.7)

    /// <summary>
    /// Save current note as a template (Requirements 6.6, 6.7)
    /// </summary>
    private void SaveAsTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var note = _viewModel.ToNote();
        var dialog = new SaveAsTemplateDialog(note)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.CreatedTemplate != null)
        {
            CustomDialog.ShowSuccess("Template Saved",
                $"Template '{dialog.CreatedTemplate.Name}' has been saved.");
        }
    }

    #endregion
}
