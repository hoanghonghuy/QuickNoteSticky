using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing note tags (Requirements 1.1, 8.3)
/// Extracted from MainViewModel to follow Single Responsibility Principle
/// </summary>
public interface ITagManagementService
{
    /// <summary>
    /// Create a new tag with optional name and color
    /// </summary>
    NoteTag CreateTag(string? name = null, string? color = null);
    
    /// <summary>
    /// Delete a tag and remove it from all notes
    /// </summary>
    void DeleteTag(Guid tagId);
    
    /// <summary>
    /// Rename an existing tag
    /// </summary>
    void RenameTag(Guid tagId, string newName);
    
    /// <summary>
    /// Change the color of an existing tag
    /// </summary>
    void ChangeTagColor(Guid tagId, string newColor);
    
    /// <summary>
    /// Add a tag to a note
    /// </summary>
    void AddTagToNote(Guid noteId, Guid tagId);
    
    /// <summary>
    /// Remove a tag from a note
    /// </summary>
    void RemoveTagFromNote(Guid noteId, Guid tagId);
    
    /// <summary>
    /// Get all tags
    /// </summary>
    IReadOnlyList<NoteTag> GetAllTags();
}
