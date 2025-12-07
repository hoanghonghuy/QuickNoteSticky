using DevSticky.Interfaces;
using DevSticky.Models;
using WpfApplication = System.Windows.Application;
using WpfResourceDictionary = System.Windows.ResourceDictionary;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DevSticky.Services;

/// <summary>
/// Service for managing application theme
/// Coordinates theme switching and monitors system theme changes
/// </summary>
public class ThemeService : IThemeService, IDisposable
{
    private readonly IWindowsThemeDetector _detector;
    private ThemeMode _currentMode = ThemeMode.System;
    private Theme _currentTheme = Theme.Dark;
    private bool _disposed;
    private bool _initialized;

    /// <inheritdoc />
    public Theme CurrentTheme => _currentTheme;

    /// <inheritdoc />
    public ThemeMode CurrentMode => _currentMode;

    /// <inheritdoc />
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public ThemeService(IWindowsThemeDetector detector)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
    }

    /// <inheritdoc />
    public void SetThemeMode(ThemeMode mode)
    {
        var previousMode = _currentMode;
        var isFirstCall = !_initialized;
        _currentMode = mode;
        _initialized = true;

        // Unsubscribe from system theme changes if we were in System mode
        if (previousMode == ThemeMode.System && mode != ThemeMode.System && !isFirstCall)
        {
            _detector.SystemThemeChanged -= OnSystemThemeChanged;
            _detector.StopMonitoring();
        }

        // Subscribe to system theme changes if entering System mode
        // Also start monitoring on first call if mode is System (Requirements 7.3)
        if (mode == ThemeMode.System && (previousMode != ThemeMode.System || isFirstCall))
        {
            _detector.SystemThemeChanged += OnSystemThemeChanged;
            _detector.StartMonitoring();
        }

        // Determine the new theme based on mode
        var newTheme = MapModeToTheme(mode);

        // Apply theme if it changed, or on first call to ensure initial theme is applied
        if (newTheme != _currentTheme || isFirstCall)
        {
            _currentTheme = newTheme;
            ApplyTheme();
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(newTheme));
        }
    }


    /// <summary>
    /// Maps ThemeMode to Theme based on mode setting
    /// </summary>
    /// <param name="mode">The theme mode</param>
    /// <returns>The corresponding theme</returns>
    public Theme MapModeToTheme(ThemeMode mode)
    {
        return mode switch
        {
            ThemeMode.Light => Theme.Light,
            ThemeMode.Dark => Theme.Dark,
            ThemeMode.System => _detector.GetSystemTheme(),
            _ => Theme.Dark
        };
    }

    /// <inheritdoc />
    public void ApplyTheme()
    {
        var app = WpfApplication.Current;
        if (app == null)
            return;

        var themeUri = _currentTheme == Theme.Light
            ? new Uri("Resources/LightTheme.xaml", UriKind.Relative)
            : new Uri("Resources/DarkTheme.xaml", UriKind.Relative);

        // Remove existing theme dictionary
        var existingTheme = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.ToString().Contains("Theme.xaml") == true);
        
        if (existingTheme != null)
            app.Resources.MergedDictionaries.Remove(existingTheme);

        // Add new theme dictionary
        try
        {
            app.Resources.MergedDictionaries.Add(new WpfResourceDictionary { Source = themeUri });
        }
        catch (Exception)
        {
            // Log error, keep current theme if resource not found
        }
    }

    /// <inheritdoc />
    public System.Windows.Media.Color GetColor(string key)
    {
        var app = WpfApplication.Current;
        if (app == null)
            return System.Windows.Media.Colors.Transparent;

        try
        {
            if (app.Resources[key] is System.Windows.Media.Color color)
                return color;
            
            if (app.Resources[key] is SolidColorBrush brush)
                return brush.Color;
        }
        catch
        {
            // Return transparent if key not found
        }

        return System.Windows.Media.Colors.Transparent;
    }

    private void OnSystemThemeChanged(object? sender, Theme newTheme)
    {
        if (_currentMode != ThemeMode.System)
            return;

        if (newTheme != _currentTheme)
        {
            _currentTheme = newTheme;
            
            // ApplyTheme must be called on UI thread
            if (WpfApplication.Current?.Dispatcher != null)
            {
                WpfApplication.Current.Dispatcher.Invoke(ApplyTheme);
            }
            
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(newTheme));
        }
    }

    /// <summary>
    /// Dispose resources and stop monitoring
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            if (_currentMode == ThemeMode.System)
            {
                _detector.SystemThemeChanged -= OnSystemThemeChanged;
                _detector.StopMonitoring();
            }
        }

        _disposed = true;
    }
}
