using System.Windows.Input;

namespace DevSticky.Interfaces;

/// <summary>
/// Event arguments for hotkey pressed events
/// </summary>
public class HotkeyEventArgs : EventArgs
{
    public string HotkeyId { get; }
    public ModifierKeys Modifiers { get; }
    public Key Key { get; }

    public HotkeyEventArgs(string hotkeyId, ModifierKeys modifiers, Key key)
    {
        HotkeyId = hotkeyId;
        Modifiers = modifiers;
        Key = key;
    }
}

/// <summary>
/// Represents a registered hotkey binding
/// </summary>
public class HotkeyBinding
{
    public string Id { get; }
    public ModifierKeys Modifiers { get; }
    public Key Key { get; }

    public HotkeyBinding(string id, ModifierKeys modifiers, Key key)
    {
        Id = id;
        Modifiers = modifiers;
        Key = key;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }
}

/// <summary>
/// Service for managing global hotkeys (Requirements 1.1, 1.2, 1.3)
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Event raised when a registered hotkey is pressed
    /// </summary>
    event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    /// <summary>
    /// Registers a global hotkey with the specified modifiers and key
    /// </summary>
    /// <param name="id">Unique identifier for the hotkey</param>
    /// <param name="modifiers">Modifier keys (Ctrl, Alt, Shift, Win)</param>
    /// <param name="key">The key to register</param>
    /// <returns>True if registration succeeded, false if hotkey is already in use</returns>
    bool RegisterHotkey(string id, ModifierKeys modifiers, Key key);

    /// <summary>
    /// Unregisters a previously registered hotkey
    /// </summary>
    /// <param name="id">The identifier of the hotkey to unregister</param>
    /// <returns>True if unregistration succeeded</returns>
    bool UnregisterHotkey(string id);

    /// <summary>
    /// Unregisters all registered hotkeys
    /// </summary>
    void UnregisterAll();

    /// <summary>
    /// Gets all currently registered hotkeys
    /// </summary>
    IReadOnlyDictionary<string, HotkeyBinding> GetRegisteredHotkeys();

    /// <summary>
    /// Checks if a hotkey combination is available for registration
    /// </summary>
    /// <param name="modifiers">Modifier keys to check</param>
    /// <param name="key">Key to check</param>
    /// <returns>True if the hotkey is available, false if already in use</returns>
    bool IsHotkeyAvailable(ModifierKeys modifiers, Key key);

    /// <summary>
    /// Parses a hotkey string (e.g., "Ctrl+Shift+N") into modifiers and key
    /// </summary>
    /// <param name="hotkeyString">The hotkey string to parse</param>
    /// <param name="modifiers">Output modifier keys</param>
    /// <param name="key">Output key</param>
    /// <returns>True if parsing succeeded</returns>
    bool TryParseHotkey(string hotkeyString, out ModifierKeys modifiers, out Key key);

    /// <summary>
    /// Formats modifiers and key into a hotkey string
    /// </summary>
    string FormatHotkey(ModifierKeys modifiers, Key key);
}
