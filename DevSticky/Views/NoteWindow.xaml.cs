using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private IThemeService? _themeService;
    private IMonitorService? _monitorService;
    private ISnippetService? _snippetService;
    private IDebounceService? _debounceService;
    private IMarkdownService? _markdownService;
    private ILinkService? _linkService;
    private INoteService? _noteService;
    private string _currentLanguage = "PlainText";
    
    // Markdown preview state
    private bool _isPreviewVisible;
    private const string PreviewDebounceKey = "MarkdownPreview";
    private const int PreviewDebounceMs = 300; // Requirements 4.3: 300ms debounce
    
    // Snippet placeholder navigation state
    private List<SnippetPlaceholder>? _activePlaceholders;
    private int _currentPlaceholderIndex = -1;
    
    // Link autocomplete state (Requirements 7.1, 7.2)
    private LinkAutocompletePopup? _linkAutocompletePopup;
    private bool _isLinkAutocompleteActive;
    private int _linkTriggerPosition = -1;
    
    // Link tooltip state (Requirements 7.4)
    private System.Windows.Controls.ToolTip? _linkTooltip;
    private NoteLink? _hoveredLink;
    
    // Backlinks panel state (Requirements 7.6, 7.7)
    private bool _isBacklinksPanelVisible;

    public NoteWindow()
    {
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
        try
        {
            _themeService = App.GetService<IThemeService>();
            _themeService.ThemeChanged += OnThemeChanged;
            
            // Get monitor service for multi-monitor support
            _monitorService = App.GetService<IMonitorService>();
            _monitorService.MonitorsChanged += OnMonitorsChanged;
            
            // Get snippet service for snippet operations
            _snippetService = App.GetService<ISnippetService>();
            
            // Get debounce service for markdown preview updates
            _debounceService = App.GetService<IDebounceService>();
            
            // Get markdown service for preview rendering
            _markdownService = App.GetService<IMarkdownService>();
            
            // Get link service for note linking (Requirements 7.1, 7.2)
            _linkService = App.GetService<ILinkService>();
            _noteService = App.GetService<INoteService>();
        }
        catch { /* Service not available during design time */ }
        
        // Wire up markdown preview link events
        MarkdownPreview.NoteLinkClicked += OnNoteLinkClicked;
        MarkdownPreview.ExternalLinkClicked += OnExternalLinkClicked;
        
        // Initialize link autocomplete popup (Requirements 7.1, 7.2)
        InitializeLinkAutocomplete();
        
        // Initialize backlinks panel (Requirements 7.6, 7.7)
        BacklinksPanel.BacklinkClicked += OnBacklinkClicked;
        
        // Populate monitor menu
        PopulateMonitorMenu();
    }
    
    #region Link Autocomplete (Requirements 7.1, 7.2)
    
    /// <summary>
    /// Initialize the link autocomplete popup
    /// </summary>
    private void InitializeLinkAutocomplete()
    {
        _linkAutocompletePopup = new LinkAutocompletePopup();
        _linkAutocompletePopup.NoteSelected += OnLinkNoteSelected;
        _linkAutocompletePopup.Cancelled += OnLinkAutocompleteCancelled;
        
        // Wire up text input for [[ detection
        Editor.TextArea.TextEntered += OnTextEntered;
        Editor.TextArea.PreviewKeyDown += OnEditorPreviewKeyDown;
        
        // Initialize link tooltip (Requirements 7.4)
        InitializeLinkTooltip();
    }
    
    /// <summary>
    /// Initialize the link tooltip for hover preview (Requirements 7.4)
    /// </summary>
    private void InitializeLinkTooltip()
    {
        _linkTooltip = new System.Windows.Controls.ToolTip
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse,
            HasDropShadow = true
        };
        
        // Wire up mouse move for link detection
        Editor.TextArea.MouseMove += OnEditorMouseMove;
        Editor.TextArea.MouseLeave += OnEditorMouseLeave;
        
        // Wire up mouse click for link navigation (Requirements 7.3)
        Editor.TextArea.PreviewMouseLeftButtonDown += OnEditorMouseLeftButtonDown;
    }
    
    /// <summary>
    /// Handle mouse click to navigate to linked note (Requirements 7.3)
    /// </summary>
    private void OnEditorMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only handle Ctrl+Click for link navigation
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;
        
        if (_linkService == null || _noteService == null || _viewModel == null)
            return;
        
        try
        {
            // Get position from mouse
            var position = Editor.GetPositionFromPoint(e.GetPosition(Editor));
            if (position == null)
                return;
            
            var offset = Editor.Document.GetOffset(position.Value.Location);
            var content = Editor.Text;
            
            // Check if we're clicking on a link
            var link = FindLinkAtPosition(content, offset, _viewModel.Id);
            
            if (link != null && !link.IsBroken)
            {
                // Navigate to the linked note
                _viewModel.NavigateToNoteCommand?.Execute(link.TargetNoteId);
                e.Handled = true;
            }
        }
        catch
        {
            // Ignore click handling errors
        }
    }
    
    /// <summary>
    /// Handle mouse move to detect link hover (Requirements 7.4)
    /// </summary>
    private void OnEditorMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_linkService == null || _noteService == null || _viewModel == null)
            return;
        
        try
        {
            // Get position from mouse
            var position = Editor.GetPositionFromPoint(e.GetPosition(Editor));
            if (position == null)
            {
                HideLinkTooltip();
                return;
            }
            
            var offset = Editor.Document.GetOffset(position.Value.Location);
            var content = Editor.Text;
            
            // Check if we're hovering over a link
            var link = FindLinkAtPosition(content, offset, _viewModel.Id);
            
            if (link != null && !link.IsBroken)
            {
                if (_hoveredLink?.TargetNoteId != link.TargetNoteId)
                {
                    _hoveredLink = link;
                    ShowLinkTooltip(link);
                }
            }
            else
            {
                HideLinkTooltip();
            }
        }
        catch
        {
            HideLinkTooltip();
        }
    }
    
    /// <summary>
    /// Handle mouse leave to hide tooltip
    /// </summary>
    private void OnEditorMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        HideLinkTooltip();
    }
    
    /// <summary>
    /// Find a link at the given position in the content
    /// </summary>
    private NoteLink? FindLinkAtPosition(string content, int offset, Guid sourceNoteId)
    {
        if (_linkService == null)
            return null;
        
        var links = _linkService.ParseLinksFromContent(content, sourceNoteId);
        return links.FirstOrDefault(l => offset >= l.StartPosition && offset < l.StartPosition + l.Length);
    }
    
    /// <summary>
    /// Show tooltip for a link (Requirements 7.4)
    /// </summary>
    private void ShowLinkTooltip(NoteLink link)
    {
        if (_noteService == null || _linkTooltip == null)
            return;
        
        var targetNote = _noteService.GetNoteById(link.TargetNoteId);
        if (targetNote == null)
            return;
        
        // Create tooltip content with title and preview (first 100 characters)
        var preview = GetNotePreview(targetNote.Content, 100);
        
        var tooltipContent = new StackPanel { MaxWidth = 300 };
        tooltipContent.Children.Add(new TextBlock
        {
            Text = targetNote.Title,
            FontWeight = FontWeights.Bold,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap
        });
        tooltipContent.Children.Add(new TextBlock
        {
            Text = preview,
            Foreground = (System.Windows.Media.Brush)FindResource("SubtextBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        tooltipContent.Children.Add(new TextBlock
        {
            Text = "Ctrl+Click to open",
            Foreground = (System.Windows.Media.Brush)FindResource("Surface2Brush"),
            FontSize = 10,
            FontStyle = FontStyles.Italic,
            Margin = new Thickness(0, 4, 0, 0)
        });
        
        _linkTooltip.Content = tooltipContent;
        _linkTooltip.IsOpen = true;
        Editor.ToolTip = _linkTooltip;
    }
    
    /// <summary>
    /// Hide the link tooltip
    /// </summary>
    private void HideLinkTooltip()
    {
        if (_linkTooltip != null)
        {
            _linkTooltip.IsOpen = false;
        }
        _hoveredLink = null;
        Editor.ToolTip = null;
    }
    
    /// <summary>
    /// Handle text entered to detect [[ trigger (Requirements 7.1)
    /// </summary>
    private void OnTextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (e.Text == "[")
        {
            // Check if previous character is also [
            var caretOffset = Editor.CaretOffset;
            if (caretOffset >= 2)
            {
                var prevChar = Editor.Document.GetText(caretOffset - 2, 1);
                if (prevChar == "[")
                {
                    // Trigger autocomplete
                    _isLinkAutocompleteActive = true;
                    _linkTriggerPosition = caretOffset - 2;
                    UpdateLinkSuggestions("");
                }
            }
        }
        else if (_isLinkAutocompleteActive)
        {
            // Update suggestions based on typed text
            UpdateLinkSuggestionsFromCaret();
        }
    }
    
    /// <summary>
    /// Handle key down for autocomplete navigation
    /// </summary>
    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isLinkAutocompleteActive || _linkAutocompletePopup == null)
            return;
        
        switch (e.Key)
        {
            case Key.Up:
                _linkAutocompletePopup.MoveSelectionUp();
                e.Handled = true;
                break;
            case Key.Down:
                _linkAutocompletePopup.MoveSelectionDown();
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Tab:
                _linkAutocompletePopup.ConfirmSelection();
                e.Handled = true;
                break;
            case Key.Escape:
                CloseLinkAutocomplete();
                e.Handled = true;
                break;
            case Key.Back:
                // Check if we should close autocomplete
                var caretOffset = Editor.CaretOffset;
                if (caretOffset <= _linkTriggerPosition + 2)
                {
                    CloseLinkAutocomplete();
                }
                else
                {
                    // Update suggestions after backspace
                    Dispatcher.BeginInvoke(new Action(UpdateLinkSuggestionsFromCaret), 
                        System.Windows.Threading.DispatcherPriority.Input);
                }
                break;
        }
    }
    
    /// <summary>
    /// Update link suggestions based on current caret position
    /// </summary>
    private void UpdateLinkSuggestionsFromCaret()
    {
        if (!_isLinkAutocompleteActive || _linkTriggerPosition < 0)
            return;
        
        var caretOffset = Editor.CaretOffset;
        var searchLength = caretOffset - _linkTriggerPosition - 2; // -2 for [[
        
        if (searchLength < 0)
        {
            CloseLinkAutocomplete();
            return;
        }
        
        var searchText = searchLength > 0 
            ? Editor.Document.GetText(_linkTriggerPosition + 2, searchLength)
            : "";
        
        UpdateLinkSuggestions(searchText);
    }
    
    /// <summary>
    /// Update the autocomplete suggestions list
    /// </summary>
    private void UpdateLinkSuggestions(string searchText)
    {
        if (_noteService == null || _linkAutocompletePopup == null || _viewModel == null)
            return;
        
        var allNotes = _noteService.GetAllNotes()
            .Where(n => n.Id != _viewModel.Id) // Exclude current note
            .Where(n => string.IsNullOrEmpty(searchText) || 
                        n.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(n => new NoteSuggestion
            {
                NoteId = n.Id,
                Title = n.Title,
                Preview = GetNotePreview(n.Content, 50)
            })
            .ToList();
        
        if (allNotes.Count > 0)
        {
            // Position popup near caret
            var caretPosition = Editor.TextArea.Caret.CalculateCaretRectangle();
            var screenPos = Editor.TextArea.PointToScreen(caretPosition.Location);
            _linkAutocompletePopup.PositionAt(screenPos);
            _linkAutocompletePopup.UpdateSuggestions(allNotes);
        }
        else
        {
            _linkAutocompletePopup.Hide();
        }
    }
    
    /// <summary>
    /// Get a preview of note content
    /// </summary>
    private static string GetNotePreview(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
            return "(empty)";
        
        var preview = content.Replace("\r\n", " ").Replace("\n", " ").Trim();
        if (preview.Length > maxLength)
            preview = preview[..maxLength] + "...";
        
        return preview;
    }
    
    /// <summary>
    /// Handle note selection from autocomplete (Requirements 7.2)
    /// </summary>
    private void OnLinkNoteSelected(object? sender, NoteSuggestion suggestion)
    {
        if (_linkService == null || _linkTriggerPosition < 0)
            return;
        
        // Create link markup
        var linkMarkup = _linkService.CreateLinkMarkup(suggestion.NoteId, suggestion.Title);
        
        // Calculate text to replace (from [[ to current caret)
        var caretOffset = Editor.CaretOffset;
        var replaceLength = caretOffset - _linkTriggerPosition;
        
        // Replace the [[ and search text with the link
        Editor.Document.Replace(_linkTriggerPosition, replaceLength, linkMarkup);
        
        // Move caret to end of inserted link
        Editor.CaretOffset = _linkTriggerPosition + linkMarkup.Length;
        
        CloseLinkAutocomplete();
    }
    
    /// <summary>
    /// Handle autocomplete cancellation
    /// </summary>
    private void OnLinkAutocompleteCancelled(object? sender, EventArgs e)
    {
        CloseLinkAutocomplete();
    }
    
    /// <summary>
    /// Close the link autocomplete popup
    /// </summary>
    private void CloseLinkAutocomplete()
    {
        _isLinkAutocompleteActive = false;
        _linkTriggerPosition = -1;
        _linkAutocompletePopup?.Hide();
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
        if (_monitorService == null) return;

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
        if (sender is not MenuItem menuItem || _monitorService == null || _viewModel == null)
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
            
            Editor.Text = vm.Content;
            Editor.TextChanged += (_, _) => 
            {
                if (_viewModel != null)
                    _viewModel.Content = Editor.Text;
                
                // Update markdown preview if visible (Requirements 4.3)
                if (_isPreviewVisible)
                    UpdateMarkdownPreview();
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
                    UpdatePreviewButtonVisibility();
                }
            };
            
            // Update preview button visibility based on initial language
            UpdatePreviewButtonVisibility();
            
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
                    var isDarkTheme = _themeService?.CurrentTheme == Models.Theme.Dark;
                    if (isDarkTheme != false)
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

    #region Markdown Preview (Requirements 4.1, 4.2, 4.3, 4.6, 4.7)

    /// <summary>
    /// Toggle markdown preview visibility (Requirements 4.2)
    /// </summary>
    private void BtnPreview_Click(object sender, RoutedEventArgs e)
    {
        _isPreviewVisible = !_isPreviewVisible;
        UpdatePreviewVisibility();
        
        if (_isPreviewVisible)
        {
            // Initial render
            UpdateMarkdownPreview();
        }
    }

    /// <summary>
    /// Update the preview panel visibility and layout
    /// </summary>
    private void UpdatePreviewVisibility()
    {
        if (_isPreviewVisible)
        {
            // Show split view: editor on left, preview on right
            EditorColumn.Width = new GridLength(1, GridUnitType.Star);
            SplitterColumn.Width = new GridLength(4);
            PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
            PreviewSplitter.Visibility = Visibility.Visible;
            MarkdownPreview.Visibility = Visibility.Visible;
            BtnPreview.ToolTip = "Hide Preview";
        }
        else
        {
            // Hide preview, editor takes full width
            EditorColumn.Width = new GridLength(1, GridUnitType.Star);
            SplitterColumn.Width = new GridLength(0);
            PreviewColumn.Width = new GridLength(0);
            PreviewSplitter.Visibility = Visibility.Collapsed;
            MarkdownPreview.Visibility = Visibility.Collapsed;
            BtnPreview.ToolTip = "Show Preview";
        }
    }

    /// <summary>
    /// Update the preview button visibility based on language (Requirements 4.1)
    /// </summary>
    private void UpdatePreviewButtonVisibility()
    {
        // Show preview and export buttons only for Markdown language
        var isMarkdown = _currentLanguage.Equals("Markdown", StringComparison.OrdinalIgnoreCase);
        BtnPreview.Visibility = isMarkdown ? Visibility.Visible : Visibility.Collapsed;
        BtnExport.Visibility = isMarkdown ? Visibility.Visible : Visibility.Collapsed;
        
        // Hide preview if language changed away from Markdown
        if (!isMarkdown && _isPreviewVisible)
        {
            _isPreviewVisible = false;
            UpdatePreviewVisibility();
        }
    }

    /// <summary>
    /// Update markdown preview with debounce (Requirements 4.3)
    /// </summary>
    private void UpdateMarkdownPreview()
    {
        if (!_isPreviewVisible || _debounceService == null)
            return;

        _debounceService.Debounce(PreviewDebounceKey, () =>
        {
            Dispatcher.Invoke(() =>
            {
                var content = Editor.Text;
                MarkdownPreview.UpdateContent(content);
            });
        }, PreviewDebounceMs);
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
        // External links are handled by the MarkdownPreviewControl
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
        if (_markdownService == null) return;

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
                    CurrentTheme = _themeService?.CurrentTheme ?? Theme.Dark
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
        if (_markdownService == null) return;

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
        else if (e.Key == Key.Tab && _activePlaceholders != null && _activePlaceholders.Count > 0)
        {
            // Tab navigation between placeholders (Requirements 3.6)
            NavigateToNextPlaceholder(Keyboard.Modifiers == ModifierKeys.Shift);
            e.Handled = true;
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
        if (_themeService != null)
        {
            _themeService.ThemeChanged -= OnThemeChanged;
        }
        
        // Unsubscribe from monitor changes
        if (_monitorService != null)
        {
            _monitorService.MonitorsChanged -= OnMonitorsChanged;
        }
        
        // Cancel any pending preview updates
        _debounceService?.Cancel(PreviewDebounceKey);
        
        // Unsubscribe from markdown preview events
        MarkdownPreview.NoteLinkClicked -= OnNoteLinkClicked;
        MarkdownPreview.ExternalLinkClicked -= OnExternalLinkClicked;
        
        // Clean up link autocomplete (Requirements 7.1, 7.2)
        if (_linkAutocompletePopup != null)
        {
            _linkAutocompletePopup.NoteSelected -= OnLinkNoteSelected;
            _linkAutocompletePopup.Cancelled -= OnLinkAutocompleteCancelled;
            _linkAutocompletePopup.Close();
        }
        
        // Unsubscribe from editor events
        Editor.TextArea.TextEntered -= OnTextEntered;
        Editor.TextArea.PreviewKeyDown -= OnEditorPreviewKeyDown;
        Editor.TextArea.MouseMove -= OnEditorMouseMove;
        Editor.TextArea.MouseLeave -= OnEditorMouseLeave;
        Editor.TextArea.PreviewMouseLeftButtonDown -= OnEditorMouseLeftButtonDown;
        
        // Unsubscribe from backlinks panel events (Requirements 7.6, 7.7)
        BacklinksPanel.BacklinkClicked -= OnBacklinkClicked;
        
        base.OnClosed(e);
    }

    #region Snippet Integration (Requirements 3.1, 3.3, 3.4, 3.6)

    /// <summary>
    /// Save selected text as a snippet (Requirements 3.1)
    /// </summary>
    private void SaveSelectionAsSnippet()
    {
        var selectedText = Editor.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
        {
            CustomDialog.ShowInfo("Save Snippet", "Please select some text to save as a snippet.");
            return;
        }

        var dialog = new SaveSnippetDialog(selectedText, _currentLanguage)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.CreatedSnippet != null)
        {
            CustomDialog.ShowSuccess("Snippet Saved", 
                $"Snippet '{dialog.CreatedSnippet.Name}' has been saved.");
        }
    }

    /// <summary>
    /// Open snippet browser for insertion (Requirements 3.3)
    /// </summary>
    private void OpenSnippetBrowser()
    {
        var browser = new SnippetBrowserWindow
        {
            Owner = this
        };
        
        browser.SnippetInsertRequested += OnSnippetInsertRequested;
        browser.ShowDialog();
        browser.SnippetInsertRequested -= OnSnippetInsertRequested;
    }

    /// <summary>
    /// Handle snippet insertion request (Requirements 3.4)
    /// </summary>
    private async void OnSnippetInsertRequested(object? sender, Snippet snippet)
    {
        if (_snippetService == null) return;

        // Expand snippet (replace placeholders with default values for now)
        var expandedContent = await _snippetService.ExpandSnippetAsync(snippet);
        
        // Get current caret position
        var caretOffset = Editor.CaretOffset;
        
        // Insert the snippet content
        Editor.Document.Insert(caretOffset, expandedContent);
        
        // If snippet has placeholders, set up tab navigation
        if (snippet.Placeholders.Count > 0)
        {
            SetupPlaceholderNavigation(snippet, caretOffset);
        }
    }

    /// <summary>
    /// Set up placeholder navigation after snippet insertion (Requirements 3.6)
    /// </summary>
    private void SetupPlaceholderNavigation(Snippet snippet, int insertOffset)
    {
        // Calculate actual positions of placeholders in the expanded content
        _activePlaceholders = new List<SnippetPlaceholder>();
        var content = snippet.Content;
        var expandedContent = Editor.Document.GetText(insertOffset, content.Length);
        
        // Parse placeholders from the original content to find their positions
        var placeholderRegex = new Regex(@"\$\{(\d+):([^:}]+)(?::([^}]*))?\}");
        var matches = placeholderRegex.Matches(content);
        
        int offsetAdjustment = 0;
        foreach (Match match in matches.OrderBy(m => m.Index))
        {
            var index = int.Parse(match.Groups[1].Value);
            var name = match.Groups[2].Value;
            var defaultValue = match.Groups[3].Success ? match.Groups[3].Value : name;
            
            var placeholder = new SnippetPlaceholder
            {
                Index = index,
                Name = name,
                DefaultValue = defaultValue,
                StartPosition = insertOffset + match.Index - offsetAdjustment,
                Length = defaultValue.Length
            };
            
            _activePlaceholders.Add(placeholder);
            
            // Adjust for the difference between placeholder syntax and default value
            offsetAdjustment += match.Length - defaultValue.Length;
        }

        // Sort by index for tab navigation
        _activePlaceholders = _activePlaceholders.OrderBy(p => p.Index).ToList();
        
        // Navigate to first placeholder
        if (_activePlaceholders.Count > 0)
        {
            _currentPlaceholderIndex = -1;
            NavigateToNextPlaceholder(false);
        }
    }

    /// <summary>
    /// Navigate to next/previous placeholder (Requirements 3.6)
    /// </summary>
    private void NavigateToNextPlaceholder(bool reverse)
    {
        if (_activePlaceholders == null || _activePlaceholders.Count == 0)
            return;

        if (reverse)
        {
            _currentPlaceholderIndex--;
            if (_currentPlaceholderIndex < 0)
                _currentPlaceholderIndex = _activePlaceholders.Count - 1;
        }
        else
        {
            _currentPlaceholderIndex++;
            if (_currentPlaceholderIndex >= _activePlaceholders.Count)
            {
                // Exit placeholder mode after last placeholder
                ClearPlaceholderNavigation();
                return;
            }
        }

        var placeholder = _activePlaceholders[_currentPlaceholderIndex];
        
        // Select the placeholder text
        try
        {
            Editor.Select(placeholder.StartPosition, placeholder.Length);
            Editor.ScrollTo(Editor.TextArea.Caret.Line, Editor.TextArea.Caret.Column);
        }
        catch
        {
            // Position might be invalid if text was modified
            ClearPlaceholderNavigation();
        }
    }

    /// <summary>
    /// Clear placeholder navigation state
    /// </summary>
    private void ClearPlaceholderNavigation()
    {
        _activePlaceholders = null;
        _currentPlaceholderIndex = -1;
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
