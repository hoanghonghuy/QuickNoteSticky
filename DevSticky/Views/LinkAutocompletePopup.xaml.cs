using System.Windows;
using System.Windows.Input;
using DevSticky.Models;

namespace DevSticky.Views;

/// <summary>
/// Autocomplete popup for note linking (Requirements 7.1, 7.2)
/// Shows matching note titles when user types [[
/// </summary>
public partial class LinkAutocompletePopup : Window
{
    /// <summary>
    /// Event raised when a note is selected for linking
    /// </summary>
    public event EventHandler<NoteSuggestion>? NoteSelected;

    /// <summary>
    /// Event raised when the popup should be closed without selection
    /// </summary>
    public event EventHandler? Cancelled;

    private int _selectedIndex = -1;

    public LinkAutocompletePopup()
    {
        InitializeComponent();
        Deactivated += (_, _) => Hide();
    }

    /// <summary>
    /// Update the suggestions list with matching notes
    /// </summary>
    public void UpdateSuggestions(IEnumerable<NoteSuggestion> suggestions)
    {
        var suggestionList = suggestions.ToList();
        SuggestionsList.ItemsSource = suggestionList;
        
        if (suggestionList.Count > 0)
        {
            _selectedIndex = 0;
            SuggestionsList.SelectedIndex = 0;
            Show();
        }
        else
        {
            Hide();
        }
    }

    /// <summary>
    /// Move selection up in the list
    /// </summary>
    public void MoveSelectionUp()
    {
        if (SuggestionsList.Items.Count == 0) return;
        
        _selectedIndex--;
        if (_selectedIndex < 0)
            _selectedIndex = SuggestionsList.Items.Count - 1;
        
        SuggestionsList.SelectedIndex = _selectedIndex;
        SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
    }

    /// <summary>
    /// Move selection down in the list
    /// </summary>
    public void MoveSelectionDown()
    {
        if (SuggestionsList.Items.Count == 0) return;
        
        _selectedIndex++;
        if (_selectedIndex >= SuggestionsList.Items.Count)
            _selectedIndex = 0;
        
        SuggestionsList.SelectedIndex = _selectedIndex;
        SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
    }

    /// <summary>
    /// Confirm the current selection
    /// </summary>
    public void ConfirmSelection()
    {
        if (SuggestionsList.SelectedItem is NoteSuggestion suggestion)
        {
            NoteSelected?.Invoke(this, suggestion);
            Hide();
        }
    }

    /// <summary>
    /// Cancel and close the popup
    /// </summary>
    public void Cancel()
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    private void SuggestionsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedIndex = SuggestionsList.SelectedIndex;
    }

    private void SuggestionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    /// <summary>
    /// Position the popup relative to the caret position
    /// </summary>
    public void PositionAt(System.Windows.Point screenPosition)
    {
        Left = screenPosition.X;
        Top = screenPosition.Y + 20; // Offset below the caret
        
        // Ensure popup stays on screen
        var screen = System.Windows.Forms.Screen.FromPoint(
            new System.Drawing.Point((int)Left, (int)Top));
        var workingArea = screen.WorkingArea;
        
        if (Left + Width > workingArea.Right)
            Left = workingArea.Right - Width;
        if (Top + MaxHeight > workingArea.Bottom)
            Top = screenPosition.Y - MaxHeight - 5; // Show above caret
    }
}

/// <summary>
/// Represents a note suggestion in the autocomplete list
/// </summary>
public class NoteSuggestion
{
    public Guid NoteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
}
