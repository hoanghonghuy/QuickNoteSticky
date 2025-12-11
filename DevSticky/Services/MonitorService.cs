using System.Windows;
using DevSticky.Interfaces;
using DevSticky.Models;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace DevSticky.Services;

/// <summary>
/// Service for managing and querying display monitors using System.Windows.Forms.Screen
/// </summary>
public class MonitorService : IMonitorService, IDisposable
{
    private List<MonitorInfo> _monitors = new();
    private bool _disposed;

    public event EventHandler? MonitorsChanged;

    public MonitorService()
    {
        RefreshMonitors();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public IReadOnlyList<MonitorInfo> GetAllMonitors()
    {
        return _monitors.AsReadOnly();
    }

    public MonitorInfo? GetMonitorAt(double x, double y)
    {
        var point = new System.Drawing.Point((int)x, (int)y);
        var screen = WinForms.Screen.FromPoint(point);
        return _monitors.FirstOrDefault(m => m.DeviceId == screen.DeviceName);
    }

    public MonitorInfo GetPrimaryMonitor()
    {
        return _monitors.FirstOrDefault(m => m.IsPrimary) 
            ?? _monitors.FirstOrDefault() 
            ?? CreateDefaultMonitor();
    }

    public MonitorInfo? GetMonitorById(string deviceId)
    {
        return _monitors.FirstOrDefault(m => m.DeviceId == deviceId);
    }

    public bool IsPointVisible(double x, double y)
    {
        return _monitors.Any(m => m.WorkingArea.Contains(x, y));
    }

    public System.Windows.Point GetNearestVisiblePoint(double x, double y)
    {
        // If point is already visible, return it
        if (IsPointVisible(x, y))
        {
            return new System.Windows.Point(x, y);
        }

        // Find the nearest point on any monitor's working area
        double nearestX = x;
        double nearestY = y;
        double minDistance = double.MaxValue;

        foreach (var monitor in _monitors)
        {
            var rect = monitor.WorkingArea;
            
            // Clamp point to this monitor's bounds
            double clampedX = Math.Max(rect.Left, Math.Min(x, rect.Right - 1));
            double clampedY = Math.Max(rect.Top, Math.Min(y, rect.Bottom - 1));
            
            // Calculate distance
            double dx = x - clampedX;
            double dy = y - clampedY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestX = clampedX;
                nearestY = clampedY;
            }
        }

        return new System.Windows.Point(nearestX, nearestY);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        RefreshMonitors();
        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshMonitors()
    {
        _monitors = WinForms.Screen.AllScreens
            .Select((screen, index) => CreateMonitorInfo(screen, index))
            .ToList();
    }

    private static MonitorInfo CreateMonitorInfo(WinForms.Screen screen, int index)
    {
        // Get DPI scaling for this screen
        double dpiScale = GetDpiScale(screen);

        return new MonitorInfo
        {
            DeviceId = screen.DeviceName,
            DisplayName = $"Monitor {index + 1}" + (screen.Primary ? " (Primary)" : ""),
            WorkingArea = new Rect(
                screen.WorkingArea.X,
                screen.WorkingArea.Y,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height),
            Bounds = new Rect(
                screen.Bounds.X,
                screen.Bounds.Y,
                screen.Bounds.Width,
                screen.Bounds.Height),
            IsPrimary = screen.Primary,
            DpiScale = dpiScale
        };
    }

    private static double GetDpiScale(WinForms.Screen screen)
    {
        try
        {
            // Use the screen bounds to get DPI info
            // Default to 96 DPI (100% scaling)
            using var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            return graphics.DpiX / 96.0;
        }
        catch
        {
            return 1.0;
        }
    }

    private static MonitorInfo CreateDefaultMonitor()
    {
        return new MonitorInfo
        {
            DeviceId = "DEFAULT",
            DisplayName = "Default Monitor",
            WorkingArea = new Rect(0, 0, 1920, 1080),
            Bounds = new Rect(0, 0, 1920, 1080),
            IsPrimary = true,
            DpiScale = 1.0
        };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to ensure system event handlers are cleaned up
    /// </summary>
    ~MonitorService()
    {
        Dispose(false);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        // Always clean up system event subscriptions
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        if (disposing)
        {
            // Clean up managed resources
            _monitors.Clear();
        }
    }
}
