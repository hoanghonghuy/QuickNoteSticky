using System.IO;
using System.Text.Json;

namespace DevSticky.Models;

/// <summary>
/// Hotkey configuration settings for global keyboard shortcuts
/// </summary>
public class HotkeySettings
{
    public string NewNoteHotkey { get; set; } = "Ctrl+Shift+N";
    public string ToggleVisibilityHotkey { get; set; } = "Ctrl+Shift+D";
    public string QuickCaptureHotkey { get; set; } = "Ctrl+Shift+Q";
    public string SnippetBrowserHotkey { get; set; } = "Ctrl+Shift+I";
}

/// <summary>
/// Cloud provider enumeration
/// </summary>
public enum CloudProvider
{
    OneDrive,
    GoogleDrive
}

/// <summary>
/// Cloud synchronization settings
/// </summary>
public class CloudSyncSettings
{
    public bool IsEnabled { get; set; }
    public CloudProvider? Provider { get; set; }
    public int SyncIntervalSeconds { get; set; } = 300;
    public bool EncryptData { get; set; } = true;
}

/// <summary>
/// Application settings
/// </summary>
public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DevSticky", "settings.json");

    public double DefaultOpacity { get; set; } = 0.9;
    public int DefaultFontSize { get; set; } = 13;
    public int DefaultWidth { get; set; } = 320;
    public int DefaultHeight { get; set; } = 220;
    public int AutoSaveDelayMs { get; set; } = 500;
    public string Theme { get; set; } = "Dark";
    public string ThemeMode { get; set; } = "System"; // "Light", "Dark", or "System"
    public bool StartWithWindows { get; set; } = false;
    public string Language { get; set; } = "en"; // en, vi

    // v2.0 property for user information (used in templates)
    public string? AuthorName { get; set; }

    // v2.0 properties for global hotkeys
    public HotkeySettings Hotkeys { get; set; } = new();

    // v2.0 properties for cloud synchronization
    public CloudSyncSettings CloudSync { get; set; } = new();

    // v2.0 property for encryption passphrase hash (stored securely)
    public string? EncryptionPassphraseHash { get; set; }

    // v2.1 properties for backup
    public bool AutoBackupEnabled { get; set; } = true;
    public int BackupIntervalMinutes { get; set; } = 30;
    public int MaxBackupCount { get; set; } = 10;

    // v2.1 properties for recent notes
    public int MaxRecentNotes { get; set; } = 10;

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* Ignore save errors */ }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { /* Ignore load errors */ }
        
        return new AppSettings();
    }
}
