using System.Text.Json;
using System.Text.Json.Serialization;
using DevSticky.Models;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for HotkeySettings serialization round-trip
/// **Feature: devsticky-v2, Property 2: Hotkey settings persistence round-trip**
/// **Validates: Requirements 1.7**
/// </summary>
public class HotkeySettingsPropertyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Property 2: Hotkey settings persistence round-trip
    /// For any valid hotkey configuration, saving and loading settings 
    /// should preserve all hotkey bindings exactly.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HotkeySettings_SerializeDeserialize_ShouldPreserveAllBindings()
    {
        return Prop.ForAll(HotkeySettingsGenerator(), hotkeySettings =>
        {
            var json = JsonSerializer.Serialize(hotkeySettings, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<HotkeySettings>(json, JsonOptions);

            return deserialized != null &&
                   deserialized.NewNoteHotkey == hotkeySettings.NewNoteHotkey &&
                   deserialized.ToggleVisibilityHotkey == hotkeySettings.ToggleVisibilityHotkey &&
                   deserialized.QuickCaptureHotkey == hotkeySettings.QuickCaptureHotkey &&
                   deserialized.SnippetBrowserHotkey == hotkeySettings.SnippetBrowserHotkey;
        });
    }

    /// <summary>
    /// Property 2 (extended): AppSettings with HotkeySettings round-trip
    /// For any valid AppSettings with hotkey configuration, serializing and deserializing
    /// should preserve all hotkey bindings exactly.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AppSettings_WithHotkeys_SerializeDeserialize_ShouldPreserveHotkeyBindings()
    {
        return Prop.ForAll(AppSettingsWithHotkeysGenerator(), appSettings =>
        {
            var json = JsonSerializer.Serialize(appSettings, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

            return deserialized != null &&
                   deserialized.Hotkeys != null &&
                   deserialized.Hotkeys.NewNoteHotkey == appSettings.Hotkeys.NewNoteHotkey &&
                   deserialized.Hotkeys.ToggleVisibilityHotkey == appSettings.Hotkeys.ToggleVisibilityHotkey &&
                   deserialized.Hotkeys.QuickCaptureHotkey == appSettings.Hotkeys.QuickCaptureHotkey &&
                   deserialized.Hotkeys.SnippetBrowserHotkey == appSettings.Hotkeys.SnippetBrowserHotkey;
        });
    }

    /// <summary>
    /// Property 2 (extended): CloudSyncSettings round-trip
    /// For any valid CloudSyncSettings, serializing and deserializing
    /// should preserve all settings exactly.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CloudSyncSettings_SerializeDeserialize_ShouldPreserveAllSettings()
    {
        return Prop.ForAll(CloudSyncSettingsGenerator(), cloudSyncSettings =>
        {
            var json = JsonSerializer.Serialize(cloudSyncSettings, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<CloudSyncSettings>(json, JsonOptions);

            return deserialized != null &&
                   deserialized.IsEnabled == cloudSyncSettings.IsEnabled &&
                   deserialized.Provider == cloudSyncSettings.Provider &&
                   deserialized.SyncIntervalSeconds == cloudSyncSettings.SyncIntervalSeconds &&
                   deserialized.EncryptData == cloudSyncSettings.EncryptData;
        });
    }

    private static Arbitrary<HotkeySettings> HotkeySettingsGenerator()
    {
        var modifiers = new[] { "Ctrl", "Alt", "Shift", "Ctrl+Shift", "Ctrl+Alt", "Alt+Shift" };
        var keys = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", 
                          "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
                          "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12" };

        var hotkeyGen = from modifier in Gen.Elements(modifiers)
                        from key in Gen.Elements(keys)
                        select $"{modifier}+{key}";

        var gen = from newNote in hotkeyGen
                  from toggleVisibility in hotkeyGen
                  from quickCapture in hotkeyGen
                  from snippetBrowser in hotkeyGen
                  select new HotkeySettings
                  {
                      NewNoteHotkey = newNote,
                      ToggleVisibilityHotkey = toggleVisibility,
                      QuickCaptureHotkey = quickCapture,
                      SnippetBrowserHotkey = snippetBrowser
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<CloudSyncSettings> CloudSyncSettingsGenerator()
    {
        var gen = from isEnabled in Arb.Generate<bool>()
                  from provider in Gen.Elements<CloudProvider?>(null, CloudProvider.OneDrive, CloudProvider.GoogleDrive)
                  from syncInterval in Gen.Choose(60, 3600)
                  from encryptData in Arb.Generate<bool>()
                  select new CloudSyncSettings
                  {
                      IsEnabled = isEnabled,
                      Provider = provider,
                      SyncIntervalSeconds = syncInterval,
                      EncryptData = encryptData
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<AppSettings> AppSettingsWithHotkeysGenerator()
    {
        var gen = from hotkeys in HotkeySettingsGenerator().Generator
                  from cloudSync in CloudSyncSettingsGenerator().Generator
                  from defaultOpacity in Gen.Choose(20, 100).Select(x => x / 100.0)
                  from theme in Gen.Elements("Light", "Dark")
                  from themeMode in Gen.Elements("Light", "Dark", "System")
                  from language in Gen.Elements("en", "vi")
                  select new AppSettings
                  {
                      DefaultOpacity = defaultOpacity,
                      Theme = theme,
                      ThemeMode = themeMode,
                      Language = language,
                      Hotkeys = hotkeys,
                      CloudSync = cloudSync
                  };

        return Arb.From(gen);
    }
}
