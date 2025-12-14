using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for managing timeline view functionality
/// </summary>
public class TimelineService : ITimelineService
{
    private readonly INoteService _noteService;
    private readonly Func<ITagManagementService> _tagManagementServiceFactory;

    public TimelineService(INoteService noteService, Func<ITagManagementService> tagManagementServiceFactory)
    {
        _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
        _tagManagementServiceFactory = tagManagementServiceFactory ?? throw new ArgumentNullException(nameof(tagManagementServiceFactory));
    }

    /// <summary>
    /// Gets timeline items for all notes, optionally filtered by date range
    /// </summary>
    public async Task<IReadOnlyList<TimelineItem>> GetTimelineItemsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var notes = _noteService.GetAllNotes();
            if (notes == null || !notes.Any())
            {
                return Array.Empty<TimelineItem>();
            }
            
            ITagManagementService? tagService = null;
            IReadOnlyList<NoteTag> allTags = Array.Empty<NoteTag>();
            
            try
            {
                tagService = _tagManagementServiceFactory();
                allTags = tagService?.GetAllTags() ?? Array.Empty<NoteTag>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TimelineService] Failed to get tags: {ex.Message}");
                // Continue without tags
            }
            
            // Create a dictionary for fast tag lookup
            var tagLookup = allTags.ToDictionary(t => t.Id, t => t.Name);
            
            var timelineItems = notes.Select(note => CreateTimelineItem(note, tagLookup)).ToList();
            
            // Apply date filtering if specified
            if (fromDate.HasValue || toDate.HasValue)
            {
                timelineItems = FilterByDateRange(timelineItems, 
                    fromDate ?? DateTime.MinValue, 
                    toDate ?? DateTime.MaxValue).ToList();
            }
            
            // Sort by creation date descending (newest first)
            timelineItems.Sort((a, b) => b.CreatedDate.CompareTo(a.CreatedDate));
            
            return timelineItems.AsReadOnly();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TimelineService] Error getting timeline items: {ex.Message}");
            return Array.Empty<TimelineItem>();
        }
    }

    /// <summary>
    /// Groups timeline items by date (calendar day)
    /// </summary>
    public Dictionary<DateTime, IReadOnlyList<TimelineItem>> GroupByDate(IEnumerable<TimelineItem> items)
    {
        return items
            .GroupBy(item => item.CreatedDate.Date)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TimelineItem>)group
                    .OrderByDescending(item => item.CreatedDate)
                    .ToList()
                    .AsReadOnly()
            );
    }

    /// <summary>
    /// Filters timeline items by date range (inclusive)
    /// </summary>
    public IReadOnlyList<TimelineItem> FilterByDateRange(IEnumerable<TimelineItem> items, DateTime fromDate, DateTime toDate)
    {
        return items
            .Where(item => item.CreatedDate.Date >= fromDate.Date && item.CreatedDate.Date <= toDate.Date)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Creates a TimelineItem from a Note
    /// </summary>
    private TimelineItem CreateTimelineItem(Note note, Dictionary<Guid, string> tagLookup)
    {
        // Create content preview (first 100 characters or less)
        var contentPreview = string.IsNullOrEmpty(note.Content) 
            ? string.Empty 
            : note.Content.Length <= 100 
                ? note.Content 
                : note.Content[..100] + "...";

        // Get tag names for this note
        var tagNames = note.TagIds
            .Where(tagId => tagLookup.ContainsKey(tagId))
            .Select(tagId => tagLookup[tagId])
            .ToList()
            .AsReadOnly();

        return new TimelineItem(
            note.Id,
            note.Title,
            contentPreview,
            note.CreatedDate,
            note.ModifiedDate,
            tagNames
        );
    }
}