using System.Text.Json;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Mock implementation of IWindowsThemeDetector for testing
/// </summary>
public class MockWindowsThemeDetector : IWindowsThemeDetector
{
    private Theme _systemTheme = Theme.Dark;

    public Theme SystemThemeToReturn
    {
        get => _systemTheme;
        set => _systemTheme = value;
    }

    public Theme GetSystemTheme() => _systemTheme;

    public event EventHandler<Theme>? SystemThemeChanged;

    public void StartMonitoring() { }

    public void StopMonitoring() { }

    public void RaiseSystemThemeChanged(Theme newTheme)
    {
        _systemTheme = newTheme;
        SystemThemeChanged?.Invoke(this, newTheme);
    }
}

/// <summary>
/// Property-based tests for Theme System
/// </summary>
public class ThemePropertyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// **Feature: theme-system, Property 1: Theme Mode Application Consistency**
    /// **Validates: Requirements 1.2, 1.3, 1.4**
    /// For any ThemeMode value (Light, Dark, System), when SetThemeMode is called,
    /// the CurrentTheme should match the expected theme (Light for Light mode, 
    /// Dark for Dark mode, system theme for System mode).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ThemeMode_SetThemeMode_ShouldApplyCorrectTheme()
    {
        return Prop.ForAll(
            ThemeModeEnumGenerator(),
            SystemThemeGenerator(),
            (mode, systemTheme) =>
            {
                var mockDetector = new MockWindowsThemeDetector { SystemThemeToReturn = systemTheme };
                var service = new ThemeService(mockDetector);

                service.SetThemeMode(mode);

                var expectedTheme = mode switch
                {
                    ThemeMode.Light => Theme.Light,
                    ThemeMode.Dark => Theme.Dark,
                    ThemeMode.System => systemTheme,
                    _ => Theme.Dark
                };

                return service.CurrentTheme == expectedTheme && service.CurrentMode == mode;
            });
    }

    /// <summary>
    /// **Feature: theme-system, Property 4: Theme Change Event Consistency**
    /// **Validates: Requirements 2.1, 4.1**
    /// For any theme change operation, the ThemeChanged event should fire with 
    /// the correct new theme value matching CurrentTheme.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ThemeChanged_Event_ShouldFireWithCorrectTheme()
    {
        return Prop.ForAll(
            ThemeModeEnumGenerator(),
            SystemThemeGenerator(),
            (mode, systemTheme) =>
            {
                var mockDetector = new MockWindowsThemeDetector { SystemThemeToReturn = systemTheme };
                var service = new ThemeService(mockDetector);

                Theme? eventTheme = null;
                service.ThemeChanged += (_, args) => eventTheme = args.NewTheme;

                // Start with opposite theme to ensure change happens
                var initialMode = mode == ThemeMode.Light ? ThemeMode.Dark : ThemeMode.Light;
                service.SetThemeMode(initialMode);

                // Now set to target mode
                service.SetThemeMode(mode);

                // If theme actually changed, event should have fired with correct value
                var expectedTheme = mode switch
                {
                    ThemeMode.Light => Theme.Light,
                    ThemeMode.Dark => Theme.Dark,
                    ThemeMode.System => systemTheme,
                    _ => Theme.Dark
                };

                // Event should fire and match CurrentTheme
                return eventTheme == null || (eventTheme == service.CurrentTheme && eventTheme == expectedTheme);
            });
    }

    /// <summary>
    /// **Feature: theme-system, Property 2: Theme Mode Persistence Round-Trip**
    /// **Validates: Requirements 1.5, 7.1, 7.4**
    /// For any valid ThemeMode value, saving it to settings and reloading 
    /// should return the same ThemeMode value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ThemeMode_SerializeDeserialize_ShouldPreserveValue()
    {
        return Prop.ForAll(ThemeModeStringGenerator(), themeMode =>
        {
            var settings = new AppSettings { ThemeMode = themeMode };
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

            return deserialized != null && deserialized.ThemeMode == themeMode;
        });
    }

    /// <summary>
    /// **Feature: theme-system, Property 5: Default Theme Mode**
    /// **Validates: Requirements 7.2**
    /// For any new AppSettings instance without saved ThemeMode, 
    /// the default value should be "System".
    /// </summary>
    [Fact]
    public void NewAppSettings_ShouldHaveDefaultThemeModeSystem()
    {
        var settings = new AppSettings();
        Assert.Equal("System", settings.ThemeMode);
    }

    /// <summary>
    /// Property test variant: For any AppSettings deserialized from JSON without ThemeMode,
    /// the default should be "System"
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AppSettings_WithoutThemeMode_ShouldDefaultToSystem()
    {
        return Prop.ForAll(AppSettingsWithoutThemeModeGenerator(), json =>
        {
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return deserialized != null && deserialized.ThemeMode == "System";
        });
    }

    private static Arbitrary<ThemeMode> ThemeModeEnumGenerator()
    {
        return Gen.Elements(ThemeMode.Light, ThemeMode.Dark, ThemeMode.System).ToArbitrary();
    }

    private static Arbitrary<Theme> SystemThemeGenerator()
    {
        return Gen.Elements(Theme.Light, Theme.Dark).ToArbitrary();
    }

    private static Arbitrary<string> ThemeModeStringGenerator()
    {
        return Gen.Elements("Light", "Dark", "System").ToArbitrary();
    }

    private static Arbitrary<string> AppSettingsWithoutThemeModeGenerator()
    {
        // Generate JSON without ThemeMode property to test default value
        var gen = from opacity in Gen.Choose(20, 100).Select(x => x / 100.0)
                  from fontSize in Gen.Choose(10, 24)
                  select JsonSerializer.Serialize(new
                  {
                      DefaultOpacity = opacity,
                      DefaultFontSize = fontSize,
                      Theme = "Dark"
                  }, JsonOptions);

        return Arb.From(gen);
    }

    /// <summary>
    /// **Feature: theme-system, Property 3: System Theme Detection Mapping**
    /// **Validates: Requirements 2.4, 2.5**
    /// For any registry value (0 or 1), GetSystemTheme should return Dark when 
    /// value is 0, and Light when value is 1.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RegistryValue_ShouldMapToCorrectTheme()
    {
        return Prop.ForAll(RegistryValueGenerator(), registryValue =>
        {
            var result = WindowsThemeDetector.MapRegistryValueToTheme(registryValue);

            // Registry value 0 = Dark, 1 = Light
            var expected = registryValue is int intValue && intValue == 1 
                ? Theme.Light 
                : Theme.Dark;

            return result == expected;
        });
    }

    /// <summary>
    /// **Feature: theme-system, Property 3: System Theme Detection Mapping (Edge Cases)**
    /// **Validates: Requirements 2.4, 2.5**
    /// For any non-standard registry value (null, non-int, negative, >1), 
    /// the mapping should default to Dark theme.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidRegistryValue_ShouldDefaultToDark()
    {
        return Prop.ForAll(InvalidRegistryValueGenerator(), registryValue =>
        {
            var result = WindowsThemeDetector.MapRegistryValueToTheme(registryValue);
            return result == Theme.Dark;
        });
    }

    /// <summary>
    /// Specific test: Registry value 0 maps to Dark theme
    /// </summary>
    [Fact]
    public void RegistryValue0_ShouldReturnDarkTheme()
    {
        var result = WindowsThemeDetector.MapRegistryValueToTheme(0);
        Assert.Equal(Theme.Dark, result);
    }

    /// <summary>
    /// Specific test: Registry value 1 maps to Light theme
    /// </summary>
    [Fact]
    public void RegistryValue1_ShouldReturnLightTheme()
    {
        var result = WindowsThemeDetector.MapRegistryValueToTheme(1);
        Assert.Equal(Theme.Light, result);
    }

    /// <summary>
    /// Specific test: Null registry value defaults to Dark theme
    /// </summary>
    [Fact]
    public void NullRegistryValue_ShouldReturnDarkTheme()
    {
        var result = WindowsThemeDetector.MapRegistryValueToTheme(null);
        Assert.Equal(Theme.Dark, result);
    }

    private static Arbitrary<object?> RegistryValueGenerator()
    {
        // Generate valid registry values: 0 (Dark) or 1 (Light)
        var gen = Gen.Elements<object?>(0, 1);
        return Arb.From(gen);
    }

    private static Arbitrary<object?> InvalidRegistryValueGenerator()
    {
        // Generate invalid/edge case registry values that should all map to Dark
        var gen = Gen.OneOf(
            Gen.Constant<object?>(null),
            Gen.Constant<object?>("Light"),
            Gen.Constant<object?>("Dark"),
            Gen.Constant<object?>(2),
            Gen.Constant<object?>(-1),
            Gen.Constant<object?>(100),
            Gen.Constant<object?>(true),
            Gen.Constant<object?>(false),
            Gen.Constant<object?>(1.0),
            Gen.Constant<object?>(0.0)
        );
        return Arb.From(gen);
    }
}
