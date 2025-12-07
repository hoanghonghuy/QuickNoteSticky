using System.Text.Json;
using System.Xml.Linq;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for FormatterService
/// </summary>
public class FormatterPropertyTests
{
    private readonly FormatterService _formatter = new();

    /// <summary>
    /// **Feature: devsticky, Property 7: JSON Format Round-Trip**
    /// **Validates: Requirements 6.1**
    /// For any valid JSON string, formatting (pretty-print) and then parsing 
    /// SHALL produce semantically equivalent JSON.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FormatJson_ShouldPreserveSemantics()
    {
        var jsonGen = from key in Gen.Elements("name", "value", "data", "id", "count")
                      from strVal in Gen.Elements("test", "hello", "world", "foo", "bar")
                      from numVal in Gen.Choose(0, 1000)
                      from boolVal in Arb.Generate<bool>()
                      select $"{{\"{key}\": \"{strVal}\", \"number\": {numVal}, \"flag\": {boolVal.ToString().ToLower()}}}";

        return Prop.ForAll(
            Arb.From(jsonGen),
            (string json) =>
            {
                var formatted = _formatter.FormatJson(json);
                using var original = JsonDocument.Parse(json);
                using var result = JsonDocument.Parse(formatted);
                return JsonEquals(original.RootElement, result.RootElement);
            });
    }

    /// <summary>
    /// **Feature: devsticky, Property 8: XML Format Round-Trip**
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FormatXml_ShouldPreserveSemantics()
    {
        var xmlGen = from root in Gen.Elements("root", "data", "item", "config")
                     from child in Gen.Elements("name", "value", "id")
                     from content in Gen.Elements("test", "hello", "world")
                     select $"<{root}><{child}>{content}</{child}></{root}>";

        return Prop.ForAll(
            Arb.From(xmlGen),
            (string xml) =>
            {
                var formatted = _formatter.FormatXml(xml);
                var original = XDocument.Parse(xml);
                var result = XDocument.Parse(formatted);
                return XNode.DeepEquals(original, result);
            });
    }


    /// <summary>
    /// **Feature: devsticky, Property 9: Invalid Format Preservation**
    /// **Validates: Requirements 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidFormat_ShouldPreserveOriginal()
    {
        var invalidGen = Gen.Elements(
            "not json or xml",
            "{ invalid json",
            "<unclosed tag",
            "random text 123",
            "{{{{",
            ">>>>",
            "hello world",
            "12345",
            "true false null"
        );

        return Prop.ForAll(
            Arb.From(invalidGen),
            (string invalid) =>
            {
                var jsonResult = _formatter.FormatJson(invalid);
                var xmlResult = _formatter.FormatXml(invalid);
                return jsonResult == invalid && xmlResult == invalid;
            });
    }

    private static bool JsonEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
            return false;

        return a.ValueKind switch
        {
            JsonValueKind.Object => ObjectEquals(a, b),
            JsonValueKind.Array => ArrayEquals(a, b),
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Number => a.GetDecimal() == b.GetDecimal(),
            JsonValueKind.True or JsonValueKind.False => a.GetBoolean() == b.GetBoolean(),
            JsonValueKind.Null => true,
            _ => false
        };
    }

    private static bool ObjectEquals(JsonElement a, JsonElement b)
    {
        var aProps = a.EnumerateObject().OrderBy(p => p.Name).ToList();
        var bProps = b.EnumerateObject().OrderBy(p => p.Name).ToList();
        if (aProps.Count != bProps.Count) return false;
        return aProps.Zip(bProps, (ap, bp) => 
            ap.Name == bp.Name && JsonEquals(ap.Value, bp.Value)).All(x => x);
    }

    private static bool ArrayEquals(JsonElement a, JsonElement b)
    {
        var aItems = a.EnumerateArray().ToList();
        var bItems = b.EnumerateArray().ToList();
        if (aItems.Count != bItems.Count) return false;
        return aItems.Zip(bItems, JsonEquals).All(x => x);
    }
}
