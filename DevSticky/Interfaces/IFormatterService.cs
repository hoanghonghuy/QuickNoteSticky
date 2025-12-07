namespace DevSticky.Interfaces;

/// <summary>
/// Service for formatting JSON and XML content
/// </summary>
public interface IFormatterService
{
    string FormatJson(string input);
    string FormatXml(string input);
    bool IsValidJson(string input);
    bool IsValidXml(string input);
}
