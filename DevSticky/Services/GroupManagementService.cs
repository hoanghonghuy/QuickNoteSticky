using System.Collections.ObjectModel;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.ViewModels;

namespace DevSticky.Services;

/// <summary>
/// Service for managing note groups (Requirements 1.1, 8.3)
/// Implements Single Responsibility Principle by extracting group management logic
/// </summary>
public class GroupManagementService : IGroupManagementService
{
    private readonly ObservableCollection<NoteGroup> _groups;
    private readonly ObservableCollection<NoteViewModel> _notes;
    private readonly CacheService _cacheService;
    private readonly Action _saveCallback;

    public GroupManagementService(
        ObservableCollection<NoteGroup> groups,
        ObservableCollection<NoteViewModel> notes,
        CacheService cacheService,
        Action saveCallback)
    {
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _notes = notes ?? throw new ArgumentNullException(nameof(notes));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));
    }

    /// <summary>
    /// Create a new group with optional name (Requirements 1.1)
    /// </summary>
    public NoteGroup CreateGroup(string? name = null)
    {
        var group = new NoteGroup 
        { 
            Name = name ?? L.Get("DefaultGroupName") 
        };
        _groups.Add(group);
        _saveCallback();
        return group;
    }

    /// <summary>
    /// Delete a group and move its notes to ungrouped (Requirements 1.1)
    /// </summary>
    public void DeleteGroup(Guid groupId)
    {
        // Optimized: Single pass to find group and update notes
        NoteGroup? groupToRemove = null;
        
        // Find the group to remove
        foreach (var group in _groups)
        {
            if (group.Id == groupId)
            {
                groupToRemove = group;
                break;
            }
        }
        
        if (groupToRemove != null)
        {
            // Single pass to move notes to ungrouped
            foreach (var note in _notes)
            {
                if (note.GroupId == groupId)
                {
                    note.GroupId = null;
                }
            }
            
            _groups.Remove(groupToRemove);
            _cacheService.InvalidateGroupCache();
            _saveCallback();
        }
    }

    /// <summary>
    /// Rename an existing group (Requirements 1.1)
    /// </summary>
    public void RenameGroup(Guid groupId, string newName)
    {
        // Optimized: Direct search without LINQ
        foreach (var group in _groups)
        {
            if (group.Id == groupId)
            {
                // Limit name length to 30 characters
                group.Name = Helpers.StringHelper.Truncate(newName, 30);
                _saveCallback();
                break;
            }
        }
    }

    /// <summary>
    /// Move a note to a specific group or ungrouped (Requirements 1.1)
    /// </summary>
    public void MoveNoteToGroup(Guid noteId, Guid? groupId)
    {
        // Optimized: Direct search without LINQ
        foreach (var note in _notes)
        {
            if (note.Id == noteId)
            {
                note.GroupId = groupId;
                _saveCallback();
                break;
            }
        }
    }

    /// <summary>
    /// Get all groups (Requirements 8.3)
    /// </summary>
    public IReadOnlyList<NoteGroup> GetAllGroups()
    {
        // Optimized: Direct list creation without ToList()
        return new List<NoteGroup>(_groups).AsReadOnly();
    }
}
