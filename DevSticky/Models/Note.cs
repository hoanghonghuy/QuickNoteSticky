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
    
    // v2.2 properties for folders and kanban
    private Guid? _folderId;
    private KanbanStatus? _kanbanStatus;
    
    // v2.1 lazy loading support
    private bool _isContentLoaded = true;
    private string _contentPreview = string.Empty;

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

    // v2.2 properties for folders and kanban
    public Guid? FolderId
    {
        get => _folderId;
        set => SetProperty(ref _folderId, value);
    }
    
    public KanbanStatus? KanbanStatus
    {
        get => _kanbanStatus;
        set => SetProperty(ref _kanbanStatus, value);
    }
    
    /// <summary>
    /// Whether the full content is loaded in memory (for lazy loading)
    /// </summary>
    public bool IsContentLoaded
    {
        get => _isContentLoaded;
        set => SetProperty(ref _isContentLoaded, value);
    }
    
    /// <summary>
    /// Preview of content (first 200 chars) for display in lists
    /// </summary>
    public string ContentPreview
    {
        get => _contentPreview;
        set => SetProperty(ref _contentPreview, value ?? string.Empty);
    }
    
    /// <summary>
    /// Updates content preview from current content
    /// </summary>
    public void UpdateContentPreview()
    {
        ContentPreview = Content.Length > 200 ? Content[..200] + "..." : Content;
    }
    
    /// <summary>
    /// Unloads content to free memory (keeps preview)
    /// </summary>
    public void UnloadContent()
    {
        if (IsContentLoaded && !string.IsNullOrEmpty(Content))
        {
            UpdateContentPreview();
            _content = string.Empty;
            IsContentLoaded = false;
        }
    }
}
