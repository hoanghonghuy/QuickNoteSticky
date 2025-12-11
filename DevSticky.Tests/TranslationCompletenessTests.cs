using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace DevSticky.Tests
{
    /// <summary>
    /// Tests to verify translation completeness between English and Vietnamese resource files.
    /// Validates: Requirements 3.4
    /// </summary>
    public class TranslationCompletenessTests
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

        [Fact]
        public void AllEnglishKeys_ShouldHaveTranslations()
        {
            // Arrange
            var englishKeys = GetResourceKeys(EnglishResourcePath);
            var vietnameseKeys = GetResourceKeys(VietnameseResourcePath);

            // Act
            var missingKeys = englishKeys.Except(vietnameseKeys).ToList();

            // Assert
            Assert.Empty(missingKeys);
        }

        [Fact]
        public void AllVietnameseKeys_ShouldHaveEnglishCounterparts()
        {
            // Arrange
            var englishKeys = GetResourceKeys(EnglishResourcePath);
            var vietnameseKeys = GetResourceKeys(VietnameseResourcePath);

            // Act
            var extraKeys = vietnameseKeys.Except(englishKeys).ToList();

            // Assert
            Assert.Empty(extraKeys);
        }

        [Fact]
        public void AllEnglishKeys_ShouldHaveNonEmptyValues()
        {
            // Arrange
            var englishEntries = GetResourceEntries(EnglishResourcePath);

            // Act
            var emptyValues = englishEntries
                .Where(kvp => string.IsNullOrWhiteSpace(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();

            // Assert
            Assert.Empty(emptyValues);
        }

        [Fact]
        public void AllVietnameseKeys_ShouldHaveNonEmptyValues()
        {
            // Arrange
            var vietnameseEntries = GetResourceEntries(VietnameseResourcePath);

            // Act
            var emptyValues = vietnameseEntries
                .Where(kvp => string.IsNullOrWhiteSpace(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();

            // Assert
            Assert.Empty(emptyValues);
        }

        [Fact]
        public void ResourceFiles_ShouldHaveSameKeyCount()
        {
            // Arrange
            var englishKeys = GetResourceKeys(EnglishResourcePath);
            var vietnameseKeys = GetResourceKeys(VietnameseResourcePath);

            // Assert
            Assert.Equal(englishKeys.Count, vietnameseKeys.Count);
        }

        private HashSet<string> GetResourceKeys(string resourcePath)
        {
            if (!File.Exists(resourcePath))
            {
                throw new FileNotFoundException($"Resource file not found: {resourcePath}");
            }

            var doc = XDocument.Load(resourcePath);
            var keys = doc.Descendants("data")
                .Select(e => e.Attribute("name")?.Value)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToHashSet();

            return keys;
        }

        private Dictionary<string, string> GetResourceEntries(string resourcePath)
        {
            if (!File.Exists(resourcePath))
            {
                throw new FileNotFoundException($"Resource file not found: {resourcePath}");
            }

            var doc = XDocument.Load(resourcePath);
            var entries = doc.Descendants("data")
                .Where(e => e.Attribute("name") != null)
                .ToDictionary(
                    e => e.Attribute("name")!.Value,
                    e => e.Element("value")?.Value ?? string.Empty
                );

            return entries;
        }
    }
}
