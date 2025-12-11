using System.Collections.Concurrent;
using System.Windows;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for managing note windows with multi-monitor support
/// </summary>
public class WindowService : IWindowService, IDisposable
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
                MonitorBoundsHelper.EnsureWindowInBounds(window, targetMonitor);
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

    private void MoveWindowToMonitorCenter(Window window, Note note, MonitorInfo monitor)
    {
        window.Width = note.WindowRect.Width;
        window.Height = note.WindowRect.Height;
        MonitorBoundsHelper.CenterWindowOnMonitor(window, monitor);
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
            MonitorBoundsHelper.CenterWindowOnMonitor(window, targetMonitor);
        }
        else
        {
            // Calculate relative position and apply to target monitor
            var (relativeX, relativeY) = MonitorBoundsHelper.CalculateRelativePosition(window, currentMonitor);
            MonitorBoundsHelper.ApplyRelativePosition(window, targetMonitor, relativeX, relativeY);
        }

        // Ensure window is within bounds
        MonitorBoundsHelper.EnsureWindowInBounds(window, targetMonitor);
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

    /// <summary>
    /// Disposes the window service and releases all resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Close all managed windows
            var windowIds = _windows.Keys.ToList();
            foreach (var windowId in windowIds)
            {
                CloseNote(windowId);
            }
            _windows.Clear();
        }
    }
}
