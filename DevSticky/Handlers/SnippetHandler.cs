using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Views;
using ICSharpCode.AvalonEdit;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DevSticky.Handlers;

/// <summary>
/// Handles snippet insertion and placeholder navigation for NoteWindow (Requirements 2.3)
/// Manages snippet expansion, placeholder tab-stop navigation, and snippet browser integration
/// </summary>
public class SnippetHandler : IDisposable
{
    private readonly ISnippetService _snippetService;
    
    private TextEditor? _editor;
    private Window? _ownerWindow;
    private string _currentLanguage = "PlainText";
    
    // Placeholder navigation state (Requirements 3.6)
    private List<SnippetPlaceholder>? _activePlaceholders;
    private int _currentPlaceholderIndex = -1;
    
    /// <summary>
    /// Gets whether placeholder navigation is currently active
    /// </summary>
    public bool IsPlaceholderNavigationActive => _activePlaceholders != null && _activePlaceholders.Count > 0;
    
    /// <summary>
    /// Creates a new SnippetHandler
    /// </summary>
    /// <param name="snippetService">Service for snippet operations</param>
    public SnippetHandler(ISnippetService snippetService)
    {
        _snippetService = snippetService;
    }
    
    /// <summary>
    /// Initialize the handler with the editor
    /// </summary>
    /// <param name="editor">The AvalonEdit TextEditor</param>
    /// <param name="ownerWindow">The owner window for dialogs</param>
    public void Initialize(TextEditor editor, Window ownerWindow)
    {
        _editor = editor;
        _ownerWindow = ownerWindow;
    }
    
    /// <summary>
    /// Set the current language for snippet filtering
    /// </summary>
    /// <param name="language">The current language</param>
    public void SetLanguage(string language)
    {
        _currentLanguage = language;
    }

    
    #region Snippet Saving (Requirements 3.1)
    
    /// <summary>
    /// Save selected text as a snippet (Requirements 3.1)
    /// </summary>
    public void SaveSelectionAsSnippet()
    {
        if (_editor == null || _ownerWindow == null)
            return;
        
        var selectedText = _editor.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
        {
            CustomDialog.ShowInfo("Save Snippet", "Please select some text to save as a snippet.");
            return;
        }

        var dialog = new SaveSnippetDialog(selectedText, _currentLanguage)
        {
            Owner = _ownerWindow
        };

        if (dialog.ShowDialog() == true && dialog.CreatedSnippet != null)
        {
            CustomDialog.ShowSuccess("Snippet Saved", 
                $"Snippet '{dialog.CreatedSnippet.Name}' has been saved.");
        }
    }
    
    #endregion
    
    #region Snippet Browser (Requirements 3.3)
    
    /// <summary>
    /// Open snippet browser for insertion (Requirements 3.3)
    /// </summary>
    public void OpenSnippetBrowser()
    {
        if (_ownerWindow == null)
            return;
        
        var browser = new SnippetBrowserWindow
        {
            Owner = _ownerWindow
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
        await InsertSnippetAsync(snippet);
    }
    
    #endregion
    
    #region Snippet Insertion (Requirements 3.4)
    
    /// <summary>
    /// Insert a snippet at the current caret position (Requirements 3.4)
    /// </summary>
    /// <param name="snippet">The snippet to insert</param>
    public async Task InsertSnippetAsync(Snippet snippet)
    {
        if (_editor == null)
            return;
        
        try
        {
            // Expand snippet (replace placeholders with default values for now)
            var expandedContent = await _snippetService.ExpandSnippetAsync(snippet);
            
            // Get current caret position
            var caretOffset = _editor.CaretOffset;
            
            // Insert the snippet content
            _editor.Document.Insert(caretOffset, expandedContent);
            
            // If snippet has placeholders, set up tab navigation
            if (snippet.Placeholders.Count > 0)
            {
                SetupPlaceholderNavigation(snippet, caretOffset);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to insert snippet: {ex.Message}");
            CustomDialog.ShowError("Snippet Error", $"Failed to insert snippet: {ex.Message}");
        }
    }
    
    #endregion
    
    #region Placeholder Navigation (Requirements 3.6)
    
    /// <summary>
    /// Set up placeholder navigation after snippet insertion (Requirements 3.6)
    /// </summary>
    private void SetupPlaceholderNavigation(Snippet snippet, int insertOffset)
    {
        if (_editor == null)
            return;
        
        // Calculate actual positions of placeholders in the expanded content
        _activePlaceholders = new List<SnippetPlaceholder>();
        var content = snippet.Content;
        
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
    /// Handle Tab key for placeholder navigation (Requirements 3.6)
    /// </summary>
    /// <param name="e">The key event args</param>
    /// <returns>True if the key was handled, false otherwise</returns>
    public bool HandleKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Tab && IsPlaceholderNavigationActive)
        {
            NavigateToNextPlaceholder(Keyboard.Modifiers == ModifierKeys.Shift);
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Navigate to next/previous placeholder (Requirements 3.6)
    /// </summary>
    /// <param name="reverse">True to navigate backwards</param>
    public void NavigateToNextPlaceholder(bool reverse)
    {
        if (_activePlaceholders == null || _activePlaceholders.Count == 0 || _editor == null)
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
            _editor.Select(placeholder.StartPosition, placeholder.Length);
            _editor.ScrollTo(_editor.TextArea.Caret.Line, _editor.TextArea.Caret.Column);
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
    public void ClearPlaceholderNavigation()
    {
        _activePlaceholders = null;
        _currentPlaceholderIndex = -1;
    }
    
    #endregion
    
    #region IDisposable
    
    public void Dispose()
    {
        ClearPlaceholderNavigation();
    }
    
    #endregion
}
