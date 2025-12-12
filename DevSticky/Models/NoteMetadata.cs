namespace DevSticky.Models;

/// <summary>
/// Lightweight metadata for notes - used for lazy loading.
/// Contains only essential info for displaying in lists without loading full content.
/// </summary>
public class NoteMetadata
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "Untitled Note";
    public string Language { get; set; } = "PlainText";
    public bool IsPinned { get; set; } = true;
    public double Opacity { get; set; } = 0.9;
    public WindowRect WindowRect { get; set; } = new();
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
    public Guid? GroupId { get; set; }
    public List<Guid> TagIds { get; set; } = new();
    public string? MonitorDeviceId { get; set; }
    public Guid? TemplateId { get; set; }
    public long SyncVersion { get; set; }
    public DateTime? LastSyncedDate { get; set; }
    
    /// <summary>
    /// Preview of content (first 100 characters) for display in lists
    /// </summary>
    public string ContentPreview { get; set; } = string.Empty;
    
    /// <summary>
    /// Size of content in bytes - useful for memory management
    /// </summary>
    public int ContentSize { get; set; }
    
    /// <summary>
    /// Whether the full content is currently loaded in memory
    /// </summary>
    public bool IsContentLoaded { get; set; }

    /// <summary>
    /// Creates metadata from a full Note object
    /// </summary>
    public static NoteMetadata FromNote(Note note)
    {
        var content = note.Content ?? string.Empty;
        return new NoteMetadata
        {
            Id = note.Id,
            Title = note.Title,
            Language = note.Language,
            IsPinned = note.IsPinned,
            Opacity = note.Opacity,
            WindowRect = note.WindowRect,
            CreatedDate = note.CreatedDate,
            ModifiedDate = note.ModifiedDate,
            GroupId = note.GroupId,
            TagIds = note.TagIds ?? new List<Guid>(),
            MonitorDeviceId = note.MonitorDeviceId,
            TemplateId = note.TemplateId,
            SyncVersion = note.SyncVersion,
            LastSyncedDate = note.LastSyncedDate,
            ContentPreview = content.Length > 100 ? content[..100] + "..." : content,
            ContentSize = System.Text.Encoding.UTF8.GetByteCount(content),
            IsContentLoaded = true
        };
    }

    /// <summary>
    /// Creates a Note object with metadata only (content will be loaded later)
    /// </summary>
    public Note ToNote(string? content = null)
    {
        return new Note
        {
            Id = Id,
            Title = Title,
            Content = content ?? string.Empty,
            Language = Language,
            IsPinned = IsPinned,
            Opacity = Opacity,
            WindowRect = WindowRect,
            CreatedDate = CreatedDate,
            ModifiedDate = ModifiedDate,
            GroupId = GroupId,
            TagIds = TagIds,
            MonitorDeviceId = MonitorDeviceId,
            TemplateId = TemplateId,
            SyncVersion = SyncVersion,
            LastSyncedDate = LastSyncedDate
        };
    }
}
