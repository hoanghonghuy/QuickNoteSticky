using DevSticky.Interfaces;
using DevSticky.Models;
using Microsoft.Win32;

namespace DevSticky.Services;

/// <summary>
/// Service for detecting and monitoring Windows system theme
/// Reads from Windows registry and monitors for theme changes
/// </summary>
public class WindowsThemeDetector : IWindowsThemeDetector, IDisposable
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryKey = "AppsUseLightTheme";

    private bool _isMonitoring;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<Theme>? SystemThemeChanged;

    /// <inheritdoc />
    public Theme GetSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            var value = key?.GetValue(RegistryKey);
            
            return MapRegistryValueToTheme(value);
        }
        catch
        {
            // Default to Dark on any error (registry access denied, key not found, etc.)
            return Theme.Dark;
        }
    }

    /// <summary>
    /// Maps Windows registry AppsUseLightTheme value to Theme enum.
    /// Registry value: 0 = Dark, 1 = Light, null/other = Dark (default)
    /// </summary>
    /// <param name="registryValue">The registry value (expected to be int 0 or 1)</param>
    /// <returns>Theme.Light if value is 1, Theme.Dark otherwise</returns>
    public static Theme MapRegistryValueToTheme(object? registryValue)
    {
        // Registry value: 0 = Dark, 1 = Light
        return registryValue is int intValue && intValue == 1 ? Theme.Light : Theme.Dark;
    }

    /// <inheritdoc />
    public void StartMonitoring()
    {
        if (_isMonitoring || _disposed)
            return;

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _isMonitoring = true;
    }

    /// <inheritdoc />
    public void StopMonitoring()
    {
        if (!_isMonitoring)
            return;

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _isMonitoring = false;
    }


    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // UserPreferenceCategory.General is fired when theme changes
        if (e.Category == UserPreferenceCategory.General)
        {
            var currentTheme = GetSystemTheme();
            SystemThemeChanged?.Invoke(this, currentTheme);
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
            StopMonitoring();
        }

        _disposed = true;
    }
}
