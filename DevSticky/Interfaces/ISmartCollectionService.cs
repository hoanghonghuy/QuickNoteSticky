using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing smart collections that automatically group notes by criteria
/// </summary>
public interface ISmartCollectionService
{
    IReadOnlyList<SmartCollection> GetDefaultCollections();
    Task<SmartCollection> CreateCollectionAsync(string name, FilterCriteria criteria);
    Task<bool> DeleteCollectionAsync(Guid collectionId);
    Task<IReadOnlyList<Note>> GetNotesForCollectionAsync(Guid collectionId);
    Task<IReadOnlyList<Note>> ApplyFilterAsync(FilterCriteria criteria, IEnumerable<Note> notes);
    Task SaveAsync();
    Task LoadAsync();
}