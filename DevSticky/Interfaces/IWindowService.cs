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
}
