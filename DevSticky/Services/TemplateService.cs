using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for managing note templates with storage, placeholder support, and built-in templates
/// </summary>
public partial class TemplateService : ITemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    // Regex pattern for placeholder syntax: {{name}} or {{type:name}}
    [GeneratedRegex(@"\{\{(\w+)(?::(\w+))?\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    private readonly string _storagePath;
    private readonly List<NoteTemplate> _templates = new();
    private readonly List<NoteTemplate> _builtInTemplates;
    private readonly AppSettings _settings;
    private readonly IErrorHandler _errorHandler;
    private bool _isLoaded;

    public TemplateService(AppSettings settings, IErrorHandler errorHandler)
    {
        _settings = settings;
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _storagePath = PathHelper.Combine(
            PathHelper.GetAppDataPath(AppConstants.AppDataFolderName),
            AppConstants.TemplatesFileName);
        _builtInTemplates = CreateBuiltInTemplates();
    }

    /// <summary>
    /// Constructor for testing with custom storage path
    /// </summary>
    public TemplateService(string storagePath, AppSettings? settings = null, IErrorHandler? errorHandler = null)
    {
        _storagePath = storagePath;
        _settings = settings ?? new AppSettings();
        _errorHandler = errorHandler ?? new ErrorHandler();
        _builtInTemplates = CreateBuiltInTemplates();
    }


    private static List<NoteTemplate> CreateBuiltInTemplates()
    {
        return new List<NoteTemplate>
        {
            new NoteTemplate
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                Name = "Meeting Notes",
                Description = "Template for capturing meeting notes with attendees, agenda, and action items",
                Category = "Meeting",
                Content = @"# Meeting Notes - {{date}}

## Attendees
- {{author}}
- 

## Agenda
1. 

## Discussion Notes


## Action Items
- [ ] 

## Next Meeting
Date: 
",
                DefaultLanguage = "Markdown",
                DefaultTags = new List<string> { "meeting", "notes" },
                Placeholders = new List<TemplatePlaceholder>
                {
                    new() { Name = "date", DisplayName = "Meeting Date", Type = PlaceholderType.Date, DefaultValue = "" },
                    new() { Name = "author", DisplayName = "Your Name", Type = PlaceholderType.User, DefaultValue = "" }
                },
                IsBuiltIn = true,
                CreatedDate = DateTime.UtcNow
            },
            new NoteTemplate
            {
                Id = new Guid("00000000-0000-0000-0000-000000000002"),
                Name = "Code Review",
                Description = "Template for code review comments and feedback",
                Category = "Development",
                Content = @"# Code Review - {{date}}

## File/PR
**File:** 
**Reviewer:** {{author}}

## Summary


## Comments

### Positive
- 

### Suggestions
- 

### Issues
- 

## Approval Status
- [ ] Approved
- [ ] Needs Changes
- [ ] Rejected
",
                DefaultLanguage = "Markdown",
                DefaultTags = new List<string> { "code-review", "development" },
                Placeholders = new List<TemplatePlaceholder>
                {
                    new() { Name = "date", DisplayName = "Review Date", Type = PlaceholderType.Date, DefaultValue = "" },
                    new() { Name = "author", DisplayName = "Reviewer Name", Type = PlaceholderType.User, DefaultValue = "" }
                },
                IsBuiltIn = true,
                CreatedDate = DateTime.UtcNow
            },
            new NoteTemplate
            {
                Id = new Guid("00000000-0000-0000-0000-000000000003"),
                Name = "Bug Report",
                Description = "Template for documenting bugs with steps to reproduce",
                Category = "Development",
                Content = @"# Bug Report - {{datetime}}

## Title


## Environment
- OS: 
- Version: 
- Browser/Runtime: 

## Steps to Reproduce
1. 
2. 
3. 

## Expected Behavior


## Actual Behavior


## Screenshots/Logs


## Priority
- [ ] Critical
- [ ] High
- [ ] Medium
- [ ] Low

## Reported By
{{author}}
",
                DefaultLanguage = "Markdown",
                DefaultTags = new List<string> { "bug", "issue" },
                Placeholders = new List<TemplatePlaceholder>
                {
                    new() { Name = "datetime", DisplayName = "Report Date/Time", Type = PlaceholderType.DateTime, DefaultValue = "" },
                    new() { Name = "author", DisplayName = "Reporter Name", Type = PlaceholderType.User, DefaultValue = "" }
                },
                IsBuiltIn = true,
                CreatedDate = DateTime.UtcNow
            },
            new NoteTemplate
            {
                Id = new Guid("00000000-0000-0000-0000-000000000004"),
                Name = "Daily Standup",
                Description = "Template for daily standup meeting notes",
                Category = "Meeting",
                Content = @"# Daily Standup - {{date}}

## {{author}}

### Yesterday
- 

### Today
- 

### Blockers
- None

---
",
                DefaultLanguage = "Markdown",
                DefaultTags = new List<string> { "standup", "daily" },
                Placeholders = new List<TemplatePlaceholder>
                {
                    new() { Name = "date", DisplayName = "Standup Date", Type = PlaceholderType.Date, DefaultValue = "" },
                    new() { Name = "author", DisplayName = "Your Name", Type = PlaceholderType.User, DefaultValue = "" }
                },
                IsBuiltIn = true,
                CreatedDate = DateTime.UtcNow
            },
            new NoteTemplate
            {
                Id = new Guid("00000000-0000-0000-0000-000000000005"),
                Name = "TODO List",
                Description = "Simple TODO list with priority markers",
                Category = "Personal",
                Content = @"# TODO List - {{date}}

## High Priority
- [ ] 

## Medium Priority
- [ ] 

## Low Priority
- [ ] 

## Completed
- [x] 
",
                DefaultLanguage = "Markdown",
                DefaultTags = new List<string> { "todo", "tasks" },
                Placeholders = new List<TemplatePlaceholder>
                {
                    new() { Name = "date", DisplayName = "Date", Type = PlaceholderType.Date, DefaultValue = "" }
                },
                IsBuiltIn = true,
                CreatedDate = DateTime.UtcNow
            }
        };
    }


    private async Task EnsureLoadedAsync()
    {
        if (!_isLoaded)
        {
            await LoadTemplatesAsync();
            _isLoaded = true;
        }
    }

    private async Task LoadTemplatesAsync()
    {
        await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            if (File.Exists(_storagePath))
            {
                var json = await File.ReadAllTextAsync(_storagePath).ConfigureAwait(false);
                var templates = JsonSerializer.Deserialize<List<NoteTemplate>>(json, JsonOptions);
                if (templates != null)
                {
                    _templates.Clear();
                    _templates.AddRange(templates);
                }
            }
            return true;
        }, 
        false, 
        "TemplateService.LoadTemplatesAsync - Loading templates from storage");
    }

    private async Task SaveTemplatesAsync()
    {
        await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            var directory = PathHelper.GetDirectoryName(_storagePath);
            if (!StringHelper.IsNullOrEmpty(directory))
            {
                PathHelper.EnsureDirectoryExists(directory);
            }

            // Only save user templates, not built-in ones
            var userTemplates = _templates.Where(t => !t.IsBuiltIn).ToList();
            var json = JsonSerializer.Serialize(userTemplates, JsonOptions);
            await File.WriteAllTextAsync(_storagePath, json).ConfigureAwait(false);
            return true;
        }, 
        false, 
        "TemplateService.SaveTemplatesAsync - Saving templates to storage");
    }

    public async Task<IReadOnlyList<NoteTemplate>> GetAllTemplatesAsync()
    {
        await EnsureLoadedAsync();
        return _builtInTemplates.Concat(_templates).ToList().AsReadOnly();
    }

    public async Task<NoteTemplate?> GetTemplateByIdAsync(Guid id)
    {
        await EnsureLoadedAsync();
        return _builtInTemplates.FirstOrDefault(t => t.Id == id) ??
               _templates.FirstOrDefault(t => t.Id == id);
    }

    public async Task<NoteTemplate> CreateTemplateAsync(NoteTemplate template)
    {
        await EnsureLoadedAsync();

        template.Id = Guid.NewGuid();
        template.CreatedDate = DateTime.UtcNow;
        template.IsBuiltIn = false;
        template.Placeholders = ParsePlaceholders(template.Content).ToList();

        _templates.Add(template);
        await SaveTemplatesAsync();

        return template;
    }

    public async Task UpdateTemplateAsync(NoteTemplate template)
    {
        await EnsureLoadedAsync();

        // Cannot update built-in templates
        if (_builtInTemplates.Any(t => t.Id == template.Id))
        {
            throw new InvalidOperationException("Cannot update built-in templates");
        }

        var index = _templates.FindIndex(t => t.Id == template.Id);
        if (index >= 0)
        {
            template.Placeholders = ParsePlaceholders(template.Content).ToList();
            _templates[index] = template;
            await SaveTemplatesAsync();
        }
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        await EnsureLoadedAsync();

        // Cannot delete built-in templates
        if (_builtInTemplates.Any(t => t.Id == id))
        {
            throw new InvalidOperationException("Cannot delete built-in templates");
        }

        var template = _templates.FirstOrDefault(t => t.Id == id);
        if (template != null)
        {
            _templates.Remove(template);
            await SaveTemplatesAsync();
        }
    }

    public async Task<Note> CreateNoteFromTemplateAsync(Guid templateId, Dictionary<string, string>? variables = null)
    {
        var template = await GetTemplateByIdAsync(templateId);
        if (template == null)
        {
            throw new ArgumentException($"Template with ID {templateId} not found", nameof(templateId));
        }

        var content = ReplacePlaceholders(template.Content, variables);
        var now = DateTime.UtcNow;

        return new Note
        {
            Id = Guid.NewGuid(),
            Title = template.Name,
            Content = content,
            Language = template.DefaultLanguage,
            IsPinned = true,
            Opacity = _settings.DefaultOpacity,
            WindowRect = new WindowRect
            {
                Top = 100,
                Left = 100,
                Width = WindowRect.DefaultWidth,
                Height = WindowRect.DefaultHeight
            },
            CreatedDate = now,
            ModifiedDate = now,
            TemplateId = templateId
        };
    }


    public async Task ExportTemplatesAsync(string filePath)
    {
        await EnsureLoadedAsync();

        var directory = PathHelper.GetDirectoryName(filePath);
        if (!StringHelper.IsNullOrEmpty(directory))
        {
            PathHelper.EnsureDirectoryExists(directory);
        }

        // Export only user templates
        var userTemplates = _templates.Where(t => !t.IsBuiltIn).ToList();
        var json = JsonSerializer.Serialize(userTemplates, JsonOptions);
        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
    }

    public async Task ImportTemplatesAsync(string filePath)
    {
        await EnsureLoadedAsync();

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Import file not found", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        var importedTemplates = JsonSerializer.Deserialize<List<NoteTemplate>>(json, JsonOptions);

        if (importedTemplates == null || importedTemplates.Count == 0)
        {
            return;
        }

        foreach (var imported in importedTemplates)
        {
            // Skip built-in templates
            if (imported.IsBuiltIn)
            {
                continue;
            }

            var existing = _templates.FirstOrDefault(t => t.Id == imported.Id);
            if (existing != null)
            {
                // Replace existing template
                var index = _templates.IndexOf(existing);
                imported.Placeholders = ParsePlaceholders(imported.Content).ToList();
                _templates[index] = imported;
            }
            else
            {
                // Add new template
                imported.Placeholders = ParsePlaceholders(imported.Content).ToList();
                _templates.Add(imported);
            }
        }

        await SaveTemplatesAsync();
    }

    public IReadOnlyList<NoteTemplate> GetBuiltInTemplates()
    {
        return _builtInTemplates.AsReadOnly();
    }

    public async Task<NoteTemplate> CreateTemplateFromNoteAsync(Note note, string templateName, string description, string category)
    {
        var template = new NoteTemplate
        {
            Id = Guid.NewGuid(),
            Name = templateName,
            Description = description,
            Category = category,
            Content = note.Content,
            DefaultLanguage = note.Language,
            DefaultTags = new List<string>(),
            IsBuiltIn = false,
            CreatedDate = DateTime.UtcNow
        };

        template.Placeholders = ParsePlaceholders(template.Content).ToList();

        await EnsureLoadedAsync();
        _templates.Add(template);
        await SaveTemplatesAsync();

        return template;
    }

    public IReadOnlyList<TemplatePlaceholder> ParsePlaceholders(string content)
    {
        var placeholders = new List<TemplatePlaceholder>();
        var matches = PlaceholderRegex().Matches(content);
        var seenNames = new HashSet<string>();

        foreach (Match match in matches)
        {
            string name;
            PlaceholderType type;

            if (match.Groups[2].Success)
            {
                // Format: {{type:name}}
                var typeStr = match.Groups[1].Value.ToLowerInvariant();
                name = match.Groups[2].Value;
                type = typeStr switch
                {
                    "date" => PlaceholderType.Date,
                    "datetime" => PlaceholderType.DateTime,
                    "user" => PlaceholderType.User,
                    "custom" => PlaceholderType.Custom,
                    _ => PlaceholderType.Text
                };
            }
            else
            {
                // Format: {{name}} - infer type from name
                name = match.Groups[1].Value;
                type = name.ToLowerInvariant() switch
                {
                    "date" => PlaceholderType.Date,
                    "datetime" => PlaceholderType.DateTime,
                    "author" or "user" => PlaceholderType.User,
                    _ => PlaceholderType.Text
                };
            }

            // Only add unique placeholders
            if (!seenNames.Contains(name))
            {
                seenNames.Add(name);
                placeholders.Add(new TemplatePlaceholder
                {
                    Name = name,
                    DisplayName = FormatDisplayName(name),
                    Type = type,
                    DefaultValue = string.Empty
                });
            }
        }

        return placeholders.AsReadOnly();
    }

    public string ReplacePlaceholders(string content, Dictionary<string, string>? variables = null)
    {
        return PlaceholderRegex().Replace(content, match =>
        {
            string name;
            PlaceholderType type;

            if (match.Groups[2].Success)
            {
                var typeStr = match.Groups[1].Value.ToLowerInvariant();
                name = match.Groups[2].Value;
                type = typeStr switch
                {
                    "date" => PlaceholderType.Date,
                    "datetime" => PlaceholderType.DateTime,
                    "user" => PlaceholderType.User,
                    _ => PlaceholderType.Text
                };
            }
            else
            {
                name = match.Groups[1].Value;
                type = name.ToLowerInvariant() switch
                {
                    "date" => PlaceholderType.Date,
                    "datetime" => PlaceholderType.DateTime,
                    "author" or "user" => PlaceholderType.User,
                    _ => PlaceholderType.Text
                };
            }

            // Check if a custom value was provided
            if (variables != null && variables.TryGetValue(name, out var customValue))
            {
                return customValue;
            }

            // Use default replacement based on type
            return type switch
            {
                PlaceholderType.Date => DateTime.Now.ToString("yyyy-MM-dd"),
                PlaceholderType.DateTime => DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                PlaceholderType.User => _settings.AuthorName ?? "User",
                _ => match.Value // Keep original if no replacement
            };
        });
    }

    private static string FormatDisplayName(string name)
    {
        // Convert camelCase or snake_case to Title Case
        var result = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        result = result.Replace("_", " ");
        return StringHelper.ToTitleCase(result);
    }
}
