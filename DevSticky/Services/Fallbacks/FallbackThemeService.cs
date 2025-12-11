using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services.Fallbacks;

/// <summary>
/// Fallback implementation of IThemeService with basic theme support
/// Used when the primary ThemeService fails to initialize or theme resources are unavailable
/// </summary>
public class FallbackThemeService : IThemeService
{
    private readonly IErrorHandler _errorHandler;
    private Theme _currentTheme = Theme.Dark;
    private ThemeMode _currentMode = ThemeMode.System;

    public Theme CurrentTheme => _currentTheme;
    public ThemeMode CurrentMode => _currentMode;

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public FallbackThemeService(IErrorHandler errorHandler)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    public void SetThemeMode(ThemeMode mode)
    {
        _errorHandler.HandleWithFallback(() =>
        {
            var previousTheme = _currentTheme;
            _currentMode = mode;
            
            // Simple theme mapping without system detection
            _currentTheme = mode switch
            {
                ThemeMode.Light => Theme.Light,
                ThemeMode.Dark => Theme.Dark,
                ThemeMode.System => Theme.Dark, // Default to dark in fallback mode
                _ => Theme.Dark
            };

            if (_currentTheme != previousTheme)
            {
                ApplyTheme();
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(_currentTheme));
            }
            
            return true;
        },
        false,
        "FallbackThemeService.SetThemeMode");
    }

    public void ApplyTheme()
    {
        _errorHandler.HandleWithFallback(() =>
        {
            // In fallback mode, we can't load external theme files
            // Just set basic system colors
            var app = System.Windows.Application.Current;
            if (app == null) return false;

            // Apply basic colors directly to application resources
            if (_currentTheme == Theme.Light)
            {
                ApplyLightThemeColors(app);
            }
            else
            {
                ApplyDarkThemeColors(app);
            }
            
            return true;
        },
        false,
        "FallbackThemeService.ApplyTheme");
    }

    public System.Windows.Media.Color GetColor(string key)
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            var app = System.Windows.Application.Current;
            if (app == null)
                return System.Windows.Media.Colors.Transparent;

            // Try to get from resources first
            try
            {
                if (app.Resources[key] is System.Windows.Media.Color color)
                    return color;
                
                if (app.Resources[key] is System.Windows.Media.SolidColorBrush brush)
                    return brush.Color;
            }
            catch
            {
                // Fall through to default colors
            }

            // Fallback to basic colors based on key and theme
            return GetFallbackColor(key);
        },
        System.Windows.Media.Colors.Transparent,
        "FallbackThemeService.GetColor");
    }

    private void ApplyLightThemeColors(System.Windows.Application app)
    {
        try
        {
            app.Resources["BackgroundColor"] = System.Windows.Media.Colors.White;
            app.Resources["ForegroundColor"] = System.Windows.Media.Colors.Black;
            app.Resources["BorderColor"] = System.Windows.Media.Colors.LightGray;
            app.Resources["AccentColor"] = System.Windows.Media.Colors.DodgerBlue;
            
            app.Resources["BackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
            app.Resources["ForegroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
            app.Resources["BorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);
            app.Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DodgerBlue);
        }
        catch
        {
            // Ignore errors in fallback theme application
        }
    }

    private void ApplyDarkThemeColors(System.Windows.Application app)
    {
        try
        {
            app.Resources["BackgroundColor"] = System.Windows.Media.Color.FromRgb(32, 32, 32);
            app.Resources["ForegroundColor"] = System.Windows.Media.Colors.White;
            app.Resources["BorderColor"] = System.Windows.Media.Color.FromRgb(64, 64, 64);
            app.Resources["AccentColor"] = System.Windows.Media.Colors.DodgerBlue;
            
            app.Resources["BackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 32));
            app.Resources["ForegroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
            app.Resources["BorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(64, 64, 64));
            app.Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DodgerBlue);
        }
        catch
        {
            // Ignore errors in fallback theme application
        }
    }

    private System.Windows.Media.Color GetFallbackColor(string key)
    {
        // Provide basic fallback colors based on common key patterns
        var lowerKey = key.ToLowerInvariant();
        
        if (_currentTheme == Theme.Light)
        {
            return lowerKey switch
            {
                var k when k.Contains("background") => System.Windows.Media.Colors.White,
                var k when k.Contains("foreground") || k.Contains("text") => System.Windows.Media.Colors.Black,
                var k when k.Contains("border") => System.Windows.Media.Colors.LightGray,
                var k when k.Contains("accent") || k.Contains("primary") => System.Windows.Media.Colors.DodgerBlue,
                var k when k.Contains("error") => System.Windows.Media.Colors.Red,
                var k when k.Contains("warning") => System.Windows.Media.Colors.Orange,
                var k when k.Contains("success") => System.Windows.Media.Colors.Green,
                _ => System.Windows.Media.Colors.Black
            };
        }
        else
        {
            return lowerKey switch
            {
                var k when k.Contains("background") => System.Windows.Media.Color.FromRgb(32, 32, 32),
                var k when k.Contains("foreground") || k.Contains("text") => System.Windows.Media.Colors.White,
                var k when k.Contains("border") => System.Windows.Media.Color.FromRgb(64, 64, 64),
                var k when k.Contains("accent") || k.Contains("primary") => System.Windows.Media.Colors.DodgerBlue,
                var k when k.Contains("error") => System.Windows.Media.Colors.Red,
                var k when k.Contains("warning") => System.Windows.Media.Colors.Orange,
                var k when k.Contains("success") => System.Windows.Media.Colors.Green,
                _ => System.Windows.Media.Colors.White
            };
        }
    }
}