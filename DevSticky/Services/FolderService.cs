using System.IO;
using System.Text.Json;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for managing folder operations and hierarchy
/// </summary>
public class FolderService : IFolderService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptionsFactory.Default;
    
    private readonly List<NoteFolder> _folders = new();
    private readonly string _foldersPath;
    private readonly IErrorHandler _errorHandler;
    private readonly IFileSystem _fileSystem;
    private readonly INoteService _noteService;

    public FolderService(IErrorHandler errorHandler, IFileSystem fileSystem, INoteService noteService)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
        
        _foldersPath = PathHelper.Combine(
            PathHelper.GetAppDataPath(AppConstants.AppDataFolderName),
            "folders.json");
    }

    public async Task<NoteFolder> CreateFolderAsync(string name, Guid? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Folder name cannot be empty", nameof(name));

        // Validate parent exists if specified
        if (parentId.HasValue)
        {
            var parent = _folders.FirstOrDefault(f => f.Id == parentId.Value);
            if (parent == null)
                throw new ArgumentException("Parent folder not found", nameof(parentId));
        }

        var folder = new NoteFolder
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            ParentId = parentId,
            CreatedDate = DateTime.UtcNow,
            SortOrder = _folders.Count(f => f.ParentId == parentId)
        };

        _folders.Add(folder);
        await SaveAsync();
        return folder;
    }

    public async Task<bool> DeleteFolderAsync(Guid folderId)
    {
        var folder = _folders.FirstOrDefault(f => f.Id == folderId);
        if (folder == null)
            return false;

        // Move all child folders to parent
        var childFolders = _folders.Where(f => f.ParentId == folderId).ToList();
        foreach (var childFolder in childFolders)
        {
            childFolder.ParentId = folder.ParentId;
        }

        // Move all notes in this folder to parent
        var notesInFolder = _noteService.GetAllNotes().Where(n => n.FolderId == folderId).ToList();
        foreach (var note in notesInFolder)
        {
            note.FolderId = folder.ParentId;
        }

        _folders.Remove(folder);
        await SaveAsync();
        return true;
    }

    public async Task<bool> MoveFolderAsync(Guid folderId, Guid? newParentId)
    {
        var folder = _folders.FirstOrDefault(f => f.Id == folderId);
        if (folder == null)
            return false;

        // Prevent circular reference
        if (newParentId.HasValue && IsDescendant(newParentId.Value, folderId))
            return false;

        // Validate new parent exists if specified
        if (newParentId.HasValue)
        {
            var newParent = _folders.FirstOrDefault(f => f.Id == newParentId.Value);
            if (newParent == null)
                return false;
        }

        folder.ParentId = newParentId;
        await SaveAsync();
        return true;
    }

    public async Task<bool> MoveNoteToFolderAsync(Guid noteId, Guid? folderId)
    {
        var note = _noteService.GetNoteById(noteId);
        if (note == null)
            return false;

        // Validate folder exists if specified
        if (folderId.HasValue)
        {
            var folder = _folders.FirstOrDefault(f => f.Id == folderId.Value);
            if (folder == null)
                return false;
        }

        note.FolderId = folderId;
        return true;
    }

    public async Task<IReadOnlyList<NoteFolder>> GetRootFoldersAsync()
    {
        return _folders.Where(f => f.ParentId == null)
                      .OrderBy(f => f.SortOrder)
                      .ThenBy(f => f.Name)
                      .ToList()
                      .AsReadOnly();
    }

    public async Task<IReadOnlyList<NoteFolder>> GetChildFoldersAsync(Guid parentId)
    {
        return _folders.Where(f => f.ParentId == parentId)
                      .OrderBy(f => f.SortOrder)
                      .ThenBy(f => f.Name)
                      .ToList()
                      .AsReadOnly();
    }

    public async Task<IReadOnlyList<Note>> GetNotesInFolderAsync(Guid? folderId)
    {
        return _noteService.GetAllNotes()
                          .Where(n => n.FolderId == folderId)
                          .OrderBy(n => n.CreatedDate)
                          .ToList()
                          .AsReadOnly();
    }

    public async Task SaveAsync()
    {
        await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            var directory = PathHelper.GetDirectoryName(_foldersPath);
            if (!StringHelper.IsNullOrEmpty(directory))
            {
                PathHelper.EnsureDirectoryExists(directory);
            }

            var json = JsonSerializer.Serialize(_folders, JsonOptions);
            await _fileSystem.WriteAllTextAsync(_foldersPath, json).ConfigureAwait(false);
            return true;
        },
        false,
        "FolderService.SaveAsync - Saving folders to storage");
    }

    public async Task LoadAsync()
    {
        await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            if (!_fileSystem.FileExists(_foldersPath))
            {
                _folders.Clear();
                return true;
            }

            var json = await _fileSystem.ReadAllTextAsync(_foldersPath).ConfigureAwait(false);
            var folders = JsonSerializer.Deserialize<List<NoteFolder>>(json, JsonOptions);
            
            _folders.Clear();
            if (folders != null)
            {
                _folders.AddRange(folders);
            }
            
            return true;
        },
        true,
        "FolderService.LoadAsync - Loading folders from storage");
    }

    /// <summary>
    /// Checks if a folder is a descendant of another folder (prevents circular references)
    /// </summary>
    private bool IsDescendant(Guid potentialDescendantId, Guid ancestorId)
    {
        var current = _folders.FirstOrDefault(f => f.Id == potentialDescendantId);
        
        while (current?.ParentId != null)
        {
            if (current.ParentId == ancestorId)
                return true;
            
            current = _folders.FirstOrDefault(f => f.Id == current.ParentId);
        }
        
        return false;
    }

    /// <summary>
    /// Disposes the folder service and releases all resources.
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
            _folders.Clear();
        }
    }
}