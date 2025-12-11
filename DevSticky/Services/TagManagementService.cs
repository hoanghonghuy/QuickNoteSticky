using System.Collections.ObjectModel;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.ViewModels;

namespace DevSticky.Services;

/// <summary>
/// Service for managing note tags (Requirements 1.1, 8.3)
/// Implements Single Responsibility Principle by extracting tag management logic
/// </summary>
public class TagManagementService : ITagManagementService
{
    private readonly ObservableCollection<NoteTag> _tags;
    private readonly ObservableCollection<NoteViewModel> _notes;
    private readonly CacheService _cacheService;
    private readonly Action _saveCallback;

    public TagManagementService(
        ObservableCollection<NoteTag> tags,
        ObservableCollection<NoteViewModel> notes,
        CacheService cacheService,
        Action saveCallback)
    {
        _tags = tags ?? throw new ArgumentNullException(nameof(tags));
        _notes = notes ?? throw new ArgumentNullException(nameof(notes));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));
    }

    /// <summary>
    /// Create a new tag with optional name and color (Requirements 1.1)
    /// </summary>
    public NoteTag CreateTag(string? name = null, string? color = null)
    {
        var tag = new NoteTag 
        { 
            Name = name ?? L.Get("DefaultTagName"),
            Color = color ?? NoteTag.DefaultColors[_tags.Count % NoteTag.DefaultColors.Length]
        };
        _tags.Add(tag);
        _saveCallback();
        return tag;
    }

    /// <summary>
    /// Delete a tag and remove it from all notes (Requirements 1.1)
    /// </summary>
    public void DeleteTag(Guid tagId)
    {
        // Optimized: Single pass to find tag and update notes
        NoteTag? tagToRemove = null;
        
        // Find the tag to remove
        foreach (var tag in _tags)
        {
            if (tag.Id == tagId)
            {
                tagToRemove = tag;
                break;
            }
        }
        
        if (tagToRemove != null)
        {
            // Single pass to remove tag from all notes
            foreach (var note in _notes)
            {
                note.TagIds.Remove(tagId);
            }
            
            _tags.Remove(tagToRemove);
            _cacheService.InvalidateTagCache();
            _saveCallback();
        }
    }

    /// <summary>
    /// Rename an existing tag (Requirements 1.1)
    /// </summary>
    public void RenameTag(Guid tagId, string newName)
    {
        // Optimized: Direct search without LINQ
        foreach (var tag in _tags)
        {
            if (tag.Id == tagId)
            {
                // Limit name length to 20 characters
                tag.Name = Helpers.StringHelper.Truncate(newName, 20);
                _saveCallback();
                break;
            }
        }
    }

    /// <summary>
    /// Change the color of an existing tag (Requirements 1.1)
    /// </summary>
    public void ChangeTagColor(Guid tagId, string newColor)
    {
        // Optimized: Direct search without LINQ
        foreach (var tag in _tags)
        {
            if (tag.Id == tagId)
            {
                tag.Color = newColor;
                _saveCallback();
                break;
            }
        }
    }

    /// <summary>
    /// Add a tag to a note (Requirements 1.1)
    /// </summary>
    public void AddTagToNote(Guid noteId, Guid tagId)
    {
        // Optimized: Direct search without LINQ
        foreach (var note in _notes)
        {
            if (note.Id == noteId)
            {
                if (!note.TagIds.Contains(tagId) && note.TagIds.Count < 5)
                {
                    note.TagIds.Add(tagId);
                    _saveCallback();
                }
                break;
            }
        }
    }

    /// <summary>
    /// Remove a tag from a note (Requirements 1.1)
    /// </summary>
    public void RemoveTagFromNote(Guid noteId, Guid tagId)
    {
        // Optimized: Direct search without LINQ
        foreach (var note in _notes)
        {
            if (note.Id == noteId)
            {
                note.TagIds.Remove(tagId);
                _saveCallback();
                break;
            }
        }
    }

    /// <summary>
    /// Get all tags (Requirements 8.3)
    /// </summary>
    public IReadOnlyList<NoteTag> GetAllTags()
    {
        // Optimized: Direct list creation without ToList()
        return new List<NoteTag>(_tags).AsReadOnly();
    }
}
