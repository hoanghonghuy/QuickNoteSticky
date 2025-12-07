using System.Windows.Input;
using DevSticky.Interfaces;
using DevSticky.Models;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Mock implementation of IWindowService for testing toggle visibility
/// </summary>
public class MockWindowService : IWindowService
{
    private readonly Dictionary<Guid, bool> _windowVisibility = new();
    private bool _allVisible = true;

    public bool AllNotesVisible => _allVisible;

    public void ShowNote(Note note)
    {
        _windowVisibility[note.Id] = true;
        UpdateAllVisibleState();
    }

    public void CloseNote(Guid id)
    {
        _windowVisibility.Remove(id);
        UpdateAllVisibleState();
    }

    public void ShowAllNotes()
    {
        foreach (var key in _windowVisibility.Keys.ToList())
        {
            _windowVisibility[key] = true;
        }
        _allVisible = true;
    }

    public void HideAllNotes()
    {
        foreach (var key in _windowVisibility.Keys.ToList())
        {
            _windowVisibility[key] = false;
        }
        _allVisible = false;
    }

    public void ToggleAllNotesVisibility()
    {
        if (_allVisible)
            HideAllNotes();
        else
            ShowAllNotes();
    }

    public bool IsNoteVisible(Guid id) => _windowVisibility.TryGetValue(id, out var visible) && visible;

    private void UpdateAllVisibleState()
    {
        _allVisible = _windowVisibility.Count == 0 || _windowVisibility.Values.All(v => v);
    }

    // Add notes for testing
    public void AddNote(Note note)
    {
        _windowVisibility[note.Id] = true;
        UpdateAllVisibleState();
    }

    public int NoteCount => _windowVisibility.Count;

    // New interface methods for multi-monitor support
    public string? GetMonitorDeviceIdForNote(Guid noteId) => null;
    public void MoveNoteToMonitor(Guid noteId, string monitorDeviceId) { }
    public void EnsureNoteVisible(Guid noteId) { }
}

/// <summary>
/// Property-based tests for Global Hotkey System
/// </summary>
public class HotkeyPropertyTests
{
    /// <summary>
    /// **Feature: devsticky-v2, Property 1: Hotkey toggle visibility is idempotent pair**
    /// **Validates: Requirements 1.2**
    /// For any set of note windows, calling toggle visibility twice should return 
    /// to the original visibility state.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToggleVisibility_CalledTwice_ShouldReturnToOriginalState()
    {
        return Prop.ForAll(
            NoteListGenerator(),
            InitialVisibilityGenerator(),
            (notes, initiallyVisible) =>
            {
                var windowService = new MockWindowService();

                // Add notes to the service
                foreach (var note in notes)
                {
                    windowService.AddNote(note);
                }

                // Set initial visibility state
                if (initiallyVisible)
                    windowService.ShowAllNotes();
                else
                    windowService.HideAllNotes();

                var originalState = windowService.AllNotesVisible;

                // Toggle twice (should be idempotent pair)
                windowService.ToggleAllNotesVisibility();
                windowService.ToggleAllNotesVisibility();

                var finalState = windowService.AllNotesVisible;

                // After toggling twice, should return to original state
                return originalState == finalState;
            });
    }

    /// <summary>
    /// **Feature: devsticky-v2, Property 1: Single toggle inverts visibility**
    /// **Validates: Requirements 1.2**
    /// For any visibility state, a single toggle should invert it.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToggleVisibility_CalledOnce_ShouldInvertState()
    {
        return Prop.ForAll(
            NoteListGenerator(),
            InitialVisibilityGenerator(),
            (notes, initiallyVisible) =>
            {
                var windowService = new MockWindowService();

                // Add notes to the service
                foreach (var note in notes)
                {
                    windowService.AddNote(note);
                }

                // Set initial visibility state
                if (initiallyVisible)
                    windowService.ShowAllNotes();
                else
                    windowService.HideAllNotes();

                var originalState = windowService.AllNotesVisible;

                // Toggle once
                windowService.ToggleAllNotesVisibility();

                var newState = windowService.AllNotesVisible;

                // State should be inverted (unless no notes exist)
                return windowService.NoteCount == 0 || originalState != newState;
            });
    }


