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
    void CreateDirectory(string path);
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
    Task<T?> ShowCustomDialogAsync<T>(Func<Window> dialogFactory) where T : class;
}
```

**Implementation**: `DialogService`

## Crash Handling & Recovery

DevSticky includes comprehensive crash detection, analysis, and recovery systems to ensure reliable operation.

### ICrashAnalyticsService

Service for analyzing and tracking application crashes.

```csharp
public interface ICrashAnalyticsService
{
    Task<CrashReport> AnalyzeCrashAsync(Exception exception, string context = "");
    Task<IReadOnlyList<CrashReport>> GetRecentCrashesAsync(TimeSpan timeWindow);
    Task<bool> HasRecentCrashesAsync(int threshold = 3, TimeSpan window = TimeSpan.FromMinutes(5));
    void TrackCrashPattern(string component, Exception exception);
    Task<CrashAnalyticsReport> GenerateAnalyticsReportAsync();
}
```

**Features**:
- Windows Event Log analysis for crash detection
- Stack trace analysis and component identification
- Crash pattern recognition and frequency tracking
- Comprehensive crash reporting with context

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

Centralized exception logging with context and categorization.

```csharp
public interface IExceptionLogger
{
    Task LogExceptionAsync(Exception exception, string context = "", ExceptionSeverity severity = ExceptionSeverity.Error);
    Task LogExceptionAsync(Exception exception, Dictionary<string, object> contextData);
    Task<IReadOnlyList<ExceptionLogEntry>> GetRecentExceptionsAsync(TimeSpan timeWindow);
    Task<ExceptionStatistics> GetExceptionStatisticsAsync();
    void SetContextProperty(string key, object value);
    void ClearContext();
}
```

**Features**:
- Dual logging (file and Windows Event Log)
- Exception categorization and severity levels
- Context data collection
- Exception statistics and analysis

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
    public IList<string> DisabledServices { get; set; }
    public IList<string> DisabledFeatures { get; set; }
    public bool UseDefaultTheme { get; set; }
    public bool DisableCloudSync { get; set; }
    public bool DisableHotkeys { get; set; }
    public int MaxNoteCount { get; set; }
    public TimeSpan StartupTimeout { get; set; }
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
public interface INoteService
{
    Task<Note> CreateNoteAsync(string? content = null, NoteGroup? group = null);
    Task<Note?> GetNoteAsync(Guid noteId);
    Task<IReadOnlyList<Note>> GetAllNotesAsync();
    Task SaveNoteAsync(Note note);
    Task DeleteNoteAsync(Guid noteId);
    Task<IReadOnlyList<Note>> SearchNotesAsync(string query);
}
```

**Implementation**: `NoteService`

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

## Data Models

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