using System.Windows;
using System.Windows.Controls;
using DevSticky.Interfaces;
using DevSticky.Services;
using DevSticky.Views;
using ICSharpCode.AvalonEdit;
using Button = System.Windows.Controls.Button;

namespace DevSticky.Handlers;

/// <summary>
/// Handles markdown preview functionality for NoteWindow (Requirements 2.2)
/// Manages preview visibility, debounced updates, and preview button state
/// </summary>
public class MarkdownPreviewHandler : IDisposable
{
    private readonly IDebounceService _debounceService;
    private readonly IMarkdownService? _markdownService;
    private readonly IThemeService? _themeService;
    private readonly INoteService? _noteService;
    
    private TextEditor? _editor;
    private MarkdownPreviewControl? _previewControl;
    private Button? _previewButton;
    private Button? _exportButton;
    private GridSplitter? _splitter;
    private ColumnDefinition? _editorColumn;
    private ColumnDefinition? _splitterColumn;
    private ColumnDefinition? _previewColumn;
    
    // Preview state
    private bool _isPreviewVisible;
    private string _currentLanguage = "PlainText";
    
    // Debounce configuration
    private const string PreviewDebounceKey = "MarkdownPreview";
    private const int PreviewDebounceMs = 300; // Requirements 4.3: 300ms debounce
    
    /// <summary>
    /// Event raised when a note link is clicked in the preview
    /// </summary>
    public event EventHandler<Guid>? NoteLinkClicked;
    
    /// <summary>
    /// Event raised when an external link is clicked in the preview
    /// </summary>
    public event EventHandler<string>? ExternalLinkClicked;
    
    /// <summary>
    /// Gets whether the preview is currently visible
    /// </summary>
    public bool IsPreviewVisible => _isPreviewVisible;
    
    /// <summary>
    /// Creates a new MarkdownPreviewHandler
    /// </summary>
    /// <param name="debounceService">Service for debouncing preview updates</param>
    /// <param name="markdownService">Service for rendering markdown (optional, for injection into preview control)</param>
    /// <param name="themeService">Service for theme management (optional, for injection into preview control)</param>
    /// <param name="noteService">Service for note operations (optional, for injection into preview control)</param>
    public MarkdownPreviewHandler(
        IDebounceService debounceService,
        IMarkdownService? markdownService = null,
        IThemeService? themeService = null,
        INoteService? noteService = null)
    {
        _debounceService = debounceService;
        _markdownService = markdownService;
        _themeService = themeService;
        _noteService = noteService;
    }

    
    /// <summary>
    /// Initialize the handler with UI elements
    /// </summary>
    /// <param name="editor">The AvalonEdit TextEditor</param>
    /// <param name="previewControl">The markdown preview control</param>
    /// <param name="previewButton">The preview toggle button</param>
    /// <param name="exportButton">The export button</param>
    /// <param name="splitter">The grid splitter between editor and preview</param>
    /// <param name="editorColumn">The editor column definition</param>
    /// <param name="splitterColumn">The splitter column definition</param>
    /// <param name="previewColumn">The preview column definition</param>
    public void Initialize(
        TextEditor editor,
        MarkdownPreviewControl previewControl,
        Button previewButton,
        Button exportButton,
        GridSplitter splitter,
        ColumnDefinition editorColumn,
        ColumnDefinition splitterColumn,
        ColumnDefinition previewColumn)
    {
        _editor = editor;
        _previewControl = previewControl;
        _previewButton = previewButton;
        _exportButton = exportButton;
        _splitter = splitter;
        _editorColumn = editorColumn;
        _splitterColumn = splitterColumn;
        _previewColumn = previewColumn;
        
        // Initialize preview control with services if available
        if (_markdownService != null && _themeService != null && _noteService != null)
        {
            _previewControl.Initialize(_markdownService, _themeService, _noteService);
        }
        
        // Wire up preview control events
        _previewControl.NoteLinkClicked += OnPreviewNoteLinkClicked;
        _previewControl.ExternalLinkClicked += OnPreviewExternalLinkClicked;
        
        // Initialize visibility based on current language
        UpdatePreviewButtonVisibility();
    }
    
