namespace DevSticky.Models;

/// <summary>
/// Core domain model for a note
/// </summary>
public class Note : TrackableModel
{
    private Guid _id = Guid.NewGuid();
    private string _title = "Untitled Note";
    private string _content = string.Empty;
    private string _language = "PlainText";
    private bool _isPinned = true;
    private double _opacity = 0.9;
    private WindowRect _windowRect = new();
    private DateTime _createdDate = DateTime.UtcNow;
    private DateTime _modifiedDate = DateTime.UtcNow;
    private Guid? _groupId;
    private List<Guid> _tagIds = new();
    private string? _monitorDeviceId;
    private Guid? _templateId;
    private long _syncVersion;
    private DateTime? _lastSyncedDate;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value ?? "Untitled Note");
    }

    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value ?? string.Empty))
            {
                ModifiedDate = DateTime.UtcNow;
            }
        }
    }

    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, value ?? "PlainText");
    }

    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    public double Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, value);
    }

    public WindowRect WindowRect
    {
        get => _windowRect;
        set => SetProperty(ref _windowRect, value ?? new());
    }

    public DateTime CreatedDate
    {
        get => _createdDate;
        set => SetProperty(ref _createdDate, value);
    }

    public DateTime ModifiedDate
    {
        get => _modifiedDate;
        set => SetProperty(ref _modifiedDate, value);
    }

    public Guid? GroupId
    {
        get => _groupId;
        set => SetProperty(ref _groupId, value);
    }

    public List<Guid> TagIds
    {
        get => _tagIds;
        set => SetProperty(ref _tagIds, value ?? new());
    }

    // v2.0 properties for multi-monitor support
    public string? MonitorDeviceId
    {
        get => _monitorDeviceId;
        set => SetProperty(ref _monitorDeviceId, value);
    }

    // v2.0 properties for template tracking
    public Guid? TemplateId
    {
        get => _templateId;
        set => SetProperty(ref _templateId, value);
    }

    // v2.0 properties for cloud sync
    public long SyncVersion
    {
        get => _syncVersion;
        set => SetProperty(ref _syncVersion, value);
    }

    public DateTime? LastSyncedDate
    {
        get => _lastSyncedDate;
        set => SetProperty(ref _lastSyncedDate, value);
    }
}
