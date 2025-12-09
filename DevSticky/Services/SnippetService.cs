using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for managing code snippets with storage, search, and placeholder support
/// </summary>
public partial class SnippetService : ISnippetService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Regex pattern for placeholder syntax: ${index:name} or ${index:name:defaultValue}
    [GeneratedRegex(@"\$\{(\d+):([^:}]+)(?::([^}]*))?\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    private readonly string _storagePath;
    private readonly List<Snippet> _snippets = new();
    private bool _isLoaded;

    public SnippetService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var devStickyFolder = Path.Combine(appDataPath, AppConstants.AppDataFolderName);
        _storagePath = Path.Combine(devStickyFolder, AppConstants.SnippetsFileName);
    }

    /// <summary>
    /// Constructor for testing with custom storage path
    /// </summary>
    public SnippetService(string storagePath)
    {
        _storagePath = storagePath;
    }

    private async Task EnsureLoadedAsync()
    {
        if (!_isLoaded)
        {
            await LoadSnippetsAsync();
            _isLoaded = true;
        }
    }

    private async Task LoadSnippetsAsync()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                var json = await File.ReadAllTextAsync(_storagePath);
                var snippets = JsonSerializer.Deserialize<List<Snippet>>(json, JsonOptions);
                if (snippets != null)
                {
                    _snippets.Clear();
                    _snippets.AddRange(snippets);
                }
            }
        }
        catch (JsonException)
        {
            // If file is corrupted, start fresh
            _snippets.Clear();
        }
    }

    private async Task SaveSnippetsAsync()
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_snippets, JsonOptions);
        await File.WriteAllTextAsync(_storagePath, json);
    }

    public async Task<IReadOnlyList<Snippet>> GetAllSnippetsAsync()
    {
        await EnsureLoadedAsync();
        return _snippets.AsReadOnly();
    }

    public async Task<Snippet?> GetSnippetByIdAsync(Guid id)
    {
        await EnsureLoadedAsync();
        return _snippets.FirstOrDefault(s => s.Id == id);
    }

    public async Task<IReadOnlyList<Snippet>> SearchSnippetsAsync(string query)
    {
        await EnsureLoadedAsync();

        if (string.IsNullOrWhiteSpace(query))
        {
            return _snippets.AsReadOnly();
        }

        var lowerQuery = query.ToLowerInvariant();
        return _snippets
            .Where(s => MatchesQuery(s, lowerQuery))
            .ToList()
            .AsReadOnly();
    }

    private static bool MatchesQuery(Snippet snippet, string lowerQuery)
    {
        return snippet.Name.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
               snippet.Description.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
               snippet.Content.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
               snippet.Tags.Any(t => t.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Snippet> CreateSnippetAsync(Snippet snippet)
    {
        await EnsureLoadedAsync();

        var now = DateTime.UtcNow;
        snippet.Id = Guid.NewGuid();
        snippet.CreatedDate = now;
        snippet.ModifiedDate = now;
        snippet.Placeholders = ParsePlaceholders(snippet.Content).ToList();

        _snippets.Add(snippet);
        await SaveSnippetsAsync();

        return snippet;
    }

    public async Task UpdateSnippetAsync(Snippet snippet)
    {
        await EnsureLoadedAsync();

        var index = _snippets.FindIndex(s => s.Id == snippet.Id);
        if (index >= 0)
        {
            snippet.ModifiedDate = DateTime.UtcNow;
            snippet.Placeholders = ParsePlaceholders(snippet.Content).ToList();
            _snippets[index] = snippet;
            await SaveSnippetsAsync();
        }
    }

    public async Task DeleteSnippetAsync(Guid id)
    {
        await EnsureLoadedAsync();

        var snippet = _snippets.FirstOrDefault(s => s.Id == id);
        if (snippet != null)
        {
            _snippets.Remove(snippet);
            await SaveSnippetsAsync();
        }
    }

    public Task<string> ExpandSnippetAsync(Snippet snippet, Dictionary<string, string>? variables = null)
    {
        var content = snippet.Content;
        var placeholders = ParsePlaceholders(content);

        // Sort by position descending to replace from end to start (preserves positions)
        var sortedPlaceholders = placeholders.OrderByDescending(p => p.StartPosition).ToList();

        foreach (var placeholder in sortedPlaceholders)
        {
            string replacement;
            if (variables != null && variables.TryGetValue(placeholder.Name, out var value))
            {
                replacement = value;
            }
            else
            {
                replacement = placeholder.DefaultValue;
            }

            content = content.Remove(placeholder.StartPosition, placeholder.Length)
                            .Insert(placeholder.StartPosition, replacement);
        }

        return Task.FromResult(content);
    }

    public async Task ExportSnippetsAsync(string filePath)
    {
        await EnsureLoadedAsync();

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_snippets, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task ImportSnippetsAsync(string filePath, ConflictResolution resolution)
    {
        await EnsureLoadedAsync();

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Import file not found", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath);
        var importedSnippets = JsonSerializer.Deserialize<List<Snippet>>(json, JsonOptions);

        if (importedSnippets == null || importedSnippets.Count == 0)
        {
            return;
        }

        foreach (var imported in importedSnippets)
        {
            var existing = _snippets.FirstOrDefault(s => s.Id == imported.Id);

            if (existing != null)
            {
                switch (resolution)
                {
                    case ConflictResolution.Skip:
                        // Do nothing, keep existing
                        break;

                    case ConflictResolution.Replace:
                        var index = _snippets.IndexOf(existing);
                        imported.ModifiedDate = DateTime.UtcNow;
                        _snippets[index] = imported;
                        break;

                    case ConflictResolution.KeepBoth:
                        imported.Id = Guid.NewGuid();
                        imported.Name = $"{imported.Name} (Imported)";
                        imported.ModifiedDate = DateTime.UtcNow;
                        _snippets.Add(imported);
                        break;
                }
            }
            else
            {
                _snippets.Add(imported);
            }
        }

        await SaveSnippetsAsync();
    }

    public IReadOnlyList<SnippetPlaceholder> ParsePlaceholders(string content)
    {
        var placeholders = new List<SnippetPlaceholder>();
        var matches = PlaceholderRegex().Matches(content);

        foreach (Match match in matches)
        {
            var placeholder = new SnippetPlaceholder
            {
                Index = int.Parse(match.Groups[1].Value),
                Name = match.Groups[2].Value,
                DefaultValue = match.Groups[3].Success ? match.Groups[3].Value : match.Groups[2].Value,
                StartPosition = match.Index,
                Length = match.Length
            };
            placeholders.Add(placeholder);
        }

        return placeholders.OrderBy(p => p.Index).ThenBy(p => p.StartPosition).ToList().AsReadOnly();
    }
}
