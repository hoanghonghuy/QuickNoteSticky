using System.Windows;
using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing and querying display monitors
/// </summary>
public interface IMonitorService
{
    /// <summary>
    /// Event raised when monitors are connected or disconnected
    /// </summary>
    event EventHandler? MonitorsChanged;

    /// <summary>
    /// Gets information about all available monitors
    /// </summary>
    IReadOnlyList<MonitorInfo> GetAllMonitors();

    /// <summary>
    /// Gets the monitor that contains the specified point
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <returns>MonitorInfo if point is on a monitor, null otherwise</returns>
    MonitorInfo? GetMonitorAt(double x, double y);

    /// <summary>
    /// Gets the primary monitor
    /// </summary>
    MonitorInfo GetPrimaryMonitor();

    /// <summary>
    /// Gets a monitor by its device ID
    /// </summary>
    /// <param name="deviceId">The monitor device ID</param>
    /// <returns>MonitorInfo if found, null otherwise</returns>
    MonitorInfo? GetMonitorById(string deviceId);

    /// <summary>
    /// Checks if a point is visible on any monitor
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <returns>True if the point is within any monitor's bounds</returns>
    bool IsPointVisible(double x, double y);

    /// <summary>
    /// Gets the nearest visible point on any monitor for a given point
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <returns>The nearest point that is within visible screen bounds</returns>
    System.Windows.Point GetNearestVisiblePoint(double x, double y);
}
