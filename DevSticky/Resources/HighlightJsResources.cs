using System.IO;
using System.Reflection;

namespace DevSticky.Resources;

/// <summary>
/// Provides access to embedded Highlight.js resources for syntax highlighting in markdown preview
/// </summary>
public static class HighlightJsResources
{
    private static string? _highlightJs;
    private static string? _darkCss;
    private static string? _lightCss;

    /// <summary>
    /// Gets the Highlight.js library code (minified)
    /// </summary>
    public static string HighlightJs => _highlightJs ??= LoadResource("DevSticky.Resources.highlight.min.js");

    /// <summary>
    /// Gets the dark theme CSS (VS2015/VSCode Dark+)
    /// </summary>
    public static string DarkCss => _darkCss ??= LoadResource("DevSticky.Resources.highlight-dark.css");

    /// <summary>
    /// Gets the light theme CSS (VS/VSCode Light)
    /// </summary>
    public static string LightCss => _lightCss ??= LoadResource("DevSticky.Resources.highlight-light.css");

    /// <summary>
    /// Gets the appropriate CSS for the given theme
    /// </summary>
    public static string GetCssForTheme(bool isDark) => isDark ? DarkCss : LightCss;

    private static string LoadResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        
        if (stream == null)
        {
            System.Diagnostics.Debug.WriteLine($"Resource not found: {resourceName}");
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
