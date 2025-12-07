using System.IO;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// Service for formatting JSON and XML content
/// </summary>
public class FormatterService : IFormatterService
{
    private static readonly JsonSerializerOptions PrettyPrintOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Formats JSON with 2-space indentation.
    /// Returns original content if invalid JSON.
    /// </summary>
    public string FormatJson(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        try
        {
            using var doc = JsonDocument.Parse(input);
            return JsonSerializer.Serialize(doc.RootElement, PrettyPrintOptions);
        }
        catch (JsonException)
        {
            return input;
        }
    }

    /// <summary>
    /// Formats XML with proper indentation.
    /// Returns original content if invalid XML.
    /// </summary>
    public string FormatXml(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        try
        {
            var doc = XDocument.Parse(input);
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                OmitXmlDeclaration = !input.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            };

            using var sw = new StringWriter();
            using (var writer = XmlWriter.Create(sw, settings))
            {
                doc.WriteTo(writer);
            }
            return sw.ToString();
        }
        catch (XmlException)
        {
            return input;
        }
    }

    /// <summary>
    /// Checks if the input is valid JSON.
    /// </summary>
    public bool IsValidJson(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(input);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the input is valid XML.
    /// </summary>
    public bool IsValidXml(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        try
        {
            XDocument.Parse(input);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
