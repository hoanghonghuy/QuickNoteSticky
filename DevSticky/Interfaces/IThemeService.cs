using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing application theme
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Current active theme (Light or Dark)
    /// </summary>
    Theme CurrentTheme { get; }

    /// <summary>
    /// Current theme mode setting (Light, Dark, or System)
    /// </summary>
    ThemeMode CurrentMode { get; }

    /// <summary>
    /// Event fired when theme changes
    /// </summary>
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// Set the theme mode and apply appropriate theme
    /// </summary>
    /// <param name="mode">The theme mode to set</param>
    void SetThemeMode(ThemeMode mode);

    /// <summary>
    /// Apply current theme to application resources
    /// </summary>
    void ApplyTheme();

    /// <summary>
    /// Get color value for a specific theme key
    /// </summary>
    /// <param name="key">The resource key for the color</param>
    /// <returns>The color value for the current theme</returns>
    System.Windows.Media.Color GetColor(string key);
}
