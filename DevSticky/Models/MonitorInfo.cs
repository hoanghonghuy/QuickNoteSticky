using System.Windows;

namespace DevSticky.Models;

/// <summary>
/// Represents information about a display monitor
/// </summary>
public class MonitorInfo
{
    /// <summary>
    /// Unique identifier for the monitor device
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the monitor
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The working area of the monitor (excludes taskbar and other docked windows)
    /// </summary>
    public Rect WorkingArea { get; set; }

    /// <summary>
    /// The full bounds of the monitor
    /// </summary>
    public Rect Bounds { get; set; }

    /// <summary>
    /// Whether this is the primary monitor
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// DPI scaling factor for the monitor (1.0 = 100%, 1.25 = 125%, etc.)
    /// </summary>
    public double DpiScale { get; set; } = 1.0;
}
