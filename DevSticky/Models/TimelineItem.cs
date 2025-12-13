namespace DevSticky.Models;

/// <summary>
/// Represents a note item displayed on the timeline view
/// </summary>
public record TimelineItem(
    Guid NoteId,
    string Title,
    string ContentPreview,
    DateTime CreatedDate,
    DateTime ModifiedDate,
    IReadOnlyList<string> Tags);