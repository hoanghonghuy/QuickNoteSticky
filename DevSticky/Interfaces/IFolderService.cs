using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing folder operations and hierarchy
/// </summary>
public interface IFolderService
{
    Task<NoteFolder> CreateFolderAsync(string name, Guid? parentId = null);
    Task<bool> DeleteFolderAsync(Guid folderId);
    Task<bool> MoveFolderAsync(Guid folderId, Guid? newParentId);
    Task<bool> MoveNoteToFolderAsync(Guid noteId, Guid? folderId);
    Task<IReadOnlyList<NoteFolder>> GetRootFoldersAsync();
    Task<IReadOnlyList<NoteFolder>> GetChildFoldersAsync(Guid parentId);
    Task<IReadOnlyList<Note>> GetNotesInFolderAsync(Guid? folderId);
    Task SaveAsync();
    Task LoadAsync();
}