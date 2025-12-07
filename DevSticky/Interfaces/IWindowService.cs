using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing note windows
/// </summary>
public interface IWindowService
{
    void ShowNote(Note note);
    void CloseNote(Guid id);
    void ShowAllNotes();
    void HideAllNotes();
    void ToggleAllNotesVisibility();
    
    /// <summary>
    /// Gets the monitor device ID for a note window's current position
    /// </summary>
    string? GetMonitorDeviceIdForNote(Guid noteId);
    
    /// <summary>
    /// Moves a note window to a specific monitor
    /// </summary>
    void MoveNoteToMonitor(Guid noteId, string monitorDeviceId);
    
    /// <summary>
    /// Ensures a note window is positioned within visible screen bounds
    /// </summary>
    void EnsureNoteVisible(Guid noteId);
}
