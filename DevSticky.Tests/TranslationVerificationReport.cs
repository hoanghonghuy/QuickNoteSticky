using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Xml.Linq;

namespace DevSticky.Tests
{
    /// <summary>
    /// Utility class to generate a translation completeness report.
    /// </summary>
    public class TranslationVerificationReport
    {
        public static void GenerateReport()
        {
            var projectRoot = GetProjectRoot();
            var englishPath = Path.Combine(projectRoot, "DevSticky", "Resources", "Strings.resx");
            var vietnamesePath = Path.Combine(projectRoot, "DevSticky", "Resources", "Strings.vi.resx");

            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("TRANSLATION COMPLETENESS VERIFICATION REPORT");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            // Load resource files
            var englishKeys = GetResourceKeys(englishPath);
            var vietnameseKeys = GetResourceKeys(vietnamesePath);
            var englishEntries = GetResourceEntries(englishPath);
            var vietnameseEntries = GetResourceEntries(vietnamesePath);

            // Check 1: All English keys have Vietnamese translations
            Console.WriteLine("1. Checking English → Vietnamese translation coverage...");
            var missingInVietnamese = englishKeys.Except(vietnameseKeys).ToList();
            if (missingInVietnamese.Any())
            {
                Console.WriteLine($"   ❌ FAILED: {missingInVietnamese.Count} keys missing in Vietnamese:");
                foreach (var key in missingInVietnamese.Take(10))
                {
                    Console.WriteLine($"      - {key}");
                }
                if (missingInVietnamese.Count > 10)
                {
                    Console.WriteLine($"      ... and {missingInVietnamese.Count - 10} more");
                }
            }
            else
            {
                Console.WriteLine($"   ✓ PASSED: All {englishKeys.Count} English keys have Vietnamese translations");
            }
            Console.WriteLine();

            // Check 2: All Vietnamese keys have English counterparts
            Console.WriteLine("2. Checking Vietnamese → English key consistency...");
            var extraInVietnamese = vietnameseKeys.Except(englishKeys).ToList();
            if (extraInVietnamese.Any())
            {
                Console.WriteLine($"   ❌ FAILED: {extraInVietnamese.Count} extra keys in Vietnamese:");
                foreach (var key in extraInVietnamese.Take(10))
                {
                    Console.WriteLine($"      - {key}");
                }
                if (extraInVietnamese.Count > 10)
                {
                    Console.WriteLine($"      ... and {extraInVietnamese.Count - 10} more");
                }
            }
            else
            {
                Console.WriteLine($"   ✓ PASSED: All Vietnamese keys have English counterparts");
            }
            Console.WriteLine();

            // Check 3: No empty values in English
            Console.WriteLine("3. Checking for empty English values...");
            var emptyEnglish = englishEntries.Where(kvp => string.IsNullOrWhiteSpace(kvp.Value)).ToList();
            if (emptyEnglish.Any())
            {
                Console.WriteLine($"   ❌ FAILED: {emptyEnglish.Count} empty values in English:");
                foreach (var kvp in emptyEnglish.Take(10))
                {
                    Console.WriteLine($"      - {kvp.Key}");
                }
            }
            else
            {
                Console.WriteLine($"   ✓ PASSED: All English keys have non-empty values");
            }
            Console.WriteLine();

            // Check 4: No empty values in Vietnamese
            Console.WriteLine("4. Checking for empty Vietnamese values...");
            var emptyVietnamese = vietnameseEntries.Where(kvp => string.IsNullOrWhiteSpace(kvp.Value)).ToList();
            if (emptyVietnamese.Any())
            {
                Console.WriteLine($"   ❌ FAILED: {emptyVietnamese.Count} empty values in Vietnamese:");
                foreach (var kvp in emptyVietnamese.Take(10))
                {
                    Console.WriteLine($"      - {kvp.Key}");
                }
            }
            else
            {
                Console.WriteLine($"   ✓ PASSED: All Vietnamese keys have non-empty values");
            }
            Console.WriteLine();

            // Summary
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("SUMMARY");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"Total English keys:    {englishKeys.Count}");
            Console.WriteLine($"Total Vietnamese keys: {vietnameseKeys.Count}");
            Console.WriteLine($"Missing translations:  {missingInVietnamese.Count}");
            Console.WriteLine($"Extra translations:    {extraInVietnamese.Count}");
            Console.WriteLine($"Empty English values:  {emptyEnglish.Count}");
            Console.WriteLine($"Empty Vietnamese values: {emptyVietnamese.Count}");
            Console.WriteLine();

            var allPassed = !missingInVietnamese.Any() && !extraInVietnamese.Any() && 
                           !emptyEnglish.Any() && !emptyVietnamese.Any();
            
            if (allPassed)
            {
                Console.WriteLine("✓ ALL CHECKS PASSED - Translation is 100% complete!");
            }
            else
            {
                Console.WriteLine("❌ SOME CHECKS FAILED - Please review the issues above");
            }
            Console.WriteLine("=".PadRight(80, '='));
        }

        private static string GetProjectRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "DevSticky.sln")))
            {
                directory = directory.Parent;
            }
            return directory?.FullName ?? throw new InvalidOperationException("Could not find project root");
        }

        private static HashSet<string> GetResourceKeys(string resourcePath)
        {
            if (!File.Exists(resourcePath))
            {
                throw new FileNotFoundException($"Resource file not found: {resourcePath}");
            }

            var doc = XDocument.Load(resourcePath);
            var keys = doc.Descendants("data")
                .Select(e => e.Attribute("name")?.Value)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToHashSet();

            return keys!;
        }

        private static Dictionary<string, string> GetResourceEntries(string resourcePath)
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
