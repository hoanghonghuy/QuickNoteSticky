using System.Collections.Concurrent;
using System.Windows;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for managing note windows with multi-monitor support
/// </summary>
public class WindowService : IWindowService
{
    private readonly ConcurrentDictionary<Guid, Window> _windows = new();
    private readonly Func<Note, Window> _windowFactory;
    private readonly IMonitorService _monitorService;
    private readonly Action<Note>? _onNoteMonitorChanged;

    public WindowService(Func<Note, Window> windowFactory, IMonitorService monitorService, Action<Note>? onNoteMonitorChanged = null)
    {
        _windowFactory = windowFactory;
        _monitorService = monitorService;
        _onNoteMonitorChanged = onNoteMonitorChanged;
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
        
        // Restore note to correct monitor or fallback to primary
        RestoreNotePosition(window, note);
        
        window.Topmost = note.IsPinned;
        window.Opacity = note.Opacity;

        window.Closed += (_, _) => _windows.TryRemove(note.Id, out _);
        
        _windows[note.Id] = window;
        window.Show();
    }

    /// <summary>
    /// Restores a note window to its correct monitor position
    /// </summary>
    private void RestoreNotePosition(Window window, Note note)
    {
        // Check if the note has a stored monitor assignment
        if (!string.IsNullOrEmpty(note.MonitorDeviceId))
        {
            var targetMonitor = _monitorService.GetMonitorById(note.MonitorDeviceId);
            
            if (targetMonitor != null)
            {
                // Monitor is available - restore to stored position
                window.Left = note.WindowRect.Left;
                window.Top = note.WindowRect.Top;
                window.Width = note.WindowRect.Width;
                window.Height = note.WindowRect.Height;
                
                // Ensure the window is within the monitor's bounds
                EnsureWindowInMonitorBounds(window, targetMonitor);
            }
            else
            {
                // Monitor not available - fallback to primary monitor
                var primaryMonitor = _monitorService.GetPrimaryMonitor();
                MoveWindowToMonitorCenter(window, note, primaryMonitor);
                
                // Update the note's monitor assignment
                note.MonitorDeviceId = primaryMonitor.DeviceId;
                _onNoteMonitorChanged?.Invoke(note);
            }
        }
        else
        {
            // No monitor assignment - use stored position but ensure visibility
            window.Left = note.WindowRect.Left;
            window.Top = note.WindowRect.Top;
            window.Width = note.WindowRect.Width;
            window.Height = note.WindowRect.Height;
            
            // Ensure the window is visible on some monitor
            if (!_monitorService.IsPointVisible(window.Left, window.Top))
            {
                var nearestPoint = _monitorService.GetNearestVisiblePoint(window.Left, window.Top);
                window.Left = nearestPoint.X;
                window.Top = nearestPoint.Y;
            }
        }
    }

    private void EnsureWindowInMonitorBounds(Window window, MonitorInfo monitor)
    {
        var workingArea = monitor.WorkingArea;
        
        // Ensure window is within the monitor's working area
        if (window.Left < workingArea.Left)
            window.Left = workingArea.Left;
        if (window.Top < workingArea.Top)
            window.Top = workingArea.Top;
        if (window.Left + window.Width > workingArea.Right)
            window.Left = Math.Max(workingArea.Left, workingArea.Right - window.Width);
        if (window.Top + window.Height > workingArea.Bottom)
            window.Top = Math.Max(workingArea.Top, workingArea.Bottom - window.Height);
    }

    private void MoveWindowToMonitorCenter(Window window, Note note, MonitorInfo monitor)
    {
        var workingArea = monitor.WorkingArea;
        window.Width = note.WindowRect.Width;
        window.Height = note.WindowRect.Height;
        window.Left = workingArea.Left + (workingArea.Width - window.Width) / 2;
        window.Top = workingArea.Top + (workingArea.Height - window.Height) / 2;
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

    public string? GetMonitorDeviceIdForNote(Guid noteId)
    {
        if (_windows.TryGetValue(noteId, out var window))
        {
            var monitor = _monitorService.GetMonitorAt(window.Left, window.Top);
            return monitor?.DeviceId;
        }
        return null;
    }

    public void MoveNoteToMonitor(Guid noteId, string monitorDeviceId)
    {
        if (!_windows.TryGetValue(noteId, out var window))
            return;

        var targetMonitor = _monitorService.GetMonitorById(monitorDeviceId);
        if (targetMonitor == null)
            return;

        // Calculate relative position within current monitor
        var currentMonitor = _monitorService.GetMonitorAt(window.Left, window.Top);
        if (currentMonitor == null)
        {
            // Window is off-screen, just center on target monitor
            window.Left = targetMonitor.WorkingArea.Left + (targetMonitor.WorkingArea.Width - window.Width) / 2;
            window.Top = targetMonitor.WorkingArea.Top + (targetMonitor.WorkingArea.Height - window.Height) / 2;
        }
        else
        {
            // Calculate relative position and apply to target monitor
            double relativeX = (window.Left - currentMonitor.WorkingArea.Left) / currentMonitor.WorkingArea.Width;
            double relativeY = (window.Top - currentMonitor.WorkingArea.Top) / currentMonitor.WorkingArea.Height;
            
            window.Left = targetMonitor.WorkingArea.Left + relativeX * targetMonitor.WorkingArea.Width;
            window.Top = targetMonitor.WorkingArea.Top + relativeY * targetMonitor.WorkingArea.Height;
        }

        // Ensure window is within bounds
        EnsureWindowInMonitorBounds(window, targetMonitor);
    }

    public void EnsureNoteVisible(Guid noteId)
    {
        if (!_windows.TryGetValue(noteId, out var window))
            return;

        if (!_monitorService.IsPointVisible(window.Left, window.Top))
        {
            var nearestPoint = _monitorService.GetNearestVisiblePoint(window.Left, window.Top);
            window.Left = nearestPoint.X;
            window.Top = nearestPoint.Y;
        }
    }
}
