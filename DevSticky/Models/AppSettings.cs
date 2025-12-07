using System.IO;
using System.Text.Json;

namespace DevSticky.Models;

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
