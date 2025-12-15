# DevSticky API Documentation

This document provides comprehensive documentation for DevSticky's services, interfaces, and helper classes.

## Table of Contents

- [Core Interfaces](#core-interfaces)
- [Crash Handling & Recovery](#crash-handling--recovery)
- [Memory Management](#memory-management)
- [Business Logic Services](#business-logic-services)
- [Infrastructure Services](#infrastructure-services)
- [Helper Classes](#helper-classes)
- [Data Models](#data-models)
- [Error Handling](#error-handling)

## Core Interfaces

### ITrackable

Base interface for entities that support dirty tracking.

```csharp
public interface ITrackable
{
    bool IsDirty { get; set; }
    void MarkClean();
    void MarkDirty();
}
```

**Usage**: Implement this interface on models that need change tracking for optimized saves.

### IDateTimeProvider

Abstraction for date/time operations to enable testing.

```csharp
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}
```

**Implementation**: `SystemDateTimeProvider`

### IFileSystem

Abstraction for file system operations.

```csharp
public interface IFileSystem
{
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string content);
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void DeleteFile(string path);
    Task DeleteFileAsync(string path);
    void MoveFile(string sourcePath, string destinationPath);
    Task MoveFileAsync(string sourcePath, string destinationPath);
    string? GetDirectoryName(string path);
    string Combine(params string[] paths);
}
```

**Implementation**: `FileSystemAdapter`

### IDialogService

Abstraction for UI dialogs.

```csharp
public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
    Task ShowInfoAsync(string title, string message);
    Task ShowSuccessAsync(string title, string message);
    Task ShowWarningAsync(string title, string message);
    Task<T?> ShowCustomDialogAsync<T>(Func<Window> dialogFactory) where T : class;
    Task<T?> ShowCustomDialogAsync<T>(Window owner, Func<Window> dialogFactory) where T : class;
}
```

**Implementation**: `DialogService`

## Crash Handling & Recovery

DevSticky includes comprehensive crash detection, analysis, and recovery systems to ensure reliable operation.

### ICrashAnalyticsService

Service for crash reporting and analytics.

```csharp
public interface ICrashAnalyticsService : IDisposable
{
    Task RecordCrashAsync(CrashReport crashReport);
    Task RecordRecoveryAttemptAsync(RecoveryAttempt recoveryAttempt);
    Task RecordSafeModeUsageAsync(SafeModeUsage safeModeUsage);
    Task<CrashFrequencyStats> GetCrashFrequencyStatsAsync();
    Task<FailurePatternAnalysis> AnalyzeFailurePatternsAsync();
    Task<RecoverySuccessStats> GetRecoverySuccessStatsAsync();
    Task<SafeModeStats> GetSafeModeStatsAsync();
    Task<CrashAnalyticsReport> GenerateAnalyticsReportAsync();
    Task CleanupOldDataAsync(TimeSpan maxAge);
}
```

**Features**:
- Crash recording and tracking
- Recovery attempt monitoring
- Safe mode usage statistics
- Failure pattern analysis
- Comprehensive analytics reporting

**Implementation**: `CrashAnalyticsService`

### IStartupValidator

Validates application prerequisites and dependencies during startup.

```csharp
public interface IStartupValidator
{
    Task<ValidationResult> ValidateAsync();
    Task<ValidationResult> ValidateDirectoriesAsync();
    Task<ValidationResult> ValidateConfigurationAsync();
    Task<ValidationResult> ValidateDependenciesAsync();
    Task<ValidationResult> ValidateServicesAsync();
    Task<ValidationResult> ValidateResourcesAsync();
}
```

**Features**:
- Directory structure validation
- Configuration file integrity checks
- Dependency and DLL verification
- Service registration validation
- Resource accessibility verification

**Implementation**: `StartupValidator`

### IRecoveryManager

Manages automatic recovery from common startup and configuration issues.

```csharp
public interface IRecoveryManager
{
    Task<RecoveryResult> AttemptRecoveryAsync(ValidationResult validationResult);
    Task<bool> CreateDefaultConfigurationAsync();
    Task<bool> RecreateDirectoryStructureAsync();
    Task<bool> RestoreFromBackupAsync(DateTime? backupDate = null);
    Task<bool> RepairCorruptedFilesAsync();
    Task<IReadOnlyList<RecoveryAction>> GetAvailableActionsAsync();
}
```

**Features**:
- Automatic configuration file creation
- Directory structure recreation
- Backup and restore functionality
- Corrupted file repair
- Service fallback management

**Implementation**: `RecoveryManager`

### ISafeModeController

Controls safe mode startup and minimal service configuration.

```csharp
public interface ISafeModeController
{
    bool IsSafeModeActive { get; }
    Task<bool> StartSafeModeAsync();
    Task<bool> ExitSafeModeAsync();
    Task<SafeModeConfig> GetSafeModeConfigAsync();
    Task RegisterMinimalServicesAsync(IServiceCollection services);
    Task<IReadOnlyList<string>> GetDisabledFeaturesAsync();
}
```

**Features**:
- Minimal service registration for safe startup
- Safe mode status tracking
- Feature disable/enable management
- Safe mode configuration management

**Implementation**: `SafeModeController`

### IStartupDiagnostics

Provides detailed diagnostic information about the startup process.

```csharp
public interface IStartupDiagnostics
{
    void StartStep(string stepName);
    void CompleteStep(string stepName, bool success = true, string? errorMessage = null);
    void LogDiagnostic(string message, DiagnosticLevel level = DiagnosticLevel.Info);
    Task<StartupDiagnosticsReport> GenerateReportAsync();
    IReadOnlyList<StartupStep> GetStartupSteps();
    TimeSpan GetTotalStartupTime();
}
```

**Features**:
- Step-by-step startup monitoring
- Performance metrics collection
- Diagnostic message logging
- Comprehensive startup reporting

**Implementation**: `StartupDiagnostics`

### IExceptionLogger

Centralized exception logging with startup context and resource tracking.

```csharp
public interface IExceptionLogger : IDisposable
{
    void LogStartupException(Exception exception, StartupExceptionContext context);
    Task LogStartupExceptionAsync(Exception exception, StartupExceptionContext context);
    void TrackResource(IDisposable resource);
    void CleanupTrackedResources();
    T ExecuteWithResourceTracking<T>(Func<T> operation, StartupExceptionContext context);
    Task<T> ExecuteWithResourceTrackingAsync<T>(Func<Task<T>> operation, StartupExceptionContext context);
}
```

**Features**:
- Dual logging (file and Windows Event Log)
- Startup-specific context information
- Resource tracking for cleanup on failure
- Safe execution with automatic cleanup

**Implementation**: `ExceptionLogger`

### Crash Handling Data Models

#### CrashReport
```csharp
public class CrashReport
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ExceptionType { get; set; }
    public string Message { get; set; }
    public string StackTrace { get; set; }
    public string Component { get; set; }
    public string Context { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public string SystemInfo { get; set; }
    public CrashSeverity Severity { get; set; }
}
```

#### ValidationResult
```csharp
public class ValidationResult
{
    public bool IsValid { get; set; }
    public IList<ValidationIssue> Issues { get; set; }
    public TimeSpan ValidationDuration { get; set; }
    public DateTime ValidationTime { get; set; }
    public string ValidatorVersion { get; set; }
}
```

#### ValidationIssue
```csharp
public class ValidationIssue
{
    public string Component { get; set; }
    public string Issue { get; set; }
    public ValidationSeverity Severity { get; set; }
    public string SuggestedAction { get; set; }
    public bool IsAutoFixable { get; set; }
    public Dictionary<string, object> Details { get; set; }
}
```

#### StartupStep
```csharp
public class StartupStep
{
    public string Name { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

#### RecoveryAction
```csharp
public class RecoveryAction
{
    public string Name { get; set; }
    public string Description { get; set; }
    public RecoveryActionType Type { get; set; }
    public RecoveryRiskLevel RiskLevel { get; set; }
    public bool RequiresUserConfirmation { get; set; }
    public Func<Task<bool>> ExecuteAsync { get; set; }
}
```

#### SafeModeConfig
```csharp
public class SafeModeConfig
{
    public bool IsEnabled { get; set; }
    public string Reason { get; set; }
    public DateTime ActivatedAt { get; set; }
    public bool UseDefaultSettings { get; set; }
    public bool DisableNonEssentialServices { get; set; }
    public bool DisableCloudSync { get; set; }
    public bool DisableHotkeys { get; set; }
    public bool DisableMarkdownPreview { get; set; }
    public bool DisableSnippetsAndTemplates { get; set; }
    public bool DisableThemeSwitching { get; set; }
    public int MaxNotesToLoad { get; set; }
    public bool ShowSafeModeIndicator { get; set; }
    public List<string> StartupFailures { get; set; }
    public int AutoActivateThreshold { get; set; }
}
```

### Crash Handling Enums

```csharp
public enum CrashSeverity
{
    Low,        // Minor issues, application continues
    Medium,     // Significant issues, some features affected
    High,       // Major issues, application functionality impaired
    Critical    // Severe issues, application cannot continue
}

public enum ValidationSeverity
{
    Info,       // Informational message
    Warning,    // Potential issue, not blocking
    Error,      // Definite problem, may cause issues
    Critical    // Severe problem, will cause failure
}

public enum RecoveryActionType
{
    ConfigurationReset,
    FileRecreation,
    DirectoryCreation,
    ServiceRestart,
    BackupRestore,
    CacheClear,
    IndexRebuild
}

public enum RecoveryRiskLevel
{
    Safe,       // No risk of data loss
    Low,        // Minimal risk, easily reversible
    Medium,     // Some risk, backup recommended
    High        // Significant risk, user confirmation required
}

public enum ExceptionSeverity
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}

public enum DiagnosticLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error
}
```

## Memory Management

### ILruCache<TKey, TValue>

Generic Least Recently Used cache implementation.

```csharp
public interface ILruCache<TKey, TValue>
{
    void Add(TKey key, TValue value);
    bool TryGetValue(TKey key, out TValue value);
    void Remove(TKey key);
    void Clear();
    int Count { get; }
    int MaxSize { get; }
}
```

**Features**:
- Automatic eviction when size limit is reached
- Thread-safe operations
- O(1) access time
- Implements IDisposable for proper cleanup

**Implementation**: `LruCache<TKey, TValue>`

### ICacheService

High-level caching service for application entities.

```csharp
public interface ICacheService : IDisposable
{
    NoteTag? GetTag(Guid tagId);
    NoteGroup? GetGroup(Guid groupId);
    List<NoteTag> GetTags(IEnumerable<Guid> tagIds);
    void InvalidateTagCache();
    void InvalidateGroupCache();
    void InvalidateAll();
    CacheStatistics GetStatistics();
}
```

**Features**:
- LRU-based caching for tags and groups
- Cache statistics and hit rate tracking
- Selective cache invalidation
- Memory usage monitoring

**Implementation**: `EnhancedCacheService`

### IDirtyTracker<T>

Tracks changes to entities for optimized persistence.

```csharp
public interface IDirtyTracker<T>
{
    void Track(T item);
    void MarkDirty(T item);
    void MarkClean(T item);
    IEnumerable<T> GetDirtyItems();
    void Clear();
}
```

**Features**:
- Thread-safe change tracking
- Bulk operations for dirty items
- Memory-efficient weak references

**Implementation**: `DirtyTracker<T>`

## v2.1.0 New Services

### IFolderService

Manages hierarchical folder structure for notes.

```csharp
public interface IFolderService
{
    Task<NoteFolder> CreateFolderAsync(string name, Guid? parentId = null);
    Task<bool> DeleteFolderAsync(Guid folderId);
    Task<bool> RenameFolderAsync(Guid folderId, string newName);
    Task<bool> MoveFolderAsync(Guid folderId, Guid? newParentId);
    Task<bool> MoveNoteToFolderAsync(Guid noteId, Guid? folderId);
    Task<IReadOnlyList<NoteFolder>> GetAllFoldersAsync();
    Task<IReadOnlyList<NoteFolder>> GetRootFoldersAsync();
    Task<IReadOnlyList<NoteFolder>> GetChildFoldersAsync(Guid parentId);
    Task<IReadOnlyList<Note>> GetNotesInFolderAsync(Guid? folderId);
    Task SaveAsync();
    Task LoadAsync();
}
```

**Features**:
- Hierarchical folder structure with unlimited nesting
- Drag-and-drop support for notes and folders
- Folder colors and icons
- JSON persistence

**Implementation**: `FolderService`

### ISmartCollectionService

Manages smart collections that automatically group notes by criteria.

```csharp
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
```

**Default Collections**:
- "Today" - Notes created/modified today
- "This Week" - Notes from current week
- "Has TODO" - Notes with unchecked checkboxes
- "Code Notes" - Notes with code blocks

**Implementation**: `SmartCollectionService`

### IKanbanService

Manages Kanban board functionality for task management.

```csharp
public interface IKanbanService
{
    Task<bool> UpdateNoteStatusAsync(Guid noteId, KanbanStatus status);
    Task<IReadOnlyList<Note>> GetNotesByStatusAsync(KanbanStatus status);
    Task<Dictionary<KanbanStatus, IReadOnlyList<Note>>> GetAllKanbanNotesAsync();
}
```

**KanbanStatus Enum**:
- `ToDo` - Tasks to be done
- `InProgress` - Tasks in progress
- `Done` - Completed tasks

**Implementation**: `KanbanService`

### ITimelineService

Provides timeline view functionality for notes.

```csharp
public interface ITimelineService
{
    Task<IReadOnlyList<TimelineItem>> GetTimelineItemsAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Dictionary<DateTime, IReadOnlyList<TimelineItem>> GroupByDate(IEnumerable<TimelineItem> items);
    IReadOnlyList<TimelineItem> FilterByDateRange(IEnumerable<TimelineItem> items, DateTime fromDate, DateTime toDate);
}
```

**Implementation**: `TimelineService`

### IFuzzySearchService

Intelligent search with typo tolerance.

```csharp
public interface IFuzzySearchService
{
    IReadOnlyList<FuzzySearchResult> Search(string query, IEnumerable<Note> notes, int maxResults = 20);
    int CalculateLevenshteinDistance(string source, string target);
    double CalculateRelevanceScore(string query, string text, MatchType matchType);
}
```

**Features**:
- Levenshtein distance-based fuzzy matching
- Relevance scoring (exact > partial > fuzzy)
- Highlight positions for matched terms

**Implementation**: `FuzzySearchService`

### IFileDropService

Handles file drag-and-drop operations.

```csharp
public interface IFileDropService
{
    Task<string> ProcessDroppedFileAsync(string filePath);
    Task<string> ProcessDroppedFilesAsync(IEnumerable<string> filePaths);
    bool IsTextFile(string filePath);
    bool IsCodeFile(string filePath);
    bool IsImageFile(string filePath);
}
```

**Supported File Types**:
- Text files (.txt, .md, .json, .xml): Insert content
- Code files (.cs, .js, .py, etc.): Insert with syntax highlighting
- Images (.png, .jpg, .gif): Insert as markdown image
- Other files: Insert file path

**Implementation**: `FileDropService`

### IMemoryCleanupService

Automatic memory cleanup of unused resources.

```csharp
public interface IMemoryCleanupService : IDisposable
{
    void Start();
    void Stop();
    void CleanupNow();
    void MarkAccessed(Guid noteId);
    void MarkClosed(Guid noteId);
    MemoryStats GetStats();
    event EventHandler<CleanupEventArgs>? CleanupPerformed;
}
```

**Features**:
- Automatic cleanup every 5 minutes
- Releases content of notes closed > 10 minutes
- Memory usage statistics

**Implementation**: `MemoryCleanupService`

### IRecentNotesService

Tracks recently accessed notes.

```csharp
public interface IRecentNotesService
{
    void AddRecentNote(Guid noteId, string title);
    IReadOnlyList<RecentNoteInfo> GetRecentNotes(int count = 10);
    void ClearRecentNotes();
    void RemoveNote(Guid noteId);
}
```

**Implementation**: `RecentNotesService`

### IBackupService

Automatic backup management.

```csharp
public interface IBackupService : IDisposable
{
    void Start();
    void Stop();
    Task BackupNowAsync();
    Task<IReadOnlyList<BackupInfo>> GetAvailableBackupsAsync();
    Task<bool> RestoreFromBackupAsync(string backupPath);
    Task CleanupOldBackupsAsync(int keepCount = 10);
}
```

**Features**:
- Automatic backup every 30 minutes (configurable)
- Keeps up to 10 backup versions
- Backup restore with selection dialog

**Implementation**: `BackupService`

### IUndoRedoService

Command pattern-based undo/redo.

```csharp
public interface IUndoRedoService
{
    void Execute(IUndoableCommand command);
    bool CanUndo { get; }
    bool CanRedo { get; }
    void Undo();
    void Redo();
    void Clear();
}
```

**Features**:
- 50-step history limit
- Command batching support
- Integration with AvalonEdit's built-in undo

**Implementation**: `UndoRedoService`

### IPerformanceMonitoringService

Performance monitoring and metrics collection.

```csharp
public interface IPerformanceMonitoringService : IDisposable
{
    void StartCategoryTiming(string category);
    void StopCategoryTiming(string category);
    void MarkMilestone(string milestone);
    PerformanceMetrics GetPerformanceMetrics();
    Task ExportPerformanceMetricsAsync(string filePath);
    void LogPerformanceSummary();
    IReadOnlyList<PerformanceWarning> GetWarnings();
    void UpdateThresholds(PerformanceThresholds newThresholds);
}
```

**Implementation**: `PerformanceMonitoringService`

### IServiceFallbackManager

Manages service fallback mechanisms during startup failures.

```csharp
public interface IServiceFallbackManager
{
    T? CreateFallbackService<T>(string serviceName, Exception originalException) where T : class;
    bool DetectServiceFailure(Type serviceType, object? serviceInstance);
    string? GetEmbeddedFallbackResource(string resourceType, string resourceKey);
    ServiceFallbackResult ConfigureGracefulDegradation(string serviceName, int degradationLevel);
    void RegisterFallback<TInterface, TFallback>() where TInterface : class where TFallback : class, TInterface;
    bool HasFallback<T>() where T : class;
    Dictionary<Type, Type> GetRegisteredFallbacks();
}
```

**Implementation**: `ServiceFallbackManager`

## Business Logic Services

### IGroupManagementService

Manages note groups and organization.

```csharp
public interface IGroupManagementService
{
    NoteGroup CreateGroup(string? name = null);
    void DeleteGroup(Guid groupId);
    void RenameGroup(Guid groupId, string newName);
    void MoveNoteToGroup(Guid noteId, Guid? groupId);
    IReadOnlyList<NoteGroup> GetAllGroups();
}
```

**Features**:
- CRUD operations for note groups
- Note-to-group assignment
- Validation and error handling
- Event notifications for UI updates

**Implementation**: `GroupManagementService`

### ITagManagementService

Handles note tagging and categorization.

```csharp
public interface ITagManagementService
{
    NoteTag CreateTag(string? name = null, string? color = null);
    void DeleteTag(Guid tagId);
    void RenameTag(Guid tagId, string newName);
    void ChangeTagColor(Guid tagId, string newColor);
    void AddTagToNote(Guid noteId, Guid tagId);
    void RemoveTagFromNote(Guid noteId, Guid tagId);
    IReadOnlyList<NoteTag> GetAllTags();
}
```

**Features**:
- Tag lifecycle management
- Color-coded categorization
- Many-to-many note-tag relationships
- Bulk tag operations

**Implementation**: `TagManagementService`

### INoteService

Core note operations and lifecycle management.

```csharp
public interface INoteService : IDisposable
{
    Note CreateNote();
    void AddNote(Note note);
    void UpdateNote(Note note);
    void DeleteNote(Guid id);
    Note? GetNoteById(Guid id);
    IReadOnlyList<Note> GetAllNotes();
    void TogglePin(Guid id);
    double AdjustOpacity(Guid id, double step);
    void LoadNotes(IEnumerable<Note> notes);
    
    // Lazy Loading Methods (v2.1.0)
    Task PreloadContentsAsync(IEnumerable<Guid> noteIds);
    Task<bool> EnsureContentLoadedAsync(Guid noteId);
    void UnloadNoteContent(Guid noteId);
    Task<string?> GetNoteContentAsync(Guid noteId);
    Task SaveNoteContentAsync(Guid noteId, string content);
}
```

**Features**:
- CRUD operations for notes
- Lazy loading support for on-demand content loading
- Content preloading for batch operations
- Memory management via content unloading

**Implementation**: `NoteService`

### IStorageService

Data persistence and lazy loading storage.

```csharp
public interface IStorageService : IDisposable
{
    Task<AppData> LoadAsync();
    Task SaveAsync(AppData data);
    Task SaveNotesAsync(IEnumerable<Note> notes, AppData currentData);
    string GetStoragePath();
    
    // Lazy Loading Methods (v2.1.0)
    bool IsLazyLoadingFormat { get; }
    Task<AppData> LoadMetadataOnlyAsync();
    Task<string?> LoadNoteContentAsync(Guid noteId);
    Task SaveNoteContentAsync(Guid noteId, string content);
    Task DeleteNoteContentAsync(Guid noteId);
    Task<bool> MigrateToLazyLoadingFormatAsync();
    Task PreloadNoteContentsAsync(IEnumerable<Guid> noteIds);
}
```

**Features**:
- JSON-based local storage
- Lazy loading format with separate content files
- Automatic migration from legacy format
- Thread-safe per-note operations

**Implementation**: `StorageService`

### ISearchService

Full-text search across notes and metadata.

```csharp
public interface ISearchService
{
    Task<IReadOnlyList<SearchMatch>> SearchAsync(string query);
    Task<IReadOnlyList<SearchMatch>> SearchInNotesAsync(string query, IEnumerable<Note> notes);
    void IndexNote(Note note);
    void RemoveFromIndex(Guid noteId);
    void ClearIndex();
}
```

**Implementation**: `SearchService`

## Infrastructure Services

### ICloudProviderRegistry

Extensible cloud provider management following Open/Closed Principle.

```csharp
public interface ICloudProviderRegistry
{
    void RegisterProvider(CloudProvider provider, Func<ICloudStorageProvider> factory);
    ICloudStorageProvider CreateProvider(CloudProvider provider);
    IEnumerable<CloudProvider> GetAvailableProviders();
}
```

**Features**:
- Plugin-style provider registration
- Factory pattern for provider creation
- Support for multiple cloud providers

**Implementation**: `CloudProviderRegistry`

### Cloud Sync Interfaces (Interface Segregation)

The cloud sync functionality is split into focused interfaces:

#### ICloudConnection
```csharp
public interface ICloudConnection
{
    Task<bool> ConnectAsync(CloudProvider provider);
    Task DisconnectAsync();
    CloudProvider? CurrentProvider { get; }
    SyncStatus Status { get; }
}
```

#### ICloudSync
```csharp
public interface ICloudSync
{
    Task<SyncResult> SyncAsync();
    Task<SyncResult> SyncNoteAsync(Guid noteId);
    void QueueNoteForSync(Guid noteId, SyncChangeType changeType);
    IReadOnlyList<PendingSyncChange> PendingChanges { get; }
}
```

#### ICloudConflictResolver
```csharp
public interface ICloudConflictResolver
{
    Task<Note> ResolveConflictAsync(Guid noteId, SyncConflictResolution resolution, Note localNote, Note remoteNote);
    event EventHandler<SyncConflictEventArgs>? SyncConflict;
}
```

### IErrorHandler

Centralized error handling with context and recovery strategies.

```csharp
public interface IErrorHandler
{
    void Handle(Exception exception, string context = "");
    void HandleAsync(Exception exception, string context = "");
    T HandleWithFallback<T>(Func<T> operation, T fallback, string context = "");
    Task<T> HandleWithFallbackAsync<T>(Func<Task<T>> operation, T fallback, string context = "");
}
```

**Features**:
- Contextual error information
- Fallback value support
- Async error handling
- Logging and telemetry integration

**Implementation**: `ErrorHandler`

### IDebounceService

Optimized debouncing service using single timer.

```csharp
public interface IDebounceService : IDisposable
{
    void Debounce(string key, Action action, TimeSpan delay);
    void Debounce<T>(string key, Action<T> action, T parameter, TimeSpan delay);
    void Cancel(string key);
    void CancelAll();
}
```

**Implementation**: `OptimizedDebounceService` (uses PriorityQueue for efficiency)

## Helper Classes

### JsonSerializerOptionsFactory

Shared JSON serialization configuration to eliminate duplication.

```csharp
public static class JsonSerializerOptionsFactory
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    public static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
```

### MonitorBoundsHelper

Multi-monitor window positioning utilities.

```csharp
public static class MonitorBoundsHelper
{
    public static void EnsureWindowInBounds(Window window, MonitorInfo monitor);
    public static void CenterWindowOnMonitor(Window window, MonitorInfo monitor);
    public static (double X, double Y) CalculateRelativePosition(Window window, MonitorInfo monitor);
    public static void ApplyRelativePosition(Window window, MonitorInfo monitor, double relativeX, double relativeY);
}
```

**Features**:
- Multi-monitor aware positioning
- Automatic bounds correction
- Relative positioning calculations
- Fallback to primary monitor

### Other Helper Classes

- **CollectionHelper**: LINQ optimization utilities
- **DateTimeHelper**: Date/time formatting and parsing
- **GuidHelper**: GUID generation and validation
- **OpacityHelper**: Window opacity calculations
- **PathHelper**: File path manipulation utilities
- **StringHelper**: String processing and validation
- **ValidationHelper**: Input validation utilities
- **WeakEventHelper**: Memory-safe event handling
- **WpfResourceHelper**: WPF resource cleanup utilities
- **MemoryLeakDetector**: Detects potential memory leaks
- **PerformanceBenchmark**: Performance measurement utilities

## Data Models

### v2.1.0 New Models

#### NoteFolder
```csharp
public class NoteFolder
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Guid? ParentId { get; set; }
    public string Color { get; set; }
    public string Icon { get; set; }
    public DateTime CreatedDate { get; set; }
    public int SortOrder { get; set; }
}
```

#### SmartCollection
```csharp
public class SmartCollection
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public FilterCriteria Criteria { get; set; }
    public bool IsBuiltIn { get; set; }
}
```

#### FilterCriteria
```csharp
public class FilterCriteria
{
    public List<Guid> TagIds { get; set; }
    public DateRangeType? DateRange { get; set; }
    public DateTime? CustomFromDate { get; set; }
    public DateTime? CustomToDate { get; set; }
    public string? ContentContains { get; set; }
    public string? Language { get; set; }
    public bool? HasCheckboxes { get; set; }
    public bool? HasCodeBlocks { get; set; }
}
```

#### TimelineItem
```csharp
public record TimelineItem(
    Guid NoteId,
    string Title,
    string ContentPreview,
    DateTime CreatedDate,
    DateTime ModifiedDate,
    IReadOnlyList<string> Tags
);
```

#### FuzzySearchResult
```csharp
public record FuzzySearchResult(
    Note Note,
    double Score,
    MatchType MatchType,
    IReadOnlyList<HighlightRange> Highlights
);
```

#### MemoryStats
```csharp
public class MemoryStats
{
    public long TotalMemoryBytes { get; set; }
    public long ManagedMemoryBytes { get; set; }
    public int LoadedNotesCount { get; set; }
    public int CachedItemsCount { get; set; }
    public DateTime LastCleanupTime { get; set; }
    public int ItemsCleanedLastRun { get; set; }
}
```

### TrackableModel

Base class for entities with dirty tracking.

```csharp
public abstract class TrackableModel : ITrackable
{
    public bool IsDirty { get; set; }
    
    public void MarkClean() => IsDirty = false;
    public void MarkDirty() => IsDirty = true;
    
    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            MarkDirty();
        }
    }
}
```

### CacheStatistics

Cache performance metrics.

```csharp
public class CacheStatistics
{
    public int HitCount { get; set; }
    public int MissCount { get; set; }
    public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
    public int CurrentSize { get; set; }
    public int MaxSize { get; set; }
    public DateTime LastAccess { get; set; }
}
```

## Error Handling

### ErrorSeverity

```csharp
public enum ErrorSeverity 
{ 
    Info, 
    Warning, 
    Error, 
    Critical 
}
```

### ErrorContext

```csharp
public class ErrorContext
{
    public string Operation { get; set; } = "";
    public string Component { get; set; } = "";
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

### Error Recovery Strategies

1. **File I/O Errors**: Retry with exponential backoff
2. **Network Errors**: Queue for later retry
3. **Validation Errors**: Show user-friendly message
4. **Critical Errors**: Log and gracefully shutdown

## Dependency Injection Configuration

Services are registered with appropriate lifetimes:

```csharp
// Singletons
services.AddSingleton<ICacheService, EnhancedCacheService>();
services.AddSingleton<IErrorHandler, ErrorHandler>();
services.AddSingleton<ICloudProviderRegistry, CloudProviderRegistry>();

// Scoped
services.AddScoped<IGroupManagementService, GroupManagementService>();
services.AddScoped<ITagManagementService, TagManagementService>();
services.AddScoped<INoteService, NoteService>();

// Transient
services.AddTransient<IDialogService, DialogService>();
services.AddTransient<IFileSystem, FileSystemAdapter>();
```

## Performance Considerations

- **Memory Usage**: Target <50MB for 100 notes (40% reduction from baseline)
- **Save Performance**: Target <50ms per save operation (75% improvement)
- **Cache Hit Rate**: Target >90% for frequently accessed data
- **LINQ Optimization**: Single-pass operations where possible
- **Async Operations**: Non-blocking UI with proper ConfigureAwait usage

## Testing

The codebase includes comprehensive testing:

- **Unit Tests**: >80% code coverage
- **Property-Based Tests**: All correctness properties validated
- **Integration Tests**: Critical user workflows
- **Performance Tests**: Memory and speed benchmarks

For testing examples and patterns, see the `DevSticky.Tests` project.