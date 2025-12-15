using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevSticky.Handlers;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Resources;
using DevSticky.Services;
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
    private readonly NoteWindowCoordinator _coordinator;
    private readonly IFileDropService _fileDropService;
    private readonly Helpers.EventSubscriptionManager _eventManager = new();
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
    
    // Flag to prevent auto-save during initial content load
    private bool _isInitializingContent;

    /// <summary>
    /// Creates a new NoteWindow with all required services injected via NoteWindowContext.
    /// </summary>
    /// <param name="context">The context containing all required services</param>
    /// <param name="coordinator">The coordinator for window operations</param>
    public NoteWindow(NoteWindowContext context, NoteWindowCoordinator coordinator)
    {
        // Initialize services from context (Requirements 4.1, 4.2, 4.3)
        _themeService = context.ThemeService;
        _monitorService = context.MonitorService;
        _snippetService = context.SnippetService;
        _debounceService = context.DebounceService;
        _markdownService = context.MarkdownService;
        _linkService = context.LinkService;
        _noteService = context.NoteService;
        _fileDropService = context.FileDropService;
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        KeyDown += OnKeyDown;
        
        LanguageCombo.ItemsSource = new[] 
        { 
            "PlainText", "Markdown", "CSharp", "Java", "JavaScript", "TypeScript", 
            "Json", "Xml", "Sql", "Python", "Bash", "Go"
        };
        LanguageCombo.SelectedIndex = 0;
        LanguageCombo.SelectionChanged += OnLanguageComboSelectionChanged;
        
        // Subscribe to theme changes for syntax highlighting using weak event manager
        _eventManager.Subscribe<ThemeChangedEventArgs>(_themeService, nameof(_themeService.ThemeChanged), OnThemeChanged);
        
        // Subscribe to monitor changes for multi-monitor support using weak event manager
        _eventManager.Subscribe(_monitorService, nameof(_monitorService.MonitorsChanged), OnMonitorsChanged);
        
        // Initialize markdown preview handler (Requirements 2.2)
        _markdownPreviewHandler = new MarkdownPreviewHandler(
            _debounceService, _markdownService, _themeService, _noteService);
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
        BacklinksPanel.Initialize(_linkService, _noteService);
        BacklinksPanel.BacklinkClicked += OnBacklinkClicked;
        
        // Subscribe to Editor.TextChanged once in constructor
        Editor.TextChanged += OnEditorTextChanged;
        
        // Populate monitor menu
        PopulateMonitorMenu();
    }
    
    /// <summary>
    /// Handle editor text changes - updates ViewModel and triggers auto-save
    /// </summary>
    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        // Skip updating ViewModel during initial content load to avoid triggering auto-save
        if (_isInitializingContent) return;
        
        if (_viewModel != null)
            _viewModel.Content = Editor.Text;
        
        // Update markdown preview if visible (Requirements 4.3, 2.2)
        _markdownPreviewHandler?.RequestPreviewUpdate();
    }
    
    /// <summary>
    /// Handle language selection changes
    /// </summary>
    private void OnLanguageComboSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedItem is string lang && _viewModel != null)
        {
            _viewModel.Language = lang;
            ApplySyntaxHighlighting(lang);
            // Update handlers with new language (Requirements 2.2, 2.3)
            _markdownPreviewHandler?.SetLanguage(lang);
            _snippetHandler?.SetLanguage(lang);
        }
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
            BtnBacklinks.ToolTip = L.Get("HideBacklinks");
        }
        else
        {
            BacklinksSplitterColumn.Width = new GridLength(0);
            BacklinksColumn.Width = new GridLength(0);
            BacklinksSplitter.Visibility = Visibility.Collapsed;
            BacklinksPanel.Visibility = Visibility.Collapsed;
            BtnBacklinks.ToolTip = L.Get("ShowBacklinks");
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
        _coordinator.MoveWindowToMonitor(this, targetDeviceId!, _viewModel);
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
            
            // Set initial content with flag to prevent auto-save
            _isInitializingContent = true;
            Editor.Text = vm.Content;
            _isInitializingContent = false;
            
            Opacity = vm.Opacity;
            Topmost = vm.IsPinned;
            OpacitySlider.Value = vm.Opacity;
            
            var langIndex = Array.IndexOf(
                new[] { "PlainText", "Markdown", "CSharp", "Java", "JavaScript", "TypeScript", "Json", "Xml", "Sql", "Python", "Bash", "Go" },
                vm.Language);
            if (langIndex >= 0) LanguageCombo.SelectedIndex = langIndex;
            
            ApplySyntaxHighlighting(vm.Language);
            
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
            "Go" => "C#", // Go has similar syntax to C#
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
        BtnPin.ToolTip = _isPinned ? L.Get("UnpinOnTop") : L.Get("PinTooltip");
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
    /// Delegates to coordinator
    /// </summary>
    private async void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _coordinator.ExportAsHtmlAsync(Editor.Text, _viewModel.Title);
        }
    }

    /// <summary>
    /// Export as PDF (Requirements 4.9)
    /// Delegates to coordinator
    /// </summary>
    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _coordinator.ExportAsPdfAsync(Editor.Text, _viewModel.Title);
        }
    }

    /// <summary>
    /// Export as plain Markdown (Requirements 4.9)
    /// Delegates to coordinator
    /// </summary>
    private async void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _coordinator.ExportAsMarkdownAsync(Editor.Text, _viewModel.Title);
        }
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
        UpdateSearchPlaceholder();
        if (_viewModel != null)
            _viewModel.SearchTerm = SearchBox.Text;
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e) => UpdateSearchPlaceholder();
    private void SearchBox_LostFocus(object sender, RoutedEventArgs e) => UpdateSearchPlaceholder();

    private void UpdateSearchPlaceholder()
    {
        if (SearchPlaceholder != null)
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) && !SearchBox.IsFocused
                ? Visibility.Visible : Visibility.Collapsed;
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
        else if (e.Key == Key.F1)
        {
            // Open keyboard shortcuts help
            var window = new KeyboardShortcutsWindow { Owner = this };
            window.ShowDialog();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (SearchPanel.Visibility == Visibility.Visible)
                SearchPanel.Visibility = Visibility.Collapsed;
            else
            {
                // Force save before hiding to ensure content is persisted
                _viewModel?.SaveCommand.Execute(null);
                Hide();
            }
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
        // Dispose weak event manager (automatically unsubscribes from all events)
        _eventManager.Dispose();
        
        // Unsubscribe from editor and language combo events
        Editor.TextChanged -= OnEditorTextChanged;
        LanguageCombo.SelectionChanged -= OnLanguageComboSelectionChanged;
        
        // Clean up AvalonEdit TextEditor resources
        WpfResourceHelper.DisposeTextEditor(Editor);
        
        // Clean up markdown preview handler (Requirements 2.2)
        if (_markdownPreviewHandler != null)
        {
            _markdownPreviewHandler.NoteLinkClicked -= OnNoteLinkClicked;
            _markdownPreviewHandler.ExternalLinkClicked -= OnExternalLinkClicked;
            _markdownPreviewHandler.Dispose();
        }
        
        // Clean up markdown preview control
        if (MarkdownPreview is IDisposable disposablePreview)
        {
            disposablePreview.Dispose();
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
        
        // Clean up any remaining event subscriptions
        DataContextChanged -= OnDataContextChanged;
        KeyDown -= OnKeyDown;
        
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
    /// Delegates to coordinator
    /// </summary>
    private async void SaveAsTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var note = _viewModel.ToNote();
        await _coordinator.SaveAsTemplateAsync(this, note);
    }

    #endregion

    #region Drag & Drop Support (Requirements 1.1, 1.2, 1.3, 1.4, 1.5)

    /// <summary>
    /// Handle drag over event to show drop feedback
    /// </summary>
    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        // Check if the dragged data contains files
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    /// <summary>
    /// Handle drop event to process dropped files
    /// </summary>
    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                // Process the dropped files using the FileDropService
                var content = _fileDropService.ProcessDroppedFiles(files);
                
                if (!string.IsNullOrEmpty(content))
                {
                    // Insert the content at the current cursor position
                    var cursorPosition = Editor.CaretOffset;
                    Editor.Document.Insert(cursorPosition, content);
                    
                    // Update the view model content
                    if (_viewModel != null)
                    {
                        _viewModel.Content = Editor.Text;
                    }
                }
            }
        }
        e.Handled = true;
    }

    #endregion
}
