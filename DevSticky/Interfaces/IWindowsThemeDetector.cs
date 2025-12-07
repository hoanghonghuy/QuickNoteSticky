using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for detecting and monitoring Windows system theme
/// </summary>
public interface IWindowsThemeDetector
{
    /// <summary>
    /// Get current Windows theme from registry
    /// </summary>
    /// <returns>The current system theme (Light or Dark)</returns>
    Theme GetSystemTheme();

    /// <summary>
    /// Event fired when Windows theme changes
    /// </summary>
    event EventHandler<Theme>? SystemThemeChanged;

    /// <summary>
    /// Start monitoring Windows theme changes
    /// </summary>
    void StartMonitoring();

    /// <summary>
    /// Stop monitoring Windows theme changes
    /// </summary>
    void StopMonitoring();
}
