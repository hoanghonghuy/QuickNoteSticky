using System.Windows;
using System.Windows.Input;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Views;

/// <summary>
/// Panel showing all notes that link to the current note (Requirements 7.6, 7.7)
/// </summary>
public partial class BacklinksPanel : System.Windows.Controls.UserControl
{
    /// <summary>
    /// Event raised when a backlink is clicked to navigate
    /// </summary>
    public event EventHandler<Guid>? BacklinkClicked;

    private ILinkService? _linkService;
    private INoteService? _noteService;
    private Guid _currentNoteId;

    public BacklinksPanel()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// Initialize services for the backlinks panel.
    /// Call this method after construction to inject services.
    /// </summary>
    public void Initialize(ILinkService linkService, INoteService noteService)
    {
        _linkService = linkService;
        _noteService = noteService;
    }

    /// <summary>
    /// Update the backlinks list for a specific note
    /// </summary>
    public void UpdateBacklinks(Guid noteId)
    {
        _currentNoteId = noteId;
        
        if (_linkService == null || _noteService == null)
        {
            ShowEmptyState();
            return;
        }

        var backlinks = _linkService.GetBacklinksToNote(noteId);
        
        if (backlinks.Count == 0)
        {
            ShowEmptyState();
            return;
        }

        var backlinkItems = backlinks
            .Select(link =>
            {
                var sourceNote = _noteService.GetNoteById(link.SourceNoteId);
                return sourceNote != null ? new BacklinkItem
                {
                    NoteId = link.SourceNoteId,
                    Title = sourceNote.Title,
                    Preview = GetNotePreview(sourceNote.Content, 50)
                } : null;
            })
            .Where(item => item != null)
            .Distinct(new BacklinkItemComparer())
            .ToList();

        if (backlinkItems.Count == 0)
        {
            ShowEmptyState();
            return;
        }

        BacklinksList.ItemsSource = backlinkItems;
        CountText.Text = $"({backlinkItems.Count})";
        BacklinksList.Visibility = Visibility.Visible;
        EmptyText.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Show empty state when no backlinks exist
    /// </summary>
    private void ShowEmptyState()
    {
        BacklinksList.ItemsSource = null;
        CountText.Text = "(0)";
        BacklinksList.Visibility = Visibility.Collapsed;
        EmptyText.Visibility = Visibility.Visible;
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
    /// Handle double-click to navigate to the backlinked note
    /// </summary>
    private void BacklinksList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (BacklinksList.SelectedItem is BacklinkItem item)
        {
            BacklinkClicked?.Invoke(this, item.NoteId);
        }
    }
}

/// <summary>
/// Represents a backlink item in the list
/// </summary>
public class BacklinkItem
{
    public Guid NoteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
}

/// <summary>
/// Comparer to ensure unique backlink items by NoteId
/// </summary>
internal class BacklinkItemComparer : IEqualityComparer<BacklinkItem?>
{
    public bool Equals(BacklinkItem? x, BacklinkItem? y)
    {
        if (x == null && y == null) return true;
        if (x == null || y == null) return false;
        return x.NoteId == y.NoteId;
    }

    public int GetHashCode(BacklinkItem? obj)
    {
        return obj?.NoteId.GetHashCode() ?? 0;
    }
}
