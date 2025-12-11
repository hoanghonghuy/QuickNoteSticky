using System.Windows;
using DevSticky.Models;

namespace DevSticky.Helpers;

/// <summary>
/// Helper class for managing window positioning and bounds within monitor working areas.
/// Provides utilities for ensuring windows stay within monitor bounds, centering windows,
/// and calculating relative positions for cross-monitor movement.
/// </summary>
public static class MonitorBoundsHelper
{
    /// <summary>
    /// Ensures a window is fully within the specified monitor's working area.
    /// Adjusts the window's position if it extends beyond the monitor bounds.
    /// </summary>
    /// <param name="window">The window to constrain</param>
    /// <param name="monitor">The monitor whose bounds to enforce</param>
    public static void EnsureWindowInBounds(Window window, MonitorInfo monitor)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        if (monitor == null) throw new ArgumentNullException(nameof(monitor));

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

    /// <summary>
    /// Centers a window on the specified monitor's working area.
    /// </summary>
    /// <param name="window">The window to center</param>
    /// <param name="monitor">The monitor on which to center the window</param>
    public static void CenterWindowOnMonitor(Window window, MonitorInfo monitor)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        if (monitor == null) throw new ArgumentNullException(nameof(monitor));

        var workingArea = monitor.WorkingArea;
        window.Left = workingArea.Left + (workingArea.Width - window.Width) / 2;
        window.Top = workingArea.Top + (workingArea.Height - window.Height) / 2;
    }

    /// <summary>
    /// Calculates the relative position of a window within a monitor's working area.
    /// Returns values between 0.0 and 1.0 representing the position as a fraction
    /// of the monitor's width and height.
    /// </summary>
    /// <param name="window">The window whose position to calculate</param>
    /// <param name="monitor">The monitor to calculate relative to</param>
    /// <returns>A tuple containing (relativeX, relativeY) values between 0.0 and 1.0</returns>
    public static (double X, double Y) CalculateRelativePosition(Window window, MonitorInfo monitor)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        if (monitor == null) throw new ArgumentNullException(nameof(monitor));

        var workingArea = monitor.WorkingArea;
        
        // Avoid division by zero
        double relativeX = workingArea.Width > 0 
            ? (window.Left - workingArea.Left) / workingArea.Width 
            : 0.0;
        double relativeY = workingArea.Height > 0 
            ? (window.Top - workingArea.Top) / workingArea.Height 
            : 0.0;
        
        return (relativeX, relativeY);
    }

    /// <summary>
    /// Applies a relative position to a window on a monitor's working area.
    /// The relative position values should be between 0.0 and 1.0, representing
    /// the position as a fraction of the monitor's width and height.
    /// </summary>
    /// <param name="window">The window to position</param>
    /// <param name="monitor">The monitor on which to position the window</param>
    /// <param name="relativeX">The relative X position (0.0 to 1.0)</param>
    /// <param name="relativeY">The relative Y position (0.0 to 1.0)</param>
    public static void ApplyRelativePosition(Window window, MonitorInfo monitor, double relativeX, double relativeY)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        if (monitor == null) throw new ArgumentNullException(nameof(monitor));

        var workingArea = monitor.WorkingArea;
        window.Left = workingArea.Left + relativeX * workingArea.Width;
        window.Top = workingArea.Top + relativeY * workingArea.Height;
    }
}
