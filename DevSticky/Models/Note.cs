namespace DevSticky.Models;

/// <summary>
/// Core domain model for a note
/// </summary>
public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Untitled Note";
    public string Content { get; set; } = string.Empty;
    public string Language { get; set; } = "PlainText";
    public bool IsPinned { get; set; } = true;
    public double Opacity { get; set; } = 0.9;
    public WindowRect WindowRect { get; set; } = new();
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
    public Guid? GroupId { get; set; }
    public List<Guid> TagIds { get; set; } = new();

    // v2.0 properties for multi-monitor support
    public string? MonitorDeviceId { get; set; }

    // v2.0 properties for template tracking
    public Guid? TemplateId { get; set; }

    // v2.0 properties for cloud sync
    public long SyncVersion { get; set; }
    public DateTime? LastSyncedDate { get; set; }
}
