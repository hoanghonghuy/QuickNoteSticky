using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevSticky.Helpers;

/// <summary>
/// Factory for creating standardized JsonSerializerOptions configurations.
/// Eliminates duplication of JSON serialization settings across the application.
/// </summary>
public static class JsonSerializerOptionsFactory
{
    /// <summary>
    /// Default JSON serialization options with indented formatting.
    /// Use for file storage and human-readable output.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Compact JSON serialization options without indentation.
    /// Use for network transmission and minimal file size.
    /// </summary>
    public static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
