using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing note groups (Requirements 1.1, 8.3)
/// Extracted from MainViewModel to follow Single Responsibility Principle
/// </summary>
public interface IGroupManagementService
{
    /// <summary>
    /// Create a new group with optional name
    /// </summary>
    NoteGroup CreateGroup(string? name = null);
    
    /// <summary>
    /// Delete a group and move its notes to ungrouped
    /// </summary>
    void DeleteGroup(Guid groupId);
    
    /// <summary>
    /// Rename an existing group
    /// </summary>
    void RenameGroup(Guid groupId, string newName);
    
    /// <summary>
    /// Move a note to a specific group or ungrouped (null)
    /// </summary>
    void MoveNoteToGroup(Guid noteId, Guid? groupId);
    
    /// <summary>
    /// Get all groups
    /// </summary>
    IReadOnlyList<NoteGroup> GetAllGroups();
}
