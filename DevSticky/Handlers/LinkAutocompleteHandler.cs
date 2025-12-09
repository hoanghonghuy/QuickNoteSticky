using System.Windows;
using System.Windows.Input;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Views;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace DevSticky.Handlers;

/// <summary>
/// Handles link autocomplete functionality for NoteWindow (Requirements 2.1)
/// Manages the [[ trigger detection, suggestion display, and link insertion
/// </summary>
public class LinkAutocompleteHandler : IDisposable
{
    private readonly INoteService _noteService;
    private readonly ILinkService _linkService;
    
    private TextEditor? _editor;
    private LinkAutocompletePopup? _popup;
    private Guid _currentNoteId;
    
    // Link autocomplete state
    private bool _isActive;
    private int _triggerPosition = -1;
    
    // Link tooltip state (Requirements 7.4)
    private System.Windows.Controls.ToolTip? _linkTooltip;
    private NoteLink? _hoveredLink;
    
    /// <summary>
    /// Event raised when a note is selected for navigation (Ctrl+Click)
    /// </summary>
    public event EventHandler<Guid>? NoteNavigationRequested;
    
    /// <summary>
    /// Creates a new LinkAutocompleteHandler
    /// </summary>
    /// <param name="noteService">Service for accessing notes</param>
    /// <param name="linkService">Service for link operations</param>
    public LinkAutocompleteHandler(INoteService noteService, ILinkService linkService)
    {
        _noteService = noteService;
        _linkService = linkService;
    }
    
    /// <summary>
    /// Initialize the handler with the editor and current note ID
    /// </summary>
    /// <param name="editor">The AvalonEdit TextEditor</param>
    /// <param name="currentNoteId">The ID of the note being edited</param>
    public void Initialize(TextEditor editor, Guid currentNoteId)
    {
        _editor = editor;
        _currentNoteId = currentNoteId;

        // Create popup
        _popup = new LinkAutocompletePopup();
        _popup.NoteSelected += OnPopupNoteSelected;
        _popup.Cancelled += OnPopupCancelled;
        
        // Wire up text input for [[ detection
        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.PreviewKeyDown += OnPreviewKeyDown;
        
        // Initialize link tooltip (Requirements 7.4)
        InitializeLinkTooltip();
    }
    
    /// <summary>
    /// Update the current note ID (when note changes)
    /// </summary>
    public void SetCurrentNoteId(Guid noteId)
    {
        _currentNoteId = noteId;
    }
    
    /// <summary>
    /// Initialize the link tooltip for hover preview (Requirements 7.4)
    /// </summary>
    private void InitializeLinkTooltip()
    {
        if (_editor == null) return;
        
        _linkTooltip = new System.Windows.Controls.ToolTip
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse,
            HasDropShadow = true
        };
        