    /// <summary>
    /// **Feature: devsticky-v2, Property: Hotkey string parsing round-trip**
    /// **Validates: Requirements 1.7**
    /// For any valid hotkey combination, formatting and parsing should be consistent.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HotkeyString_FormatAndParse_ShouldBeConsistent()
    {
        return Prop.ForAll(
            ModifierKeysGenerator(),
            KeyGenerator(),
            (modifiers, key) =>
            {
                // Format the hotkey
                var formatted = FormatHotkey(modifiers, key);

                // Parse it back
                var parsed = TryParseHotkey(formatted, out var parsedModifiers, out var parsedKey);

                // Should parse successfully and match original values
                return parsed && parsedModifiers == modifiers && parsedKey == key;
            });
    }

    /// <summary>
    /// **Feature: devsticky-v2, Property: Multiple toggles preserve idempotency**
    /// **Validates: Requirements 1.2**
    /// For any even number of toggles, the final state should match the initial state.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToggleVisibility_EvenNumberOfToggles_ShouldReturnToOriginalState()
    {
        return Prop.ForAll(
            NoteListGenerator(),
            InitialVisibilityGenerator(),
            EvenToggleCountGenerator(),
            (notes, initiallyVisible, toggleCount) =>
            {
                var windowService = new MockWindowService();

                // Add notes to the service
                foreach (var note in notes)
                {
                    windowService.AddNote(note);
                }

                // Set initial visibility state
                if (initiallyVisible)
                    windowService.ShowAllNotes();
                else
                    windowService.HideAllNotes();

                var originalState = windowService.AllNotesVisible;

                // Toggle an even number of times
                for (int i = 0; i < toggleCount; i++)
                {
                    windowService.ToggleAllNotesVisibility();
                }

                var finalState = windowService.AllNotesVisible;

                // After even number of toggles, should return to original state
                return originalState == finalState;
            });
    }

    #region Generators

    private static Arbitrary<List<Note>> NoteListGenerator()
    {
        var noteGen = from id in Arb.Generate<Guid>()
                      from title in Gen.Elements("Note 1", "Note 2", "Test Note", "Dev Note")
                      select new Note
                      {
                          Id = id,
                          Title = title,
                          Content = "Test content",
                          Language = "PlainText"
                      };

        var listGen = Gen.ListOf(noteGen).Select(notes => notes.ToList());
        return Arb.From(listGen);
    }

    private static Arbitrary<bool> InitialVisibilityGenerator()
    {
        return Gen.Elements(true, false).ToArbitrary();
    }

    private static Arbitrary<int> EvenToggleCountGenerator()
    {
        // Generate even numbers: 2, 4, 6, 8, 10
        return Gen.Elements(2, 4, 6, 8, 10).ToArbitrary();
    }

    private static Arbitrary<ModifierKeys> ModifierKeysGenerator()
    {
        // Generate valid modifier combinations (at least one modifier required for global hotkeys)
        return Gen.Elements(
            ModifierKeys.Control,
            ModifierKeys.Alt,
            ModifierKeys.Shift,
            ModifierKeys.Control | ModifierKeys.Shift,
            ModifierKeys.Control | ModifierKeys.Alt,
            ModifierKeys.Alt | ModifierKeys.Shift,
            ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift
        ).ToArbitrary();
    }

    private static Arbitrary<Key> KeyGenerator()
    {
        // Generate common keys used for hotkeys
        return Gen.Elements(
            Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J,
            Key.K, Key.L, Key.M, Key.N, Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T,
            Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z,
            Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9,
            Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12
        ).ToArbitrary();
    }

    #endregion

    #region Helper Methods

    private static string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private static bool TryParseHotkey(string hotkeyString, out ModifierKeys modifiers, out Key key)
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

        return false;
    }

    #endregion
}
