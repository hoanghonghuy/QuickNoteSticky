using System.Collections.Concurrent;
using System.Windows;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for managing note windows
/// </summary>
public class WindowService : IWindowService
{
    private readonly ConcurrentDictionary<Guid, Window> _windows = new();
    private readonly Func<Note, Window> _windowFactory;

    public WindowService(Func<Note, Window> windowFactory)
    {
        _windowFactory = windowFactory;
    }

    public void ShowNote(Note note)
    {
        if (_windows.TryGetValue(note.Id, out var existingWindow))
        {
            existingWindow.Show();
            existingWindow.Activate();
            return;
        }

        var window = _windowFactory(note);
        window.Left = note.WindowRect.Left;
        window.Top = note.WindowRect.Top;
        window.Width = note.WindowRect.Width;
        window.Height = note.WindowRect.Height;
        window.Topmost = note.IsPinned;
        window.Opacity = note.Opacity;

        window.Closed += (_, _) => _windows.TryRemove(note.Id, out _);
        
        _windows[note.Id] = window;
        window.Show();
    }

    public void CloseNote(Guid id)
    {
        if (_windows.TryRemove(id, out var window))
        {
            try
            {
                if (window.IsLoaded)
                    window.Close();
            }
            catch { /* Window may already be closed */ }
        }
    }

    public void ShowAllNotes()
    {
        foreach (var window in _windows.Values)
        {
            window.Show();
            window.Activate();
        }
    }

    public void HideAllNotes()
    {
        foreach (var window in _windows.Values)
        {
            window.Hide();
        }
    }

    public void ToggleAllNotesVisibility()
    {
        var anyVisible = _windows.Values.Any(w => w.IsVisible);
        if (anyVisible)
            HideAllNotes();
        else
            ShowAllNotes();
    }

    public void UpdateWindowRect(Guid id, WindowRect rect)
    {
        if (_windows.TryGetValue(id, out var window))
        {
            window.Left = rect.Left;
            window.Top = rect.Top;
            window.Width = rect.Width;
            window.Height = rect.Height;
        }
    }

    public void UpdateTopmost(Guid id, bool topmost)
    {
        if (_windows.TryGetValue(id, out var window))
        {
            window.Topmost = topmost;
        }
    }

    public void UpdateOpacity(Guid id, double opacity)
    {
        if (_windows.TryGetValue(id, out var window))
        {
            window.Opacity = opacity;
        }
    }
}