        // Wire up mouse events for link detection
        _editor.TextArea.MouseMove += OnEditorMouseMove;
        _editor.TextArea.MouseLeave += OnEditorMouseLeave;
        _editor.TextArea.PreviewMouseLeftButtonDown += OnEditorMouseLeftButtonDown;
    }
    
    #region Text Entry and Autocomplete
    
    /// <summary>
    /// Handle text entered to detect [[ trigger (Requirements 7.1)
    /// </summary>
    private void OnTextEntered(object sender, TextCompositionEventArgs e)
    {
        if (_editor == null) return;
        
        if (e.Text == "[")
        {
            // Check if previous character is also [
            var caretOffset = _editor.CaretOffset;
            if (caretOffset >= 2)
            {
                var prevChar = _editor.Document.GetText(caretOffset - 2, 1);
                if (prevChar == "[")
                {
                    // Trigger autocomplete
                    _isActive = true;
                    _triggerPosition = caretOffset - 2;
                    UpdateSuggestions("");
                }
            }
        }
        else if (_isActive)
        {
            // Update suggestions based on typed text
            UpdateSuggestionsFromCaret();
        }
    }
    
    /// <summary>
    /// Handle key down for autocomplete navigation
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isActive || _popup == null || _editor == null)
            return;
        
        switch (e.Key)
        {
            case Key.Up:
                _popup.MoveSelectionUp();
                e.Handled = true;
                break;
            case Key.Down:
                _popup.MoveSelectionDown();
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Tab:
                _popup.ConfirmSelection();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Back:
                // Check if we should close autocomplete
                var caretOffset = _editor.CaretOffset;
                if (caretOffset <= _triggerPosition + 2)
                {
                    Close();
                }
                else
                {
                    // Update suggestions after backspace
                    _editor.Dispatcher.BeginInvoke(new Action(UpdateSuggestionsFromCaret),
                        System.Windows.Threading.DispatcherPriority.Input);
                }
                break;
        }
    }
    
    /// <summary>
    /// Update link suggestions based on current caret position
    /// </summary>
    private void UpdateSuggestionsFromCaret()
    {
        if (!_isActive || _triggerPosition < 0 || _editor == null)
            return;
        
        var caretOffset = _editor.CaretOffset;
        var searchLength = caretOffset - _triggerPosition - 2; // -2 for [[
        
        if (searchLength < 0)
        {
            Close();
            return;
        }
        
        var searchText = searchLength > 0
            ? _editor.Document.GetText(_triggerPosition + 2, searchLength)
            : "";
        
        UpdateSuggestions(searchText);
    }
    
    /// <summary>
    /// Update the autocomplete suggestions list
    /// </summary>
    private void UpdateSuggestions(string searchText)
    {
        if (_popup == null || _editor == null)
            return;
        
        var allNotes = _noteService.GetAllNotes()
            .Where(n => n.Id != _currentNoteId) // Exclude current note
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
            var caretPosition = _editor.TextArea.Caret.CalculateCaretRectangle();
            var screenPos = _editor.TextArea.PointToScreen(caretPosition.Location);
            _popup.PositionAt(screenPos);
            _popup.UpdateSuggestions(allNotes);
        }
        else
        {
            _popup.Hide();
        }
    }
    
    /// <summary>
    /// Handle note selection from autocomplete (Requirements 7.2)
    /// </summary>
    private void OnPopupNoteSelected(object? sender, NoteSuggestion suggestion)
    {
        if (_triggerPosition < 0 || _editor == null)
            return;
        
        // Create link markup
        var linkMarkup = _linkService.CreateLinkMarkup(suggestion.NoteId, suggestion.Title);
        
        // Calculate text to replace (from [[ to current caret)
        var caretOffset = _editor.CaretOffset;
        var replaceLength = caretOffset - _triggerPosition;
        
        // Replace the [[ and search text with the link
        _editor.Document.Replace(_triggerPosition, replaceLength, linkMarkup);
        
        // Move caret to end of inserted link
        _editor.CaretOffset = _triggerPosition + linkMarkup.Length;
        
        Close();
    }
    
    /// <summary>
    /// Handle autocomplete cancellation
    /// </summary>
    private void OnPopupCancelled(object? sender, EventArgs e)
    {
        Close();
    }
    
    /// <summary>
    /// Close the link autocomplete popup
    /// </summary>
    public void Close()
    {
        _isActive = false;
        _triggerPosition = -1;
        _popup?.Hide();
    }
    
    #endregion
    
    #region Link Tooltip and Navigation
    
    /// <summary>
    /// Handle mouse click to navigate to linked note (Requirements 7.3)
    /// </summary>
    private void OnEditorMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only handle Ctrl+Click for link navigation
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;
        
        if (_editor == null)
            return;
        
        try
        {
            // Get position from mouse
            var position = _editor.GetPositionFromPoint(e.GetPosition(_editor));
            if (position == null)
                return;
            
            var offset = _editor.Document.GetOffset(position.Value.Location);
            var content = _editor.Text;
            
            // Check if we're clicking on a link
            var link = FindLinkAtPosition(content, offset);
            
            if (link != null && !link.IsBroken)
            {
                // Raise navigation event
                NoteNavigationRequested?.Invoke(this, link.TargetNoteId);
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
    private void OnEditorMouseMove(object sender, MouseEventArgs e)
    {
        if (_editor == null)
            return;
        
        try
        {
            // Get position from mouse
            var position = _editor.GetPositionFromPoint(e.GetPosition(_editor));
            if (position == null)
            {
                HideLinkTooltip();
                return;
            }
            
            var offset = _editor.Document.GetOffset(position.Value.Location);
            var content = _editor.Text;
            
            // Check if we're hovering over a link
            var link = FindLinkAtPosition(content, offset);
            
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
    private void OnEditorMouseLeave(object sender, MouseEventArgs e)
    {
        HideLinkTooltip();
    }
    
    /// <summary>
    /// Find a link at the given position in the content
    /// </summary>
    private NoteLink? FindLinkAtPosition(string content, int offset)
    {
        var links = _linkService.ParseLinksFromContent(content, _currentNoteId);
        return links.FirstOrDefault(l => offset >= l.StartPosition && offset < l.StartPosition + l.Length);
    }
    
    /// <summary>
    /// Show tooltip for a link (Requirements 7.4)
    /// </summary>
    private void ShowLinkTooltip(NoteLink link)
    {
        if (_linkTooltip == null || _editor == null)
            return;
        
        var targetNote = _noteService.GetNoteById(link.TargetNoteId);
        if (targetNote == null)
            return;
        
        // Create tooltip content with title and preview (first 100 characters)
        var preview = GetNotePreview(targetNote.Content, 100);
        
        var tooltipContent = new System.Windows.Controls.StackPanel { MaxWidth = 300 };
        tooltipContent.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = targetNote.Title,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        tooltipContent.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = preview,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        tooltipContent.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Ctrl+Click to open",
            FontSize = 10,
            FontStyle = FontStyles.Italic,
            Margin = new Thickness(0, 4, 0, 0)
        });
        
        _linkTooltip.Content = tooltipContent;
        _linkTooltip.IsOpen = true;
        _editor.ToolTip = _linkTooltip;
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
        if (_editor != null)
        {
            _editor.ToolTip = null;
        }
    }
    
    #endregion
    
    #region Helpers
    
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
    
    #endregion
    
    #region IDisposable
    
    public void Dispose()
    {
        if (_editor != null)
        {
            _editor.TextArea.TextEntered -= OnTextEntered;
            _editor.TextArea.PreviewKeyDown -= OnPreviewKeyDown;
            _editor.TextArea.MouseMove -= OnEditorMouseMove;
            _editor.TextArea.MouseLeave -= OnEditorMouseLeave;
            _editor.TextArea.PreviewMouseLeftButtonDown -= OnEditorMouseLeftButtonDown;
        }
        
        if (_popup != null)
        {
            _popup.NoteSelected -= OnPopupNoteSelected;
            _popup.Cancelled -= OnPopupCancelled;
            _popup.Close();
        }
        
        HideLinkTooltip();
    }
    
    #endregion
}
