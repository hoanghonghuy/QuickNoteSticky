using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing timeline view functionality
/// </summary>
public interface ITimelineService
{
    /// <summary>
    /// Gets timeline items for all notes, optionally filtered by date range
    /// </summary>
    Task<IReadOnlyList<TimelineItem>> GetTimelineItemsAsync(DateTime? fromDate = null, DateTime? toDate = null);
    
    /// <summary>
    /// Groups timeline items by date
    /// </summary>
    Dictionary<DateTime, IReadOnlyList<TimelineItem>> GroupByDate(IEnumerable<TimelineItem> items);
    
    /// <summary>
    /// Filters timeline items by date range
    /// </summary>
    IReadOnlyList<TimelineItem> FilterByDateRange(IEnumerable<TimelineItem> items, DateTime fromDate, DateTime toDate);
}