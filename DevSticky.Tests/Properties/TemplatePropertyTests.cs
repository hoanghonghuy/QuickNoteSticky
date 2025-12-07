using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevSticky.Models;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for Template operations
/// **Feature: devsticky-v2, Properties 19-24: Template Operations**
/// **Validates: Requirements 6.2, 6.3, 6.4, 6.5, 6.7, 6.9**
/// </summary>
public class TemplatePropertyTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _testDirectory;

    public TemplatePropertyTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"DevStickyTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    /// <summary>
    /// Property 19: Template application preserves structure
    /// For any template, creating a note from it should produce a note with the 
    /// template's content structure and default metadata.
    /// **Feature: devsticky-v2, Property 19: Template application preserves structure**
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Template_Application_ShouldPreserveStructure()
    {
        return Prop.ForAll(ValidTemplateGenerator(), template =>
        {
            var storagePath = Path.Combine(_testDirectory, $"templates_{Guid.NewGuid()}.json");
            var settings = new AppSettings { AuthorName = "TestUser" };
            var service = new TemplateService(storagePath, settings);

            var created = service.CreateTemplateAsync(template).GetAwaiter().GetResult();
            var note = service.CreateNoteFromTemplateAsync(created.Id).GetAwaiter().GetResult();

            // Note should have template's default language
            var hasCorrectLanguage = note.Language == template.DefaultLanguage;

            // Note should reference the template
            var hasTemplateId = note.TemplateId == created.Id;

            // Note title should match template name
            var hasCorrectTitle = note.Title == template.Name;

            // Content structure should be preserved (placeholders replaced but structure intact)
            // Check that content has similar line count (structure preserved)
            var templateLines = template.Content.Split('\n').Length;
            var noteLines = note.Content.Split('\n').Length;
            var structurePreserved = templateLines == noteLines;

            return hasCorrectLanguage && hasTemplateId && hasCorrectTitle && structurePreserved;
        });
    }


    /// <summary>
    /// Property 20: Template data completeness
    /// For any saved template, all required fields (name, content, defaultLanguage) 
    /// should be preserved.
    /// **Feature: devsticky-v2, Property 20: Template data completeness**
    /// **Validates: Requirements 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Template_DataCompleteness_ShouldPreserveRequiredFields()
    {
        return Prop.ForAll(ValidTemplateGenerator(), template =>
        {
            var storagePath = Path.Combine(_testDirectory, $"templates_{Guid.NewGuid()}.json");
            var service = new TemplateService(storagePath);

            var created = service.CreateTemplateAsync(template).GetAwaiter().GetResult();
            var retrieved = service.GetTemplateByIdAsync(created.Id).GetAwaiter().GetResult();

            return retrieved != null &&
                   !string.IsNullOrEmpty(retrieved.Name) &&
                   !string.IsNullOrEmpty(retrieved.Content) &&
                   !string.IsNullOrEmpty(retrieved.DefaultLanguage) &&
                   retrieved.Name == template.Name &&
                   retrieved.Content == template.Content &&
                   retrieved.DefaultLanguage == template.DefaultLanguage &&
                   retrieved.Category == template.Category;
        });
    }

    /// <summary>
    /// Property 21: Date placeholder replacement
    /// For any template with {{date}} or {{datetime}} placeholders, the resulting note 
    /// should have those placeholders replaced with valid date strings.
    /// **Feature: devsticky-v2, Property 21: Date placeholder replacement**
    /// **Validates: Requirements 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Template_DatePlaceholder_ShouldBeReplaced()
    {
        return Prop.ForAll(DatePlaceholderTemplateGenerator(), template =>
        {
            var storagePath = Path.Combine(_testDirectory, $"templates_{Guid.NewGuid()}.json");
            var settings = new AppSettings();
            var service = new TemplateService(storagePath, settings);

            var created = service.CreateTemplateAsync(template).GetAwaiter().GetResult();
            var note = service.CreateNoteFromTemplateAsync(created.Id).GetAwaiter().GetResult();

            // Content should not contain {{date}} or {{datetime}} placeholders
            var noDatePlaceholder = !note.Content.Contains("{{date}}");
            var noDateTimePlaceholder = !note.Content.Contains("{{datetime}}");

            // Content should contain a date-like string (yyyy-MM-dd format)
            var containsDateFormat = System.Text.RegularExpressions.Regex.IsMatch(
                note.Content, @"\d{4}-\d{2}-\d{2}");

            return noDatePlaceholder && noDateTimePlaceholder && containsDateFormat;
        });
    }

    /// <summary>
    /// Property 22: User placeholder replacement
    /// For any template with {{author}} placeholder and configured user info, 
    /// the resulting note should have the placeholder replaced with the user info.
    /// **Feature: devsticky-v2, Property 22: User placeholder replacement**
    /// **Validates: Requirements 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Template_UserPlaceholder_ShouldBeReplaced()
    {
        return Prop.ForAll(UserPlaceholderTemplateGenerator(), AuthorNameGenerator(), (template, authorName) =>
        {
            var storagePath = Path.Combine(_testDirectory, $"templates_{Guid.NewGuid()}.json");
            var settings = new AppSettings { AuthorName = authorName };
            var service = new TemplateService(storagePath, settings);

            var created = service.CreateTemplateAsync(template).GetAwaiter().GetResult();
            var note = service.CreateNoteFromTemplateAsync(created.Id).GetAwaiter().GetResult();

            // Content should not contain {{author}} placeholder
            var noAuthorPlaceholder = !note.Content.Contains("{{author}}");

            // Content should contain the author name
            var containsAuthorName = note.Content.Contains(authorName);

            return noAuthorPlaceholder && containsAuthorName;
        });
    }


    /// <summary>
    /// Property 23: Note to template conversion
    /// For any note saved as template, the template content should match the original note content.
    /// **Feature: devsticky-v2, Property 23: Note to template conversion**
    /// **Validates: Requirements 6.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Template_FromNote_ShouldPreserveContent()
    {
        return Prop.ForAll(ValidNoteGenerator(), TemplateMetadataGenerator(), (note, metadata) =>
        {
            var storagePath = Path.Combine(_testDirectory, $"templates_{Guid.NewGuid()}.json");
            var service = new TemplateService(storagePath);

            var template = service.CreateTemplateFromNoteAsync(
                note, 
                metadata.Name, 
                metadata.Description, 
                metadata.Category
            ).GetAwaiter().GetResult();

            // Template content should match note content
            var contentMatches = template.Content == note.Content;

            // Template language should match note language
            var languageMatches = template.DefaultLanguage == note.Language;

            // Template metadata should be set correctly
            var nameMatches = template.Name == metadata.Name;
            var descriptionMatches = template.Description == metadata.Description;
            var categoryMatches = template.Category == metadata.Category;

            // Template should not be built-in
            var notBuiltIn = !template.IsBuiltIn;

            return contentMatches && languageMatches && nameMatches && 
                   descriptionMatches && categoryMatches && notBuiltIn;
        });
    }

    /// <summary>
    /// Property 24: Template export/import round-trip
    /// For any collection of templates, exporting to JSON and importing back 
    /// should produce an equivalent collection.
    /// **Feature: devsticky-v2, Property 24: Template export/import round-trip**
    /// **Validates: Requirements 6.9**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property Template_ExportImport_ShouldRoundTrip()
    {
        return Prop.ForAll(TemplateListGenerator(), templates =>
        {
            var exportPath = Path.Combine(_testDirectory, $"export_{Guid.NewGuid()}.json");
            var service1 = new TemplateService(Path.Combine(_testDirectory, $"source_{Guid.NewGuid()}.json"));
            var service2 = new TemplateService(Path.Combine(_testDirectory, $"target_{Guid.NewGuid()}.json"));

            // Create all templates in source service
            var createdTemplates = new List<NoteTemplate>();
            foreach (var template in templates)
            {
                var created = service1.CreateTemplateAsync(template).GetAwaiter().GetResult();
                createdTemplates.Add(created);
            }

            // Export from source
            service1.ExportTemplatesAsync(exportPath).GetAwaiter().GetResult();

            // Import to target
            service2.ImportTemplatesAsync(exportPath).GetAwaiter().GetResult();

            // Verify all templates were imported (excluding built-in)
            var allTemplates = service2.GetAllTemplatesAsync().GetAwaiter().GetResult();
            var imported = allTemplates.Where(t => !t.IsBuiltIn).ToList();

            return imported.Count == createdTemplates.Count &&
                   createdTemplates.All(original =>
                       imported.Any(imp =>
                           imp.Id == original.Id &&
                           imp.Name == original.Name &&
                           imp.Content == original.Content &&
                           imp.DefaultLanguage == original.DefaultLanguage &&
                           imp.Category == original.Category));
        });
    }

    #region Generators

    private static Arbitrary<NoteTemplate> ValidTemplateGenerator()
    {
        var gen = from name in Gen.Elements("MyTemplate", "CodeTemplate", "NoteTemplate", "QuickNote")
                  from description in Gen.Elements("A useful template", "Quick note template", "Development template")
                  from category in Gen.Elements("General", "Development", "Meeting", "Personal")
                  from content in Gen.Elements(
                      "# Title\n\nContent here",
                      "## Header\n\n- Item 1\n- Item 2",
                      "Task: \nDue: \nNotes: ")
                  from language in Gen.Elements("Markdown", "PlainText")
                  from tagCount in Gen.Choose(0, 3)
                  from tags in Gen.ListOf(tagCount, Gen.Elements("note", "template", "quick", "dev"))
                  select new NoteTemplate
                  {
                      Name = name,
                      Description = description,
                      Category = category,
                      Content = content,
                      DefaultLanguage = language,
                      DefaultTags = tags.Distinct().ToList()
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<NoteTemplate> DatePlaceholderTemplateGenerator()
    {
        var gen = from name in Gen.Elements("DateTemplate", "MeetingTemplate")
                  from placeholderType in Gen.Elements("{{date}}", "{{datetime}}")
                  from prefix in Gen.Elements("# Meeting - ", "Date: ", "Created: ")
                  from suffix in Gen.Elements("\n\nNotes:", "\n\n## Content", "")
                  select new NoteTemplate
                  {
                      Name = name,
                      Description = "Template with date placeholder",
                      Category = "General",
                      Content = $"{prefix}{placeholderType}{suffix}",
                      DefaultLanguage = "Markdown",
                      DefaultTags = new List<string>()
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<NoteTemplate> UserPlaceholderTemplateGenerator()
    {
        var gen = from name in Gen.Elements("AuthorTemplate", "UserTemplate")
                  from prefix in Gen.Elements("Author: ", "Created by: ", "## ")
                  from suffix in Gen.Elements("\n\nContent", "\n\n---", "")
                  select new NoteTemplate
                  {
                      Name = name,
                      Description = "Template with author placeholder",
                      Category = "General",
                      Content = $"{prefix}{{{{author}}}}{suffix}",
                      DefaultLanguage = "Markdown",
                      DefaultTags = new List<string>()
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<string> AuthorNameGenerator()
    {
        return Arb.From(Gen.Elements("John Doe", "Jane Smith", "Dev User", "Test Author"));
    }

    private static Arbitrary<Note> ValidNoteGenerator()
    {
        var gen = from title in Gen.Elements("My Note", "Quick Note", "Dev Notes")
                  from content in Gen.Elements(
                      "# Title\n\nSome content here",
                      "## Notes\n\n- Item 1\n- Item 2",
                      "Plain text content")
                  from language in Gen.Elements("Markdown", "PlainText", "CSharp")
                  select new Note
                  {
                      Id = Guid.NewGuid(),
                      Title = title,
                      Content = content,
                      Language = language,
                      CreatedDate = DateTime.UtcNow,
                      ModifiedDate = DateTime.UtcNow
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<TemplateMetadata> TemplateMetadataGenerator()
    {
        var gen = from name in Gen.Elements("New Template", "Custom Template", "My Template")
                  from description in Gen.Elements("A custom template", "User template", "Quick template")
                  from category in Gen.Elements("Custom", "Personal", "Work")
                  select new TemplateMetadata
                  {
                      Name = name,
                      Description = description,
                      Category = category
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<List<NoteTemplate>> TemplateListGenerator()
    {
        var templateGen = ValidTemplateGenerator().Generator;
        var gen = from count in Gen.Choose(1, 5)
                  from templates in Gen.ListOf(count, templateGen)
                  select templates.ToList();

        return Arb.From(gen);
    }

    #endregion
}

public class TemplateMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
