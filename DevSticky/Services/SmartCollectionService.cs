using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for managing smart collections that automatically group notes by criteria
/// </summary>
public class SmartCollectionService : ISmartCollectionService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptionsFactory.Default;
    
    private readonly List<SmartCollection> _collections = new();
    private readonly string _collectionsPath;
    private readonly IErrorHandler _errorHandler;
    private readonly IFileSystem _fileSystem;
    private readonly INoteService _noteService;

    public SmartCollectionService(IErrorHandler errorHandler, IFileSystem fileSystem, INoteService noteService)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
        
        _collectionsPath = PathHelper.Combine(
            PathHelper.GetAppDataPath(AppConstants.AppDataFolderName),
            "smart-collections.json");
    }

    public IReadOnlyList<SmartCollection> GetDefaultCollections()
    {
        return new List<SmartCollection>
        {
            new SmartCollection
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "Today",
                Icon = "◉",
                Criteria = new FilterCriteria { DateRange = DateRangeType.Today },
                IsBuiltIn = true
            },
            new SmartCollection
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Name = "This Week",
                Icon = "◎",
                Criteria = new FilterCriteria { DateRange = DateRangeType.ThisWeek },
                IsBuiltIn = true
            },
            new SmartCollection
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Name = "Has TODO",
                Icon = "☐",
                Criteria = new FilterCriteria { HasUncheckedTodos = true },
                IsBuiltIn = true
            },
            new SmartCollection
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                Name = "Code Notes",
                Icon = "⟨⟩",
                Criteria = new FilterCriteria { HasCodeBlocks = true },
                IsBuiltIn = true
            }
        }.AsReadOnly();
    }

    public async Task<SmartCollection> CreateCollectionAsync(string name, FilterCriteria criteria)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be empty", nameof(name));

        if (criteria == null)
            throw new ArgumentNullException(nameof(criteria));

        var collection = new SmartCollection
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Criteria = criteria,
            IsBuiltIn = false
        };

        _collections.Add(collection);
        await SaveAsync();
        return collection;
    }

    public async Task<bool> DeleteCollectionAsync(Guid collectionId)
    {
        var collection = _collections.FirstOrDefault(c => c.Id == collectionId);
        if (collection == null || collection.IsBuiltIn)
            return false;

        _collections.Remove(collection);
        await SaveAsync();
        return true;
    }

    public async Task<IReadOnlyList<Note>> GetNotesForCollectionAsync(Guid collectionId)
    {
        // Check built-in collections first
        var defaultCollections = GetDefaultCollections();
        var collection = defaultCollections.FirstOrDefault(c => c.Id == collectionId) 
                        ?? _collections.FirstOrDefault(c => c.Id == collectionId);

        if (collection == null)
            return new List<Note>().AsReadOnly();

        var allNotes = _noteService.GetAllNotes();
        return await ApplyFilterAsync(collection.Criteria, allNotes);
    }

    public async Task<IReadOnlyList<Note>> ApplyFilterAsync(FilterCriteria criteria, IEnumerable<Note> notes)
    {
        if (criteria == null)
            return notes.ToList().AsReadOnly();

        var filteredNotes = notes.AsEnumerable();

        // Apply date range filter
        if (criteria.DateRange.HasValue)
        {
            var now = DateTime.Now;
            DateTime startDate, endDate;

            switch (criteria.DateRange.Value)
            {
                case DateRangeType.Today:
                    startDate = now.Date;
                    endDate = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case DateRangeType.ThisWeek:
                    var daysFromMonday = (int)now.DayOfWeek - (int)DayOfWeek.Monday;
                    if (daysFromMonday < 0) daysFromMonday += 7;
                    startDate = now.Date.AddDays(-daysFromMonday);
                    endDate = startDate.AddDays(7).AddTicks(-1);
                    break;
                case DateRangeType.ThisMonth:
                    startDate = new DateTime(now.Year, now.Month, 1);
                    endDate = startDate.AddMonths(1).AddTicks(-1);
                    break;
                case DateRangeType.Custom:
                    startDate = criteria.DateFrom ?? DateTime.MinValue;
                    endDate = criteria.DateTo ?? DateTime.MaxValue;
                    break;
                default:
                    startDate = DateTime.MinValue;
                    endDate = DateTime.MaxValue;
                    break;
            }

            filteredNotes = filteredNotes.Where(n => 
                (n.CreatedDate >= startDate && n.CreatedDate <= endDate) ||
                (n.ModifiedDate >= startDate && n.ModifiedDate <= endDate));
        }

        // Apply custom date range filter
        if (criteria.DateFrom.HasValue)
        {
            filteredNotes = filteredNotes.Where(n => 
                n.CreatedDate >= criteria.DateFrom.Value || 
                n.ModifiedDate >= criteria.DateFrom.Value);
        }

        if (criteria.DateTo.HasValue)
        {
            filteredNotes = filteredNotes.Where(n => 
                n.CreatedDate <= criteria.DateTo.Value || 
                n.ModifiedDate <= criteria.DateTo.Value);
        }

        // Apply tag filter
        if (criteria.TagIds != null && criteria.TagIds.Count > 0)
        {
            filteredNotes = filteredNotes.Where(n => 
                criteria.TagIds.Any(tagId => n.TagIds.Contains(tagId)));
        }

        // Apply content pattern filter
        if (!string.IsNullOrWhiteSpace(criteria.ContentPattern))
        {
            try
            {
                var regex = new Regex(criteria.ContentPattern, RegexOptions.IgnoreCase);
                filteredNotes = filteredNotes.Where(n => 
                    regex.IsMatch(n.Title) || regex.IsMatch(n.Content));
            }
            catch (ArgumentException)
            {
                // If regex is invalid, treat as literal string
                var pattern = criteria.ContentPattern.ToLowerInvariant();
                filteredNotes = filteredNotes.Where(n => 
                    n.Title.ToLowerInvariant().Contains(pattern) || 
                    n.Content.ToLowerInvariant().Contains(pattern));
            }
        }

        // Apply code blocks filter
        if (criteria.HasCodeBlocks == true)
        {
            filteredNotes = filteredNotes.Where(n => 
                n.Content.Contains("```") && 
                Regex.IsMatch(n.Content, @"```\w+"));
        }

        // Apply unchecked todos filter
        if (criteria.HasUncheckedTodos == true)
        {
            filteredNotes = filteredNotes.Where(n => 
                n.Content.Contains("- [ ]"));
        }

        return filteredNotes.ToList().AsReadOnly();
    }

    public async Task SaveAsync()
    {
        await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            var directory = PathHelper.GetDirectoryName(_collectionsPath);
            if (!StringHelper.IsNullOrEmpty(directory))
            {
                PathHelper.EnsureDirectoryExists(directory);
            }

            var json = JsonSerializer.Serialize(_collections, JsonOptions);
            await _fileSystem.WriteAllTextAsync(_collectionsPath, json).ConfigureAwait(false);
            return true;
        },
        false,
        "SmartCollectionService.SaveAsync - Saving collections to storage");
    }

    public async Task LoadAsync()
    {
        await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            if (!_fileSystem.FileExists(_collectionsPath))
            {
                _collections.Clear();
                return true;
            }

            var json = await _fileSystem.ReadAllTextAsync(_collectionsPath).ConfigureAwait(false);
            var collections = JsonSerializer.Deserialize<List<SmartCollection>>(json, JsonOptions);
            
            _collections.Clear();
            if (collections != null)
            {
                _collections.AddRange(collections);
            }
            
            return true;
        },
        true,
        "SmartCollectionService.LoadAsync - Loading collections from storage");
    }

    /// <summary>
    /// Disposes the smart collection service and releases all resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _collections.Clear();
        }
    }
}