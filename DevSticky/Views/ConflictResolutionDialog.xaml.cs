using System.Windows;
using DevSticky.Interfaces;
using DevSticky.Models;
using Application = System.Windows.Application;

namespace DevSticky.Views;

/// <summary>
/// Dialog for resolving sync conflicts between local and remote note versions.
/// </summary>
public partial class ConflictResolutionDialog : Window
{
    /// <summary>
    /// Gets the resolution chosen by the user.
    /// </summary>
    public SyncConflictResolution Resolution { get; private set; } = SyncConflictResolution.None;

    /// <summary>
    /// Gets the local note version.
    /// </summary>
    public Note LocalNote { get; }

    /// <summary>
    /// Gets the remote note version.
    /// </summary>
    public Note RemoteNote { get; }

    /// <summary>
    /// Creates a new ConflictResolutionDialog.
    /// </summary>
    /// <param name="localNote">The local version of the note.</param>
    /// <param name="remoteNote">The remote version of the note.</param>
    public ConflictResolutionDialog(Note localNote, Note remoteNote)
    {
        LocalNote = localNote ?? throw new ArgumentNullException(nameof(localNote));
        RemoteNote = remoteNote ?? throw new ArgumentNullException(nameof(remoteNote));

        InitializeComponent();
        SetupDialog();
    }

    private void SetupDialog()
    {
        NoteTitle.Text = $"Note: {LocalNote.Title}";
        
        LocalModifiedText.Text = $"Modified: {LocalNote.ModifiedDate:g}";
        RemoteModifiedText.Text = $"Modified: {RemoteNote.ModifiedDate:g}";
        
        LocalContentText.Text = TruncateContent(LocalNote.Content, 2000);
        RemoteContentText.Text = TruncateContent(RemoteNote.Content, 2000);
    }

    private static string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
            return "(empty)";
        
        if (content.Length <= maxLength)
            return content;
        
        return content[..maxLength] + "\n\n... (content truncated)";
    }

    private void KeepLocalBtn_Click(object sender, RoutedEventArgs e)
    {
        Resolution = SyncConflictResolution.KeepLocal;
        DialogResult = true;
        Close();
    }

    private void KeepRemoteBtn_Click(object sender, RoutedEventArgs e)
    {
        Resolution = SyncConflictResolution.KeepRemote;
        DialogResult = true;
        Close();
    }

    private void MergeBtn_Click(object sender, RoutedEventArgs e)
    {
        Resolution = SyncConflictResolution.Merge;
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        Resolution = SyncConflictResolution.None;
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Shows the conflict resolution dialog and returns the user's choice.
    /// </summary>
    /// <param name="localNote">The local version of the note.</param>
    /// <param name="remoteNote">The remote version of the note.</param>
    /// <param name="owner">Optional owner window.</param>
    /// <returns>The resolution chosen by the user.</returns>
    public static SyncConflictResolution ShowDialog(Note localNote, Note remoteNote, Window? owner = null)
    {
        var dialog = new ConflictResolutionDialog(localNote, remoteNote);
        
        if (owner != null)
            dialog.Owner = owner;
        else if (Application.Current.MainWindow?.IsVisible == true)
            dialog.Owner = Application.Current.MainWindow;
        
        dialog.ShowDialog();
        return dialog.Resolution;
    }
}
