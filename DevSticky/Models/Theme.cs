namespace DevSticky.Models;

/// <summary>
/// Represents the visual theme of the application (Light or Dark)
/// </summary>
public enum Theme
{
    Light,
    Dark
}

/// <summary>
/// Represents the theme mode setting (Light, Dark, or System-following)
/// </summary>
public enum ThemeMode
{
    Light,
    Dark,
    System
}

/// <summary>
/// Event arguments for theme change events
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    public Theme NewTheme { get; }

    public ThemeChangedEventArgs(Theme newTheme)
    {
        NewTheme = newTheme;
    }
}
