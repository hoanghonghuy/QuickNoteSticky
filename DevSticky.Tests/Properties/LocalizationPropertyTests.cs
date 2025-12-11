using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for localization key existence
/// **Feature: code-refactor, Property 5: Localization Key Existence**
/// **Validates: Requirements 3.1, 3.2**
/// </summary>
public class LocalizationPropertyTests
{
    private static string GetProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "DevSticky.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Could not find project root");
    }

    private static string EnglishResourcePath => Path.Combine(GetProjectRoot(), "DevSticky", "Resources", "Strings.resx");
    private static string VietnameseResourcePath => Path.Combine(GetProjectRoot(), "DevSticky", "Resources", "Strings.vi.resx");

    /// <summary>
    /// Property 5: Localization Key Existence
    /// For any localization key used in code, the key should exist in the resource file.
    /// **Feature: code-refactor, Property 5: Localization Key Existence**
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LocalizationKeys_UsedInCode_ShouldExistInResourceFiles()
    {
        return Prop.ForAll(LocalizationKeyGenerator(), key =>
        {
            // Get all keys from resource files
            var englishKeys = GetResourceKeys(EnglishResourcePath);
            var vietnameseKeys = GetResourceKeys(VietnameseResourcePath);

            // Test that the key exists in both resource files
            var existsInEnglish = englishKeys.Contains(key);
            var existsInVietnamese = vietnameseKeys.Contains(key);

            // Test that LocalizationService can retrieve the key
            var localizationService = LocalizationService.Instance;
            var englishValue = GetLocalizedValue(localizationService, key, "en");
            var vietnameseValue = GetLocalizedValue(localizationService, key, "vi");

            // Key should exist in both resource files and return non-empty values
            // Note: It's valid for the localized value to be the same as the key (e.g., "Yes" -> "Yes")
            // We only check that the service doesn't return null/empty (which would indicate missing key)
            var result = existsInEnglish && 
                        existsInVietnamese && 
                        !string.IsNullOrEmpty(englishValue) && 
                        !string.IsNullOrEmpty(vietnameseValue);

            if (!result)
            {
                var message = $"Key '{key}': ExistsInEnglish={existsInEnglish}, ExistsInVietnamese={existsInVietnamese}, " +
                             $"EnglishValue='{englishValue}', VietnameseValue='{vietnameseValue}'";
                return result.ToProperty().Label(message);
            }

            return result.ToProperty();
        });
    }

    /// <summary>
    /// Property test to verify that all keys found in code exist in resource files
    /// </summary>
    [Property(MaxTest = 50)]
    public Property AllKeysFoundInCode_ShouldExistInResourceFiles()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                // Get all localization keys used in the codebase
                var keysUsedInCode = GetLocalizationKeysFromCode();
                var englishKeys = GetResourceKeys(EnglishResourcePath);
                var vietnameseKeys = GetResourceKeys(VietnameseResourcePath);

                // Check that all keys used in code exist in both resource files
                var missingInEnglish = keysUsedInCode.Except(englishKeys).ToList();
                var missingInVietnamese = keysUsedInCode.Except(vietnameseKeys).ToList();

                // If there are missing keys, the property fails
                var result = missingInEnglish.Count == 0 && missingInVietnamese.Count == 0;
                
                if (!result)
                {
                    var message = $"Missing keys - English: [{string.Join(", ", missingInEnglish)}], Vietnamese: [{string.Join(", ", missingInVietnamese)}]";
                    return result.ToProperty().Label(message);
                }
                
                return result.ToProperty();
            });
    }

    /// <summary>
    /// Property test to verify resource file consistency
    /// </summary>
    [Property(MaxTest = 10)]
    public Property ResourceFiles_ShouldHaveConsistentKeys()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var englishKeys = GetResourceKeys(EnglishResourcePath);
                var vietnameseKeys = GetResourceKeys(VietnameseResourcePath);

                // Both resource files should have the same keys
                var missingInVietnamese = englishKeys.Except(vietnameseKeys).ToList();
                var extraInVietnamese = vietnameseKeys.Except(englishKeys).ToList();

                var result = missingInVietnamese.Count == 0 && extraInVietnamese.Count == 0;
                
                if (!result)
                {
                    var message = $"Inconsistent keys - Missing in Vietnamese: [{string.Join(", ", missingInVietnamese)}], Extra in Vietnamese: [{string.Join(", ", extraInVietnamese)}]";
                    return result.ToProperty().Label(message);
                }
                
                return result.ToProperty();
            });
    }

    private static Arbitrary<string> LocalizationKeyGenerator()
    {
        // Generate keys from actual resource files to test existing keys
        var englishKeys = GetResourceKeys(EnglishResourcePath);
        var keysArray = englishKeys.ToArray();
        
        if (keysArray.Length == 0)
        {
            // Fallback to some common keys if resource file is empty
            keysArray = new[] { "OK", "Cancel", "Save", "Delete", "Settings" };
        }

        var gen = Gen.Elements(keysArray);
        return Arb.From(gen);
    }

    private static HashSet<string> GetResourceKeys(string resourcePath)
    {
        if (!File.Exists(resourcePath))
        {
            return new HashSet<string>();
        }

        try
        {
            var doc = XDocument.Load(resourcePath);
            var keys = doc.Descendants("data")
                .Select(e => e.Attribute("name")?.Value)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToHashSet();

            return keys;
        }
        catch
        {
            return new HashSet<string>();
        }
    }

    private static string GetLocalizedValue(LocalizationService service, string key, string cultureCode)
    {
        try
        {
            var originalCulture = service.CurrentCulture;
            service.SetCulture(cultureCode);
            var value = service.GetString(key);
            service.CurrentCulture = originalCulture; // Restore original culture
            return value;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static HashSet<string> GetLocalizationKeysFromCode()
    {
        var keys = new HashSet<string>();
        var projectRoot = GetProjectRoot();
        var devStickyPath = Path.Combine(projectRoot, "DevSticky");

        if (!Directory.Exists(devStickyPath))
        {
            return keys;
        }

        // Patterns to match L.Get("key") and L.Get("key", args)
        var patterns = new[]
        {
            @"L\.Get\s*\(\s*[""']([^""']+)[""']",
            @"LocalizationService\.Instance\.GetString\s*\(\s*[""']([^""']+)[""']",
            @"LocalizationService\.Instance\[\s*[""']([^""']+)[""']\s*\]"
        };

        try
        {
            var csFiles = Directory.GetFiles(devStickyPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"));

            foreach (var file in csFiles)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    
                    foreach (var pattern in patterns)
                    {
                        var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                        foreach (Match match in matches)
                        {
                            if (match.Groups.Count > 1)
                            {
                                keys.Add(match.Groups[1].Value);
                            }
                        }
                    }
                }
                catch
                {
                    // Skip files that can't be read
                    continue;
                }
            }
        }
        catch
        {
            // If we can't scan the code, return empty set
        }

        return keys;
    }
}