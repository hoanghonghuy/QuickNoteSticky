using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using DevSticky.Interfaces;
using Application = System.Windows.Application;
using HwndSource = System.Windows.Interop.HwndSource;

namespace DevSticky.Services;

/// <summary>
/// Service for managing global hotkeys using Win32 API (Requirements 1.4, 1.5, 1.6)
/// </summary>
public class HotkeyService : IHotkeyService
{
    private const int WM_HOTKEY = 0x0312;
    
    // Win32 API imports
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Win32 modifier key constants
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private readonly ConcurrentDictionary<string, HotkeyBinding> _registeredHotkeys = new();
    private readonly ConcurrentDictionary<int, string> _hotkeyIdMap = new();
    private readonly object _lock = new();
    
    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private int _nextHotkeyId = 1;
    private bool _disposed;

    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    public HotkeyService()
    {
        // Create hidden window for receiving hotkey messages
        CreateMessageWindow();
    }

    private void CreateMessageWindow()
    {
        // Must be called on UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Create a hidden window to receive WM_HOTKEY messages
            var parameters = new HwndSourceParameters("DevStickyHotkeyWindow")
            {
                Width = 0,
                Height = 0,
                PositionX = -100,
                PositionY = -100,
                WindowStyle = unchecked((int)0x80000000) // WS_POPUP
            };

            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);
            _windowHandle = _hwndSource.Handle;
        });
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();
            if (_hotkeyIdMap.TryGetValue(hotkeyId, out var hotkeyName) &&
                _registeredHotkeys.TryGetValue(hotkeyName, out var binding))
            {
                HotkeyPressed?.Invoke(this, new HotkeyEventArgs(hotkeyName, binding.Modifiers, binding.Key));
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public bool RegisterHotkey(string id, ModifierKeys modifiers, Key key)
    {
        if (_disposed) return false;
        if (string.IsNullOrEmpty(id)) return false;
        
        lock (_lock)
        {
            // Check if already registered with same id
            if (_registeredHotkeys.ContainsKey(id))
            {
                UnregisterHotkey(id);
            }

            // Check if hotkey combination is available
            if (!IsHotkeyAvailable(modifiers, key))
            {
                return false;
            }

            int hotkeyId = _nextHotkeyId++;
            uint win32Modifiers = ConvertModifiers(modifiers) | MOD_NOREPEAT;
            uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

            bool result = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                result = RegisterHotKey(_windowHandle, hotkeyId, win32Modifiers, virtualKey);
            });

            if (result)
            {
                var binding = new HotkeyBinding(id, modifiers, key);
                _registeredHotkeys[id] = binding;
                _hotkeyIdMap[hotkeyId] = id;
                return true;
            }

            return false;
        }
    }

    public bool UnregisterHotkey(string id)
    {
        if (_disposed) return false;
        
        lock (_lock)
        {
            if (!_registeredHotkeys.TryRemove(id, out _))
            {
                return false;
            }

            // Find and remove the hotkey ID mapping
            int? hotkeyIdToRemove = null;
            foreach (var kvp in _hotkeyIdMap)
            {
                if (kvp.Value == id)
                {
                    hotkeyIdToRemove = kvp.Key;
                    break;
                }
            }

            if (hotkeyIdToRemove.HasValue)
            {
                _hotkeyIdMap.TryRemove(hotkeyIdToRemove.Value, out _);
                
                bool result = false;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    result = UnregisterHotKey(_windowHandle, hotkeyIdToRemove.Value);
                });
                return result;
            }

            return false;
        }
    }

    public void UnregisterAll()
    {
        if (_disposed) return;
        
        lock (_lock)
        {
            var ids = _registeredHotkeys.Keys.ToList();
            foreach (var id in ids)
            {
                UnregisterHotkey(id);
            }
        }
    }

    public IReadOnlyDictionary<string, HotkeyBinding> GetRegisteredHotkeys()
    {
        return new Dictionary<string, HotkeyBinding>(_registeredHotkeys);
    }

    public bool IsHotkeyAvailable(ModifierKeys modifiers, Key key)
    {
        // Check if any registered hotkey uses this combination
        foreach (var binding in _registeredHotkeys.Values)
        {
            if (binding.Modifiers == modifiers && binding.Key == key)
            {
                return false;
            }
        }

        // Try to register and immediately unregister to check system availability
        int testId = int.MaxValue - 1;
        uint win32Modifiers = ConvertModifiers(modifiers) | MOD_NOREPEAT;
        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

        bool available = false;
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (RegisterHotKey(_windowHandle, testId, win32Modifiers, virtualKey))
            {
                UnregisterHotKey(_windowHandle, testId);
                available = true;
            }
        });

        return available;
    }

    public bool TryParseHotkey(string hotkeyString, out ModifierKeys modifiers, out Key key)
    {
        modifiers = ModifierKeys.None;
        key = Key.None;

        if (string.IsNullOrWhiteSpace(hotkeyString))
            return false;

        var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        // Last part is the key, rest are modifiers
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var modifier = parts[i].ToUpperInvariant();
            switch (modifier)
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= ModifierKeys.Control;
                    break;
                case "ALT":
                    modifiers |= ModifierKeys.Alt;
                    break;
                case "SHIFT":
                    modifiers |= ModifierKeys.Shift;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= ModifierKeys.Windows;
                    break;
                default:
                    return false;
            }
        }

        // Parse the key
        var keyString = parts[^1];
        if (Enum.TryParse<Key>(keyString, true, out var parsedKey))
        {
            key = parsedKey;
            return true;
        }

        // Try common key name mappings
        key = keyString.ToUpperInvariant() switch
        {
            "0" => Key.D0,
            "1" => Key.D1,
            "2" => Key.D2,
            "3" => Key.D3,
            "4" => Key.D4,
            "5" => Key.D5,
            "6" => Key.D6,
            "7" => Key.D7,
            "8" => Key.D8,
            "9" => Key.D9,
            _ => Key.None
        };

        return key != Key.None;
    }

    public string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private static uint ConvertModifiers(ModifierKeys modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= MOD_WIN;
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();

        Application.Current?.Dispatcher.Invoke(() =>
        {
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource?.Dispose();
            _hwndSource = null;
        });
    }
}