    /// <summary>
    /// Set the current language and update preview button visibility
    /// </summary>
    /// <param name="language">The current language</param>
    public void SetLanguage(string language)
    {
        _currentLanguage = language;
        UpdatePreviewButtonVisibility();
    }
    
    /// <summary>
    /// Toggle preview visibility (Requirements 4.2)
    /// </summary>
    public void TogglePreview()
    {
        _isPreviewVisible = !_isPreviewVisible;
        UpdatePreviewVisibility();
        
        if (_isPreviewVisible)
        {
            // Initial render
            UpdatePreview();
        }
    }
    
    /// <summary>
    /// Request a preview update with debouncing (Requirements 4.3)
    /// </summary>
    public void RequestPreviewUpdate()
    {
        if (!_isPreviewVisible)
            return;
        
        UpdatePreview();
    }
    
    /// <summary>
    /// Update markdown preview with debounce (Requirements 4.3)
    /// </summary>
    private void UpdatePreview()
    {
        if (!_isPreviewVisible || _editor == null || _previewControl == null)
            return;

        _debounceService.Debounce(PreviewDebounceKey, () =>
        {
            _editor.Dispatcher.Invoke(() =>
            {
                var content = _editor.Text;
                _previewControl.UpdateContent(content);
            });
        }, PreviewDebounceMs);
    }
    
    /// <summary>
    /// Update the preview panel visibility and layout
    /// </summary>
    private void UpdatePreviewVisibility()
    {
        if (_splitter == null || _previewControl == null || _previewButton == null ||
            _editorColumn == null || _splitterColumn == null || _previewColumn == null)
            return;
        
        if (_isPreviewVisible)
        {
            // Show split view: editor on left, preview on right
            _editorColumn.Width = new GridLength(1, GridUnitType.Star);
            _splitterColumn.Width = new GridLength(4);
            _previewColumn.Width = new GridLength(1, GridUnitType.Star);
            _splitter.Visibility = Visibility.Visible;
            _previewControl.Visibility = Visibility.Visible;
            _previewButton.ToolTip = L.Get("HidePreview");
        }
        else
        {
            // Hide preview, editor takes full width
            _editorColumn.Width = new GridLength(1, GridUnitType.Star);
            _splitterColumn.Width = new GridLength(0);
            _previewColumn.Width = new GridLength(0);
            _splitter.Visibility = Visibility.Collapsed;
            _previewControl.Visibility = Visibility.Collapsed;
            _previewButton.ToolTip = L.Get("ShowPreview");
        }
    }
    
    /// <summary>
    /// Update the preview button visibility based on language (Requirements 4.1)
    /// </summary>
    private void UpdatePreviewButtonVisibility()
    {
        if (_previewButton == null || _exportButton == null)
            return;
        
        // Show preview and export buttons only for Markdown language
        var isMarkdown = _currentLanguage.Equals("Markdown", StringComparison.OrdinalIgnoreCase);
        _previewButton.Visibility = isMarkdown ? Visibility.Visible : Visibility.Collapsed;
        _exportButton.Visibility = isMarkdown ? Visibility.Visible : Visibility.Collapsed;
        
        // Hide preview if language changed away from Markdown
        if (!isMarkdown && _isPreviewVisible)
        {
            _isPreviewVisible = false;
            UpdatePreviewVisibility();
        }
    }
    
    /// <summary>
    /// Handle note link click from preview (Requirements 4.7)
    /// </summary>
    private void OnPreviewNoteLinkClicked(object? sender, Guid noteId)
    {
        NoteLinkClicked?.Invoke(this, noteId);
    }
    
    /// <summary>
    /// Handle external link click from preview (Requirements 4.6)
    /// </summary>
    private void OnPreviewExternalLinkClicked(object? sender, string uri)
    {
        ExternalLinkClicked?.Invoke(this, uri);
    }
    
    #region IDisposable
    
    public void Dispose()
    {
        // Cancel any pending preview updates
        _debounceService.Cancel(PreviewDebounceKey);
        
        // Unsubscribe from preview control events
        if (_previewControl != null)
        {
            _previewControl.NoteLinkClicked -= OnPreviewNoteLinkClicked;
            _previewControl.ExternalLinkClicked -= OnPreviewExternalLinkClicked;
        }
    }
    
    #endregion
}
