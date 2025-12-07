using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for Snippet operations
/// **Feature: devsticky-v2, Properties 7-10: Snippet Operations**
/// **Validates: Requirements 3.2, 3.5, 3.7, 3.10**
/// </summary>
public class SnippetPropertyTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _testDirectory;
    private readonly string _testStoragePath;

    public SnippetPropertyTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"DevStickyTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _testStoragePath = Path.Combine(_testDirectory, "snippets.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    /// <summary>
    /// Property 7: Snippet data completeness
    /// For any saved snippet, all required fields (name, content, language, category) 
    /// should be non-empty and preserved after save.
    /// **Feature: devsticky-v2, Property 7: Snippet data completeness**
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Snippet_DataCompleteness_ShouldPreserveRequiredFields()
    {
        return Prop.ForAll(ValidSnippetGenerator(), snippet =>
        {
            var storagePath = Path.Combine(_testDirectory, $"data_{Guid.NewGuid()}.json");
            var service = new SnippetService(storagePath);
            var created = service.CreateSnippetAsync(snippet).GetAwaiter().GetResult();
            var retrieved = service.GetSnippetByIdAsync(created.Id).GetAwaiter().GetResult();

            return retrieved != null &&
                   !string.IsNullOrEmpty(retrieved.Name) &&
                   !string.IsNullOrEmpty(retrieved.Content) &&
                   !string.IsNullOrEmpty(retrieved.Language) &&
                   !string.IsNullOrEmpty(retrieved.Category) &&
                   retrieved.Name == snippet.Name &&
                   retrieved.Content == snippet.Content &&
                   retrieved.Language == snippet.Language &&
                   retrieved.Category == snippet.Category;
        });
    }

    /// <summary>
    /// Property 8: Placeholder parsing consistency
    /// For any snippet content with placeholder syntax ${n:name}, parsing should extract 
    /// all placeholders with correct index, name, and position.
    /// **Feature: devsticky-v2, Property 8: Placeholder parsing consistency**
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Snippet_PlaceholderParsing_ShouldExtractCorrectly()
    {
        return Prop.ForAll(PlaceholderContentGenerator(), content =>
        {
            var service = new SnippetService(_testStoragePath);
            var placeholders = service.ParsePlaceholders(content);

            // Count expected placeholders in content
            var expectedCount = System.Text.RegularExpressions.Regex.Matches(
                content, @"\$\{(\d+):([^:}]+)(?::([^}]*))?\}").Count;

            // All parsed placeholders should have valid data
            var allValid = placeholders.All(p =>
                p.Index > 0 &&
                !string.IsNullOrEmpty(p.Name) &&
                p.StartPosition >= 0 &&
                p.Length > 0 &&
                p.StartPosition + p.Length <= content.Length);

            // Verify positions are correct by checking the content at those positions
            var positionsCorrect = placeholders.All(p =>
            {
                var extracted = content.Substring(p.StartPosition, p.Length);
                return extracted.StartsWith("${") && extracted.EndsWith("}");
            });

            return placeholders.Count == expectedCount && allValid && positionsCorrect;
        });
    }

    /// <summary>
    /// Property 9: Snippet export/import round-trip
    /// For any collection of snippets, exporting to JSON and importing back 
    /// should produce an equivalent collection.
    /// **Feature: devsticky-v2, Property 9: Snippet export/import round-trip**
    /// **Validates: Requirements 3.7**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property Snippet_ExportImport_ShouldRoundTrip()
    {
        return Prop.ForAll(SnippetListGenerator(), snippets =>
        {
            var exportPath = Path.Combine(_testDirectory, $"export_{Guid.NewGuid()}.json");
            var service1 = new SnippetService(Path.Combine(_testDirectory, $"source_{Guid.NewGuid()}.json"));
            var service2 = new SnippetService(Path.Combine(_testDirectory, $"target_{Guid.NewGuid()}.json"));

            // Create all snippets in source service
            var createdSnippets = new List<Snippet>();
            foreach (var snippet in snippets)
            {
                var created = service1.CreateSnippetAsync(snippet).GetAwaiter().GetResult();
                createdSnippets.Add(created);
            }

            // Export from source
            service1.ExportSnippetsAsync(exportPath).GetAwaiter().GetResult();

            // Import to target
            service2.ImportSnippetsAsync(exportPath, ConflictResolution.Replace).GetAwaiter().GetResult();

            // Verify all snippets were imported
            var imported = service2.GetAllSnippetsAsync().GetAwaiter().GetResult();

            return imported.Count == createdSnippets.Count &&
                   createdSnippets.All(original =>
                       imported.Any(imp =>
                           imp.Id == original.Id &&
                           imp.Name == original.Name &&
                           imp.Content == original.Content &&
                           imp.Language == original.Language &&
                           imp.Category == original.Category));
        });
    }

    /// <summary>
    /// Property 10: Snippet search completeness
    /// For any snippet and search term, if the term appears in name, description, 
    /// content, or any tag, the snippet should be included in search results.
    /// **Feature: devsticky-v2, Property 10: Snippet search completeness**
    /// **Validates: Requirements 3.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Snippet_Search_ShouldFindMatchingSnippets()
    {
        return Prop.ForAll(SearchableSnippetGenerator(), testCase =>
        {
            var service = new SnippetService(Path.Combine(_testDirectory, $"search_{Guid.NewGuid()}.json"));

            // Create the snippet
            var created = service.CreateSnippetAsync(testCase.Snippet).GetAwaiter().GetResult();

            // Search for the term
            var results = service.SearchSnippetsAsync(testCase.SearchTerm).GetAwaiter().GetResult();

            // The snippet should be found if the search term appears in any searchable field
            var shouldBeFound = testCase.Snippet.Name.Contains(testCase.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                               testCase.Snippet.Description.Contains(testCase.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                               testCase.Snippet.Content.Contains(testCase.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                               testCase.Snippet.Tags.Any(t => t.Contains(testCase.SearchTerm, StringComparison.OrdinalIgnoreCase));

            var wasFound = results.Any(r => r.Id == created.Id);

            return shouldBeFound == wasFound;
        });
    }

    #region Generators

    private static Arbitrary<Snippet> ValidSnippetGenerator()
    {
        var gen = from name in Gen.Elements("MySnippet", "CodeBlock", "Template", "Helper", "Utility")
                  from content in Gen.Elements(
                      "console.log('hello');",
                      "public void Method() { }",
                      "SELECT * FROM table;",
                      "def function(): pass")
                  from language in Gen.Elements("JavaScript", "CSharp", "SQL", "Python")
                  from category in Gen.Elements("General", "Database", "Web", "Utilities")
                  from tagCount in Gen.Choose(0, 3)
                  from tags in Gen.ListOf(tagCount, Gen.Elements("common", "useful", "snippet", "code"))
                  select new Snippet
                  {
                      Name = name,
                      Content = content,
                      Language = language,
                      Category = category,
                      Tags = tags.Distinct().ToList()
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<string> PlaceholderContentGenerator()
    {
        var gen = from prefix in Gen.Elements("var ", "const ", "let ", "")
                  from index1 in Gen.Choose(1, 5)
                  from name1 in Gen.Elements("name", "value", "param", "arg")
                  from defaultVal1 in Gen.Elements("default", "value", "")
                  from middle in Gen.Elements(" = ", ": ", " => ")
                  from index2 in Gen.Choose(1, 5)
                  from name2 in Gen.Elements("type", "result", "output")
                  from suffix in Gen.Elements(";", "", "}")
                  let placeholder1 = string.IsNullOrEmpty(defaultVal1)
                      ? $"${{{index1}:{name1}}}"
                      : $"${{{index1}:{name1}:{defaultVal1}}}"
                  let placeholder2 = $"${{{index2}:{name2}}}"
                  select $"{prefix}{placeholder1}{middle}{placeholder2}{suffix}";

        return Arb.From(gen);
    }

    private static Arbitrary<List<Snippet>> SnippetListGenerator()
    {
        var snippetGen = ValidSnippetGenerator().Generator;
        var gen = from count in Gen.Choose(1, 5)
                  from snippets in Gen.ListOf(count, snippetGen)
                  select snippets.ToList();

        return Arb.From(gen);
    }

    private static Arbitrary<SearchTestCase> SearchableSnippetGenerator()
    {
        var gen = from name in Gen.Elements("MySnippet", "CodeBlock", "Template")
                  from description in Gen.Elements("A useful snippet", "Helper code", "Common pattern")
                  from content in Gen.Elements("console.log('test');", "public void Test() { }", "SELECT id FROM users;")
                  from language in Gen.Elements("JavaScript", "CSharp", "SQL")
                  from category in Gen.Elements("General", "Database", "Web")
                  from tags in Gen.ListOf(2, Gen.Elements("common", "useful", "test"))
                  from searchSource in Gen.Choose(0, 4) // 0=name, 1=description, 2=content, 3=tag, 4=random
                  let snippet = new Snippet
                  {
                      Name = name,
                      Description = description,
                      Content = content,
                      Language = language,
                      Category = category,
                      Tags = tags.Distinct().ToList()
                  }
                  let searchTerm = searchSource switch
                  {
                      0 => name.Substring(0, Math.Min(3, name.Length)),
                      1 => description.Split(' ').FirstOrDefault() ?? "useful",
                      2 => content.Substring(0, Math.Min(5, content.Length)),
                      3 => tags.FirstOrDefault() ?? "common",
                      _ => "xyz" // Random term unlikely to match
                  }
                  select new SearchTestCase { Snippet = snippet, SearchTerm = searchTerm };

        return Arb.From(gen);
    }

    #endregion
}

public class SearchTestCase
{
    public Snippet Snippet { get; set; } = new();
    public string SearchTerm { get; set; } = string.Empty;
}
